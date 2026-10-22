# Contratos HTTP y UI — 015

Contrato diseñado, no implementado. Spec autoritativa: [../spec.md](../spec.md). DTOs/predicados exactos: [../data-model.md](../data-model.md). Todos GET autenticados con cookie existente; enums camelCase, errores en español argentino, dinero nuevo como string canónico con dos decimales. Ningún endpoint cambia datos. ConsultadoEn UTC ISO8601 con Z; DateOnly YYYY-MM-DD, período mensual YYYY-MM. Cantidades enteras no monetarias.

## 1. Separación de superficies

**Legacy012**: `/api/viajes/reporte`, `/api/vencimientos/reporte`, `/api/flota/vencimientos/reporte`, `/api/facturas/vencimientos/reporte`, `/api/movimientos-caja/reporte` permanecen con parámetros/políticas/cabeceras actuales. No requieren reportes.consultar. El único cambio de comportamiento explícito es rechazar XLSX con pérdida monetaria, AC-05, también en legacy. No alterar sus JSON monetarios de listados fuente.

**Consultas015**: prefijo `/api/reportes`; nuevos DTOs localizados. **Exportaciones015**: mismo recurso + `/exportacion?formato=pdf|excel`; no reemplazan legacy. Formato obligatorio, sin alias xlsx en query; extensión de entrega .xlsx.

## 2. Permisos por ruta

Todas las consultas salvo dashboard exigen `reportes.consultar` + permiso sección. Toda exportación nueva agrega `reportes.emitir`. Políticas `PoliticasAutorizacion.ParaTodos` existentes, registradas en Program, ningún if por rol.

| Recurso GET bajo /api/reportes | Permiso sección |
|---|---|
| `/dashboard` | Sólo base; devuelve exclusivamente secciones autorizadas, con estados sinPermiso sin datos |
| `/viajes`, `/viajes/exportacion` | viajes.consultar |
| `/facturacion`, `/facturacion/exportacion` | facturacion.consultar |
| `/facturacion/cobros`, `/facturacion/cobros/exportacion` | facturacion.consultar |
| `/liquidaciones`, `/liquidaciones/exportacion` | liquidaciones.consultar |
| `/liquidaciones/pagos`, `/liquidaciones/pagos/exportacion` | liquidaciones.consultar |
| `/caja`, `/caja/exportacion` | caja.consultar |
| `/flota`, `/flota/exportacion` | reportes.flota.consultar |
| `/vencimientos/choferes`, `/vencimientos/choferes/exportacion` | choferes.vencimientos.consultar |
| `/vencimientos/flota`, `/vencimientos/flota/exportacion` | flota.vencimientos.consultar |
| `/vencimientos/facturas`, `/vencimientos/facturas/exportacion` | facturacion.consultar |

Rutas de opciones/grupos heredan política de su recurso, sin reportes.emitir por ser lectura. Ambos permisos nuevos se siembran sólo Gerencia y AdministradorSistema de forma explícita, sin implicación de emisión/gestión. reportes.consultar agrega una entrada de menú Reportes, las pestañas internas se filtran por sección; reportes.flota.consultar sin base no crea entrada ni acceso. reportes.emitir continúa sin pantalla propia.

## 3. Parámetros

`desde`, `hasta`: obligatorios dashboard y siete consultas nuevas de actividad/documentos/flujos; inclusive. Reportes vencimientos no los aceptan: no fingir período histórico. Fechas inválidas/ausentes/rango invertido:400. Liquidaciones devuelve además `periodoLiquidadoDesde/Hasta` derivado de meses extremos; desde/hasta originales se mantienen para compartir navegación y pagos exactos. No límite de años nuevo. Referencias usan id:int positivo; desconocido como filtro válido devuelve vacío, nunca elimina filtro silenciosamente.

`pagina` default1, entero>=1; `tamanioPagina` default20, rango1..200. Sólo afectan detalle; exportación ignora página y tamaño si enviados y lo documenta, pero valida todos los filtros de negocio. Ordenes admitidos por lista cerrada abajo; no SQL libre. Query inválida retorna error de campo. Texto búsqueda/número sólo trim exterior, sin truncar. Opciones de lookup nunca se cargan todas en frontend.

