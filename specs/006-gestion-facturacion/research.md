# Research: Gestión de facturación (Módulo 6)

Decisiones técnicas del módulo, con la alternativa que se descartó y por qué. Cada sección responde a
una pregunta que el diseño tenía abierta después de leer la spec.

La spec llegó con **17 preguntas ya resueltas** en su sesión de clarificación (§Clarifications), así
que acá no queda ningún `NEEDS CLARIFICATION` de producto. Lo que sigue son decisiones **técnicas**:
cómo se construye lo que la spec ya decidió.

---

## §1 — Con qué se genera el PDF

**Decisión**: **QuestPDF** (paquete `QuestPDF`, versión `2026.7.3` al momento de escribir esto), como
única dependencia nueva del backend, consumida detrás de una interfaz de la capa de aplicación
(`IArmadorDocumentoFactura`) e implementada en `GT.Infrastructure`.

**Por qué**:

- Es **C# puro con motor nativo propio**, sin binario externo que instalar ni proceso que lanzar. Se
  restaura con `dotnet restore` y funciona igual en Windows para desarrollo y en el contenedor Linux.
- Renderiza a `byte[]` / `Stream` en memoria. Eso es lo que hace posible FR-033 al pie de la letra:
  la **vista previa produce el documento y no lo guarda**, y la emisión llama exactamente al mismo
  armador y sí guarda el resultado (§2).
- Su modelo de composición cubre lo que pide FR-031 sin pelearse: bandas a todo el ancho, columnas,
  y sobre todo una **tabla que se corta sola entre páginas** repitiendo el encabezado, que es lo que
  exige "una factura agrupa muchos viajes… el documento sigue de largo las páginas que haga falta"
  (FR-031e).
- **Licencia**: gratuita para organizaciones con menos de USD 1M de facturación anual. G&T Logística
  entra holgadamente. Queda anotado como condición de uso, no como supuesto oculto.

**Lo que hay que tocar además, y no es opcional**: el motor de texto de QuestPDF necesita
`libfontconfig1` y `libfreetype6` en la imagen de ejecución, y
`mcr.microsoft.com/dotnet/aspnet:10.0` no los trae. **`backend/Dockerfile` se modifica** con un
`apt-get install` en la etapa final. Sin eso el backend compila, arranca y falla recién al emitir la
primera factura — el peor momento posible para enterarse. Va acompañado de un test de integración
que genera un PDF de verdad, para que la falta se note en CI y no en producción.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| **iText 7** | Licencia AGPL: obliga a publicar el código de todo el sistema o a comprar licencia comercial. Es la restricción más cara de las tres opciones |
| **wkhtmltopdf** (DinkToPdf) o **Chromium headless** (Puppeteer) | Maquetar el comprobante en HTML y fotografiarlo. Descartada por dos motivos: mete un binario nativo de decenas o cientos de MB en la imagen, y **abre la puerta a la segunda maqueta que FR-033 prohíbe** — con HTML disponible, la tentación de dibujar la vista previa "directo en React" deja de tener freno técnico |
| **PdfSharp / MigraDoc** | Alcanza para el documento, pero la tabla de detalle que fluye entre páginas hay que paginarla a mano. Es escribir de nuevo lo que QuestPDF ya resuelve |
| **Guardar HTML en vez de PDF** | El comprobante se le manda al cliente. Un HTML no es un archivo que alguien adjunte a un correo esperando que se vea igual del otro lado |

---

## §2 — Cómo se garantiza que la vista previa y el documento no se separen

**Decisión**: **un único armador en el servidor, invocado por los dos caminos, sobre el mismo tipo de
entrada**. La vista previa arma en memoria la **entidad `FacturaCliente` que todavía no existe**, la
mapea a los datos del documento y renderiza; la emisión arma la misma entidad, la persiste, y mapea y
renderiza con **la misma función**.

