# Dataset de presentación — DEMO015 v2

Aceptación local de DEMO015 v2: 23/10/2026.
Alcance de pruebas: build y verificaciones focales; sin suites completas.

## Auditoría previa de marcas

Se revisaron todas las apariciones de DEMO/DEMO015/demo/Demo en GT.SeedDemo antes
de modificar el generador. Clasificación aplicada:

| Uso original | Clasificación | Tratamiento v2 |
|---|---|---|
| Namespace, nombre de herramienta, servicio Compose, variables externas | Técnico | Se conservan |
| Dataset.Mark, versión, logs, comentarios, applock, mensajes de error del tooling | Técnico | Se conservan |
| Username usado como marca ready/incomplete | Visible y técnico mezclados | Usuarios naturales; estado sólo en metadata SQL |
| Nombre/apellido, razón social, nombre comercial, email, domicilio | Visible | Catálogos naturales y dominios .invalid |
| Tipo de vehículo/documento/banco, modelo de unidad | Visible | Nombres normales; se pueden reutilizar catálogos ajenos activos |
| Origen/destino, remitos, carga, conceptos, motivos, referencias de pagos | Visible | Rutas bonaerenses/litoraleñas y conceptos de negocio |
| Nombre original y contenido del PDF adjunto | Visible | Constancia de documentación, sin marcas de dataset |
| Contains/StartsWith sobre nombres/remitos para reset/validación | Técnico dependiente de texto visible | Eliminados del camino v2; sólo compatibilidad de reset v1 |
| Empresa emisora y copias congeladas en facturas | Visible | Se corrige sólo el emisor exacto creado por v1 antes de generar las facturas nuevas |

No se modificó producto, modelo, migraciones, políticas ni contratos de negocio.
Gestión de Empleados ya está integrada en main y se usa su Persona existente.

## Identificación y seguridad

Antes: las raíces se reconocían por DEMO015 en campos visibles y los hijos por FK;
la cuenta demo.DEMO015.v1.ready identificaba completitud.
Ahora: propiedades extendidas SQL con prefijo GT.SeedDemo.DEMO015 registran las PK
exactas de las filas insertadas por el tooling, incluidas uniones e históricos.
No hay tablas ni columnas adicionales. No se usan nombres comerciales como llave
para reset v2. Los campos del negocio no llevan metadata encubierta.

La metadata incluye versión 2, estado completo/incompleto, fragmentos comprimidos
y hash. Permanece en el volumen SQL y no sale por API/UI ni se versiona como dump.
Cada SaveChanges registra filas y PK en la misma transacción; se respeta la del
coordinador cuando existe. La API no incorpora este interceptor. Una PK faltante,
metadata incompleta o corrupta impiden declarar el dataset válido. La metadata
no es un checksum de todos los importes ni de todas las ediciones del negocio.

El reset usa PK parametrizada, orden inverso de FK y transacción Serializable.
Una referencia ajena se rechaza antes de borrar: no se incorpora al conjunto por
el hecho de apuntar a una fila propia. No hay TRUNCATE ni DELETE sin WHERE ni
reseeding de identidades. Catálogos reutilizados, roles, permisos, admin original
y configuración compartida se conservan. Identificación textual legacy sólo para
el dataset v1 con su cuenta exacta; nunca para reconstruir metadata perdida de v2.

## Regeneración y cantidades

Comando ejecutado tras build y tests focales:

```bash
docker compose run --rm seed-demo --reset-demo --prepare --confirm
```

3973 filas físicas v1 eliminadas y 3973 filas propias v2 creadas, sin sumar dos veces
los subtipos TPH. Admin original conservado. La empresa exacta creada por v1 se
presenta ahora como G&T Logística S.A.; el código no sobrescribe una empresa ajena.
--prepare también incorpora las semillas estructurales de Empleados del main
integrado: permisos 30→32 y vínculos RolPermisos 65→70. No son filas del dataset.
Random(15015) se mantiene, así como volúmenes, estados, importes y fechas principales.

