# Investigación técnica — Feature 013

Fecha: 2026-10-14. Investigación sobre el código local, la spec 013 y `AGENTS.md`; no se cambia el comportamiento del producto durante planificación.

## 1. Identidad del proveedor

**Decisión**: proveedor normal conserva sus datos propios; Fletero sólo referencia `TransportistaId` y deja nulos razón social, CUIT, teléfono y email propios. Las consultas usan una proyección SQL condicional sobre el vínculo. Nombre comercial, dirección y estado son propios del proveedor. El documento guarda únicamente la identificación histórica exigida por FR-019.

**Motivo**: `backend/src/GT.Domain/Choferes/Transportista.cs` ya contiene `Nombre`, `Cuit`, `Telefono`, `Email`; FR-006 prohíbe identidad o contacto paralelos. Los campos opcionales del proveedor normal no heredan la obligatoriedad de Transportistas.

**Alternativas**: copiar CUIT o contacto para simplificar consultas introduce divergencia; mover Transportistas a una nueva entidad compartida invade otros módulos. Ambas se descartan.

## 2. Unicidad cruzada de CUIT

**Decisión**: índices únicos filtrados para CUIT propio y TransportistaId, más coordinación transaccional SQL de escrituras de identidad. Antes de leer o modificar identidad, adquirir `sp_getapplock` exclusivo con propietario `Transaction` y recurso fijo `GT.Proveedores.Identidad`. Participan altas/ediciones de Proveedores y modificaciones de Transportistas, incluso si todavía no tienen vínculo: comprobar sólo si está vinculado antes del lock deja una carrera con el alta del Fletero. Después se consulta el CUIT efectivo entre los demás proveedores, incluidos inactivos, y se guarda dentro de la misma transacción.

**Protocolo común por CUIT normalizado**: un único coordinador de persistencia, utilizado sin variantes por alta/edición de Proveedor y modificación de Transportista, toma primero el recurso global anterior. Bajo esa coordinación resuelve y normaliza los CUIT efectivos anterior y propuesto (para Fletero, desde Transportista), elimina duplicados y adquiere `sp_getapplock` exclusivo para cada recurso `GT.Proveedores.Cuit:<onceDigitos>`, en orden ordinal ascendente. El recurso depende únicamente del CUIT normalizado, nunca del tipo, Id de proveedor o existencia de fila. Por eso dos operaciones sobre el mismo CUIT esperan el mismo lock aunque aún no exista en ninguna tabla. En una edición se protegen tanto el CUIT anterior como el nuevo. Todos usan el mismo principal SQL, base, modo Exclusive y propietario Transaction; conservan locks hasta commit/rollback y abortan ante retorno negativo. Sólo después se toman locks de filas, se vuelve a comprobar unicidad y se escribe. No usar una consulta de existencia como condición para adquirir el lock. El registro de factura usa el mismo coordinador para el CUIT efectivo actual antes de tomar filas. Se conserva deliberadamente la coordinación global ya diseñada para evitar inversiones con las consultas cruzadas: no se promete paralelismo de escrituras entre CUIT distintos en este ajuste.

**Motivo**: `RepositorioTransportistas` sólo tiene índice de CUIT dentro de Transportistas. El CUIT efectivo de Fletero no es una columna de Proveedores; el índice de CUIT propio no garantiza la unicidad entre ambos tipos. El lock vive en SQL Server y coordina instancias de backend; no se usa un semáforo en memoria. Una modificación de contacto que no cambie CUIT conserva las reglas anteriores.

**Alternativas**: consulta previa sin coordinación deja carreras; duplicar CUIT efectivo viola FR-006; una vista indexada sobre identidad condicional requiere una complejidad mayor. El lock global de estas escrituras es deliberadamente simple para el volumen especificado; no bloquea búsquedas ni lectura de facturas.

