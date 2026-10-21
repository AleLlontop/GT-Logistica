# Modelos y consultas de lectura — 015

No nuevas entidades persistidas, tablas de negocio, cambios de estado ni bitácora de emisión. DbContext/entidades existentes son fuente; DTOs Application son proyecciones efímeras. Posibles índices y opción de base snapshot: research §§2/5; no migraciones en planificación.

## Foto y unidades

`PeriodoAnalisis(Desde, Hasta)` DateOnly inclusivos; `ContextoAnalisis(Desde,Hasta,ConsultadoEn UTC,HoyArgentina,criterioTemporal)`. Capturar reloj una vez por request. Nuevas respuestas se materializan dentro de una transacción Snapshot y devuelven su contexto. Cada exportación hace su propia foto; no se comparte transacción entre requests ni entre navegador y descarga. Tras materializar DTO/ReporteTabular, liberar transacción antes del render. No escrituras, tracking de entidades ni llamadas a casos de uso de pago.

Fechas negocio: comparar DateOnly directamente. Caja UTC: desde 00:00 -03 hasta día siguiente a hasta 00:00 -03 exclusivo. Manejar 9999-12-31 sin AddDays overflow usando límite abierto equivalente o rechazo de rango representable documentado de entrada; no cambiar días por UTC. Liquidaciones: (año,mes) desde mes de Desde hasta mes de Hasta, inclusivos; sin convertir a fecha de generación/prorratear. Serie de meses parciales: fecha exacta para flujos/viajes/facturas y mes completo para liquidación; tooltip/contexto advierten diferencia.

Importe de fila SQL decimal(18,2); sumas SQL pueden ser decimal(38,2), proyección a decimal .NET con comprobación de overflow. DTO nuevo serializa decimal como cadena canónica de dos decimales sin separadores, también negativa para neto Caja. Converters localizados existentes de 013/014: validar que cubran signo/sumas antes de reusar, sin tocar contratos previos. Valor fuera de decimal .NET: error analisis_fuera_de_rango, no total parcial. Cantidades long no monetarias; usar JSON entero sólo mientras sea seguro en JS (volumen previsto muy inferior a 2^53); validar overflow.

## Proyecciones compartidas

- `PaginaDe<T> {items,total,pagina,tamanioPagina}` existente, default20; repositorio recibe tamaño opcional sólo con default en interfaz. Orden total por Fecha/Numero/Id o ranking/campo/Id.
- `SerieMes {periodo:"YYYY-MM",cantidad?,importe?,ingresos?,egresos?,neto?}`: DTOs concretos por sección, sin bolsa de propiedades arbitrarias. Completar meses vacíos con cero en Application; no inferir actividad.
- `GrupoAnalisis {id nullable, nombre,cantidad,importe?}`: agrupar por Id, nombre es presentación; null explícito para «Sin transportista/asignación» cuando corresponde. Clientes facturados conservan snapshots: agrupar por ClienteId; etiqueta actual identificada como tal en agrupación, filas conservan nombre histórico; no dividir una persona por cada renombre.
- `Resumen`, `series`, `distribuciones` y `detalle` específicos en cada respuesta. Tabla completa de distribución paginada independientemente del detalle; gráficos obtienen resumen top10+Otros del total SQL, no de la página.
- `ReporteTabular` existente para filas/columnas/totales de exportación. Cantidad de filas no reemplaza cantidad de viajes (R6 cuenta unidades por fila). Resúmenes enteros se expresan como Entero con extensión aditiva si hace falta pie; no importes ficticios.

## Consultas y reglas por sección

### Viajes (R1)

Base Viajes Fecha entre días, cliente/transportista/búsqueda equivalentes a 005, estado específico si provisto; default excluye Anulado como listado legacy. Campos: id/numero/fecha/cliente/origen/destino/chofer/vehículo/transportista/estado/importe. Orden Fecha DESC, Numero DESC. Total completo y SUM Importe, grupos por estado, cliente, transportista y mes. Realizados = Estado 2 Rendido o4 Facturado; pendientesRendicion =1 EnCurso; pendientes =0. Los conteos suman particiones del mismo universo; anulado aparece sólo con filtro explícito (o distribución de anulados solicitada mediante ese filtro), nunca como realizado. Dashboard sin filtro de estado sigue universo default no anulado; etiqueta deja visible esa exclusión. No medir remitos faltantes leyendo NumeroRemito: rendición existente es la señal aprobada.

