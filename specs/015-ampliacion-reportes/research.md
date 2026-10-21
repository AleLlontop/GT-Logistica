# Investigación técnica — 015 Ampliación de reportes

Fecha: 2026-10-19. Diseño, no implementación ni evidencia de ejecución. Spec aprobada e intacta; AC-01–05 y C-01–04 cerradas. Contexto `.specify/feature.json`: 015; rama 015-ampliacion-reportes. PowerShell (`pwsh`) no disponible en este entorno: se genera el equivalente documental de setup-plan con destinos explícitos, sin ejecutar tasks ni alterar scripts.

## 1. Código real y reutilización

Inspeccionados: spec/plan/tasks/contratos de 012; cinco fuentes `GT.Application/Reportes/Fuentes/`; `ReporteTabular`, `ArmadoDeReporte`, opciones/formato/resultado; endpoints y respuestas de Api; dos armadores Infrastructure y tests Reportes unitarios/integración; componente, servicio y tests frontend. Revisados entidades, DTOs, repositorios, configuraciones EF y permisos de 003/004/005/006/009/011/013/014; constitución, AGENTS y plantilla del plan.

Stack en manifiestos: SDK .NET 10.0.302; EF SQL Server 10.0.10; QuestPDF 2026.7.3; ClosedXML 0.105.1. React 19.2.8, Router 7.18.2, TS 6.0.2, Tailwind 4, Node 24; Vitest/Testing Library y xUnit/WebApplicationFactory con SQL Server real. Sin repositorio genérico ni framework nuevo.

012 tiene cinco GET de exportación, sin dashboard ni persistencia de emisiones. `ArmadoDeReporte` verifica tope 5.000, captura encabezado/autor/instante y elige armador; `ReporteTabular` conserva valores tipados. QuestPDF sirve PDF inline y ClosedXML XLSX attachment; ambos usan el mismo modelo neutro. UI `GenerarReporte` conserva filtros/página y traduce el conflicto de tope como resultado. Reutilizar esos componentes y contratos, con extensiones aditivas para precisión/cantidades y nuevos tipos de reporte. No cambiar sus nombres/rutas ni el número de columnas legacy.

Los totales actuales de 006 seleccionan por Fecha de factura, no FechaCobro: no reutilizarlos para flujo temporal. 009 filtra mes/año; 014 agregó OP.Vigente y origen/reversión. Caja ya tiene corte argentino y referencia OPP separada. `Vehiculo` tiene tipo/propietario actual y documentación; `Viaje` una asignación, no historia de reasignaciones. Los modelos de 013 sólo contextualizan; no incorporar facturas de proveedor como ventas ni sumar OPP a sus OP.

## 2. Foto de lectura

Decisión: `RepositorioAnalisisReportes` con GtDbContext scoped, proyecciones AsNoTracking y transacción `IsolationLevel.Snapshot` por respuesta nueva. Capturar TimeProvider una vez; todas las consultas de totales, series, categorías, conteo y página de esa respuesta usan la misma transacción/conexión y hoy argentino. Ejecutarlas secuencialmente: EF no soporta operaciones paralelas sobre la misma instancia. DTO materializado antes de cerrar; PDF/XLSX se renderiza después, sin mantener abierta la transacción durante formato/IO.

SQL Server necesita `ALLOW_SNAPSHOT_ISOLATION ON`. No encontrado en configuración/migraciones actuales: precondición administrativa explícita de despliegue y fixture, no supuesto. Implementación futura: script operativo versionado específico que verifica la base destino y estado, habilita la opción fuera de transacción y espera estado ON; no altera READ_COMMITTED_SNAPSHOT, no usa SINGLE_USER ni ROLLBACK IMMEDIATE. Verificación de readiness; si no está ON, nuevas consultas devuelven 503 analisis_no_disponible, sin fallback a lectura inconsistente. El usuario de runtime no necesita ALTER DATABASE. Registrar uso de version store/tempdb y duración de transacciones en la medición; propagar CancellationToken y mantener la transacción sólo durante lectura para no retener versiones durante render de archivos. Fixture habilita tras crear/migrar la base efímera, antes de consultas 015. Ninguna opción se modifica durante este planning.

Justificación: SNAPSHOT da consistencia de transacción; RCSI sólo de sentencia no alcanza para varios SELECT. SERIALIZABLE/REPEATABLE READ agregaría bloqueos/rangos y riesgo de interferir con pagos; NOLOCK permite lecturas sucias. No se agregan UPDATE no-op, applocks ni locks económicos a Reportes. Las consultas legacy quedan con su semántica actual; exportaciones nuevas materializan su foto con snapshot. Dashboard comparte una transacción para todas las secciones permitidas. 401/403 se resuelven antes; fallo SQL de la foto retorna error completo, no KPI parciales contradictorios. No foto persistida ni coherencia entre requests distintos. Fuente: [Microsoft, aislamiento SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/statements/set-transaction-isolation-level-transact-sql?view=sql-server-ver17).