```
  POST /api/facturas/vista-previa ─┐
                                   ├─▶ FacturaCliente (en memoria) ─▶ DatosDelDocumento.Desde(...) ─▶ IArmadorDocumentoFactura ─▶ PDF
  POST /api/facturas ──────────────┘                                                                                              │
                                                                                    la vista previa lo devuelve ◀────────────────┤
                                                                                    la emisión lo guarda ◀──────────────────────┘
```

**Por qué así y no sólo "la misma clase armadora"**: si la vista previa le pasara al armador un DTO
construido a partir del formulario y la emisión le pasara otro construido a partir de la fila
guardada, habría **dos traducciones** al mismo destino y podrían diferir sin que nadie lo note —que es
exactamente el problema que FR-033 quiere evitar, un escalón más abajo. Con la entidad como entrada
única, el mapeo es uno solo y SC-007b se verifica con un test que compara byte a byte los dos PDF de
la misma factura.

**Consecuencia para el frontend**: la vista previa no se dibuja en React. Se pide por `POST`
—porque lleva la selección de viajes en el cuerpo—, se recibe `application/pdf`, y se muestra en un
`<iframe>` sobre una URL de `Blob`. Es un patrón nuevo en este frontend y está anotado como tal en
`contracts/README.md`.

**Alternativa descartada**: maquetar la vista previa en HTML/CSS "que se parezca". Es lo que la spec
descarta explícitamente en su clarificación de FR-033, y el motivo técnico lo confirma: dos maquetas
paralelas se separan sin que nadie lo note, y entonces revisar la vista previa deja de servir para
algo.

---

## §3 — Dónde vive el estado `vencida`

**Decisión**: **derivado al leer, nunca en columna**. La factura guarda uno de tres valores
—`pendiente`, `pagada`, `anulada`— y `vencida` se calcula comparando el vencimiento de pago con el
día en curso. La regla se escribe **dos veces a propósito**: una vez en el dominio como función pura
que recibe la fecha por parámetro, y una vez como predicado dentro de la consulta SQL.

```csharp
// Dominio: recibe la fecha, no lee el reloj (convención [005]).
public static EstadoFacturaVisible Derivar(EstadoFactura guardado, DateOnly vencimientoPago, DateOnly hoy)
```

**Por qué dos veces**: el listado tiene que **filtrar** por el estado derivado (FR-058a) y filtrar en
memoria después de paginar devolvería páginas incompletas. El predicado va escrito en el árbol de la
consulta, y la duplicación se cubre con **un test que evalúa las dos sobre el mismo dato** y compara,
que es la convención [003] del proyecto y ya existe el precedente en el Módulo 3.

Es la cuarta vez que el sistema resuelve así un estado derivable: vencimientos de documentación
(Módulos 3 y 4), `demorado` (Módulo 5) y ahora `vencida`. La decisión no está en discusión; lo que
esta feature agrega es que **el derivado además se filtra**, y que sus cuatro valores son
**excluyentes**: una factura impaga y pasada de fecha sale bajo `vencida` y **no** bajo `pendiente`
(FR-058a, US3 esc. 11).

**Alternativa descartada**: una columna `Vencida` mantenida por un proceso nocturno. Es lo que el
enunciado sugería al decir que la factura pasa "automáticamente" a vencida. Se descarta por lo mismo
de siempre: hay que mantener el proceso al día, y una columna que copia un hecho puede discrepar del
hecho. Además obligaría a que el sistema tenga un planificador, que hoy no tiene.

---

## §4 — Cómo se garantiza que un viaje no entre en dos facturas

**Decisión**: **la columna `Viajes.FacturaId`** —nula mientras el viaje no esté facturado— más un
**`UPDATE` condicional cuyo número de filas afectadas se verifica**, dentro de la transacción que crea
la factura:

```csharp
var afectados = await contexto.Viajes
    .Where(v => seleccionados.Contains(v.Id) && v.Estado == EstadoViaje.Rendido && v.FacturaId == null)
    .ExecuteUpdateAsync(...);

if (afectados != seleccionados.Count) { /* rollback + rechazo nombrando el viaje y su comprobante */ }
```