### Facturación documental (R3-documentos)

FacturaCliente por Fecha inclusiva; clienteId y númeroComprobante combinados, estado visible exclusivo según hoy capturado: pendiente guardado0 con VencimientoPago>=hoy; vencida guardado0 anterior a hoy; pagada1; anulada2. Default sin filtro excluye Anulada en análisis financiero; estado=anulada permite detalle/historia. Usar mismos predicados que 006, sin invocar ConsultarTotales existente para flujo.

Cada fila: id, numeroComprobante, fecha, clienteId, clienteRazonSocial snapshot, neto/iva/total, estadoVisible, fechaCobro. Importes documentales anulados visibles pero aportan cero a facturado/cobradoVigente/pendienteVigente; desglose de estados/cantidad informa anuladas seleccionadas. Facturado=sumTotal no anuladas del conjunto; cobradoVigente=sumTotal EstadoPagada; pendienteVigente=sumTotal EstadoPendiente (incluye vencidas). Total financiero no se calcula desde cobros temporales. Refacturación es documento independiente: original anulada no suma, reemplazo vigente sí. Orden Fecha DESC, NumeroComprobante DESC, Id DESC (número puede repetirse históricamente).

Series documentales por Fecha mes, distribución estado, facturado por ClienteId; lista de clientes incluye inactivos presentes en fuente.

### Cobros temporales (R3-cobros)

FacturaCliente EstadoPagada y FechaCobro dentro de días, sin limitar Fecha de emisión al rango. Filtros clienteId/número comunes; filtro estado pertenece sólo a perspectiva documental y no se propaga ocultamente a cobros (una factura pendiente no tiene cobro). Fila id/numero/cliente snapshot/fechaFactura/fechaCobro/importeCobrado=Total; no inventar pago parcial. Orden FechaCobro DESC, Id DESC. Resumen cantidadCobros e importeCobrado; series por FechaCobro mes y grupos ClienteId. Puede traer documentos emitidos fuera del mes; enlaces con facturacion.consultar.

### Liquidaciones documentales (R4-documentos)

Liquidaciones por (PeriodoAnio,PeriodoMes), transportistaId y estado. Default financiero excluye Anulada; filtro explícito Anulada conserva filas. Campos id/numero visible LQ/periodo/transportista actual y activo/estado/importeTotal/pagadoVigente/pendienteVigente. Para no anuladas: pagadoVigente=ImportePagado del agregado protegido de 009; pendiente=ImporteTotal-ImportePagado. Para anuladas: total nominal visible, contribución financiera cero, pendiente no aplicable/null como DTO actual; nunca deuda. Orden PeriodoAnio DESC, PeriodoMes DESC, Numero DESC.

Resumen cantidad, importeLiquidado (no anuladas), pagadoVigenteDocumental, pendienteVigenteDocumental; series por mes liquidado, grupos estado y transportista. Pruebas cotejan agregado pagado con SUM(OP Vigente); no join plano entre OP y vínculos de viajes para sumar (multiplicaría filas).

### Pagos temporales (R4-pagos)

OrdenesDePago por FechaPago entre días, join Liquidacion y Transportista, sin restricción de período liquidado. Filtros transportistaId y vigencia (todos|vigentes|revertidos, default todos para historia visible). Estado de liquidación aplica sólo tabla documental, no filtro implícito del flujo. Fila OP id/numero, liquidacionId/numero/periodo, transportista, fechaPago, importe, vigente, origen manual|opp, oppId/numero nullable, revertidaEn UTC/autor cuando exista. OP histórica manual con origen null cuenta si Vigente; OP propia OPP cuenta una vez si Vigente. OPP nunca es otro sumando.