| Entidad / tabla | DEMO actuales | Total |
|---|---:|---:|
| Adelanto / Adelantos | 40 | 40 |
| Banco / Bancos | 6 | 6 |
| Caja / Cajas | 2 | 2 |
| CambioDeAdelanto / CambiosDeAdelanto | 80 | 80 |
| CambioDeEstadoFactura / CambiosDeEstadoFactura | 197 | 197 |
| CambioDeEstadoViaje / CambiosDeEstadoViaje | 1336 | 1336 |
| CambioDeLiquidacion / CambiosDeLiquidacion | 164 | 164 |
| CambioDeLiquidacionViaje / CambiosDeLiquidacionViajes | 60 | 60 |
| Chofer / Choferes | 50 | 50 |
| Cliente / Clientes | 35 | 35 |
| Documentacion / Documentaciones | 45 | 45 |
| DocumentacionVehiculo / DocumentacionesVehiculo | 280 | 280 |
| DocumentacionTipo / DocumentacionTipos | 4 | 4 |
| EmpresaEmisora / EmpresaEmisora | 0 | 1 |
| FacturaCliente / Facturas | 140 | 140 |
| FacturaProveedor / FacturasProveedor | 120 | 120 |
| CambioEstadoCheque / HistorialEstadosCheque | 22 | 22 |
| Liquidacion / Liquidaciones | 60 | 60 |
| LiquidacionViaje / LiquidacionViajes | 254 | 254 |
| Cheque / MediosPagoProveedor | 28 | 28 |
| Efectivo / MediosPagoProveedor | 4 | 4 |
| MedioPagoProveedor / MediosPagoProveedor | 36 | 36 |
| Transferencia / MediosPagoProveedor | 4 | 4 |
| MovimientoDeCaja / MovimientosDeCaja | 185 | 185 |
| OrdenDePago / OrdenesDePago | 51 | 51 |
| OrdenPagoProveedor / OrdenesPagoProveedor | 30 | 30 |
| OrdenPagoProveedorFactura / OrdenPagoProveedorFacturas | 50 | 50 |
| Permiso / Permisos | 0 | 32 |
| Persona / Personas | 100 | 100 |
| Proveedor / Proveedores | 30 | 30 |
| ResultadoOperacionPago / ResultadosOperacionPago | 35 | 35 |
| Rol / Roles | 0 | 4 |
| Dictionary`2 / RolPermisos | 0 | 70 |
| TipoVehiculo / TiposVehiculo | 7 | 7 |
| Transportista / Transportistas | 21 | 21 |
| Dictionary`2 / UsuarioRoles | 4 | 5 |
| Usuario / Usuarios | 4 | 5 |
| Vehiculo / Vehiculos | 75 | 75 |
| Viaje / Viajes | 450 | 450 |

Los ceros corresponden a configuración/catálogos estructurales compartidos.
39 tipos EF, 37 DbSet. EmpresaEmisora no se contabiliza como fila propia del reset.

- Clientes 35 (30 activos/5 inactivos); proveedores 30 (20 normales/10 fleteros).
- Transportistas 21 (20 externos + propio); choferes 50 (42 activos/8 inactivos).
- Personas 100: 50 choferes y 50 empleados; sin tabla o cargo inventado.
- Documentación: 45 documentos de chofer y 280 documentos de flota.
- Flota 75: 15 tractores y 10 por Camión, Semi sider, Semi playo, Utilitario,
  Acoplado y Equipo especial. Marcas/modelos acordes a cada categoría.
- Viajes 450: agosto 80, septiembre 100, octubre 200, noviembre 70.
- Octubre: 25 pendientes,25 en curso,71 rendidos,69 facturados,10 anulados.
- Facturas cliente 140:83 pendientes,54 pagadas,3 anuladas;15 de septiembre cobradas en octubre.
- Facturas proveedor 120; liquidaciones 60:28 pendientes (14 parciales),31 pagadas,1 anulada.
- OP 51 (45 vigentes/6 revertidas); OPP30, incluidas agrupaciones de tres liquidaciones.
- Caja2, movimientos185 (180 manuales y5 vinculados a OPP, con compensación).
- Adelantos40; bancos6; cheques28, transferencias4 y efectivos4.