**Por qué esto y no un índice único filtrado**, que es lo que fijó la convención [005]: acá la
exclusividad **ya es estructural**. Una columna escalar no puede apuntar a dos facturas: no hay nada
que un índice pudiera agregar. Lo que queda por cerrar no es la unicidad sino **la carrera** (SC-005),
y eso lo cierra el `UPDATE` condicional: bajo el nivel de aislamiento por defecto de SQL Server, la
segunda transacción se bloquea sobre la fila que la primera está modificando, y al desbloquearse
reevalúa el `WHERE` contra el dato ya comprometido —`FacturaId` no nulo—, afecta cero filas y se
rechaza. **La garantía sigue estando en la base y no en la pantalla**, que es lo que [005] realmente
pide; sólo cambia el mecanismo, porque el dato tiene otra forma.

La consulta previa —la lista de facturables ya excluye los viajes con factura— sigue dando el mensaje
bueno en el caso normal, igual que en el Módulo 5.

**Alternativa descartada**: una tabla intermedia `FacturaViajes` con índice único filtrado sobre
`ViajeId`. Daría la misma garantía y agregaría una tabla que puede desincronizarse del estado del
viaje. Es literalmente la "tabla de ocupaciones aparte" que [005] descartó.

**Lo que sí lleva índice único filtrado en este módulo** son las dos exclusividades que **no** son
estructurales:

- `IX_Facturas_Numero … WHERE [Estado] <> 2` — el número de comprobante, único entre las no anuladas
  (FR-027).
- `IX_Facturas_FacturaReemplazada … WHERE [FacturaReemplazadaId] IS NOT NULL` — una factura anulada la
  reemplaza a lo sumo una Refacturación (FR-049a).

Los dos llevan el **valor numérico del enum escrito a mano** en el filtro, con la misma trampa que el
Módulo 5 documentó: reordenar `EstadoFactura` no falla al compilar y deja el índice protegiendo el
estado equivocado. Va el mismo test de integración que inserta una fila en cada estado y verifica
dónde cada índice acepta y dónde rechaza.

---

## §5 — Qué se congela en la factura y qué no

**Decisión**: se copian a la factura, al emitirla, **los diez datos de texto del emisor** (FR-034) y
**los tres del cliente** (FR-034a). **El logo no se copia**: se lee siempre de la configuración
vigente.

**Por qué el logo es la excepción**: congelar los datos de texto alcanza para lo que la regla protege
—que una factura emitida no cambie de domicilio, de CUIT ni de CBU—, y guardar una copia del archivo
por cada factura agregaría un archivo por comprobante sin ningún caso de uso que lo pida. Además el
documento **ya está generado** con el logo del día de la emisión: lo que se lee de la configuración es
sólo para la vista previa de facturas nuevas y para una eventual regeneración.

**Consecuencia que hay que aceptar y está declarada**: si se cambia el logo y después se corrige el
CAE de una factura vieja, el documento regenerado sale con el logo nuevo. Es el único dato del
comprobante que puede cambiar, y es una imagen, no un dato fiscal.

**La alícuota de IVA no se congela.** Se deriva del tipo de comprobante, que sí está congelado, con
`AlicuotasIva.De(tipo)`. Se evaluó guardarla en columna —el mismo argumento que FR-034: que el
documento diga siempre lo mismo— y **se descartó por el Principio III**: ninguna FR pide esa columna,
las alícuotas están fijas en el código y no las configura ninguna pantalla (*Assumptions*), y el tipo
de comprobante es inmutable después de emitir (FR-036). El único escenario donde la derivación
discreparía es un cambio de constante en el código, que es un cambio de versión del sistema y no una
operación del negocio. Queda anotado como candidato si alguna vez las alícuotas se vuelven
configurables.

---

## §6 — Cómo se guarda, se sirve y se regenera el documento

