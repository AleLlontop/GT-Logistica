# Modelo de datos — Feature 013

Los nombres describen el modelo a implementar; no se modifican entidades en esta fase. Referencia: [spec](spec.md), [decisiones y transacciones](research.md).

## Proveedor

Tabla `Proveedores`, entidad `GT.Domain.Proveedores.Proveedor`.

| Campo | Persistencia | Regla |
|---|---|---|
| Id | int identity, PK | Identidad estable para facturas y filtros |
| Tipo | enum int | Fletero o ProveedorNormal; JSON `fletero` / `proveedorNormal` |
| Activo | bool | Estado propio; default true con HasSentinel(true) |
| Version | bigint | Inicial 1; incrementa cada edición/cambio de estado efectivo |
| TransportistaId | int nullable, FK restrict | Obligatorio Fletero, nulo normal |
| RazonSocial | nvarchar(200) nullable | Obligatoria y no vacía para normal; nula Fletero |
| Cuit | varchar(11) nullable | Normalizado, válido y obligatorio normal; nulo Fletero |
| Telefono, Email | nvarchar(30), nvarchar(254) nullable | Opcionales normal; nulos Fletero; email informado válido |
| NombreComercial, Direccion | nvarchar nullable | Opcionales para ambos tipos |

Las capacidades de identidad/contacto son las de TransportistaConfiguracion: razón social 200, teléfono 30 y email 254; son capacidades técnicas heredadas y se validan sin truncar. Dirección nvarchar(200), igual a ClienteConfiguracion; nombre comercial nvarchar(max), sin límite funcional adicional. Razón social no vacía: trim en aplicación y CHECK de longitud. No crear una Persona ni convertir automáticamente Transportistas en Proveedores.

CHECK por tipo: normal exige `TransportistaId IS NULL`, razón social/CUIT `IS NOT NULL` antes de evaluar longitud; Fletero exige vínculo y los cuatro campos compartidos propios `IS NULL`. CHECK Tipo en los dos valores. FK con DeleteBehavior.Restrict. Índices únicos filtrados `IX_Proveedores_Cuit` (Cuit IS NOT NULL), `IX_Proveedores_TransportistaId` (TransportistaId IS NOT NULL). Version > 0. La unicidad del CUIT efectivo entre tipos usa el protocolo SQL de research §2, no una columna duplicada.

`ProveedorLectura` proyecta razón social/CUIT/teléfono/email con condicional por tipo, desde Transportista para Fletero. Predicados de búsqueda se construyen sobre la misma expresión SQL; no materializar antes de paginar. Reutilizar los índices existentes (Transportistas declara CUIT, no nombre); medir sobre 1.000 filas antes de añadir índices por intuición.

Transiciones: Activo → Inactivo con confirmación UI; Inactivo → Activo separada, idempotente. Edición no recibe Activo. Version controla datos editables del proveedor; datos compartidos del Fletero nunca son escritos por esa operación, y se releen al responder.

## FacturaProveedor

Tabla `FacturasProveedor`, entidad del mismo módulo Proveedores.

Los importes permanecen decimal(18,2) en este modelo; el contrato HTTP los representa como cadenas decimales canónicas de exactamente dos decimales, incluido el importe histórico, el actual de liquidación, los disponibles y errores monetarios. `ImporteLiquidacionAlRegistrar` se expone como `liquidacion.importeAlRegistrar`. No cambiar tipos persistidos ni ampliar el modelo para resolver precisión del cliente. En TypeScript se usa ImporteDecimal basado en string y centavos BigInt, nunca number para el importe completo.

| Campo | Persistencia | Regla |
|---|---|---|
| Id | int identity, PK | Referencia interna; número externo no se genera |
| ProveedorId | int FK restrict | Proveedor existente Activo al registrar |
| TipoProveedorAlRegistrar | enum int | Congelado, independiente del tipo actual |
| ProveedorRazonSocial, ProveedorCuit | nvarchar(200) / varchar(11), NOT NULL | Snapshot FR-019, sólo identificación; sin contacto copiado |
| Numero | nvarchar indexable | Trim; conservar ceros y separadores |
| Fecha, FechaVencimientoCae | date | Fecha <= hoy Argentina; vencimiento >= fecha |
| Detalle, Cae | nvarchar NOT NULL | No vacíos; sin formato fiscal impuesto |
| Neto, Iva, ImporteTotal | decimal(18,2) | Igual a FacturaConfiguracion; validar capacidad sin truncar |
| Estado | enum int | Únicamente Pendiente, JSON `pendiente` |
| LiquidacionId | int nullable FK restrict | Una obligatoria si tipo histórico Fletero; nula normal |
| ImporteLiquidacionAlRegistrar | decimal(18,2) nullable | Obligatorio con vínculo, coincide con ImporteTotal |
| ArchivoRuta | nvarchar(400) nullable | Ruta privada generada; HasMaxLength(400) |
| ArchivoNombre | nvarchar(255) nullable | Nombre original, sin truncar; HasMaxLength(255) |
| ArchivoTipoContenido | nvarchar(100) nullable | application/pdf validado por firma; HasMaxLength(100) |

