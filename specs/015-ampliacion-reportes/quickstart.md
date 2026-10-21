# Validación prevista — 015

Este documento prescribe pruebas futuras, no resultados ejecutados. Plan/spec aprobada como fuentes; no tasks ni implementación en este comando. Entorno de aceptación: SQL Server real del compose común Docker/Podman, SDK de global.json10.0.302, Node24/npm del proyecto y secretos externos. No SQLite/InMemory para agregados/aislamiento/índices. No copiar credenciales al repositorio.

## Preparación y línea base

1. Registrar rama/contexto015 y diff antes de cambios; spec permanece cerrada. Ejecutar suites 012 y módulos fuente tal como están; preservar aserciones. El baseline intermitente009 registrado en014 se observa y documenta, nunca se modifica para ocultar regresión.
2. Levantar SQL con compose existente, comprobar healthy. Fixture `GT.IntegrationTests/Infraestructura/AplicacionDePrueba` crea base efímera con migraciones y configuración externa. Durante implementación ampliar inicialización para habilitar ALLOW_SNAPSHOT_ISOLATION y confirmar ON antes de pruebas015; no alterar RCSI ni usar base de desarrollo para simular fallos.
3. Ensayar opción OFF en otra base efímera: nuevas consultas devuelven503 y diagnóstico operativo, legacy sigue con comportamiento anterior. Habilitación por cuenta autorizada fuera de transacción; runtime no requiere ALTER. Probar eliminación de bases efímeras al finalizar.
4. Registrar versiones reales, recursos CPU/RAM, motor SQL, collation y estado snapshot. Migraciones sólo si índices medidos las justifican, siempre incrementales; historial sin editar.

Comandos de validación durante implementación (no ejecutados ahora):

```bash
# Desde raíz, usar ruta explícita del SDK si dotnet resuelve otro SDK
/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore
/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore
/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore
```

Desde frontend: `npm test`, `npm run build` (incluye tsc -b), `npm run lint`. Antes de suites completas ejecutar pruebas focalizadas Reportes; no repetir suites largas si no hubo cambios posteriores que las afecten. TRX/capturas y datos temporales fuera del repositorio. No secretos en comandos registrados.

## Matriz de pruebas

| Capa | Casos exigidos |
|---|---|
| Unit dominio/application | Clasificar todos los estados de viaje,2/4 realizados sin duplicación y1 pendiente rendición; DateOnly inclusive, UTC Argentina, años/meses/bisiesto/mes parcial; series cero, mapas de estado, selección documental vs flujo; netos/sumas exactas/overflow y helpers de guardia XLSX |
| SQL agregación | Fechas exactas vs meses liquidados, clientes/transportistas inactivos, snapshot de factura vs nombre actual, filtros combinados/estados exclusivos, anulados financieros cero, refacturación sin doble importe; COUNT/SUM reales vs detalle completo |
| Pagos | Manual/parcial, variasOP por OPP, Vigente false/reversión posterior a rango, nueva OPP tras anulación, egreso/compensaciónCaja separados; no sumar OPP además de OP ni Caja al pago |
| Flota | LEFT JOIN ceros/inactivos, Rendido + Facturado por Fecha/VehiculoId, nullVehiculo separado, ranking empates/menor uso, tipo/propietario/disponibilidad actual; dominio vs predicado de disponibilidad si se duplica regla |
| Páginas/grupos | 0/20/21/200 filas, orden total, ninguna duplicación entre páginas, categorías>10 con Otros exacto y tabla completa paginada, filtros de referencias mínimos/inactivos |
| Autorización | 401, reportes.consultar solo, permiso fuente solo, ambos, emitir solo y conjunción; dos permisos nuevos sembrados explícitos a Gerencia/AdministradorSistema; no otros roles por implicación; las cinco legacy no exigen nuevo base; sección denegada no consulta ni entrega cifras/nombres; links/adjuntos fuente siguen protegidos; revocación actual |
| Exportación | PDF real resolviendoDI, XLSX reabierto, encabezado/contexto/autor/filtros/tipos, columnas legacy intactas; filas5000 admitidas y5001 rechazo antes de materializar archivo; UIvacío sin generación; formatos/errores/cabeceras/nombres |
| Sin efectos | Comparar estados/importes/historial/movimientos antes y después de todas las lecturas/exportaciones; no archivo persistido ni auditoría nueva |
| Frontend | Dashboard/secciones/pestañas/KPI/serie/tabla, filtrosURL y borrador, tooltip de cadena exacta, sin Number monetario, cancelación/exportación, estadoLoading/vacío/error/403/sinPermiso, sesiones y respuesta obsoleta resuelta al final sin reemplazar la vigente |
| Accesibilidad | Teclado, foco/anillo/retorno, Escape, controles nativos/autocomplete, aria labels y alert en párrafo, gráfico con tabla completa, contraste fondo real,375px/1280px y sin scroll horizontal global |
| Regresión | Suite012 completa y009/011/013/006/005/004/003tocados, reportes012 según filtros/orden/palabras/totales/errores, fixture general sin cambio de comportamiento de escrituras |

