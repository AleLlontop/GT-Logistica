# Contratos de interfaz — Proveedores

Diseño para feature 013; no endpoints implementados en planificación. Referencias: [spec](../spec.md), [modelo](../data-model.md). Cookie de sesión, errores y cliente HTTP existentes. Fechas date-only `yyyy-MM-dd`; enums JSON camelCase; importes HTTP como cadenas decimales canónicas en ARS, nunca JSON number. Todas las rutas `{id}` usan `{id:int}`.

## Representación monetaria exacta

Todo importe HTTP de esta feature es una cadena con punto y exactamente dos decimales, sin miles, signo positivo, exponente ni ceros iniciales innecesarios: `"100000.00"`, `"21000.00"`, `"121000.00"`, `"100000000000000.01"`. Para los importes no negativos del módulo, forma `^(0|[1-9][0-9]*)\.[0-9]{2}$`; neto además es estrictamente mayor a cero. La capacidad decimal(18,2) es técnica, sin topes comerciales adicionales; validar también que los resultados calculados sean representables.

Se aplica a `neto`, `iva`, `importeTotal`, `importeLiquidacionAlRegistrar` y su nombre público `liquidacion.importeAlRegistrar`, `liquidacion.importeActual`, el total de cada liquidación disponible y cualquier importe de un error, incluidos `importeTotalFactura` e `importeTotalLiquidacion`. Disponible usa `{ id, numero, periodo, importeTotal }` con `importeTotal` de este mismo tipo. Ningún endpoint nuevo del módulo reutiliza una respuesta monetaria numérica de Liquidaciones sin adaptarla en su borde HTTP.

C# conserva decimal/decimal(18,2) en dominio y persistencia. El borde HTTP del módulo serializa decimal como texto invariant con formato F2 y deserializa desde texto de forma exacta, validando antes de convertir, sin pasar por double y sin redondear entradas inválidas. El multipart continúa enviando `neto` como texto decimal invariant de dos decimales. El cambio se limita a los contratos de 013: no modificar serialización global de otros módulos.

TypeScript define `ImporteDecimal` como tipo semántico basado en string. Desde la respuesta hasta el cálculo/presentación no convertir el importe completo con Number, parseFloat ni coerción numérica. Centavos/BigInt para preview, comparación y redondeo comercial exactos, sin nueva biblioteca; formato ARS exacto mediante soporte de cadena decimal en moneda compartida, preservando su comportamiento previo para number. Los BigInt internos no viajan directamente en JSON. Las pruebas deben distinguir `"100000000000000.01"` de `"100000000000000.02"` en ida y vuelta HTTP, preview, lista, detalle y errores de conflicto.

## Permisos y menú

| Código | Menú | Rutas UI |
|---|---|---|
| proveedores.gestionar | Proveedores | /proveedores, /proveedores/nuevo, /proveedores/:id, /proveedores/:id/editar |
| proveedores.facturas.cargar | Cargar factura de proveedor | /proveedores/facturas/nueva |
| proveedores.facturas.consultar | Consultar facturas de proveedores | /proveedores/facturas, /proveedores/facturas/:id |

Sembrar explícitamente los tres a Administración de la empresa y Administrador del sistema. Ninguno a Gerencia/Tráfico por analogía. Se prueban permisos aislados: ninguna implicación automática. Catálogo servidor decide opciones; frontend agrupa Proveedores en Padrones y las facturas en Operación. POST de factura exige cargar y devuelve el detalle completo recién creado; GET de detalle, listado y PDF exigen exclusivamente consultar. Cargar no implica consultar. La búsqueda mínima de proveedores admite cualquiera de los tres permisos, sin permitir edición por búsqueda.

## Padrón

| Método y ruta /api | Permiso | Entrada / resultado |
|---|---|---|
| GET /proveedores | gestionar | q, razonSocial, nombreComercial, cuit, tipo, estado, pagina; página de resumen |
| GET /proveedores/{id:int} | gestionar | Detalle actual |
| POST /proveedores | gestionar | JSON de alta; 201 con detalle y Location |
| PUT /proveedores/{id:int} | gestionar | JSON de edición con version; 200 detalle releído |
| POST /proveedores/{id:int}/baja | gestionar | version; confirmación previa en UI; 200 detalle |
| POST /proveedores/{id:int}/alta | gestionar | version; idempotente; 200 detalle |
| GET /proveedores/busqueda | gestionar OR cargar OR consultar | q, soloActivos, pagina; resumen mínimo paginado |
| GET /proveedores/transportistas-externos | gestionar | q, pagina; identidad mínima paginada, externos sin exigir Activo como nueva regla |