`Numero` necesita longitud indexable para índice compuesto. Tomar capacidad máxima técnica compatible con SQL Server y el Id del índice, verificando tamaño en migración; documentar/rechazar exceso como capacidad de almacenamiento, sin imponer estructura fiscal. No truncar números ni aceptar dos prefijos iguales como un mismo número. Elegir nvarchar(400): 800 bytes + int por debajo de 900 bytes, conserva el texto íntegro admitido; documentar 400 caracteres en contrato técnico. Comparación de unicidad usa collation existente, sin regla nueva de numeración.

Índices: único `(ProveedorId, Numero)`; único filtrado `LiquidacionId IS NOT NULL`; para listado `(Fecha DESC, Id DESC)` y filtro `(ProveedorId, Fecha, Id)` si la consulta lo justifica. No agregar índices para futuras operaciones de pago.

CHECK: Neto > 0; IVA = ROUND(Neto × 0.21, 2); ImporteTotal = Neto + IVA; vencimiento >= fecha; Estado = Pendiente. No usar GETDATE en CHECK: validar fecha respecto a hoy en aplicación con fecha recibida por parámetro. CHECK de asociación: Fletero exige LiquidacionId e importe histórico NOT NULL e igual a total; normal exige ambos NULL. La pertenencia y el estado actual de liquidación se verifican bajo lock al registrar, no por CHECK que impediría cambios posteriores admitidos. No crear Version de factura porque no se edita.

El CHECK de archivo exige expresamente IS NOT NULL para todos los campos antes de longitudes/tipo; evita UNKNOWN por nulos. Ruta privada generada; nombre original para servir, nunca ruta del usuario. La relación conceptual Asociación se representa por columnas, sin tabla de reservas.

Las tres capacidades de archivo son idénticas a `backend/src/GT.Infrastructure/Persistencia/Configuraciones/DocumentacionConfiguracion.cs` y `DocumentacionVehiculoConfiguracion.cs`: todos nulos o todos completos, sin nvarchar(max) accidental ni truncamiento.

## Operaciones transaccionales

En todos los pasos siguientes, «lock de identidad» significa exactamente el coordinador de research §2: transacción → recurso global `GT.Proveedores.Identidad` → resolver CUIT efectivos anterior/propuesto y normalizarlos → recursos `GT.Proveedores.Cuit:<onceDigitos>` únicos en orden ordinal → locks de filas → revalidación → escritura → commit/rollback. Alta/edición de Proveedor y modificación de Transportista usan el mismo mecanismo, propietario y orden; el recurso por CUIT se adquiere incluso si ninguna fila lo contiene. Registro de factura protege su CUIT efectivo con ese mismo protocolo. No se cambia a un lock por Id ni se omite ante ausencia de coincidencias.

1. **Alta/edición de proveedor**: iniciar transacción → adquirir lock de identidad → bloquear/revalidar proveedor/Version si existe → leer Transportista/empresa emisora estable si Fletero → comprobar CUIT efectivo → guardar → commit → releer detalle. Si cambia Version, 409 sin mezcla. Normalizar antes de comprobar. Rechazos no dejan campos cambiados.
2. **Modificar Transportista**: lock de identidad antes de cambiar CUIT → verificar unicidad previa existente y contra proveedores efectivos excluyendo al propio Fletero → guardar o traducir conflicto al resultado ya usado. No actualizar filas de proveedores al cambiar Nombre/Telefono/Email.
3. **Baja/reactivación**: UPDATE sobre fila de proveedor condicionado a estado/Version recibido → verificar afectadas y releer si falla. Incrementar Version sólo si cambió; repetición de reactivación ya activa devuelve estado actual.
4. **Registro de factura**: validar datos y PDF → escribir archivo → iniciar SQL → adquirir lock de identidad → tomar proveedor Activo y releer → si Fletero bloquear origen y empresa emisora para lectura consistente → exigir vínculo válido y tomar liquidación Pendiente del mismo transportista → comparar total/disponibilidad → insertar factura con snapshot, archivo y vínculo → commit → releer detalle. Normal rechaza LiquidacionId informado. Ver research §4 para orden/costo de serialización y §5 para compensación y commit incierto.

No insertar orden de pago ni actualizar total/estado de liquidación, viajes o facturas cliente. La liquidación después puede editarse, anularse o pagarse según sus reglas existentes y conserva el importe histórico de asociación en la factura.
