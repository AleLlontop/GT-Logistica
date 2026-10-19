# Contratos HTTP — 014

Base `/api/pagos`. JSON camelCase; enumeraciones string camelCase. Fechas de negocio `yyyy-MM-dd`; instantes ISO UTC `Z`. Todo importe nuevo es cadena canónica `"123.45"` con exactamente dos decimales, nunca JSON number; rango decimal(18,2). `ImporteDecimalJsonConverter` en opciones locales a estos endpoints. Id int positivo, claves UUID. API no recibe roles ni usuario: identidad del principal revalidado. Cookies y manejo CSRF conforme al patrón vigente del proyecto, sin crear esquema alternativo. No exponer endpoints nuevos de escritura en otros módulos para lograr efectos internos.

## Formas compartidas

- `Pagina<T> = {items:T[],total:int,pagina:int,tamanioPagina:20}`. pagina default 1; inválida 400. SQL filtra antes de paginar. Desde/hasta opcionales inclusivos; rango invertido 400. Bool de query opcional `bool?`, default en caso de uso.
- Error `{codigo:string,mensaje:string,campo?:string,errores?:{campo:string,mensaje:string}[],facturasNoDisponibles?:int[],saldoDisponible?:Importe,importeEfectivo?:Importe}`. Paths de campos: `medios[0].numeroCheque`, etc. No arrays de datos sin autorización. 400 entrada, 404 inexistente, 409 conflictos reales de estado/versión/clave reutilizada con contenido diferente; confirmado ausente/false es validación 400, nunca una invitación a confirmar; 401/403 con contrato global actual, mismo mensaje sin revelar qué permiso falta. 503 `operacion_transitoria` ante SQL 1205/timeout, rollback íntegro y reintento con misma clave; no es conflicto funcional. 503 resultado no determinable tras fallo commit: mensaje reintentar MISMA clave. Nunca declarar éxito si rollback.
- `ProveedorOpcion={id,razonSocial,cuit,tipo:"proveedorNormal"|"fletero",activo}`; mostrar Inactivo. `BancoDto={id,nombre,activo,version}`. `Referencia={id,numero}` donde numero OP/OPP/LQ ya viene armado del servidor.
- `FacturaElegible={id,numero,fecha,importeTotal,proveedorId,tipoProveedorAlRegistrar,liquidacion:null|{id,numero,importeHistorico,importeActual,saldoActual,estado}}`.
- `MedioEntrada` unión discriminada estricta: `{renglonId,tipo,importe,...}`. Cheque: `tipo:"cheque",fechaEmision,fechaVencimiento,bancoId,numeroCheque,detalle?`; transferencia: `tipo:"transferencia",fecha,cbuOrigen,cbuDestino,numeroComprobante,observaciones?`; efectivo: `tipo:"efectivo"` sin fecha ingresada (servidor hoy). Rechazar campos ajenos al tipo, estados y totales inyectados. Detalle OPP null/ausente/blanco normaliza a null; NumeroCheque sólo Trim exterior. NumeroCheque y Nombre banco máximo técnico ratificado de 200 después de trim exterior (unidades UTF-16 coherentes con nvarchar(200)); validar en servidor, 400 `longitud_excedida` con campo y mensaje «No puede superar 200 caracteres después de quitar los espacios exteriores», mostrado en frontend; no truncar ni recortar mediante maxlength antes de normalizar. Cheque.NumeroCheque se persiste con Latin1_General_100_BIN2 e índice único (BancoId,NumeroCheque); Banco.Nombre hereda la collation textual de la base y usa índice único Nombre incluidos inactivos, siguiendo TiposVehiculo, sin override binario; otros textos nvarchar(max) sin nuevo tope de negocio. CBU ASCII 22 dígitos conservados como string.

## Rutas, permisos y respuesta