### Precisión XLSX

Probar helper con 0,0.01,0.10,1.23,importes negativos de Caja, ceros finales, escala inválida, valores alrededor de cambios de precisión de double y máximo decimal(18,2); agregados que superan el máximo de una fila y totales que pierden centavos aunque cada fila aislada sea exportable. No usar «todos los grandes fallan» como criterio: lo decide round-trip a dos decimales. Incluir un grande representable admitido y otro que efectivamente pierde un centavo rechazado.

Integración: escribir libro, SaveAs, reabrir mismos bytes, confirmar tipoNumber y comparar cada coordenada monetaria/total de manifest con decimal fuente a escala2. Caso no representable devuelve409precision_monetaria_xlsx sin archivo parcial ni Content-Disposition exitoso; PDF del mismo modelo sigue válido. Verificar guardia común también en viajes/caja/vencimientosFacturas legacy y captura tipada antes de catch genérico500. Los tests anteriores de valores normales no se debilitan.

No exigir identidad binaria, no usar epsilon para aceptar centavos perdidos, no cambiar fuente/libro ni texto monetario como workaround. Prueba de culturas es-AR e invariant y lectura con Excel/LibreOffice cuando se disponga: registrar herramienta/versión y limitaciones, sin afirmar ejecución inexistente.

### Foto concurrente de lectura

Dos conexiones/contextos independientes. Barrera/interceptor detiene lector 015 después de primer agregado de la foto; escritor cobra factura/pagaOP/anulaOPP o cambia tipo vehículo y confirma; liberar lector. Toda su respuesta debe representar la foto anterior coherente (totales,series,distribuciones,página), no combinación. Una nueva consulta ve cambio confirmado. No sleeps ni UPDATE no-op económicos desde lector. Probar fila insertada que afecte Count y página para cubrir fantasmas; Snapshot OFF→503. No paralelizar SELECT en mismo DbContext.

La exportación materializa dentro de foto y renderiza después de cerrar transacción; escritor durante render no cambia el modelo exportado. Requests distintos no se comparan como una foto histórica global.

## Medición NFR-003/004

Sembrar base sintética aislada con10.000 Viajes,5.000 Facturas,5.000 Liquidaciones,10.000 Movimientos, relaciones y estados válidos con datos distribuidos entre meses; incluir ceros/inactivos y pagos OP de ambas procedencias. Rango anual explícito. Registrar número real de OP/vehículos/clientes, CPU/RAM/SQL/.NET/Node, índices y opciónSnapshot; no usar datos reales de personas.

Instrumentar DbCommandInterceptor/contadores, métricas separadas SQL/HTTP/render y planes reales/STATISTICS IO en harness, nunca exponer datos por endpoint de producción. NFR-003: misma sección/filtros/20 vs200 filas no aumenta consultas proporcionalmente; lista de comandos y conteos junto al resultado. Cambiar cantidad de secciones autorizadas puede cambiar un número fijo de consultas, no por fila.

NFR-004: tras registrar una corrida de calentamiento separada (no contada), medir20 requests secuenciales, una sesión activa. Dashboard anual y cada primera página: al menos19/20<=5s de tiempo HTTP completo. Exportar1.000filas PDF y XLSX<15s; XLSX incluye guardar/reabrir/validar. Medir cada familia/perspectiva, no sólo Viajes. Registrar todos los tiempos/fallos, no descartar outliers silenciosamente. Tabla de gráficas usa agregados, no histórico descargado.

Comparar planes/IO antes/después de índices candidatos. Sólo aceptar índice que mejore consulta y no duplique otro; incorporar migración incremental en implementación si necesario, upgrade vacía/legacy y snapshot EF verificado. Ningún índice ni opciónSQL creada durante este plan.