Alta normal: `{ tipo: "proveedorNormal", razonSocial, cuit, nombreComercial?, telefono?, email?, direccion? }`. Alta Fletero: `{ tipo: "fletero", transportistaId, nombreComercial?, direccion? }`. Campos compartidos propios no se envían para Fletero; si se informan valores se rechaza, sin crear copias. Estado inicial Activo; cambios separados. Edición misma forma más `version` obligatoria. Campos vacíos opcionales se normalizan a null.

Resumen: `{ id, razonSocial, nombreComercial, cuit, tipo, estado }`. Detalle agrega `{ telefono, email, direccion, transportistaId, version }`; identidad/contacto efectivos leídos del origen. Estado proveedor `activo` / `inactivo`. Búsqueda no expone dirección, contacto ni documentos. `soloActivos` enlaza `bool?` con `?? false`; alta de factura lo manda true y la consulta false. Ni texto libre ni ID inexistente habilitan una selección.

Todos los listados: `{ items, total, pagina, tamanioPagina: 20 }`; pagina empieza en 1, inválida devuelve 400. Proveedores orden `razonSocial ASC, id ASC`; búsquedas aplican mismo criterio. Filtros independientes se combinan con AND; q busca en los tres datos con OR. Texto de búsqueda literal, no patrones SQL del usuario. Campos vacíos ignoran el filtro; tipos/estados desconocidos se rechazan. Sin filtro de estado se incluyen ambos y se lo declara en pantalla.

## Facturas

| Método y ruta /api | Permiso | Entrada / resultado |
|---|---|---|
| GET /proveedores/facturas | consultar | desde, hasta, proveedorId, estado, pagina; página |
| POST /proveedores/facturas | cargar | multipart de datos + archivo opcional; 201 con detalle completo recién creado y Location |
| GET /proveedores/facturas/{id:int} | consultar | Detalle |
| GET /proveedores/facturas/{id:int}/archivo | consultar | Stream application/pdf, inline, nosniff |
| GET /proveedores/{id:int}/liquidaciones-disponibles | cargar | Sólo Pendientes del transportista vinculado, no asociadas; número, período, total |

Multipart usa campos `proveedorId`, `numero`, `fecha`, `detalle`, `neto`, `cae`, `fechaVencimientoCae`, `liquidacionId` cuando Fletero, `archivo` cuando existe. Decimal se envía con punto e invariant culture, sin miles. El navegador pone boundary; no fijar Content-Type manualmente. Número conserva texto, máximo técnico de almacenamiento 400 caracteres, sin formato fiscal nuevo. Neto > 0, precisión de dos decimales y capacidad decimal existente; no truncar ni redondear neto inválido para que parezca válido. IVA/total/estado/snapshots/ruta física no son campos editables. Intentar imponer valores calculados discordantes se rechaza, no se persisten.

Normal no tiene liquidación; Fletero la exige. No hay endpoint de subida separada, edición/anulación de factura, pagos, emisión fiscal ni preview PDF generado. Liquidaciones disponibles devuelve colección con sólo datos mínimos; el permiso cargar no habilita la ficha completa del Módulo 9. Para normal devuelve colección vacía; invocar asociación al guardar igualmente se rechaza. Sin configuración externa o proveedor inactivo, devolver conflicto con motivo. El estado Activo del proveedor no depende automáticamente del del Transportista.

Resumen de factura: `{ id, numero, fecha, proveedorId, proveedorRazonSocial, proveedorCuit, importeTotal, estado }`, usando identificación histórica. Orden `fecha DESC, id DESC`. Filtros desde/hasta inclusivos e independientes; proveedor por Id de origen; estado `pendiente` o sin filtro. No filtrar por la razón social histórica en el autocomplete de origen.

Detalle agrega `{ tipoProveedorAlRegistrar, detalle, neto, iva, cae, fechaVencimientoCae, archivo, liquidacion }`. Archivo: null o `{ nombre, url }`, nunca ruta física. Liquidacion: null para normal, o `{ id, numero, periodo, importeAlRegistrar, importeActual, estadoActual }`. Número visible de liquidación viene de `NumerosVisibles`, sin reconstruirlo en TypeScript. Datos de proveedor origen pueden mostrarse con referencia interna sólo si gestionar está disponible; conservar el Id para filtro aunque no pueda abrir su padrón. No ofrecer enlace a ficha de Liquidaciones si falta consultar de ese módulo.

POST devuelve exactamente esa forma de detalle, incluyendo los campos del resumen, releída internamente después del commit. El frontend conserva temporalmente ese `DetalleFacturaProveedor` en un contexto/store local de la feature, estrictamente en memoria del runtime React, y navega a `/proveedores/facturas/{id}` sin transportar el DTO en el estado del router. El contexto se monta por encima de ambas páginas para sobrevivir a esa navegación. Sólo si el Id del DTO en memoria coincide con el de la dirección, la ficha lo muestra sin GET; si no existe o no coincide, realiza GET de detalle y el servidor exige consultar. Con sólo cargar, ese GET devuelve 403.