**Implementación acotada**: `RepositorioTransportistas.GuardarCambiosAsync` integra transacción, lock y verificación de proveedores para la modificación, conservando la traducción de `IX_Transportistas_Cuit`; el conflicto cruzado usa el resultado de CUIT duplicado existente. No afecta bajas por dependencias ni asignación de choferes. Validar retorno negativo del lock y revertir, sin seguir guardando. Es garantía del protocolo de persistencia, no un índice que proteja escrituras SQL externas que lo omitan; éstas no forman parte del flujo de la app.

Referencia oficial: [sp_getapplock](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql): el propietario transaccional requiere una transacción activa.

## 3. Factura y vínculo

**Decisión**: `FacturaProveedor` tiene `LiquidacionId` nullable y `ImporteLiquidacionAlRegistrar`; un índice único filtrado sobre LiquidacionId garantiza exclusividad. Guardar `TipoProveedorAlRegistrar` para que las consultas no cambien al editar el proveedor. Un CHECK relaciona tipo histórico, vínculo e importe.

**Motivo**: no hay edición, anulación ni liberación de estas facturas (FR-036); una columna escalar conserva la asociación completa. No hace falta tabla de vínculos con vigencia como en LiquidacionViajes, donde sí se libera y debe preservarse el pasado.

**Alternativas**: tabla adicional con Vigente anticipa un ciclo de vida excluido; un vínculo sobre Liquidacion obliga a tocar su modelo sin necesidad.

## 4. Carreras y orden de bloqueos

**Decisión**: después de escribir el PDF, registro SQL inicia adquiriendo el mismo lock de identidad y tomando el proveedor Activo mediante UPDATE sin cambio; verifica una fila afectada. Relee tipo/vínculo actuales. Si es Fletero, bloquea su Transportista mediante UPDATE sin cambio antes de leer identificación y condición externa; después bloquea Liquidacion por Id, TransportistaId y Pendiente mediante UPDATE sin cambio. Relee total y disponibilidad bajo lock y compara con total calculado. Persistir una sola factura, con vínculo y snapshot, antes de confirmar.

**Motivo**: `RepositorioLiquidaciones.cs` ya comienza editar, anular y registrar pago con UPDATE condicional; ambos caminos compiten por la misma fila. No se cambian sus reglas ni se impide operar después de la asociación. Pago parcial conserva Pendiente y sigue siendo elegible. La baja del proveedor y su edición compiten por el lock de su fila.

**Orden**: escritura de identidad y registro de factura: lock de identidad → proveedor si existe → Transportista si corresponde → Liquidacion si corresponde. Modificar Transportista adquiere primero lock de identidad, luego verifica CUIT efectivo y guarda; no actualiza proveedores vinculados. Incluir registro de factura en ese lock evita un deadlock entre su UPDATE de Proveedor y las lecturas de proveedores al modificar Transportista. El costo es serializar cargas de factura y escrituras de identidad, aceptable al volumen especificado; medir transacciones breves y escribir PDF fuera del lock. Baja/reactivación sólo toma la fila de proveedor y no busca después el lock de identidad. Leer empresa emisora con lock retenido mantiene su CUIT estable hasta terminar, sin agregar una regla al Módulo 6. No releer entidades rastreadas desactualizadas después de ExecuteUpdate.

**Alternativas**: validar y luego insertar sin locks deja pasar bajas y cambios de total; agregar una ocupación paralela de liquidaciones es redundante.

Referencia oficial: [ExecuteUpdate](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete) no sincroniza el rastreador ni aplica concurrencia automáticamente; se usa transacción explícita y se verifica el número afectado.

## 5. PDF y atomicidad

**Decisión**: una petición multipart para datos y PDF opcional. Validar campos y firma, exigir `application/pdf`, escribir el archivo completo antes de abrir la transacción SQL; registrar factura y vínculo juntos. Ante cualquier rechazo o excepción previo al commit, revertir SQL y borrar el archivo escrito con `CancellationToken.None`. Después de commit no compensar por fallo de relectura o respuesta HTTP.

