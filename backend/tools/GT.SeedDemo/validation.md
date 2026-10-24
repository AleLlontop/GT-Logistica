# Validación de DEMO015 v1

Ejecución local: 2026-10-21–22, Argentina. Operador agente; no aprobación humana.
Servidor validado: localhost,1433 desde host / sqlserver,1433 desde Compose.
Environment=Development, Database=GtLogistica. No secretos ni connection strings completas en este informe.
No código productivo, SembradorInicial, modelo, migraciones ni índices modificados.

## Resultado integral

450viajes,100personas,35clientes(30activos/5inactivos),21transportistas(20externos+1propio;
17activos/4inactivos),50choferes(42/8),30proveedores(20normales/10fleteros),75vehículos,
140facturascliente,120facturasproveedor,60liquidaciones,51OP,30OPP,28cheques,6bancos,
2cajas,185movimientos y40adelantos. Los usuarios son admin original más4DEMO; roles/permisos se conservan.

El transportista propio adicional permite adelantos a choferes propios usando exactamente la regla
que compara su CUIT con la empresa emisora. Hay20adelantos de chofer y20de empleado del padrón,
con pendientes/aprobados/rechazados/anulados por sus operaciones reales. No tabla Empleados ni
código de la rama de Juan. No se inventaron campos de año, acoplamiento tractor/semi o historia
de asignaciones inexistente. Vehículo es la entidad común; la serie/modelo es texto del catálogo real.
Las facturas de proveedor tienen vencimiento CAE; no se inventa un vencimiento comercial no modelado.

Facturas:83pendientes,54pagadas,3anuladas. Quince de septiembre cobradas en octubre.
Liquidaciones:28pendientes(incluyen14parciales),31pagadas y1anulada; OP45vigentes/6revertidas.
OPP de fleteros agrupan tres liquidaciones cada una; anulación revierte sólo sus OP. Hay4egresos
OPP y1ingreso compensatorio por otro autor. Caja manual180movimientos; no se presenta su neto como saldo.
Cheques:14pendientes,6entregados,2cobrados,2rechazados y4anulados; los depósitos intermedios
constan en historial. Bancos/CBU/comprobantes sintéticos, sin cuentas bancarias reales.

Documentación:45licencias(5choferes sin documento) y280documentos de flota, incluidos previos y
renovaciones/cargas del mismo tipo; vencimientos derivados. Hay adjuntos PDF sintéticos reales,
con leyenda sin validez, almacenados en el volumen compartido. Los documentos inactivos no se
fuerzan al panel. Cuentas DEMO con fecha de alta julio2026, anterior a todos sus eventos.

## Inventario exhaustivo del modelo

37DbSet,39tipos EF incluyendo2uniones implícitas;36tablas físicas mapeadas. Las filas de subtipos
TPH no se suman otra vez al total de MediosPagoProveedor. `Antes` corresponde a la base antes del
último reset de validación, después de preparar semillas estructurales; `Creados` incluye las
filas DEMO recreadas, no sólo variación neta. No se ocultan tablas.