**Decisión**: **el mismo mecanismo de los Módulos 3 y 4, sin tocarle una línea**. `IAlmacenDeArchivos`
guarda en el volumen de `GT_ARCHIVOS_RUTA` con nombre generado por el sistema, y el documento se sirve
por endpoint autorizado con `Content-Disposition: inline` + `nosniff`, usando `ResultadoArchivo.EnLinea`
que ya existe (convención [003]).

**El orden de las operaciones sigue la convención [003]**, con una vuelta de tuerca:

1. Se **arma el PDF y se escribe en disco antes de abrir la transacción**. No necesita el `Id` de la
   factura —el número de comprobante lo tipea el usuario—, así que nada obliga a escribirlo después.
2. Se inserta la factura con la ruta ya puesta, se marcan los viajes, se confirma.
3. Si algo falla, se hace `rollback` **y se borra el archivo**.

Deja como único estado roto posible un archivo huérfano en el volumen, nunca una fila que dice tener
documento sin tenerlo. Es exactamente el criterio del Módulo 3.

**La regeneración (FR-031b) escribe un archivo nuevo y borra el viejo después de confirmar**, nunca
sobreescribe el anterior en el lugar. Sobreescribir dejaría, ante una falla a mitad de escritura, un
PDF corrupto donde antes había uno bueno. Rige para las tres operaciones que regeneran: corregir
(FR-035), anular (FR-031b, FR-031d) y ninguna más.

**La anulación regenera dentro de la misma transacción que cambia el estado** (FR-031b): si el
documento no se puede armar, la anulación no queda aplicada a medias y los viajes no vuelven a
`rendido`.

**Sobre el logo**: se guarda con el mismo almacén y se valida con el mismo `ValidadorArchivo` por
**firma de archivo**, pero el caso de uso **rechaza el PDF**: FR-003 admite sólo JPG y PNG. El
validador devuelve el tipo deducido y quien decide qué tipos acepta es el caso de uso, que es como ya
estaba diseñado.

---

## §7 — Cuántos permisos y cómo se reparten

**Decisión**: **tres permisos nuevos**, evaluados por permiso y nunca por rol (FR-066):

| Código | Qué habilita | Roles |
|---|---|---|
| `facturacion.gestionar` | configurar la empresa emisora, emitir, corregir, registrar el cobro | Administración de la empresa, Administrador del sistema |
| `facturacion.consultar` | listado, ficha, documento, panel de vencimientos, totales | los tres anteriores **más** Gerencia |
| `facturacion.anular` | anular una factura | Administrador del sistema |

Es el segundo módulo que distingue niveles adentro (el primero fue el 4) y el **primero con tres**. El
precedente [004] ya fijó que un módulo con dos niveles lleva dos permisos y no un permiso más un
chequeo de rol; acá se aplica igual con el tercero.

**El menú se resuelve por `facturacion.consultar`** para las tres pantallas de lectura, y por
`facturacion.gestionar` para la de configuración de la empresa emisora —que no es de lectura para
nadie—. Cuatro entradas, ninguna línea de código nuevo en el frontend: `CatalogoOpcionesMenu` ya
traduce permiso → opción.

**Alternativa descartada**: un permiso único `facturacion.gestionar` con un chequeo de rol adentro del
endpoint de anulación. Descartada por la convención del Módulo 1: la autorización se evalúa por
permiso.

---

## §8 — Los seis cambios al Módulo 5, y cómo no se convierten en siete

**Decisión**: FR-056 acota los cambios a seis requisitos y el diseño los enumera uno por uno para que
la lista sea verificable en la revisión:

1. **`EstadoViaje.Facturado = 4`**, agregado **al final** del enum. Los tres índices filtrados de
   `Viajes` llevan los valores `1` y `3` escritos a mano en su `WHERE`; agregar al final no toca
   ninguno. El de remito —`WHERE [NumeroRemito] IS NOT NULL AND [Estado] <> 3`— además pasa a cubrir
   los facturados, que es lo correcto: un viaje facturado no libera su remito.