**Motivo**: `CargarDocumento.cs`, `IAlmacenDeArchivos`, `AlmacenDeArchivos` y `ResultadoArchivo.EnLinea` ya fijan ese orden. El validador compartido admite imágenes además de PDF: el caso de uso restringe el resultado a PDF sin alterar otros módulos. Tamaño máximo 10 × 1024 × 1024 bytes; nombre físico generado y nombre original sólo metadata.

**Capacidades de metadata**: `ArchivoRuta` nvarchar(400), `ArchivoNombre` nvarchar(255) y `ArchivoTipoContenido` nvarchar(100), todas anulables, iguales a `DocumentacionConfiguracion` y `DocumentacionVehiculoConfiguracion`. Configurar HasMaxLength(400/255/100), validar sin truncar y conservar el CHECK de los tres nulos o completos; no dejar nvarchar(max) por omisión.

**Límite explícito**: SQL y disco no tienen commit distribuido. La atomicidad observable exige que ninguna factura confirmada apunte a una escritura fallida y que nunca quede vínculo sin factura. Una caída entre escritura y commit o un fallo del borrado puede dejar un archivo huérfano, permitido por AGENTS [003], invisible para usuarios. No se agrega un proceso de limpieza ni almacenamiento binario en base. Si el resultado de commit es incierto por pérdida de conexión, confirmar la existencia del registro por su identidad antes de compensar; si no puede verificarse, preservar el archivo para no borrar un PDF ya referenciado. Registrar el fallo con el logging existente.

**Alternativas**: subir PDF en un endpoint separado crea facturas incompletas; guardar fila antes que archivo puede producir referencia rota. El PDF recibido no pasa por QuestPDF ni se genera un comprobante propio.

## 6. Cálculos, fechas, errores y consultas

**Decisión**: neto decimal positivo de dos decimales; IVA = redondeo comercial(neto × 0,21), total = neto + IVA. Reutilizar `CalculadorImportes.IvaSobre` con el tipo existente que representa 21%, sin adoptar el ciclo de vida de FacturaCliente. El frontend recibe y envía todos los importes HTTP de 013 como cadenas decimales canónicas con exactamente dos decimales y tipo semántico ImporteDecimal basado en string. Usa centavos BigInt en toda la capacidad decimal, con IVA en centavos = (netoCentavos × 21n + 50n) / 100n para valores positivos, sin convertir importes completos a number. Dominio/persistencia siguen en decimal(18,2); el borde HTTP exacto del módulo usa F2 invariant y rechaza entradas inválidas sin double ni redondeo silencioso. Esto incluye snapshots de liquidación, total disponible e importes de conflicto. Extender moneda compartida con una sobrecarga exacta de cadena decimal, sin alterar los resultados de sus callers number, se justifica por AGENTS [005]: todos los importes se formatean con compartido/moneda. No cambiar contratos de otros módulos ni introducir topes comerciales o biblioteca nueva. Probar ida y vuelta y presentación de 100000000000000.01 y 100000000000000.02 como valores distintos. Fechas `DateOnly`, hoy obtenido una vez mediante `FechaHoyArgentina`; formato `compartido/fechas` y `compartido/moneda`.

**Motivo**: `CalculadorImportes.cs` usa AwayFromZero; `InputImporte` ya formatea mientras se escribe. $0,50 produce IVA $0,11; $0,01 produce IVA $0,00 y total $0,01. Neto cero/negativo se rechaza, no se agregan alícuotas ni CAE fiscal.

**Consultas**: filtros y proyección enteramente SQL antes de Count/Skip/Take; tamaño 20, orden total terminado en Id, `AsNoTracking`. Escape de `%`, `_` y `[` al construir LIKE para búsquedas literales. FechaDesde/FechaHasta inclusivas sobre fecha de factura, no instante UTC. 400 para campos/duplicados, 409 para estado o disponibilidad que cambió, 401/403/404 según patrones actuales. Diferencia de importes devuelve mensaje armado y valores monetarios canónicos como cadenas desde servidor, sin reparsear el mensaje.

## 7. Autorización y frontend