| Consulta | Filtros adicionales | Orden default / alternativas |
|---|---|---|
| dashboard | ninguno | series mes ASC; rankings cantidad/importe DESC, Id ASC |
| viajes | clienteId,transportistaId,estado=pendiente/enCurso/rendido/anulado/facturado,busqueda | fechaDesc (Fecha DESC/Numero DESC) |
| facturacion | clienteId,estado=pendiente/vencida/pagada/anulada,numero | fechaDesc (Fecha/Numero/Id DESC) |
| facturacion/cobros | clienteId,numero | fechaCobroDesc/Id DESC |
| liquidaciones | transportistaId,estado=pendiente/pagada/anulada | periodoDesc (año/mes/Numero DESC) |
| liquidaciones/pagos | transportistaId,vigencia=todos/vigentes/revertidos (default todos) | fechaPagoDesc/Id DESC |
| caja | cajaId | fechaDesc/Id DESC |
| flota | vehiculoId,tipoVehiculoId,transportistaId,activo bool? (ausente todos) | cantidadDesc o cantidadAsc, Patente/Id ASC |
| vencimientos/* | pagina,tamanioPagina solamente | urgencia y desempate de panel actual |

Estado documental no se envía implícitamente al navegar cobros/pagos. Transportista/cliente/período sí se conservan. Anulados se solicitan explícitamente; default financiero no los suma. Booleanos opcionales bool?; no confundir ausente con false.

## 4. Respuestas de análisis

Wrapper de consulta nueva:
`{ contexto, resumen, series, distribuciones, detalle:{items,total,pagina,tamanioPagina} }`.
`contexto={desde,hasta,consultadoEn,hoyArgentina,criterioTemporal,periodoLiquidadoDesde?,periodoLiquidadoHasta?}`.
No usar propiedades opcionales arbitrarias para dinero: DTO concreto por recurso, todos sus totales presentes incluso cero. No campos ajenos a la sección. Vencimientos usa `{contexto:{consultadoEn,hoyArgentina,criterioTemporal:"situacionActual"},detalle}` con filas de los paneles actuales y total importes sólo facturas.

| Recurso | resumen (cadenas en importes) | detalle.items |
|---|---|---|
| viajes | cantidad,realizados,pendientesRendicion,pendientes,anulados,importeTotal | id,numero,fecha,cliente{id,nombre,activo},origen,destino,chofer?,vehiculo?,transportista?,estado,importe |
| facturacion | cantidad,facturado,cobradoVigenteDocumental,pendienteVigenteDocumental,cantidadAnuladas | id,numeroComprobante,fecha,clienteId,clienteRazonSocial,estado,neto,iva,total,fechaCobro? |
| facturacion/cobros | cantidadCobros,importeCobrado | facturaId,numeroComprobante,clienteId,clienteRazonSocial,fechaFactura,fechaCobro,importeCobrado |
| liquidaciones | cantidad,importeLiquidado,pagadoVigenteDocumental,pendienteVigenteDocumental,cantidadAnuladas | id,numero (LQ visible),periodo,transportista{id,nombre,activo},estado,importeTotal,pagadoVigente,pendienteVigente? |
| liquidaciones/pagos | cantidadPagosVigentes,importePagadoVigente,cantidadRevertidos,importeRevertidoHistorico | id,numero (OP visible),liquidacionId,liquidacionNumero,periodoLiquidado,transportista,fechaPago,importe,vigente,origen=manual/opp,oppId?,oppNumero?,revertidaEn?,revertidaPor? |
| caja | cantidad,ingresos,egresos,neto | id,cajaId,cajaNombre,fecha UTC,tipo,importe,concepto,responsable,referencia?,ordenDePagoId?,ordenPagoProveedorId? |
| flota | cantidadUnidades,totalViajesConVehiculo,sinVehiculoEnPeriodo | id,patente,tipo{id,nombre},transportista{id,nombre},activo,estadoOperativoGuardado,disponibilidadActual,cantidadViajes |

Identificadores visibles OP/LQ/OPP los arma servidor reutilizando helpers; nunca prefijar en JS. Referencias opcionales no conceden permiso para navegar/descargar ficha externa. Reportes no devuelven CBU, CAE completo ni adjuntos cuando no necesarios al análisis.

`series` son arrays mensuales con métricas del resumen según criterio de fecha: viajes cantidad/realizados/pendientesRendicion; factura facturado/cobradoVigenteDocumental/pendienteVigenteDocumental; cobros importeCobrado; liquidaciones importeLiquidado/pagadoVigenteDocumental/pendienteVigenteDocumental; pagos importePagadoVigente; caja ingresos/egresos/neto; flota cantidadViajes. Meses sin actividad=0. Fechas de documento y flujo nunca en una serie sin etiquetas distintas.

`distribuciones` entrega mapas tipados de estado, cliente, transportista o tipo según recurso. Cada distribución gráfica contiene `{top:[{id,nombre,cantidad,importe?}],otros:{cantidad,importe?},totalCategorias}`. top10 determinista por métrica DESC/Id; Otros sólo si resta, identificador sintético diferente de null «Sin asignar». Para acceder a **todas** las categorías sin descargar entidades: GET `/api/reportes/{recurso}/grupos?dimension=...&...filtros&pagina=1` devuelve PaginaDe<Grupo> con contexto/resumen completo. Recursos anidados también admiten `/grupos`. Dimensiones permitidas: viajes estado/cliente/transportista; facturacion estado/cliente; cobros cliente; liquidaciones estado/transportista; pagos transportista; caja tipo; flota tipo/transportista. Misma foto por respuesta grupos, no garantía entre llamadas.

Dashboard responde `{contexto,secciones:{viajes:{estado,datos?},facturacion:{...},liquidaciones:{...},caja:{...},flota:{...},vencimientosChoferes:{...},vencimientosFlota:{...},vencimientosFacturas:{...}}}`. Estado disponible|sinPermiso. datos contiene resumen/series/top y enlaces de cada perspectiva, nunca detalle completo. Facturacion incluye resumen/series de documentos y cobros diferenciados; Liquidaciones de documentos y pagos. Vencimientos informa situación actual y enlace autorizado, no filtros desde/hasta. Sin permiso no hay datos/importe/cantidad/nombres ni consulta de sección. Excepción SQL de la foto devuelve error de request completo; no inventar estado cero.

## 5. Referencias mínimas

GET `/api/reportes/viajes/referencias/clientes|transportistas`, `/facturacion/referencias/clientes`, `/liquidaciones/referencias/transportistas`, `/caja/referencias/cajas`, `/flota/referencias/vehiculos|tipos|transportistas`. Política igual sección. Parámetros busqueda opcional,pagina,tamanioPagina. Datos `{id,nombre,activo?}` solamente; universo registros existentes incluidos inactivos. Cobros usa lookup Facturación y Pagos el de Liquidaciones por igual permiso; no requieren permiso adicional. Respuesta PaginaDe, orden nombre/Id o patente/Id. No FKs/Version de gestión ni padrones completos. Dashboard no ofrece filtros adicionales aparte del período.

## 6. Exportaciones

Filtros idénticos consulta, sin pagina; formato pdf|excel obligatorio. R3 y R4 exportan por perspectiva independiente (documentos/cobros, documentos/pagos), los dos accesibles en su pantalla; encabezado y archivo distinguen criterio. R1 y Caja nuevos usan columnas legacy y resúmenes nuevos explícitos sin alterar exportadores legacy. Flota una fila por unidad y suma de viajes con contexto de sin vehículo separado. Vencimientos nuevos llaman fuentes existentes bajo permiso compuesto. Para todos count>5000:409, no materializar filas ni bytes parciales. Exactamente5000 admitidas; UI no emite vacío. Invocación directa con cero puede mantener archivo vacío conforme al comportamiento actual legacy, sin habilitar botón nuevo cuando vacío.

PDF: application/pdf, inline; XLSX: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, attachment. Content-Disposition filename del servidor, X-Content-Type-Options nosniff, Cache-Control no-store para nuevas consultas/archivos. Archivo incluye título/criterio, filtros en palabras, cantidad, autor y generación argentina, resumen y detalle; fechas/cantidades/moneda con tipos de reporte apropiados. Exportación XLSX valida guardia tras SaveAs/reapertura y antes de devolver cualquier byte. No incluir gráfico ni almacenar archivo/auditoría.

## 7. Errores

| HTTP | codigo | Conducta |
|---|---|---|
|400|rango_requerido / rango_invalido / filtro_invalido / formato_invalido|{codigo,mensaje,errores?:{campo:[mensaje]}}; corregir campo, sin alterar filtros aplicados anteriores |
|401|sesión existente|cliente HTTP expira sesión como hoy |
|403|sin_permiso|mensaje uniforme «No tenés permiso para consultar o emitir este reporte. Pedíselo a quien administra el sistema.»; no revelar cuál requirement falta |
|409|tope_de_filas_superado|cuerpo legacy {codigo,mensaje,filas,tope}; acotar filtros, no handshake |
|409|precision_monetaria_xlsx|{codigo,mensaje}; PDF/consulta disponibles; aplica también error nuevo a cinco legacy, preservando demás errores |
|422|analisis_fuera_de_rango|{codigo,mensaje}; suma no representable, nunca total parcial |
|503|analisis_no_disponible|snapshot no habilitado/timeout/transitorio; mensaje de reintento, no fallback de aislamiento |
|500|reporte_no_generado / analisis_no_generado|error inesperado, sin SQL/stack; conservar filtros |

Los errores legacy previos permanecen con sus textos/códigos. Nuevos códigos se añaden a tipos frontend, no sustituyen globalmente ErrorApi. No 409 de confirmación ni idempotencia económica: son GET sin efectos. Ningún dato de sección denegada en errores o logs sensibles.

## 8. Rutas UI y estado

`/reportes` Dashboard; `/reportes/viajes`, `/reportes/vencimientos`, `/reportes/facturacion` (pestañas Documentos/Cobros), `/reportes/liquidaciones` (Documentos/Pagos), `/reportes/caja`, `/reportes/flota`. Mantener rutas fuente; no mover componentes de 012. Filtros aplicados en URL (sin secretos) permiten volver/recargar, borrador separado; aplicar reinicia página. No estado de reporte en storage/history.state, datos sólo memoria de request/sesión. No exportar cuando hay filtro borrador distinto del aplicado sin avisar qué conjunto se exporta; acción usa filtros aplicados visibles.

`SelectorPeriodo`, `KpiReporte`, `GraficoAccesible`, `ResumenReporte` y tablas por perspectiva; `GenerarReporte` extendido mediante props/rutas tipadas aditivas, manteniendo unión legacy. Token de secuencia por hook ignora respuesta/error obsoleto tras cambio de filtro o sesión. El cliente HTTP actual no tiene AbortSignal: la secuencia basta para corrección; cancelación opcional futura aditiva requiere preservar 401 y AbortError sin mensaje falso de conexión. Nunca usar fetch nuevo que omita cookies/manejo401. Los datos previos no se rotulan con período nuevo mientras carga.