2. **`TransicionesDeViaje`**: dos pares nuevos, `rendido → facturado` y `facturado → rendido`.
   **Trampa a documentar en el código**: `EsTerminal(Rendido)` ya devuelve `true` y los casos de uso
   del Módulo 5 llaman a `EstadoTerminal.Rechazo` **antes** de mirar la transición. Que el par exista
   en el mapa **no** abre ningún camino HTTP nuevo, porque los tres endpoints de ciclo de vida del
   Módulo 5 tienen el estado destino fijo en el código. El cambio de estado por facturación lo hace el
   caso de uso de este módulo y nadie más.
3. **`EsTerminal` incorpora `Facturado`** (FR-052): un viaje facturado es inmutable para todos los
   roles, con el mismo alcance que ya regía para `rendido`. Los cinco caminos de escritura del Módulo
   5 quedan cerrados sin tocar ninguno de los cinco, porque los cinco ya consultan `EsTerminal`.
4. **`Viajes.FacturaId`** anulable, con clave foránea en `Restrict` (§4).
5. **`RendirViaje` exige el número de remito** (FR-055a). Es el único cambio de comportamiento sobre
   una operación existente del Módulo 5.
6. **El listado y la ficha de viajes muestran el número y la fecha de la factura** del viaje facturado
   (FR-055).

**Lo que este módulo escribe en el historial del viaje**: una línea de `CambioDeEstadoViaje` por cada
viaje, al facturar y al anular. No lo pide ninguna FR del Módulo 6, pero **FR-035 del Módulo 5 —ya
implementada y que no se modifica— exige que todo cambio de estado quede registrado**, y FR-051 declara
que estos dos son cambios de estado. Omitirlas dejaría la ficha del viaje mostrando `facturado` sin
una línea que lo explique. No es alcance nuevo: es cumplir una regla vigente sobre un estado nuevo.

**La limitación conocida de FR-019a queda como está**: un viaje que ya estaba `rendido` sin remito no
se puede facturar y **no se abre ningún camino de corrección**. Se ofrece en la lista de facturables
señalado con la palabra que lo explica, y la emisión lo rechaza nombrándolo. Abrir la edición de un
viaje rendido sería revertir la decisión que el Módulo 5 tomó a propósito.

---

## §9 — Cómo se calculan y se redondean los importes

**Decisión**: `decimal` de punta a punta, columnas `decimal(18,2)`, y una única función de dominio:

```csharp
public static ImportesDeFactura Calcular(IEnumerable<decimal> importesDeViajes, TipoComprobante tipo)
```

- **Neto** = suma exacta de los importes de los viajes (FR-022). Los importes de los viajes ya vienen
  con dos decimales de la tabla `Viajes`, así que la suma es exacta y no hay nada que redondear.
- **IVA** = `Math.Round(neto * alícuota, 2, MidpointRounding.AwayFromZero)` — redondeo comercial, la
  mitad para arriba (*Assumptions*).
- **Total** = neto + IVA.

Se verifica con el ejemplo de la propia spec, que es el mejor test que hay: `30.000,00 + 30.000,00 +
22.644,63 = 82.644,63`; con 21% el IVA da `17.355,3723` → `17.355,37` y el total `100.000,00`
(US2 esc. 8). Y con `Factura C`: IVA `0,00`, total igual al neto (FR-023).

**Los subtotales por fila del documento son informativos** (FR-031f): se calculan sobre cada importe
por separado y su suma puede diferir del total en unos centavos. El pie manda. Va un test con un caso
armado para que difieran, porque si nunca difieren en los tests nadie sabe qué pasa cuando difieren.

**En el frontend, los tres importes son de sólo lectura y llegan calculados del servidor.** El alta
podría calcularlos en pantalla para mostrarlos en vivo mientras se marcan viajes (FR-020, FR-025) —y
lo hace, sumando los importes ya mostrados—, pero **el valor que se guarda es siempre el que calcula
el backend a partir de los viajes que encontró en la base**, nunca uno enviado por el cliente. FR-024
lo pide explícitamente: "ni desde la pantalla ni invocando la acción directamente". Los campos
`neto`, `iva` y `total` **no existen en el cuerpo del `POST`**.