**Decisión**: tres permisos propios: `proveedores.gestionar`, `proveedores.facturas.cargar`, `proveedores.facturas.consultar`, sembrados explícitamente a Administración de la empresa y Administrador del sistema. Catálogo de menú y políticas existentes, sin chequeos de rol. Autocompletes mínimos del módulo no requieren permiso de Choferes ni Liquidaciones. Una política OR permite consulta mínima de proveedores para carga o consulta de facturas; no usar múltiples requirements para un OR (sería AND).

**Motivo**: `CatalogoOpcionesMenu`, `CodigosPermiso`, `SembradorInicial`, `PermisoRequirement`, `clienteHttp` y módulos Adelantos/Caja resuelven estos patrones. POST de factura exige únicamente cargar y devuelve el detalle completo recién creado, releído internamente después del commit. GET de detalle, listado y PDF exigen consultar; cargar no implica consultar. La pantalla posterior al alta conserva el DTO devuelto por POST en un contexto/store local de la feature, exclusivamente en memoria del runtime React, montado sobre las páginas de carga/detalle. Navega por URL sin DTO en location.state/history.state. Si el Id en memoria coincide, muestra ese resultado sin GET; si falta o no coincide, ejecuta GET que exige consultar, aunque sólo tenga cargar y termine en 403. La recarga completa reinicia necesariamente el contexto; no usar localStorage, sessionStorage, IndexedDB ni persistencia equivalente. Limpiar al cerrar/cambiar sesión y conservar sólo el último resultado. La relectura interna del POST no invoca una ruta HTTP ni depende de otro permiso. El enlace al PDF sólo se ofrece si consultar está disponible.

**Frontend**: módulo proveedores, autocomplete reutilizado localmente en alta y filtro, 250 ms de demora técnica, sin mínimo de caracteres como regla nueva. Input combobox y lista de opciones, ID distinto del texto, respuestas tardías descartadas, siguientes coincidencias paginadas accesibles. Tipo, estado y liquidación mantienen select nativo. La elección de Transportista usa su propio campo q y select nativo de una página de 20 con Anterior/Siguiente, sin precargar todos ni reutilizar el autocomplete de proveedores. Cambiar q vuelve a página 1; limpiar TransportistaId si no pertenece al conjunto actual recibido y descartar respuestas tardías. En edición iniciar q con el CUIT actual para recuperar su selección en página 1. Probar un transportista inicialmente en posición 21 o posterior y estados accesibles de carga/vacío/error. Cambiar proveedor limpia liquidación. Reutilizar UI compartida; no cambiar controles o textos de otras pantallas.

**Alternativas**: reutilizar el permiso gestionar de Choferes expone documentación; desplegable con todo el padrón incumple FR-011; biblioteca de autocomplete nueva no es necesaria.

## 8. Fuente GT UI restaurada

**Decisión**: restaurar únicamente `.claude/skills/gt-ui/SKILL.md` y `references/tokens.md` con autorización del usuario. Prioridad: feature 008, primitivas UI, index.css y pantallas consistentes. Documentar componentes y patrones dentro de SKILL, sin enlazar imágenes o referencias inexistentes. Leer ambos archivos antes del UI Design Check definitivo.

**Motivo**: la Constitución mantiene la dependencia y el usuario exige restaurarla. La restauración describe hechos verificables; no introduce estilos. Las correcciones de 008 y la convención 012 conservan nombres accesibles y exportación secundaria. Constitución y plan-template no se modifican.

## 9. Verificación

**Decisión**: xUnit unitario e integración con SQL Server real del compose; Vitest/Testing Library para recorridos. Pruebas de concurrencia usan dos DbContext y sincronización para garantizar el solapamiento, sin depender de sleeps; no sustituir con EF InMemory. Fallos de almacenamiento y persistencia se prueban inyectando dobles de las interfaces existentes, más un recorrido real multipart/lectura del PDF desde DI.

**Motivo**: locks, índices, CHECK y ejecución SQL no se prueban con mocks; los errores de archivo requieren probar la compensación antes y después del commit. No ejecutar suites de producto por cambios puramente documentales; los comandos y resultados esperados se fijan en quickstart.