Resumen cantidadPagosVigentes/importePagadoVigente y cantidad/importeRevertidoHistorico separados. Revertidos no aportan a total vigente incluso con RevertidaEn posterior a Hasta. Series vigentes por FechaPago mes; detalle de revertidos permite explicar su exclusión. Cuando se filtra revertidos, total vigente del conjunto=0 y total revertido explícito, sin presentar importes históricos como gasto vigente. Orden FechaPago DESC, Id DESC. Los egresos de Caja se consultan aparte, no se suman a OP.

### Caja analítica (R5)

MovimientosDeCaja por instante argentino convertido a UTC y cajaId opcional; tipos ingreso/egreso existentes. Reusar proyección de referencia de 011 (Factura/OP/OPP) y lectura responsable. Ingreso=sum tipoIngreso; egreso=sum tipoEgreso; neto=ingreso-egreso, puede ser negativo; no saldoInicial/final. Serie por mes de Argentina. Orden Fecha DESC, Id DESC. No omitir compensación ni egreso original; sus fechas determinan período independiente. R5 legacy usa fuente original; consulta nueva snapshot y exportación nueva con mismos datos/columnas.

### Utilización de flota (R6)

Subconsulta Viajes Fecha entre días y Estado in(2,4) GROUP BY VehiculoId COUNT, excluyendo null del join. LEFT JOIN desde todos Vehiculos sin filtro Activo por defecto; COALESCE conteo=0. Filtros vehiculoId/tipoVehiculoId/transportistaId/activo? sobre unidad; no filtro EstadoViaje variable que cambie la definición aprobada. Tipo/propietario actuales, disponibilidad se deriva contra hoy de foto reutilizando CalculadorEstadoVehiculo y una proyección en lote de documentación; no una consulta por unidad. Comparar dominio/predicado si se calcula disponibilidad en SQL.

Filas id/patente/tipoId/nombre/transportistaId/nombre/activo/estadoOperativoGuardado/disponibilidadActual/cantidadViajes. Ranking cantidad DESC, Patente ASC, Id ASC; menor uso puede solicitar orden=cantidadAsc con desempate igual. Incluye ceros e inactivos. Distribución por tipo actual y propietario; serie mensual opcional cuenta viajes del mismo universo de unidades filtradas. Viajes realizados sin VehiculoId: contador separado en resumen con mismo rango; filtros tipo/propietario/vehiculo no permiten atribuirles unidad, así contador rotulado «sin vehículo en el período, fuera del filtro de unidades». TotalViajesConVehiculo=sum conteos; no sumar sinVehiculo al ranking. No snapshots inventados.

### Vencimientos (R2) y dashboard

Vencimientos nuevos: fachada que consulta los tres paneles actuales, según permiso, con fechaHoy capturada; tabla y orden de urgencia actuales. Sus listas legacy no se paginan hoy: vistas nuevas paginan el resultado ya definido por el panel sin cambiar universo ni imponer período. No se agrega un filtro nuevo a endpoints legacy. Nuevas exportaciones reutilizan fuentes originales dentro del scope de foto; exigen además permisos 015.

Dashboard: `secciones` por código viajes/facturacion/liquidaciones/caja/flota/vencimientosChoferes/vencimientosFlota/vencimientosFacturas; cada una {estado:disponible|sinPermiso,datos?}. Permiso base reportes.consultar; obtener lista de permisos de sesión actual por servicio de autorización existente antes de pedir datos. Secciones no autorizadas nunca ejecutan consultas y no traen datos, nombres, cantidades o filtros de opciones. El repositorio recibe conjunto autorizado definido por Application/Api, nunca flags de query del cliente. Cada sección disponible proyecta resumen/series/top10+Otros y contexto temporal; no tablas de entidades completas. Transacción única snapshot para las secciones disponibles. Un fallo de consulta invalida la foto completa con error y UI conserva el filtro para reintentar; no datos parciales etiquetados como completos.

## Referencias de filtros

Rutas propias mínimas de Reportes para clientes/transportistas/unidades/tipos/cajas usados en cada sección; búsqueda SQL paginada20 y orden Nombre/Id (Patente/Id para unidades). Inactivos incluidos, sin padrones completos, sin Version de edición ni secretos/adjuntos. Política igual a lectura de esa sección; nombres de clientes históricos en filas congelados y opción actual rotulada como tal. No pedir gestión para cargar un selector.
