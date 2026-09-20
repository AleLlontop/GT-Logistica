# Research: Emitir reportes (Módulo 12)

**Feature**: `012-emitir-reportes` | **Fecha**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

Decisiones tomadas antes de diseñar. Cada una dice qué se eligió, por qué, y qué se descartó.

La spec no dejó ningún `NEEDS CLARIFICATION`: sus *Assumptions* ya resolvieron las preguntas de
producto. Lo que queda son decisiones técnicas, y una —§8— que **revierte parcialmente una de esas
assumptions** y lo declara.

---

## §1 · La biblioteca de Excel: ClosedXML

**Decisión**: `ClosedXML` 0.105.1, referenciada **sólo desde `GT.Infrastructure`**, igual que QuestPDF.

**Por qué**: es la biblioteca de planillas más usada del ecosistema .NET (222 millones de descargas),
licencia MIT sin cláusula comercial, y escribe `.xlsx` real de OpenXML. Lo que decide es FR-011: los
importes y las fechas tienen que quedar como **valores numéricos y de fecha**, no como texto.
ClosedXML lo hace asignando el `decimal` o el `DateTime` a la celda y fijando el formato con
`Style.NumberFormat.Format`; el valor sigue siendo un número y la planilla lo ordena, lo filtra y lo
suma sin conversión (SC-007).

**Alternativas descartadas**:

- **EPPlus**. Desde la versión 5 su licencia es *Polyform Noncommercial*: un sistema que una empresa
  usa para operar no es uso no comercial. Queda afuera por licencia, no por capacidad.