## 3. Gráficos

No hay librería ni motor gráfico analítico interno (búsqueda de chart/canvas/svg; iconos no son gráficos de datos). Evaluadas: SVG propio, Recharts y Chart.js con wrapper React. SVG evita dependencia pero obliga mantener escalas, ejes y teclado; canvas exige otra capa accesible y wrapper. Se propone **Recharts 3**, versión estable exacta a fijar al implementar y registrar en lockfile, nunca canary. Se verificó tag estable v3.0.2 como referencia compatible React 19; main no es versión aprobada (contiene canary). Peer react-is debe coincidir con React 19.2.8 si la versión estable elegida lo exige. Revalidar manifest de la versión seleccionada antes de instalar; ninguna instalación ahora.

Recharts integra componentes React/SVG y capa de accesibilidad; habilitarla explícitamente, pero no sustituye tabla/descripcion propias. Usar sólo barras y líneas, no donuts redundantes. Imports nombrados; módulo de gráficos cargado con React.lazy dentro de rutas Reportes. No introducir Redux en la app: posibles transitivas del paquete quedan internas. Medir bytes raw/gzip de chunks nuevos y comunes contra build baseline y documentarlos; no afirmar un tamaño sin build. Si aparecen dependencias/peso incompatibles, ajustar implementación gráfica dentro de la misma API de GráficoAccesible sin cambiar la información funcional. Fuentes: [manifest estable de referencia](https://raw.githubusercontent.com/recharts/recharts/v3.0.2/package.json), [accesibilidad Recharts](https://github.com/recharts/recharts/blob/main/storybook/stories/API/Accessibility.mdx).

Dinero de HTTP permanece cadena canónica; tabla/tooltips exactos usan formatearPesos. Adaptador gráfico usa centavos BigInt para normalizar escala y sólo convierte coordenadas visuales acotadas a number; no totales ni sumas con Number. Guardar el valor original para tooltip y descripción. Cantidades pueden ser enteros number seguros. Tabla completa paginada de categorías sirve a lectura asistida; etiquetas no dependen de color.

## 4. XLSX: precisión monetaria verificable

El código fuente de [ClosedXML 0.105.1 XLCellValue](https://raw.githubusercontent.com/ClosedXML/ClosedXML/0.105.1/ClosedXML/Excel/XLCellValue.cs) convierte decimal mediante decimal.ToDouble y devuelve double en GetNumber. Darle decimal no prueba exactitud. No cambiar biblioteca ni exigir igualdad binaria.

Propuesta `VerificadorMonetarioXlsx` interno de Infrastructure/Reportes, sin exponer double a Domain/Application. Contrato interno: comparar decimal fuente (escala representable <=2, sin modificarlo) contra valor numérico recuperado. Interpretar double leído mediante su representación decimal round-trip invariante (`ToString("R", InvariantCulture)` y decimal.Parse con exponente admitido); evaluar `decimal.Round(recuperado, 2, AwayFromZero) == fuente`. Este redondeo se usa exclusivamente para comparar la lectura a escala monetaria, nunca para modificar el importe fuente, contenido del libro o aceptar pérdida de un centavo. Rechazar NaN/Infinity, overflow o tipo distinto de Number. No usar epsilon binario ni cast decimal de double con reducción implícita de dígitos como único criterio. Pruebas límite deben demostrar la conversión, no asumirla.

Proceso completo: (1) escribir cada celda monetaria y total como número con formato #,##0.00, conservando manifest temporal (hoja/coordenada/decimal fuente); (2) guardar libro completo en memoria; (3) reabrir esos mismos bytes con XLWorkbook; (4) verificar todas las coordenadas y totales del manifest; (5) sólo si todo coincide devolver los bytes originales. Una sola discrepancia descarta todo y arroja `PrecisionMonetariaXlsxException` tipada; traducción a 409 precision_monetaria_xlsx con mensaje «No podemos exportar este reporte a Excel sin perder centavos. Consultalo en pantalla o generá el PDF.». No devolver dato monetario sensible adicional en error; no string workaround. El tiempo y memoria de reapertura se incluyen en NFR-004.

Guardia común en armador ClosedXML para nuevas y cinco exportaciones legacy: extensión explícita de AC-05/NFR-006. Para datos representables se preservan columnas, formatos, rutas, permisos, encabezado y descarga. Para datos no representables aparece exclusivamente el nuevo rechazo, no aproximación silenciosa. RespuestasDeReporte debe atrapar la excepción tipada antes del catch genérico del armador/ReporteNoGeneradoException, para que no termine en 500. Tests legacy de valores normales permanecen intactos; sumar casos frontera en viajes, facturas vencidas y Caja. PDF no cambia. `TotalDeReporte` hoy sólo decimal: añadir capacidad aditiva de resumen entero para cantidad/ranking sin romper constructor/uso legacy, o mantener cantidad en metadatos y filas Entero cuando no requiera pie; no convertir cantidades a dinero.

No garantía sobre fórmulas/editado posterior en Excel; sí sobre valores monetarios entregados y releídos. El round-trip se valida también con Excel/LibreOffice de aceptación cuando disponible, documentando herramienta; no se presupone comportamiento de futuros editores.

## 5. Consultas, índices y límites

Ver data-model para predicados. Todas las agregaciones se traducen a SQL antes de materializar; agrupamientos por Id estable, no sólo nombre. Dinero SQL SUM sobre decimal, resultado acumulado en C# decimal (no limitar sumas a máximo de un documento decimal(18,2)); overflow explícito, nunca wrap ni redondeo. HTTP acepta cadena con dos decimales para totales mayores que una fila; parser/formateador exacto no debe heredar tope de importe de formulario 013.

Índices existentes: Viajes Fecha/Numero DESC + ClienteId/TransportistaId/Estado/VehiculoId (este último único filtrado sólo EnCurso, no cubre utilización); Facturas Fecha/Numero, ClienteId y Estado/VencimientoPago, no FechaCobro; Liquidaciones PeriodoAnio/PeriodoMes, TransportistaId/Estado/Numero; OP LiquidacionId y origen OPP/Liquidacion, no FechaPago; Caja Fecha y CajaId/Fecha; Vehiculos Patente/TipoVehiculoId/TransportistaId.

Candidatos **no generados**, medir planes reales/IO antes de aceptar migración incremental: Facturas(FechaCobro,Id) INCLUDE(ClienteId,Estado,Total,NumeroComprobante) WHERE FechaCobro IS NOT NULL; OrdenesDePago(FechaPago,Id) INCLUDE(LiquidacionId,Importe,Vigente,OrdenPagoProveedorId,RevertidaEn); Viajes(VehiculoId,Fecha,Id) INCLUDE(Estado) para conteo por unidad. Si el dashboard anual prioriza rango Fecha, comparar alternativa (Fecha,VehiculoId) INCLUDE(Estado); no crear ambas por defecto. Existentes Fecha/Numero pueden bastar a viajes generales. No nuevos índices de Liquidaciones/Caja sin evidencia de scan costoso no resuelto por existentes. Indexar no cambia unicidad ni CHECKs; no tocar migraciones históricas.

5.000 aplica filas de cada exportación independiente: count primero bajo misma foto, cargar sólo si <=tope; no cargar histórico entero para después rechazar. KPI no llevan tope; categorías completas paginadas. Gráficos ranking muestran 10 primeros más «Otros» agregado exacto cuando exista resto; tabla completa y totales de todo el filtro. Series mensuales limitadas naturalmente a meses del rango: sin límite temporal artificial nuevo, resultado de una fila por mes (ceros completados en Application con datos ya agregados).

## 6. Riesgos y estado de la investigación

| Riesgo | Mitigación/puerta de implementación |
|---|---|
| R1 sin gráficos actuales | Recharts 3 estable propuesto, lazy-load, peer versions y bundle medidos; no instalación ahora |
| R2 XLSX | Guardar/reabrir/verificar todas las celdas/totales; nuevo rechazo documentado también en legacy |
| R3 volumen | Proyecciones/agregados SQL, índices candidatos medidos, protocolo NFR-003/004 |
| R4 doble cómputo | Separar documentos/flujos; sumar OP una vez, excluir revertidas de vigente; Caja independiente |
| R5 concurrencia | Snapshot transaccional, opción SQL requerida y readiness; sin fallback inconsistente |
| R6 compatibilidad 012 | Rutas/políticas intactas, guardia explícita y suite completa; DTOs nuevos localizados |
| R7 permisos | Políticas compuestas, proyección autorizada de dashboard antes de consultas; sin datos de secciones denegadas |
| R8 atributos de flota | Etiquetas «actual», cero/inactivas, sin reconstrucción histórica |
| R9 Empleados en paralelo | No dependencia ni cambios en su módulo; Program/App/semillas compartidos requieren merge acotado |
| R10 fuentes UI | gt-ui no encontrado en .claude/.agents/.codex de este entorno; tablas de diseño sobre primitivas reales, cotejo documental pendiente, no afirmar cumplimiento documental pleno |

No contradicción funcional detectada ni aclaración de negocio pendiente. R5 requiere capacidad operativa de habilitar snapshot; si no es posible en el despliegue, es bloqueo técnico a resolver antes de implementar consultas, no permiso para degradar NFR-005. Versiones/lib y rendimiento son decisiones/validaciones técnicas, no reapertura de AC/C. Fuentes externas consultadas sólo oficiales; no tests/build/SQL de aplicación ejecutados en esta etapa.