Ejemplos: Metalúrgica del Plata; Aceros del Sur; Transportes Ruta Sur;
Logística del Plata; Neumáticos del Plata; Repuestos Ruta 2; Martín Fernández;
Diego González; Scania R450; Volvo FH460; Randon Sider; Renault Master.
Emails visualmente normales en .invalid, sin nombres de dataset. CUIT/DNI siguen
siendo sintéticos con validación local; no se afirma haber comprobado su ausencia
en padrones oficiales.

## Evidencia y verificaciones focales

- Build focal y publish de imagen correctos. Avisos Pkcs NU1902/NU1903 y nulabilidad
  SMTP preexistentes; EF1003 del reset usa identificadores del modelo escapados y
  valores de PK parametrizados.
- --self-test: roundtrip de10.001 PK (incluida compuesta), pertenencia exacta,
  catálogos, detección case-insensitive y claves int/bigint del modelo: OK.
- --self-test-sql, antes y después de regenerar: rollback exterior y fallo inyectado
  después de escribir metadata revierten juntos filas y metadata: OK.
- --self-test-reset: un nombre editado conserva pertenencia; fila ajena con FK propia
  se rechaza antes de borrar. Prueba en memoria sobre el modelo real, sin escrituras.
- --check-visible: búsqueda SQL en1723 filas con columnas textuales,94 columnas,
  **0 coincidencias** de DEMO (incluye DEMO015), TEST, PRUEBA, FAKE, SAMPLE.
  Incluye referencias compartidas y snapshots; excepciones en README del tooling.
- Smoke HTTP:24 endpoints con datos, todas las páginas leídas y verificadas.
  14 exportaciones PDF/XLSX con apertura y texto limpio, más comprobante cliente y
  adjuntos de proveedor/chofer/flota: cuatro PDF persistidos con texto limpio.
- Chrome headless real:18 pantallas con respuesta200, filas visibles y fin de carga;
  0 palabras prohibidas en el main renderizado,0 errores JavaScript. Incluye todos
  los módulos pedidos, tres paneles de vencimientos, Empleados y Reportes.
  Capturas de Clientes y dashboard inspeccionadas visualmente.
- SQL: DBCC CHECKCONSTRAINTS sin violaciones;0 diferencias OP vigentes/LQ,
  medios/OPP, facturas/OPP y saldos de cajas cerradas. Snapshot ON,11 migraciones.
- Backend/frontend se reconstruyeron para incluir Empleados del main integrado;
  sólo se recrearon esos servicios, conservando SQL y sus volúmenes.

## Dashboard octubre, HTTP real y Chrome

01/10/2026–31/10/2026:190 viajes visibles (200 incluidos anulados),140 realizados,
25 pendientes de rendición,25 pendientes.75 unidades y varias en el ranking.

| Indicador | ARS |
|---|---:|
| Facturado |106.720.447,40|
| Pendiente facturación |68.621.636,75|
| Cobrado |58.972.777,62|
| Liquidado |131.312.711,22|
| Pendiente liquidaciones |36.526.815,62|
| Pagado |175.164.139,33|
| Caja ingresos |3.317.409,13|
| Caja egresos |2.395.107,01|
| Caja neto |922.302,12|

Vencimientos:2 choferes,48 flota y74 facturas a la fecha de validación. Estos
conteos dependen del calendario. Los importes coinciden con la aceptación v1.

## Idempotencia y uso

```bash
docker compose run --rm seed-demo --seed --prepare --confirm
docker compose run --rm seed-demo --summary --confirm
docker compose run --rm seed-demo --check-visible --confirm
```

Segunda ejecución: «Dataset DEMO015 existente. No se generan registros.»;
0 creados en todos los tipos; mismos conteos. Versiones anteriores o incompletas
requieren reset explícito: no se migran destructivamente durante el arranque.

Login de presentación: administrador. También gerencia, operaciones, tesoreria.
Password externa GT_DEMO_PASSWORD o fallback GT_ADMIN_PASSWORD_INICIAL; no se imprime.

Las capturas, logs y archivos de ejecución se conservan fuera del repositorio.
No versionarlos, ni incluir .env, volúmenes, bin/obj o credenciales.
validation.md conserva la evidencia histórica v1.