| Entidad / tabla | Antes | DEMO creados | DEMO actuales | Total |
|---|---:|---:|---:|---:|
| Adelanto / Adelantos | 40 | 40 | 40 | 40 |
| Banco / Bancos | 6 | 6 | 6 | 6 |
| Caja / Cajas | 2 | 2 | 2 | 2 |
| CambioDeAdelanto / CambiosDeAdelanto | 80 | 80 | 80 | 80 |
| CambioDeEstadoFactura / CambiosDeEstadoFactura | 197 | 197 | 197 | 197 |
| CambioDeEstadoViaje / CambiosDeEstadoViaje | 1336 | 1336 | 1336 | 1336 |
| CambioDeLiquidacion / CambiosDeLiquidacion | 164 | 164 | 164 | 164 |
| CambioDeLiquidacionViaje / CambiosDeLiquidacionViajes | 60 | 60 | 60 | 60 |
| Chofer / Choferes | 50 | 50 | 50 | 50 |
| Cliente / Clientes | 35 | 35 | 35 | 35 |
| Documentacion / Documentaciones | 45 | 45 | 45 | 45 |
| DocumentacionVehiculo / DocumentacionesVehiculo | 280 | 280 | 280 | 280 |
| DocumentacionTipo / DocumentacionTipos | 4 | 4 | 4 | 4 |
| EmpresaEmisora / EmpresaEmisora | 1 | 0 | 0 | 1 |
| FacturaCliente / Facturas | 140 | 140 | 140 | 140 |
| FacturaProveedor / FacturasProveedor | 120 | 120 | 120 | 120 |
| CambioEstadoCheque / HistorialEstadosCheque | 22 | 22 | 22 | 22 |
| Liquidacion / Liquidaciones | 60 | 60 | 60 | 60 |
| LiquidacionViaje / LiquidacionViajes | 254 | 254 | 254 | 254 |
| Cheque / MediosPagoProveedor | 28 | 28 | 28 | 28 |
| Efectivo / MediosPagoProveedor | 4 | 4 | 4 | 4 |
| MedioPagoProveedor / MediosPagoProveedor | 36 | 36 | 36 | 36 |
| Transferencia / MediosPagoProveedor | 4 | 4 | 4 | 4 |
| MovimientoDeCaja / MovimientosDeCaja | 185 | 185 | 185 | 185 |
| OrdenDePago / OrdenesDePago | 51 | 51 | 51 | 51 |
| OrdenPagoProveedor / OrdenesPagoProveedor | 30 | 30 | 30 | 30 |
| OrdenPagoProveedorFactura / OrdenPagoProveedorFacturas | 50 | 50 | 50 | 50 |
| Permiso / Permisos | 30 | 0 | 0 | 30 |
| Persona / Personas | 100 | 100 | 100 | 100 |
| Proveedor / Proveedores | 30 | 30 | 30 | 30 |
| ResultadoOperacionPago / ResultadosOperacionPago | 35 | 35 | 35 | 35 |
| Rol / Roles | 4 | 0 | 0 | 4 |
| Dictionary`2 / RolPermisos | 65 | 0 | 0 | 65 |
| TipoVehiculo / TiposVehiculo | 7 | 7 | 7 | 7 |
| Transportista / Transportistas | 21 | 21 | 21 | 21 |
| Dictionary`2 / UsuarioRoles | 5 | 4 | 4 | 5 |
| Usuario / Usuarios | 5 | 4 | 4 | 5 |
| Vehiculo / Vehiculos | 75 | 75 | 75 | 75 |
| Viaje / Viajes | 450 | 450 | 450 | 450 |

Los ceros DEMO de Roles/Permisos/RolPermisos indican catálogos estructurales, no ausencia de datos.
EmpresaEmisora se conserva como configuración compartida y no se elimina en reset; sólo se crea
si está ausente. UsuarioRoles incluye el vínculo del administrador original, que no es DEMO.
`__EFMigrationsHistory` no es DbSet de negocio: lo administra exclusivamente EF al migrar.
Históricos, vínculos e idempotencia se generaron mediante casos de uso, nunca INSERT de auditoría falsa.

## Viajes por mes y estado

| Mes2026 | Pendiente | EnCurso | Rendido | Facturado | Anulado | Total |
|---|---:|---:|---:|---:|---:|---:|
| Agosto |12|0|43|25|0|80|
| Septiembre |12|0|59|29|0|100|
| Octubre |25|25|71|69|10|200|
| Noviembre |12|0|44|14|0|70|

Clientes y vehículos con concentración determinista de actividad, otros de poca y unidades sin viajes.
Primero se garantiza cobertura del padrón y luego se concentran viajes en clientes/unidades principales;
las categorías superan10, con Top10+Otros. Remitos DEMO y centavos ocasionales, importes dentro del rango solicitado.

| Flota | Total | Activos |
|---|---:|---:|
| Tractor |15|15|
| Camión |10|6|
| Semirremolque sider |10|10|
| Semirremolque playo |10|6|
| Utilitario |10|10|
| Acoplado |10|6|
| Equipo especial |10|10|

## Dashboard octubre2026

El default de Viajes excluye10anulados:190visibles sobre200registros reales de octubre.
140realizados,25pendientes de rendición,25pendientes. Flota75unidades/140viajes con vehículo.

| Indicador | ARS / cantidad |
|---|---:|
| Facturado |106.720.447,40|
| Pendiente facturas |68.621.636,75|
| Cobrado por fecha |58.972.777,62|
| Liquidado |131.312.711,22|
| Pendiente liquidaciones |36.526.815,62|
| Pagado por fecha |175.164.139,33|
| Ingresos Caja |3.317.409,13|
| Egresos Caja |2.395.107,01|
| Movimiento neto |922.302,12|
| Vencimientos choferes/flota/facturas |2 /48 /74|

Importes y deuda documental no se restan de flujos de otros universos. Vencimientos dependen de hoy;
estas cifras de alertas corresponden a la fecha de aceptación. El dataset principal usa fechas fijas2026,
no DateTime.Now; la simulación de operaciones incluye noviembre. IDs, secuencias públicas, hashes y
rutas físicas no se prometen idénticos entre resets: se conservan secuencias para no afectar datos ajenos.
Los datos de negocio se reproducen por nombres/remitos/relaciones y escenarios, con Random15015.

## Pruebas ejecutadas