---

## §10 — Cómo se ordena y se pagina el listado

**Decisión**: la convención del Módulo 3 sin cambios: página de 20, formato
`{ items, total, pagina, tamanioPagina }`, filtros aplicados **antes** de paginar.

**El orden es `fecha de facturación DESC, número de comprobante DESC`** (FR-059). Es un orden **total**
—no puede haber dos facturas con el mismo número entre las no anuladas, y el índice único lo
garantiza— y no termina en `Id`, igual que el listado del Módulo 5 terminaba en `Numero`. La
convención [003] pide un orden total, no uno que termine en `Id`.

**Salvedad chica y real**: dos facturas **anuladas** sí pueden compartir número, porque el índice las
excluye. Dos filas anuladas del mismo día y del mismo número podrían intercambiarse entre páginas.
Es un caso que exige emitir, anular y volver a emitir con el mismo número, y el resultado —dos filas
idénticas en la columna que las ordena— no confunde a nadie. Se deja anotado y no se agrega un
desempate por `Id`, que sería ruido en el 100% de los casos restantes.

---

## §11 — Dónde se rechaza cada cosa: la tabla de códigos HTTP

**Decisión**: la regla [005] tal cual, sin excepciones nuevas:

| Situación | Código | Por qué |
|---|---|---|
| Campo faltante o mal formado (CUIT, CAE, número, fechas) | `400` | está en lo que se tipeó |
| Número de comprobante duplicado | `400` | es un duplicado, como el remito del Módulo 5 |
| Cliente sin domicilio (FR-011a) | `400` | falta un dato, y el mensaje dice cuál y dónde cargarlo |
| Empresa emisora sin configurar (FR-006) | `400` | ídem, con los cuatro campos nombrados |
| Viaje sin remito (FR-019a) | `400` | falta un dato del viaje elegido |
| Viaje ya facturado por otro (FR-053) | `409` | el estado de algo compartido cambió |
| Anulada ya reemplazada (FR-049a) | `409` | ídem |
| Transición no permitida, factura anulada inmutable | `409` | el estado no lo admite |
| Emitir con viaje en cero o con fecha anterior (FR-032) | `409` | confirmación pendiente |
| Anular una factura `pagada` (FR-043a) | `409` | el estado no lo admite |

Las dos confirmaciones de FR-032 viven **en el backend**, no en la pantalla, por el criterio [005]:
**la emisión no se deshace**. El primer intento responde `409` sin crear nada; el segundo lleva
`confirmado: true` en el cuerpo. La anulación, en cambio, la confirma la pantalla —como todas las
bajas del sistema— y además exige motivo escrito, que es un dato y no una confirmación.

---

## §12 — Cómo entra `EmpresaEmisora` siendo una única fila

**Decisión**: tabla propia con **`CHECK ([Id] = 1)`**. La fila **no existe hasta el primer guardado**:
el `GET` devuelve `{ configurada: false }` con la lista de campos faltantes, y el `PUT` crea la fila la
primera vez y la actualiza siempre después. No hay `POST` ni `DELETE`.

**Por qué el `CHECK` y no sólo la disciplina del código**: FR-001 dice "única para todo el sistema: se
edita, nunca se crea una segunda ni se borra". Una garantía escrita en la base cuesta una línea de
configuración de EF y no depende de que nadie escriba nunca un `Add` de más.

**Alternativa descartada**: sembrar una fila vacía en `SembradorInicial`. Obligaría a que las cuatro
columnas obligatorias fueran anulables en la base para poder sembrar una fila sin datos, y entonces la
base dejaría de garantizar lo que FR-002 exige. Con la fila ausente, la ausencia **es** el estado "sin
configurar", que es lo que US1 esc. 1 describe.

---

## §13 — Qué se reutiliza sin tocar

Vale enumerarlo porque es la mayor parte del módulo y es lo que sostiene el Principio I:

| Pieza | De dónde viene | Se usa para |
|---|---|---|
| `ValidadorCuit` + `NormalizadorDocumentoNumerico` | Módulo 3 | el CUIT de la empresa emisora (FR-002) |
| `IAlmacenDeArchivos` + `AlmacenDeArchivos` | Módulo 3 | el logo y el documento de la factura |
| `ValidadorArchivo` (firma, no extensión) | Módulo 3 | el logo, restringido a JPG y PNG |
| `ResultadoArchivo.EnLinea` | Módulo 3/4 | servir el documento y el logo |
| `PaginaDe<T>` | Módulo 3 | el listado de facturas |
| `ErrorResponse`, políticas por permiso, `PermisoHandler` | Módulos 1 y 2 | los tres permisos nuevos |
| `CatalogoOpcionesMenu` | Módulo 1 | las cuatro entradas de menú |
| `TimeProvider` | Módulo 5 | el instante del historial y el día en curso de `vencida` |
| `ConversorInstanteUtc` | Módulo 2 | los instantes salen con `Z` sin escribir nada |
| `compartido/moneda.ts` | Módulo 5 | los importes en pantalla |
| `compartido/fechas.ts` | Módulo 3 | las fechas en pantalla |
| `Cliente` y `Viaje` | Módulo 5 | se consumen; se modifica sólo lo que FR-051 a FR-055a fijan |

**Dependencias nuevas: una** (QuestPDF). **Variables de entorno nuevas: ninguna** — el documento va al
mismo volumen que los escaneos. **Cambios de infraestructura: uno**, el `apt-get` del Dockerfile
(§1).

---

## §14 — Qué no se puede verificar a mano, y qué se hace con eso

Tres cosas de este módulo no las puede comprobar una persona operando la aplicación, y el Principio IV
obliga a declararlo en vez de fingir que sí:

1. **La carrera de SC-005**: dos administrativos confirmando en el mismo milisegundo facturas que
   comparten un viaje. Va a un test de integración que lanza las dos operaciones en paralelo contra el
   SQL Server del compose.
2. **Que la vista previa y el documento guardado coincidan byte a byte** (SC-007b). A ojo se comparan
   los bloques; la igualdad exacta la verifica un test.
3. **Que el filtro por estado en SQL y la derivación en C# den lo mismo** (FR-058a). Test que evalúa
   las dos sobre el mismo conjunto.

Las tres quedan anotadas en `quickstart.md` como "esto lo cubre un test y por qué", en vez de pedirle
a quien valida algo que no puede hacer. Es el mismo criterio con el que el Módulo 5 trató la demora de
cinco días.

---

## §15 — Trampas identificadas, para que la implementación no las descubra sola

Cinco cosas que este diseño ya sabe que van a morder, listadas acá para que `tasks.md` las tenga:

1. **Rutas literales junto a `{id}`** (convención [005]): `/api/facturas/vencimientos`,
   `/api/facturas/totales`, `/api/facturas/facturables`, `/api/facturas/vista-previa` y
   `/api/facturas/anuladas-sin-reemplazo` conviven con `/api/facturas/{id}`. **La de identificador
   lleva `{id:int}`**. Sin la restricción, las cinco literales quedan inalcanzables y no falla ni al
   compilar ni al arrancar: falla al pedirlas.
2. **`EstadoFactura` numerado y con dos índices filtrados apoyados en sus valores**. Mismo comentario
   en el enum y mismo test que el Módulo 5.
3. **`libfontconfig1` en el Dockerfile** (§1). Falla recién al emitir la primera factura.
4. **La expresión del `vencida` derivado va escrita en el árbol de la consulta**, no extraída a un
   método propio: extraerla rompe la traducción de EF Core y la consulta pasa a evaluarse en memoria
   (convención [003]).
5. **El `PUT` de corrección no puede tocar el estado** (FR-044): el cobro y la anulación son recursos
   propios. Y la corrección de una factura `pagada` **no** puede pisarle la fecha de cobro (FR-035,
   US4 esc. 8).