| Método / ruta | Entrada | Éxito | Permiso |
|---|---|---|---|
| GET `/referencias` | ninguna | `{hoyArgentina,cajaPropia:null|{id,estado,saldoDisponible},bancosActivos:BancoDto[]}`; sólo datos necesarios, bancos sin datos de gestión como version en esta proyección | pagos.generar |
| GET `/referencias/proveedores` | `q?`, `pagina?` | Pagina<ProveedorOpcion> activos/inactivos con al menos una factura elegible, razón social/Id asc | pagos.generar |
| GET `/referencias/proveedores/{id:int}/facturas` | `pagina?` | Pagina<FacturaElegible>, Fecha/Id asc; misma elegibilidad SQL que guardado | pagos.generar |
| GET `/referencias/facturas` | `ids=1,2,...` | `{items:FacturaElegible[],noDisponibles:int[],importeTotal}`; resuelve selección entre páginas y total de servidor, sólo proveedor común elegible, nada fuera del alcance de formulario | pagos.generar |
| POST `/ordenes` | GenerarEntrada abajo | 201 DetalleOPP, Location `/api/pagos/ordenes/{id}` | pagos.generar |
| GET `/ordenes` | `desde?,hasta?,proveedorId?,estado?,pagina?` | Pagina<OrdenResumen>, Fecha desc/Id desc | pagos.consultar |
| GET `/ordenes/proveedores` | `q?,pagina?` | Pagina<ProveedorOpcion> presentes en órdenes históricas, sin excluir inactivos | pagos.consultar |
| GET `/ordenes/{id:int}` | ninguna | 200 DetalleOPP | pagos.consultar |
| POST `/ordenes/{id:int}/anulacion` | `{claveOperacion:UUID,motivo:string,confirmado:bool}` | 200 DetalleOPP; replay conserva resultado original | pagos.anular |
| GET `/cheques` | `bancoId?,estado?,desdeVencimiento?,hastaVencimiento?,pagina?` | Pagina<ChequeResumen>, vencimiento asc/Id asc, universo <=hoy+30 sin piso | pagos.cheques.consultar |
| GET `/cheques/bancos` | ninguna | `{items:{id,nombre,activo}[]}` sólo bancos presentes en histórico, activos e inactivos | pagos.cheques.consultar |
| GET `/cheques/{id:int}` | ninguna | 200 DetalleCheque (incluso fuera de alerta, para referencias desde OPP) | pagos.cheques.consultar |
| POST `/cheques/{id:int}/estado` | `{estadoAnterior,estadoNuevo}` | 200 DetalleCheque releído | pagos.cheques.cambiarEstado |
| GET `/bancos` | `pagina?` | Pagina<BancoDto>, Nombre/Id asc, incluye inactivos | pagos.bancos.consultar |
| GET `/bancos/{id:int}` | ninguna | 200 BancoDto | pagos.bancos.consultar |
| POST `/bancos` | `{nombre:string}` | 201 BancoDto, Location banco; Activo=true | pagos.bancos.gestionar |
| PUT `/bancos/{id:int}` | `{nombre:string,version:int}` | 200 BancoDto releído; no admite Activo | pagos.bancos.gestionar |
| PUT `/bancos/{id:int}/activacion` | `{}` | 200 BancoDto, idempotente | pagos.bancos.gestionar |
| PUT `/bancos/{id:int}/desactivacion` | `{}` tras confirmación de pantalla | 200 BancoDto, idempotente | pagos.bancos.gestionar |

No DELETE ni edición OPP/medios persistidos. Literales y `{id:int}` evitan colisiones. Lecturas de referencia sólo dan campos del formulario; no usar endpoints de 013/009/011 que exijan otros permisos. Consulta previa de anulación usa GET con pagos.consultar; POST necesita pagos.anular para efectos internos. Acciones de cheque y banco exigen su permiso de escritura; sus pantallas cargan mediante permiso de lectura independiente, sin implicación entre ambos. Resultado POST no obliga a realizar GET con otro permiso.

### GenerarEntrada

`{claveOperacion:UUID,proveedorId:int,fecha:string,detalle?:string|null,facturaIds:int[],medios:MedioEntrada[],importeTotalConfirmado:Importe,confirmado:bool}`.

FacturaIds únicos; al menos una y un medio. ImporteTotalConfirmado es lo mostrado al confirmar, nunca fuente del total. El usuario revisa la orden y ejecuta Generar; esa acción envía una única petición intencional con `confirmado:true`. Si falta o es false, 400 de campo `confirmado`, sin efectos, sin respuesta de confirmación ni segundo POST exigido por protocolo. Revalidar totales dentro de locks; cambio de total confirmado 409 `seleccion_modificada`, sin sustituir ni reducir facturas. No pedir confirmaciones adicionales por proveedor Inactivo. Generar prepara únicamente una OPP Pendiente.

Identidad idempotente global `(TipoOperacion,ClaveIdempotencia)`, no por usuario. Misma clave presentada por otro usuario nunca crea otra operación económica. Sesión y permiso actual de operación obligatorios; replay de otro autor requiere además pagos.consultar sobre la OPP, de lo contrario 403 genérico sin datos ni comparación reveladora. Con autorización, contenido distinto produce 409. Canonicalización versionada/SHA-256 y comparación de bytes descritas en research §2: propiedades fijas, facturas ordenadas, medios como multiconjunto semántico (preserva duplicados), decimales exactos y trims aprobados; ignorar orden incidental, renglonId UI, usuario, clave y confirmado. No incorporar reloj ni fecha de efectivo resuelta al ejecutar.