Una recarga completa crea un runtime nuevo y necesariamente pierde el DTO; probar el flujo sin restaurar artificialmente ese contexto. Prohibidos para el DTO localStorage, sessionStorage, IndexedDB, location.state, history.state y cualquier persistencia equivalente. Limpiar el resultado al cerrar/cambiar sesión y sustituirlo por el siguiente resultado de carga; mantener sólo el último DTO, sin cache de otras facturas. El resultado del POST no amplía permisos: listado y PDF siguen exigiendo consultar; la URL de archivo sigue protegida y su acción sólo se ofrece con consultar.

La metadata del adjunto respeta las capacidades existentes: ArchivoRuta 400, ArchivoNombre 255 y ArchivoTipoContenido 100 caracteres, con HasMaxLength y sin truncar. Sólo nombre/URL pública autorizada salen al cliente; la ruta física no se expone. Un exceso informado por el archivo se rechaza como error del campo archivo, sin crear una factura parcial.

## Errores

Cuerpo del patrón existente: `{ codigo, mensaje, campo? }`; datos adicionales sólo cuando ayudan a resolver. Texto del servidor en español argentino. Duplicados y campos: 400. Recurso inexistente: 404. Falta sesión: 401; falta permiso: 403, no mensaje de reintento. Recurso cambiado: 409. Error inesperado/almacenamiento: respuesta del patrón existente con mensaje recuperable, sin datos técnicos ni factura parcial.

| Situación | HTTP | Campo / dato |
|---|---|---|
| CUIT inválido/duplicado, email, obligatorio faltante | 400 | Campo correspondiente |
| Neto vacío/cero/negativo, más de dos decimales | 400 | neto |
| Fecha futura, vencimiento anterior | 400 | fecha / fechaVencimientoCae |
| Número repetido por proveedor | 400 | numero |
| Fletero sin liquidacionId; normal con él | 400 | liquidacionId |
| Archivo no PDF o > 10 MB | 400 | archivo, motivo puntual |
| Edición con Version obsoleta | 409 | mensaje de proveedor cambiado |
| Proveedor Inactivo al guardar | 409 | proveedorId, motivo actual |
| Liquidación cambió/no disponible/otro transportista | 409 | liquidacionId, motivo actual |
| Total diferente contra liquidación | 409 | liquidacionId; importeTotalFactura e importeTotalLiquidacion y mensaje armado |

La diferencia contra una liquidación es conflicto 409 porque depende del importe actual del recurso compartido, según AGENTS [005]; no requiere detectar cómo se produjo ni una Version nueva de liquidación. Neto inválido por sí mismo sigue siendo 400. El frontend muestra el mensaje y los importes actuales, conserva lo ingresado y no ofrece una confirmación que permita guardar con importes distintos. Tests fijan este contrato sin depender del reloj ni convertir el conflicto en pago.

## Interacción del autocomplete

Texto y selección separados. Escribir invalida Id y liquidación, limpiar devuelve vacío, Enter selecciona opción resaltada, flechas recorren, Escape cierra, Tab sale sin retener foco. Debounce técnico de 250 ms; no mínimo de longitud. Secuencia de peticiones descarta respuestas antiguas sin cambiar clienteHttp compartido. Listbox paginado de 20 con acción accesible para siguientes resultados; nunca ocultar más coincidencias sin salida. Los resultados tienen nombre/CUIT completos para selección inequívoca; anuncios de carga/ausencia/error en texto. Alta true, filtro false. La ausencia de disponibles de Fletero impide guardar y explica el motivo.

## Selección paginada de Transportista externo

El formulario Fletero usa un campo de búsqueda `q`, un select nativo y controles Anterior/Siguiente asociados a `GET /proveedores/transportistas-externos?q=...&pagina=...`. La respuesta es `{ items, total, pagina, tamanioPagina: 20 }`, con identidad mínima, orden nombre/Id y filtro SQL antes de paginar. El select contiene únicamente opciones de la página actual; nunca se descarga el padrón entero ni se convierte en el autocomplete de proveedores.

Cambiar el texto vuelve a página 1. Anterior/Siguiente se habilitan cuando existen páginas correspondientes y permiten seleccionar al transportista originalmente en posición 21 o posterior. Al recibir el conjunto actual tras búsqueda/paginación, si el seleccionado ya no está en él, limpiar TransportistaId; no conservar una selección invisible. Mientras cambia el conjunto no guardar usando resultados anteriores; descartar respuestas tardías. Mostrar y anunciar carga, vacío y error, conservar búsqueda ante fallo y permitir reintentar; etiquetas/foco y select siguen siendo nativos. En edición, iniciar la búsqueda con el CUIT actual y página 1 para recuperar y mostrar su selección sin precargar todas; luego aplicar las mismas reglas de búsqueda/paginación.