Frontend: registrar bundle baseline y final raw/gzip, chunks iniciales vs lazy Reportes/gráficos, peers React19 y versión estable Recharts fijada. Aceptación visual no se sustituye con build; medir render/listado y no bloquear tabla mientras se carga gráfico.

## Recorridos de aceptación operativa

1. Gerencia abreReportes: mes argentino correcto en borde UTC y seis áreas analíticas más accesos a vencimientos; cambiar período no muta nada. Cada cifra tiene título/contexto y tabla accesible.
2. Viajes: Pendiente,EnCurso,Rendido,Facturado,Anulado; EnCurso incluye terminado esperando remito sin estado nuevo. Realizados suma2/4una vez; exportaciones originales siguen en listadofuente.
3. FacturaSeptiembre cobradaOctubre, facturaOctubrePendiente y anulada/refacturada: Documentos y Cobros muestran universos distintos; deuda no es facturado menos cobrosdelmes. Filtrar cliente inactivo y número.
4. LiquidaciónSeptiembre pagadaOctubre y pago parcialmanual: seleccionar15/09–10/10incluye meses completos, pagos por días exactos. OPP con varias OP no duplica importe. Revertida posterior aHasta conserva detalle y no suma vigente.
5. Caja con egresoOPP e ingresoCompensatorio en mesesdistintos: neto del período respeta ambos hechos, no muestra saldoDisponible ni suma importes deOPotra vez.
6. Flota: cero viajes,inactivos,tipo/propietarioactual,asignaciónnula y ranking empate; no km/horas/porcentaje. Tabla explica exactamente conteoRendido + Facturado.
7. VencimientosChoferes/Flota/Facturas: mismos paneles/urgencia/formatos, situación actual visible; período global no cambia alertas.
8. Cada perspectiva exporta21filas desde página1de20; tope5001 rechazo explícito; factura grande no representable monetariamente enXLSX rechaza completo y PDF/consulta continúan.
9. Usuario con sólo base no recibe cifras fuente; ambos permisos habilitan sección; emitir independiente. URLslegacy siguen con sus permisos previos; revocación y rutasdirectas protegidas.
10. Filtros aplicados vs borrador,respuesta HTTP vieja al final,errores y vacío,teclado/foco/375px/1280px,tablas equivalentes y contraste real. Si gt-ui sigue ausente, documentar aceptación documental pendiente R10 sin inventar reglas.

## Registro pendiente

Al implementar registrar comandos exactos, versiones, conteos, TRX externos, migración/índices/opciónSnapshot, tiempos NFR, bundle, carreras, Excel y aceptaciónoperativa, regresión y riesgos R1–R10. Ninguna de esas pruebas se ejecutó durante esta planificación. `git diff --check` y verificación documental final sí corresponden a este comando. No marcar resultados futuros como cumplidos ni generar tasks.md.


## Resultados ejecutados — 2026-10-21

La sección anterior describe el estado de planificación; la ejecución posterior está registrada en [implementation-log.md](implementation-log.md) y [acceptance-evidence.md](acceptance-evidence.md).

- Backend: build correcto;848 unitarios y1306 integración SQL completos aprobados. Prueba adicional de performance aprobada. Ajuste local final de PDF validado con pruebas focalizadas, sin repetir suites completas innecesariamente.
- Frontend:699 pruebas en101archivos; build/typecheck/lint correctos.
- Snapshot: OFF produce503 sólo en nuevas lecturas; ON y foto concurrente comprobados; provisión vacía/upgrade legacy ensayados. Activación administrada requerida en despliegue; runtime no modifica opciones. Sin índices ni migración adicionales tras medir.
- Dataset anual aprobado:20/20 solicitudes dentro de5s para11recursos, máximo dashboard398ms; SQL20/200 acotado. Exportación1000filas PDF1203ms yXLSX2010ms, incluida reapertura.
- Recharts3.10.1 lazy; principal804033bytes/gzip201772, chunk gráfico375905/106656. Sin descarga gráfica fuera de Reportes.
- Aceptación real por agente: documentos/flujos, OP/OPP, permisos, límites y precisión;375/1280, teclado y contraste. No constituye aprobación humana. Bases y hosts efímeros eliminados.
- Residuales: gt-ui ausente(R10documental), warnings preexistentes y causa histórica de intermitencia009 sin declarar resuelta. Sin commits/push.