Replay misma clave+contenido normalizado: 201 JSON original y Location original, aun tras anulación. Clave distinta sobre factura no disponible: 409 `facturas_no_disponibles`, mensaje «Una o más facturas ya fueron pagadas o dejaron de estar disponibles. Actualizá la selección.». Reuso de clave con contenido diferente: 409 `clave_reutilizada`. Saldo/caja cambió: 409 `caja_no_disponible` / `saldo_insuficiente` sin operaciones parciales. Número cheque repetido: 400 `numero_cheque_duplicado`, campo correspondiente. Banco desactivado: 409 `banco_no_disponible`.

### DTOs completos

`OrdenResumen={id,numero:"OPP-…",fecha,proveedor:{id,razonSocial,cuit,tipo,activoAlGenerar,activoActual},importeTotal,estado:"pendiente"|"anulada"}`.

`DetalleOPP = OrdenResumen + {detalle:null|string,registradaPor:{id,nombre},registradaEn,facturas:[{id,numero,fecha,importeTotalAlPagar,estadoActual,vinculoVigente,proveedorRazonSocial,proveedorCuit,liquidacion:null|{id,numero,importeHistorico,importeActual,saldoActual,estadoActual,pago:{id,numero:"OP-…",fechaPago,importe,vigente,revertidaEn:null|UTC}}}],medios:MedioDetalle[],caja:null|{id,egreso:MovimientoReferencia,ingresoCompensatorio:null|MovimientoReferencia},anulacion:null|{motivo,usuario:{id,nombre},ocurridaEn,efectos:{facturasRestituidas:int[],chequesAnulados:int[],pagosRevertidos:Referencia[],movimientoCompensatorio:null|MovimientoReferencia}}}`.

`MovimientoReferencia={id,cajaId,tipo:"ingreso"|"egreso",importe,concepto,usuario:{id,nombre},fecha,ordenPagoProveedor:{id,numero}}`.

`MedioDetalle` conserva campos de MedioEntrada + `id,fecha` (en cheque emisión) y para cheque `estado,situacionVencimiento`. Datos de transferencia/efectivo sin estados inventados. En OPP se muestra detalle de medios pertenecientes al documento con pagos.consultar; abrir detalle completo/historial del cheque requiere pagos.cheques.consultar. Enlaces externos sólo cuando sesión autoriza; sin permiso se muestran identificaciones como texto.

`ChequeResumen={id,numeroCheque,banco:{id,nombre,activo},fechaEmision,fechaVencimiento,importe,estado,situacionVencimiento,ordenPagoProveedor:{id,numero}}`.

`DetalleCheque = ChequeResumen + {detalle:null|string,historial:[{id,estadoAnterior,estadoNuevo,usuario:{id,nombre},ocurridoEn}]}`. Situación `vencido|proximoAvencer|fueraDelPeriodoDeAlerta` derivada. Estado `pendiente|entregado|depositado|cobrado|rechazado|anulado`. Server valida estadoAnterior coincide y arista; 409 `estado_cheque_modificado` / `transicion_no_permitida`. POST retorna este DTO porque autoriza el resultado propio, no el GET general sin permiso.

Anular: abrir diálogo, ingresar motivo y confirmar explícitamente en UI antes de enviar la petición idempotente con `confirmado:true`. Cancelar no envía la operación. Ausente/false se rechaza como validación 400 de campo, sin efectos. No existe handshake POST → 409 de confirmación → segundo POST. Con transferencia, cheque no Pendiente, caja cerrada o reversión imposible: 409 `orden_no_anulable` con motivo de negocio sin datos de recursos ajenos. Clave de anulación independiente de Generar, identidad global (Anular,Clave), con OPPId/motivo como contenido canónico y usuario sólo auditado/autorizado; replay 200 original; diferente contenido 409. Motivo trim obligatorio. No exigir además permisos manuales.

### Contratos anteriores tocados, lista cerrada

- 013 detalle/listado: estado refleja `pendiente|pagada`, filtro admite ambos y conserva resto del contrato/cadenas monetarias. Sin nuevos permisos en endpoints antiguos.
- 009 detalle: cada OP agrega origen `{id,numero}` nullable y vigencia/revertidaEn; historial reconoce operaciones OPP. Números manuales y contratos de escritura existentes intactos; no cambiar a cadenas los importes antiguos. Las proyecciones nuevas de trazabilidad OPP usan montos exactos si los incorporan, nunca sustituyen campos previos silenciosamente.
- 011 proyección de movimiento agrega referencia `ordenPagoProveedor:{id,numero}` nullable, sin redefinir referencia OP existente. Endpoints manuales no reciben OrdenPagoProveedorId. El detalle de Caja conserva permiso caja.consultar; la referencia interna a movimiento en OPP requiere sólo pagos.consultar.