- Build nativo de GT.SeedDemo y publish/build de imagen correctos. Sin suites completas848/1306/699.
- `--self-test`: guardias locales,3destinos admitidos y4casos rechazados; sin conexión SQL de prueba.
- Production y GT_SEED_DEMO=false: salida inactiva, sin generación ni preparación de datos.
- SQL DBCC CHECKCONSTRAINTS sin violaciones; SnapshotON. Sumas OP vigentes/LQ, medios/OPP,
  facturas/OPP y saldos de cajas cerradas reconciliados por consultas independientes. Un autor
  distinto en compensación;0eventos de viaje previos al alta del actor.
-23endpoints principales con datos;14exportaciones reales PDF/XLSX. Siete libros abiertos como ZIP
  sin errores; sietePDF abiertos con pdfinfo/pdftotext. Sin archivos parciales ni firma inválida.
-30pantallas reales en Chrome: espera de respuestas200 y fin de carga,0errores JS; dashboard
  renderizado con KPI/gráficos/ranking y captura revisada. Las primeras pruebas de recarga Vite
  tuvieron timeouts; la navegación SPA final con barreras de respuesta sí completó. No cambio de UI.
- Rebuild de imágenes y down/up sin -v conservaron exactamente los39conteos del modelo.
- Stack separado con volumenSQL vacío, puertos15433/18080/15173: migraciones, semillas,
  Snapshot y DEMO automáticos antes de backend/frontend. Mismos39conteos esenciales que la base local.
- Segundo arranque: mensaje literal «Dataset DEMO015 existente. No se generan registros.»;
 0nuevos. Se compararon los39conteos.
- Reset efímero positivo: cliente no DEMO conservado con mismoId/identidad y mismos conteos DEMO.
- Reset efímero negativo: viaje ajeno con referencia a cliente DEMO provocó rechazo ANTES de borrar;
  los39conteos antes/después permanecieron idénticos. No se eligieron ni borraron datos ajenos para resolverlo.

Comandos base reales: `docker compose build seed-demo`; `docker compose run --rm seed-demo
--reset-demo --prepare --confirm`; `docker compose up -d --build`; `docker compose down` seguido
por `docker compose up -d`; `./backend/scripts/seed-demo-completo.sh --summary --confirm`;
`python3 backend/tools/GT.SeedDemo/smoke.py`. Validación efímera con proyecto `gt-demo015-verify`
y configuración privada generada desde Compose, cambiando sólo puertos/nombres de volúmenes/redes;
no se versionan credenciales. Scripts/capturas/logs temporales en /tmp/gt-demo015-validation y
/tmp/gt-demo015-smoke. Stack efímero y configuración privada retirados al cierre; stack local queda arriba.

## Fallos encontrados y corregidos en el tooling

Parseo inicial de.env como shell reemplazado por lectura de datos; seguimiento EF entre casos,
orden inverso de FK en reset y conexión antes de migrar base inexistente corregidos. Se ajustaron
escenarios a restricciones reales: vehículo disponible sólo tras documentación, Fletero sólo cheque
diferido, adelanto se aprueba antes de anular y baja de cliente sin viajes vivos. Se añadió
compensación de efectivo obligatoria y cronología julio de cuentas. Las corridas incompletas nunca
se declararon válidas; se limpiaron exclusivamente por reset DEMO y se repitieron verificaciones afectadas.

Warnings: avisos Pkcs NU1902/NU1903 y SMTP nulabilidad existentes; el tooling tiene EF1003 por
identificadores dinámicos obtenidos del modelo, escapados entre corchetes, con PK como SqlParameter.
No texto de usuario en identificadores ni valores concatenados. No se actualizaron dependencias productivas.

## Límites reales y entrega

No empleados integrados, emparejamiento tractor/semi, kilometraje, horas, inventario ni estados
no modelados. CUIT/CUIL/DNI sintéticos validados localmente y marcados, sin consulta a padrón oficial:
no se afirma garantía externa de que un número nunca haya sido asignado. Contactos `.invalid`.
Validado con Docker Compose de este entorno; no se afirma ensayo adicional con Podman.
La persistencia es named volume, no una imagenSQL poblada. Reset compartido nunca borra roles,
permisos, administrador original ni empresa emisora; aborta al detectar referencias no DEMO.

Archivos a versionar: README.md, docker-compose.yml, backend/scripts/seed-demo-completo.sh y
los archivos fuente de backend/tools/GT.SeedDemo (csproj, C#, Dockerfile, README, validation y smoke.py).
Sin commit/push ni staging. Sin .env, hashes, credenciales, dumps, MDF/LDF/BAK, TRX, capturas, bin/obj,
node_modules o dist en el diff. Los datos permanecen sólo en volúmenes locales.