- **`DocumentFormat.OpenXml` a secas**. Es la capa sobre la que ClosedXML está construida. Escribir
  cinco planillas con formato de número y de fecha a mano sobre el XML es decenas de líneas por
  reporte para resolver algo que la spec da por resuelto ("se incorpora una biblioteca conocida y de
  uso extendido, que es lo que el enunciado pide").
- **CSV**. No es lo que la spec pide —dice *Excel*— y además no puede llevar formato de número ni de
  fecha: todo llega como texto y SC-007 no se cumple.

**Requisitos nativos**: ninguno. A diferencia de QuestPDF —que necesita `libfontconfig1` y
`libfreetype6`, y por eso el `Dockerfile` los instala—, ClosedXML es C# puro. **El `Dockerfile` no
cambia.** Aun así lleva el test de integración de la convención [006]: la dependencia se ejercita de
verdad y **resolviendo el servicio del contenedor**, no instanciando la clase, porque lo que falla en
producción es lo que el arranque configura.

---

## §2 · Un modelo de reporte, dos armadores: no diez

**Decisión**: existe **un** modelo neutro en memoria, `ReporteTabular`, y **dos** armadores
—`IArmadorReportePdf` e `IArmadorReporteExcel`—, cada uno de los cuales recibe ese modelo y devuelve
bytes. Las cinco pantallas aportan **cinco fuentes** que proyectan su consulta al mismo modelo.

```
ConsultarViajes ─┐
Vencim. choferes ├─► FuenteDeReporte ─► ReporteTabular ─┬─► ArmadorReportePdf   ─► byte[]
Vencim. flota    │                                       └─► ArmadorReporteExcel ─► byte[]
Vencim. facturas │
Movimientos caja ┘
```

**Por qué**: la alternativa obvia —un armador por reporte y por formato— son **diez** clases que
dibujan la misma tabla. Con este modelo son **5 + 2**, y agregar la sexta pantalla en una feature
futura es una fuente, no dos armadores.

Pero lo que de verdad decide es la convención [006]: *cuando una misma información se produce por dos
caminos, los dos llaman al mismo armador sobre la misma entrada*. Acá los dos caminos son los dos
**formatos**. Con un modelo intermedio único, el PDF y el Excel del mismo reporte **no pueden** traer
filas distintas, en otro orden o con otro total: leen el mismo objeto. No hace falta un test que
compare los dos archivos —que además serían incomparables byte a byte, por formato— porque la
igualdad es estructural.

**La celda lleva el valor y su tipo, no texto ya formateado.** Es lo que hace posible que FR-010
(pesos argentinos **en el PDF**) y FR-011 (número y fecha de verdad **en el Excel**) convivan sin
contradecirse: cada armador formatea lo que le corresponde a partir del mismo `decimal` o el mismo
`DateOnly`. Si la celda llegara como `"$ 1.240.000,00"`, el Excel no podría sumarla.

**Alternativa descartada**: que cada fuente devuelva el archivo ya armado. Deja diez caminos y
devuelve al problema que [006] describe: dos traducciones al mismo destino se separan sin que nadie
lo note.

---

## §3 · Cómo se traen "todas las filas del filtro" sin duplicar ninguna consulta

FR-007 exige todas las filas del filtro, no las de la página visible; FR-007 y SC-002 exigen además
**el mismo orden que la pantalla**. La única manera de garantizar los dos a la vez es que el reporte
use **la misma consulta** que el listado, no una parecida.

**Decisión**, por clase de pantalla:

- **Viajes y Movimientos de caja** (paginados). Se agrega un parámetro **opcional** `tamanioPagina`
  a `IRepositorioViajes.ConsultarAsync` y a `IRepositorioCaja.ConsultarMovimientosAsync`, con el
  valor por defecto de hoy (`PaginaDe<T>.TamanioPorDefecto`). Ninguna llamada existente cambia. La
  fuente del reporte llama a la **misma** consulta con `Pagina: 1` y `tamanioPagina:
  opciones.TopeDeFilas`, y lee de la respuesta el `Total`, que esa consulta ya calcula con un
  `CountAsync` **sobre el filtro completo, antes de paginar**. El valor por defecto del parámetro se
  declara **sólo en la interfaz** y la implementación lo resuelve con `??`: repetirlo de los dos lados
  deja dos defaults que se resuelven por el tipo estático de quien llama y pueden divergir en
  silencio.
- **Los tres paneles de vencimientos** (sin paginar). Ya devuelven la lista entera hoy: la fuente
  llama al caso de uso tal cual está. **Cero cambios** en Choferes, Flota y Facturación.

**Por qué así**: el orden no se replica, se hereda — es literalmente el mismo `OrderBy`. Y el conteo
de FR-016 no cuesta una consulta nueva: el `Total` de `PaginaDe<T>` ya es el número exacto de filas
que cumplen el filtro.

**Alternativas descartadas**:

- **Un método `ConsultarTodoAsync` paralelo en cada repositorio.** Es la segunda escritura de la
  misma consulta, con sus filtros y su orden, que es justo lo que SC-002 puede romper en silencio el
  día que alguien toque un filtro de un solo lado.
- **Pedir página por página desde la fuente y concatenar.** 250 viajes de ida y vuelta para 5.000
  filas, y una fila que se inserta entre dos páginas se duplica o se pierde.

---

## §4 · El tope de 5.000 filas: dónde se cuenta, qué código HTTP, quién escribe el mensaje

**Decisión**: el tope vive en **un solo lugar del backend** —`OpcionesDeReporte.TopePorDefecto = 5000`,
inyectado como `OpcionesDeReporte` para que la aplicación de prueba pueda bajarlo (data-model §4)—, se
evalúa **en el servidor** contra el `Total` del §3, y se rechaza con **`409`** y el código
`tope_de_filas_superado`, con `filas` y `tope` en el cuerpo además del mensaje ya armado. Se rechaza
**más de** el tope: con filas iguales al tope, el reporte se entrega.

**Por qué `409` y no `400`**: la convención [005] fija `400` cuando el problema está en lo que se
tipeó y `409` cuando está en el estado de algo que se comparte o que cambió. Los filtros que se
tipearon son válidos — el listado los acepta y muestra sus resultados. Lo que rechaza la generación
es **cuántas filas hay ahora**, que cambia sin que nadie toque el filtro: el mismo filtro que hoy
deja 4.999 viajes mañana deja 5.001. Es estado, no tipeo.

**Por qué el frontend no pre-verifica el tope**, aunque la pantalla conozca el `total` del listado:
porque FR-016 exige un mensaje que diga las filas, el tope y qué hacer, y escribirlo en TypeScript
además de en C# son **dos textos que se separan**. El `409` viaja con el mensaje ya armado y la
pantalla lo muestra — el patrón de [009] ("el `409` que viaja con los importes que la pantalla
muestra"), y el `409` vuelve como **resultado** y no como excepción en el servicio del frontend.

**Lo que el frontend sí decide es el caso de cero filas** (FR-003), y ahí no hay texto duplicado: el
listado ya sabe que está vacío, la acción queda deshabilitada y al lado se explica por qué. **El
backend no rechaza cero filas**: la spec no lo pide (su regla es sobre la disponibilidad del botón) y
un reporte de cero filas invocado a mano es inofensivo. Inventarle un rechazo sería alcance fantasma.

---

## §5 · Dos permisos sobre un mismo endpoint

FR-013 pide un permiso propio, `reportes.emitir`. FR-015 pide **además** el permiso de lectura de la
pantalla de la que sale el reporte.

**Decisión**: se agrega a `PoliticasAutorizacion` un método `ParaTodos(params string[] codigos)` que
arma una política con **varios `PermisoRequirement`**. ASP.NET Core exige que **todos** los
requirements de una política se satisfagan, así que la conjunción sale gratis y el `PermisoHandler`
del Módulo 1 **no cambia una línea**. Se registran cinco políticas combinadas, una por reporte.

**Por qué no un chequeo dentro del endpoint**: porque la convención del Módulo 1 —y el Principio de
[004]— es que la autorización se evalúa por permiso y nunca a mano adentro del handler. Un `if` en el
endpoint es una regla de autorización que el pipeline no ve.

**Alternativa descartada**: un permiso de reportes **por módulo** (`viajes.reportes.emitir`, …). Son
cinco permisos donde FR-013 pide uno, y el reparto a roles sería idéntico en los cinco.

**Reparto** (FR-014): `reportes.emitir` lo reciben **Gerencia** y **Administrador del sistema**.
Tráfico y Administración de la empresa no. Lo siembra `SembradorInicial`, que es idempotente y lo
agrega solo en el próximo arranque de una instalación existente: **no hace falta migración**, porque
la fila del permiso y las del reparto las crea el sembrador, no el esquema.

**No hay entrada de menú.** El catálogo de opciones es una lista de pares permiso → pantalla; un
permiso sin pantalla propia simplemente no figura, y ningún test lo exige. *Emitir reportes* es una
capacidad sobre cinco pantallas que ya están en el menú, no una sección (spec §Aclaración).

---

## §6 · Cinco endpoints, uno por reporte, todos en un archivo

**Decisión**: cinco rutas `GET`, con `?formato=pdf|excel`, declaradas **las cinco juntas** en
`GT.Api/Reportes/ReportesEndpoints.cs`.

| Reporte | Ruta | Política |
|---|---|---|
| Viajes | `/api/viajes/reporte` | `viajes.consultar` + `reportes.emitir` |
| Vencimientos de choferes | `/api/vencimientos/reporte` | `choferes.vencimientos.consultar` + `reportes.emitir` |
| Vencimientos de flota | `/api/flota/vencimientos/reporte` | `flota.vencimientos.consultar` + `reportes.emitir` |
| Vencimientos de facturas | `/api/facturas/vencimientos/reporte` | `facturacion.consultar` + `reportes.emitir` |
| Movimientos de caja | `/api/movimientos-caja/reporte` | `caja.consultar` + `reportes.emitir` |

**Por qué cinco y no uno genérico** (`/api/reportes/{cual}`): porque FR-015 exige un permiso de
lectura **distinto por reporte**, y las políticas de ASP.NET se declaran por endpoint. Un endpoint
genérico tendría que resolver el permiso a mano adentro, que es lo que §5 descarta. Además cada
reporte recibe **los filtros de su pantalla**, con el mismo enlace de parámetros que el listado ya
usa.

**Por qué los cinco en un archivo**: FR-001 dice "ninguna otra pantalla la ofrece en esta feature".
Escrito así, la revisión **cuenta cinco `MapGet`** en un solo lugar en vez de buscarlos por cinco
carpetas.

**Las cinco rutas son literales y conviven con rutas de identificador** —`/api/viajes/{id:int}`,
`/api/facturas/{id:int}`— que **ya llevan su restricción de tipo**. Verificado: sin esa restricción
las literales quedarían inalcanzables y no falla ni al compilar ni al arrancar, falla al pedirlas
(convención [005]). No hay que agregar ninguna.

**`formato` ausente o distinto de `pdf`/`excel`** → `400 formato_invalido`. Es un problema de lo que
se pidió, no de estado.

---

## §7 · El instante de generación **sí** entra en el archivo

**Decisión**: el `ReporteTabular` lleva el instante de generación, leído del `TimeProvider` registrado
y mostrado en hora de Argentina; el PDF lo estampa además en sus metadatos (`CreationDate`).

**Por qué hay que decirlo explícitamente**: el Módulo 6 fijó exactamente lo contrario para el
documento de la factura —"un artefacto generado tiene que ser función de sus datos y de nada más… la
fecha **nunca se lee del reloj**"— y lo protege con un test de igualdad byte a byte. Esa regla **no
se extiende a los reportes**, y la spec lo dice: una factura es un comprobante que no cambia; un
reporte es la foto de un listado que sí cambia, y saber a qué momento corresponde la foto es parte
del dato (FR-006, spec §Assumptions).

**La consecuencia operativa**: **no se escribe ningún test de igualdad byte a byte sobre un
reporte.** Sería un test que pasa o falla según el reloj, que es el error que el Módulo 6 descubrió
en su recorrido manual. Lo que sí se verifica es que el encabezado traiga las cinco piezas de FR-006.

---

## §8 · La entrega del archivo al navegador — y la assumption que esto revierte

La spec asume: *"El PDF se abre a la vista y el Excel se descarga"*. Vale tal como está escrita para
el caso feliz, pero choca con FR-004, FR-016 y FR-017, que exigen que **la pantalla** anuncie el
progreso, muestre el mensaje del `409` y permita reintentar sin perder filtros ni página. Un `<a
href target="_blank">` directo al endpoint —el patrón con el que el Módulo 6 abre el documento de una
factura— manda la respuesta a **otra pestaña**: un `409` ahí se ve como JSON crudo y la pantalla de
origen no se entera de nada.

**Decisión**: la pantalla pide el archivo con `fetch` y lo recibe como `Blob` —el patrón que el
Módulo 6 ya estrenó en `obtenerPdf`—, y después:

- **Excel** → `<a download>` sobre una URL de objeto. Una descarga programática **no la bloquea
  ningún navegador**.
- **PDF** → `window.open(url, '_blank', 'noopener')`. **Si devuelve `null` —bloqueo de emergentes—,
  cae al mismo camino de descarga** y la pantalla lo anuncia ("Se descargó el reporte").

**Por qué la caída hacia la descarga no es opcional**: `window.open` desde la continuación de una
promesa sólo está permitido mientras dure la *activación transitoria* del clic, que son ~5 segundos.
SC-003 admite hasta 15 para un reporte de 1.000 filas. Sin la caída, el reporte grande —justo el que
más importa— desaparecería sin explicación en la mitad de los casos.

**Lo que esto revierte de la assumption**: el PDF se abre a la vista **cuando el navegador lo
permite**, y se descarga cuando no. Es la única parte de esa assumption que no se sostiene, y la
spec declara sus assumptions revertibles. El endpoint, por su lado, **sí** cumple la intención
entera: sirve el PDF con `Content-Disposition: inline` y el Excel con `attachment`, como manda
[003] —quién decide cómo se sirve el contenido es el backend, no el enlace—.

**El nombre del archivo lo arma el backend una sola vez** (FR-012, convención [009]) y viaja en el
`Content-Disposition`; el frontend lo lee de esa cabecera —legible porque es el mismo origen— y no lo
vuelve a componer en TypeScript. Formato: `viajes-2026-09-20-1432.pdf`, con fecha **y hora** de
Argentina, para que dos reportes del mismo día en la misma carpeta se distingan. Sólo ASCII, así el
`filename` simple alcanza y el frontend lo extrae con una expresión regular de una línea.

**El helper vive en `compartido`, no en el módulo.** `obtenerPdf` de Facturación es la primera copia;
los reportes son la segunda y la tercera necesidad a la vez (cinco pantallas, dos formatos). Es
exactamente el umbral de [009]: se lleva a `compartido/archivos.ts` como `obtenerArchivo`, se
reemplaza la copia de Facturación en esta misma feature, y la prueba de que es un refactor es que la
suite de Facturación pasa **sin modificarse**.

---

## §9 · Los filtros "en palabras" los escribe el backend

FR-006 pide que el encabezado diga los filtros aplicados **con sus valores en palabras**, o que no
hay ninguno. La pantalla tiene identificadores (`clienteId=7`), no nombres.

**Decisión**: cada fuente resuelve los nombres contra los repositorios que ya existen y arma la línea
completa; el frontend **no manda ningún texto**. Es la convención [009] sobre el formato visible que
también aparece en mensajes del servidor: escrito en C# y en TypeScript son dos formatos que se
pueden separar.

Forma de la línea, una sola para los cinco reportes:

- Con filtros: `Cliente: Aceitera del Sur · Estado: En curso · Desde: 01/09/2026 · Hasta: 20/09/2026`
- Sin filtros: `Sin filtros aplicados`

Los tres paneles de vencimientos **no tienen filtros hoy y esta feature no se los agrega** (spec
§Assumptions): su encabezado dice siempre `Sin filtros aplicados`.

Los estados van con **la misma palabra que la pantalla** (FR-010), tomada de los
`NombresDeEstado*` que ya existen por módulo — no se escribe ningún literal nuevo.

---

## §10 · Una sola disposición de página para los cinco PDF

**Decisión**: A4 **apaisado** en los cinco, márgenes de 1 cm, encabezado repetido en cada página,
número de página al pie, y la fila de totales al final de la tabla.

**Por qué apaisado en los cinco y no según el reporte**: el de viajes tiene **diez** columnas y en
vertical no entra. Elegir la orientación por reporte es una condición más y cinco resultados
distintos para revisar; una sola decisión da cinco documentos que se ven igual, que es lo que un
conjunto de reportes tiene que parecer. Los de cinco columnas quedan con aire de sobra, que no es un
problema.

---

## §11 · La acción es **secundaria**, y eso contradice una línea del sistema de diseño

FR-005 fija que *Generar reporte* es **secundaria** en las cinco pantallas y que en las de sólo
lectura no convierte a la pantalla en una con acción primaria.

`.claude/skills/gt-ui/SKILL.md` dice: *"En una pantalla de solo lectura la acción primaria es la que
quede accionable (exportar, imprimir)"*. Aplicada al pie de la letra, esta feature tendría que
promover *Generar reporte* a primaria en los tres paneles de vencimientos y en Movimientos de caja, y
dejarla secundaria en Viajes —donde compite con *Nuevo viaje*—: **la misma acción con dos pesos según
la pantalla**, que es lo contrario de lo que un sistema de diseño busca.

**Decisión: manda FR-005.** Y se sostiene contra la constitución, que es lo que de verdad gobierna:
el Principio VI exige *"a lo sumo una acción primaria"* y *"si la pantalla es de solo lectura y no hay
ninguna acción accionable, NO se inventa un primario"*. Una acción secundaria no viola ninguna de las
dos. La línea de la skill es una guía dentro de la skill, no la regla de la constitución.

**Y por eso la skill se corrige.** La convención [008] es explícita: *cuando una skill es la fuente
de un sistema de diseño, también es su destino* — una corrección que vive sólo en el código deja a la
skill diciendo una cosa y a la aplicación haciendo otra, y la feature siguiente vuelve a introducir el
valor viejo. El cambio es de una línea, en `SKILL.md` y en la fila equivalente de la tabla de
jerarquía: *exportar no se vuelve primaria por descarte; es primaria sólo cuando es el propósito de la
pantalla*.

**Razón de fondo**: el propósito de un panel de vencimientos es **resolver** lo que está por vencer,
no exportarlo. Pintar el exportador como lo más importante de la pantalla dice lo contrario.

---

## §12 · Elegir el formato: un diálogo, no un menú nuevo

FR-002 pide exactamente dos opciones —PDF y luego Excel—, y que cerrar sin elegir devuelva la
pantalla como estaba, con sus filtros y su página. SC-001 pide **dos clics**: la acción y el formato.

**Decisión**: el `Dialogo` que ya existe en `compartido/ui`, con dos botones que **ejecutan** —*PDF* y
*Excel*, en ese orden— más *Cancelar*.

**Por qué el diálogo y no un menú desplegable**: `MenuDeFila` es el `···` de una fila de tabla, con
su glifo fijo como disparador; usarlo acá obligaría a generalizarlo. El `Dialogo` ya trae lo que
FR-002 pide —foco retenido, `Escape`, foco devuelto al disparador, nada modificado al cerrar— y **no
hay que escribir ninguna primitiva nueva**. Los dos clics de SC-001 se cumplen porque el botón del
formato **es** la acción: no hay un *Generar* aparte.

**Por qué no un par de radios más un botón *Generar***: serían tres clics y SC-001 pide dos.

**La jerarquía adentro del diálogo**: *PDF* primario, *Excel* y *Cancelar* secundarios. Un primario
por superficie, y se lo lleva el primero del orden que FR-002 fija.

---

## §13 · Qué se prueba y qué no se puede probar a mano

| Qué | Dónde | Por qué ahí |
|---|---|---|
| Totales del reporte contra la suma de sus filas (SC-004) | `GT.UnitTests` | Es aritmética sobre el modelo, sin base |
| El encabezado trae las cinco piezas de FR-006 | `GT.UnitTests` | Texto armado, reloj inyectado |
| El nombre del archivo (FR-012) | `GT.UnitTests` | Función pura de reporte + formato + instante |
| La línea de filtros en palabras, con y sin filtros (FR-006, §9) | `GT.UnitTests` | Proyección pura |
| El Excel abre y sus celdas de importe y fecha **son** número y fecha (FR-011, SC-007) | `GT.IntegrationTests` | Convención [006]: la dependencia se ejercita de verdad y **resolviendo el servicio del contenedor** |
| El PDF se genera de verdad (falta de `libfontconfig1`) | `GT.IntegrationTests` | Mismo motivo; es el caso que el Módulo 6 ya vive |
| Paridad fila a fila contra el listado (SC-002) | `GT.IntegrationTests` | La fuente y la consulta del listado sobre los mismos datos sembrados: misma cantidad y mismo orden |
| El `409` del tope, con sus dos números (FR-016, SC-008) | `GT.IntegrationTests` | Con el tope bajado por configuración de prueba, no sembrando 5.001 filas |
| Los dos permisos de cada uno de los cinco endpoints (FR-013, FR-015) | `GT.IntegrationTests` | Matriz de 4 roles × 5 reportes |
| El diálogo, el anuncio, el deshabilitado, el `409` y el `403` en pantalla | Vitest + RTL | Consultas por rol, etiqueta y texto (convención [007]) |
| La caída de `window.open` a descarga (§8) | Vitest | `window.open` falseado devolviendo `null` |

**Lo que no se puede verificar a mano**: el bloqueo de emergentes de §8 (depende de la configuración
del navegador de quien prueba) y el `409` del tope con datos reales (harían falta 5.001 filas). Los
dos quedan cubiertos por los tests de arriba, y el `quickstart.md` lo declara en vez de pedir un paso
imposible.

---

## §14 · Las palabras de los estados no existen en el backend

**Decisión tomada después del diseño**, a partir del análisis de `/speckit-analyze`. No estaba en la
Fase 0 porque el diseño dio por sentado algo que el código desmiente.

FR-010 exige que los estados vayan **con la palabra de la pantalla y nunca con un código interno**.
El diseño original resolvía cada columna Estado diciendo "la palabra de `NombresDeEstado*`", dando por
hecho que esas clases del backend traducen a español. **No lo hacen**: devuelven el código del JSON.

```csharp
// lo que HAY hoy, y es correcto para lo que existe (convención [003])
public static string DelDocumento(DocumentacionEstado estado) => EnCamelCase(estado.ToString());
// ProximaAVencer → "proximaAvencer"
```

Las palabras visibles viven **sólo en TypeScript**: `NOMBRES_DE_ESTADO` en `servicioViajes.ts`,
`TEXTO_ESTADO_DOCUMENTO` en los `estados.ts` de Choferes y de Flota, el mapa de tipos de movimiento en
Caja, y `situacion(dias)` en `facturacion/servicios/api.ts`. Un reporte que se arma en C# no puede
llamarlas.

**Por qué importa tanto**: seguido al pie de la letra, el diseño original entregaba los cinco reportes
con `enCurso`, `proximaAvencer` e `ingreso` en la columna Estado. **Compila, pasa todos los tests que
las tareas describían, y sólo se descubre abriendo un archivo** — el mismo perfil de error que
`HasSentinel` en [009] y que las 23 clases sin definir de [007].

**Decisión**: cada módulo gana un `EnPantalla(...)` en la clase `NombresDeEstado*` que ya tiene, y
Facturación un `SituacionDeVencimiento.Para(int dias)` nuevo. Las palabras exactas y la tabla completa
están en data-model §2.5. Los cinco cambios son **aditivos**: ninguna firma, ningún contrato JSON,
ninguna pantalla y ninguna suite existente se tocan.

**Alternativa descartada**: que el backend sea el dueño único de cada palabra y las pantallas la
reciban armada en el JSON, que es lo que [009] pide para un formato visible que también aparece en
mensajes del servidor. Cambia el contrato de los listados de **cinco módulos** y toca unas diez
pantallas: un refactor mucho más grande que esta feature, contra el Principio I y contra la lista
cerrada de cambios a módulos anteriores del Principio III.

**Así que la copia se acepta y se declara.** Lo que la cierra es un test unitario por módulo que fija
las palabras exactas en C#, más un comentario en cada mapa de TypeScript apuntando a él. Es lo más
lejos que llega entre dos lenguajes el precedente de [003] —*cuando una regla se ejecuta en dos lados,
va un test que compara las dos sobre el mismo dato*—: no se puede comparar automáticamente C# contra
TypeScript, pero sí dejar las dos listas fijadas y señalándose.

**Lo que esto enseña para la próxima feature**: antes de escribir "usa la función que la pantalla ya
usa", hay que verificar **de qué lado de la red vive esa función**. Candidata a `AGENTS.md` como
`[012]`.
