# Research: Gestión de viajes (Módulo 5)

Decisiones técnicas del módulo, con las alternativas que se evaluaron y el motivo por el que se
descartaron. Cada sección responde a una pregunta que la spec plantea y no resuelve, o que resuelve
en términos de negocio y hay que traducir a una forma concreta.

El módulo llega a un sistema con cuatro módulos construidos, y **la mayor parte del diseño consistió
en decidir qué se reutiliza tal cual**. La respuesta corta: casi todo. Este módulo no agrega ninguna
dependencia, ninguna infraestructura y ningún servicio externo. Lo que sí agrega, y es la primera
vez en el proyecto, es **una unidad de trabajo con ciclo de vida propio, historial de quién hizo qué,
recursos compartidos que se ocupan y se liberan, y dinero**.

---

## 1. El número de viaje: una secuencia con `NO CACHE`, no la identidad de la tabla

**Decisión**: `Viajes.Numero` es una columna propia, alimentada por una secuencia de SQL Server
`dbo.NumeroDeViaje` declarada `START WITH 1 INCREMENT BY 1 NO CACHE`, con un índice único. La clave
primaria sigue siendo `Id`, la identidad de siempre.

**Motivo**: FR-011 pide tres cosas —único, generado por el sistema, **nunca reutilizado**— y las
*Assumptions* agregan una cuarta: "arranca en 1 y avanza de a uno". El escenario CA5 la mide: si se
anula el viaje 1041, el siguiente es 1042.

La tentación es usar `Id` como número y ahorrarse la columna. No alcanza: **una columna `IDENTITY` de
SQL Server 2012 en adelante salta de a 1000 tras un apagado no limpio del servidor**, por el tamaño
de caché que reserva. El salto no rompe FR-011 —sigue siendo único y no reutilizado— pero rompe la
suposición de la spec y el escenario que la verifica: después de un `compose down` abrupto, el viaje
siguiente al 12 sería el 1012. En un entorno donde el motor se levanta y se baja con el resto del
sistema, eso pasa. `NO CACHE` en una secuencia elimina exactamente ese comportamiento a cambio de una
escritura de log por número, que a decenas de viajes por día no se nota.

La anulación no devuelve el número al pozo, y eso es gratis: la secuencia sólo avanza.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Usar `Id` como número de viaje | El salto de 1000 de `IDENTITY` tras un apagado sucio contradice "avanza de a uno" y el escenario CA5 |
| `MAX(Numero) + 1` en la aplicación | Dos altas simultáneas obtienen el mismo número. El índice único salvaría la integridad, pero a costa de un error que el operador no provocó y no entiende |
| Tabla contador con bloqueo | Serializa todas las altas y reimplementa a mano lo que la secuencia ya hace bien |

---

## 2. Las tres garantías que van a la base, no al código

La spec pide tres exclusividades y las tres las escribe como **restricción en la base de datos, no
sólo como validación previa** (FR-011, FR-014, FR-026). Son índices únicos filtrados:

```sql
-- FR-011: el número no se repite nunca.
CREATE UNIQUE INDEX IX_Viajes_Numero ON Viajes (Numero);

-- FR-014: el remito es único entre los viajes NO anulados. Un remito de un viaje anulado
-- vuelve a estar libre; un viaje sin remito no ocupa nada.
CREATE UNIQUE INDEX IX_Viajes_NumeroRemito ON Viajes (NumeroRemito)
    WHERE NumeroRemito IS NOT NULL AND Estado <> 3;   -- 3 = anulado

-- FR-026: un chofer y un vehículo, en un solo viaje `en curso` a la vez.
CREATE UNIQUE INDEX IX_Viajes_ChoferEnCurso ON Viajes (ChoferId)
    WHERE ChoferId IS NOT NULL AND Estado = 1;        -- 1 = en curso

CREATE UNIQUE INDEX IX_Viajes_VehiculoEnCurso ON Viajes (VehiculoId)
    WHERE VehiculoId IS NOT NULL AND Estado = 1;
```

**Por qué filtrados y no una tabla aparte de "ocupaciones"**: los tres predicados se expresan sobre
columnas que el viaje ya tiene. Una tabla `OcupacionesEnCurso` con su propia unicidad sería una copia
del estado del viaje que hay que mantener sincronizada en cada transición, y el primer bug del módulo
sería un viaje rendido con su fila de ocupación viva. El índice filtrado **no puede desincronizarse**:
es el mismo dato.

**Esto es lo que cumple SC-005** —"el 0% de los choferes figura en más de un viaje en curso, incluso
cuando dos operadores intentan la misma asignación al mismo tiempo"—. La consulta previa cierra la
ventana normal y da el mensaje bueno, que nombra el viaje que ocupa; el índice cierra la carrera. Es
exactamente la convención [003]: los repositorios traducen la violación de índice único a una
excepción de la capa de aplicación.

**El costo, anotado**: los filtros llevan el valor numérico del enum escrito a mano (`Estado = 1`,
`Estado <> 3`). Cambiar el orden de `EstadoViaje` invalidaría los tres índices sin que nada falle al
compilar. Se cubre con un test de integración que inserta un viaje en cada estado y verifica que el
índice acepta y rechaza donde corresponde, y con un comentario en el enum.

**Nota sobre FR-027**: un viaje `pendiente` **no** ocupa a nadie, cualquiera sea su fecha. Por eso el
filtro es `Estado = 1` y no "pendiente o en curso". Dos viajes pendientes del mismo día con el mismo
chofer son válidos (US3 esc. 12).

**Los dos caminos que llegan al estado prohibido** (FR-026a): poner un viaje en curso, y **reasignar
la unidad de un viaje que ya está en curso**. La revisión de la spec destapó que el segundo no estaba
escrito, y no es un detalle: el índice único lo rechaza igual, así que sin el requisito el operador
habría recibido un error que ninguna regla explicaba. Los dos casos de uso —`PonerViajeEnCurso` y
`AsignarChoferYVehiculo`— consultan la ocupación antes de guardar, y el segundo **sólo cuando el viaje
está `en curso`**: reasignar un `pendiente` no verifica nada, porque un pendiente no ocupa.

---

## 3. La habilitación se evalúa contra la fecha del viaje, y eso no cuesta una línea de los Módulos 3 y 4

**Decisión**: la evaluación de documentación de FR-022, FR-023 y FR-024 se resuelve **reutilizando
tal cual** `CalculadorEstadoDocumento`, `CalculadorEstadoChofer.VigentesDeCadaTipo` y
`CalculadorEstadoVehiculo.VigentesDeCadaTipo`, pasándoles la fecha del viaje donde hoy reciben el día
en curso.

**Motivo**: es el hallazgo más afortunado del diseño. Los tres calculadores ya reciben la fecha de
referencia como parámetro:

```csharp
CalculadorEstadoDocumento.Calcular(fechaVencimiento, diasAvisoVencimiento, hoy)
```

Nunca leen el reloj por dentro. Se les pasó siempre `FechaHoyArgentina.Hoy()` porque eso es lo que
los Módulos 3 y 4 necesitaban, pero la fecha es un argumento, no una suposición. Pasarles
`viaje.Fecha` da exactamente la semántica que pide la clarificación: **todo contra la fecha del
viaje**, para que un viaje retroactivo se pueda cargar con la unidad que efectivamente lo hizo
(FR-021, FR-024, SC-014).

Consecuencia directa: la regla de "documento vigente por tipo" —el de vencimiento más lejano, con
desempate por `Id` mayor— y la ventana de aviso por tipo valen igual acá, sin escribirlas de nuevo.
El módulo aporta sólo la **traducción a un veredicto de asignación**:

| Estado del peor documento vigente, a la fecha del viaje | Veredicto |
|---|---|
| Alguno `vencida` | `bloqueado` — no se guarda la asignación, y el mensaje nombra tipo y número (FR-022) |
| Ninguno vencido, alguno `proximaAvencer` | `conAdvertencia` — se guarda y la advertencia llega con el resultado (FR-023) |
| Todos `vigente` | `habilitado` |
| No hay ningún documento cargado | `habilitado` — FR-024 es explícita: el sistema informa sobre lo que está cargado y no infiere lo que falta |

Ese último renglón merece un subrayado porque **contradice al Módulo 4 y está bien que lo haga**: en
la flota, una unidad sin documentación **no** puede quedar `disponible` (FR-013 del 004). Acá, un
chofer sin documentos no bloquea. No es una inconsistencia: son dos preguntas distintas. El Módulo 4
pregunta "¿esta unidad está en condiciones?" y responde que no lo sabe, así que no la deja disponible.
El Módulo 5 pregunta "¿hay algo cargado que **prohíba** este viaje?" y no lo hay. La lista de
asignables ya filtra por estado operativo guardado `disponible`, así que el Módulo 4 ya dijo lo suyo
antes de que este módulo mire un solo documento.

**Alternativa descartada**: reimplementar la evaluación con una consulta propia por viaje. Se
descartó porque duplicaría la regla que la convención [003] pide comparar con un test, y porque la
ficha del viaje evalúa **un** chofer y **un** vehículo por vez: no hay presión de rendimiento que
justifique bajarlo a SQL. La lista de asignables sí resuelve en la base el filtro de activos y de
estado operativo guardado, que son columnas.

---

## 4. Asignar y cambiar de estado son recursos propios, nunca campos del `PUT`

**Decisión**: cinco recursos aparte del `PUT /api/viajes/{id}` de datos.

| Operación | Recurso | Requisito |
|---|---|---|
| Asignar o reasignar chofer y vehículo | `POST /api/viajes/{id}/asignacion` | FR-019a |
| Poner en curso | `POST /api/viajes/{id}/en-curso` | FR-034 |
| Rendir | `POST /api/viajes/{id}/rendicion` | FR-034, FR-038 |
| Anular | `POST /api/viajes/{id}/anulacion` | FR-034, FR-036 |
| Dar de alta de nuevo un cliente | `POST /api/clientes/{id}/alta` | FR-007 |

**Motivo**: es el precedente [004] aplicado sin cambios —cambiar el estado de una entidad es un
recurso propio, nunca un campo del `PUT` de edición— y la spec lo extiende a la asignación con el
mismo razonamiento: corregir el destino de un viaje no puede tocar quién lo maneja (US3 esc. 15). El
`PUT` de datos **no acepta** `choferId`, `vehiculoId` ni `estado`: no es que los ignore, es que no
están en el contrato de entrada.

Hay un segundo motivo, propio de este módulo: la asignación es **la única operación que devuelve
bloqueos y advertencias por documentación**. Sacarla del guardado de datos deja las dos respuestas
limpias —el `PUT` responde sobre campos, la asignación responde sobre habilitación— en vez de un
endpoint que a veces falla por un motivo y a veces por otro.

**El alta de cliente no pide confirmación aparte y es idempotente** (FR-007), igual que la
reactivación de vehículo del Módulo 4: no destruye nada y se deshace con la baja, que sí la pide.

---

## 5. Las advertencias: dos formas, según si el paso se puede deshacer

FR-015a fija el criterio de negocio; acá va la forma HTTP.

### Reversible → la operación se ejecuta y la advertencia viaja con el resultado

Son origen igual a destino (FR-015) y documentación próxima a vencer al asignar (FR-023). El
recurso se crea o se modifica, la respuesta es `200`/`201`, y el cuerpo es un **sobre**:

```json
{
  "viaje": { "numero": 1042, "origen": "Rosario", "destino": "Rosario", "…": "…" },
  "advertencias": [
    { "codigo": "origen_igual_a_destino",
      "mensaje": "El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien." }
  ]
}
```

El sobre lo usan **sólo las tres operaciones que pueden advertir** —alta, edición y asignación—. El
listado y la ficha devuelven el recurso pelado, como el resto del sistema. Se prefirió el sobre a un
campo `advertencias` dentro del propio viaje porque una advertencia no es un dato del viaje: es un
dato de **esta operación**. Guardada dentro del recurso, reaparecería en cada consulta posterior de
la ficha, que es justo lo que no queremos.

### Irreversible → no se ejecuta al primer intento

Es rendir un viaje con importe en cero (FR-038). El primer `POST /rendicion` sin confirmación
responde **`409 Conflict`** con `codigo: "rendicion_requiere_confirmacion"`, **sin cambiar nada**. La
pantalla muestra el diálogo y reintenta con `{ "confirmado": true }`, que rinde.

La confirmación vive en el backend y no sólo en la pantalla porque FR-038 lo dice con todas las
letras —"el sistema NO DEBE aplicar el cambio al primer intento"— y porque SC-007a lo mide. Es una
diferencia con la baja del Módulo 4, donde la confirmación la pide la pantalla y el endpoint ejecuta:
ahí, ejecutar de más se deshace con una reactivación; acá, FR-018 deja el viaje inmutable para
siempre.

### Qué código HTTP usa cada rechazo

Una sola regla, para no decidir caso por caso:

- **`400`** cuando el problema está en **lo que se tipeó**: campos faltantes o mal formados, CUIT o
  remito duplicado, dependencias que impiden una baja.
- **`409`** cuando el problema está en **el estado de algo que se comparte o que cambió**: chofer o
  vehículo ocupado, transición no permitida, viaje rendido inmutable, confirmación pendiente.

Con eso, el frontend sabe sin leer el código si tiene que marcar un campo o mostrar un diálogo.

---

## 6. `demorado` se deriva del historial, no de una columna

**Decisión**: el listado calcula `demorado` con una subconsulta correlacionada al historial:

```csharp
// Instante en que el viaje pasó a `en curso`. Existe a lo sumo uno: `pendiente → en curso` es la
// única transición que llega a ese estado y no hay camino de vuelta (FR-033).
EnCursoDesde = contexto.CambiosDeEstadoViaje
    .Where(cambio => cambio.ViajeId == viaje.Id && cambio.EstadoNuevo == EstadoViaje.EnCurso)
    .Max(cambio => (DateTime?)cambio.OcurridoEn)
```

y marca `demorado` cuando el viaje está `en curso` y ese instante es anterior a `ahora - 5 días`.

**Motivo**: es la convención [003] —los estados derivables se calculan al leer, nunca se guardan en
columna— con un matiz que vale anotar. Acá lo derivado es la **señal**; el **instante** que la
alimenta es un hecho, y los hechos sí se guardan. La pregunta era de dónde sacarlo, y la respuesta es
el historial, que FR-035 obliga a llevar de todos modos. Una columna `EnCursoDesde` sería una copia
de una fila que ya existe, con la posibilidad de discrepar de ella.

**El umbral de 5 días es una constante del dominio**, `Viaje.DiasParaDemora = 5`, en un solo lugar del
que salen la consulta, el test y el texto de la pantalla. La clarificación fijó cinco días corridos
desde el instante en que pasó a `en curso`.

**Alternativa descartada**: columna `EnCursoDesde` mantenida en la transición. Se descartó por lo
anterior; se reconsideraría si el listado se volviera lento, que con decenas de viajes por día no va
a pasar.

---

## 7. Quién hizo el cambio: la identidad llega por parámetro, sin abstracción nueva

**Decisión**: el endpoint lee `ClaimsSesion.ObtenerIdUsuario(contexto.User)` y se lo pasa al caso de
uso como un `int` más. No se agrega ningún `IUsuarioActual` ni acceso a `IHttpContextAccessor` desde
la capa de aplicación.

**Motivo**: es lo que ya hacen `MiCuentaEndpoints` y los dos endpoints de descarga de adjuntos, y es
lo más simple que funciona (Principio I). Un servicio `IUsuarioActual` sería un envoltorio sobre
`HttpContext` que la capa de aplicación tendría que recibir igual por inyección, y que en los tests
habría que falsear en vez de pasar un número.

Este módulo es **el primero que necesita saber quién hizo algo** para guardarlo: hasta acá la
identidad servía para autorizar, no para registrar. Si un módulo futuro necesita lo mismo en más de
tres o cuatro lugares, ahí sí vale extraer la abstracción; hoy son cuatro casos de uso —alta, en
curso, rendición, anulación— y el parámetro alcanza.

**El instante lo pone el servidor**, con `TimeProvider` —ya registrado en el contenedor— y se guarda
en UTC, que la convención [002] devuelve con la `Z` sin que este módulo escriba nada.

---

## 8. La búsqueda sin acentos: `COLLATE` explícito en la consulta

**Decisión**: FR-042 se resuelve con `EF.Functions.Collate(columna, "Latin1_General_CI_AI")` sobre
origen, destino y razón social del cliente.

```csharp
consulta = consulta.Where(viaje =>
    EF.Functions.Like(EF.Functions.Collate(viaje.Origen, ColacionSinAcentos), patron) ||
    EF.Functions.Like(EF.Functions.Collate(viaje.Destino, ColacionSinAcentos), patron) ||
    EF.Functions.Like(EF.Functions.Collate(viaje.Cliente!.RazonSocial, ColacionSinAcentos), patron));
```

**Motivo**: la base se crea con la colación por defecto de la imagen de SQL Server,
`SQL_Latin1_General_CP1_CI_AS`: **insensible a mayúsculas pero sensible a acentos**. Buscar `cordoba`
no encuentra `Córdoba`, y la spec pide que encuentre en las dos direcciones (US5 esc. 3). Declarar la
colación en la comparación lo resuelve en una expresión, sin migrar la base ni agregar columnas.

**Por qué no columnas normalizadas**, que es lo que hizo el Módulo 2 con `UsernameNormalizado`: ahí la
normalización servía a **dos** fines —buscar y garantizar unicidad—, y la columna se ganaba el lugar
por el índice único. Acá sólo se busca. Tres columnas espejo que hay que mantener en sincronía en
cada alta y cada edición, sobre una tabla que no las necesita para ninguna restricción, es más
maquinaria de la que el problema pide.

**El costo, anotado**: una comparación con `COLLATE` no usa índice. Con `LIKE '%texto%'` tampoco lo
usaría de todos modos —el comodín inicial ya lo impide—, así que la colación no pierde nada que la
búsqueda parcial no hubiera perdido antes. A la escala de este sistema, un recorrido sobre la tabla de
viajes filtrada por los demás criterios se resuelve de sobra dentro del segundo que fija el objetivo.

---

## 9. El transportista del viaje: una referencia, fijada al asignar

**Decisión**: `Viajes.TransportistaId` es una clave foránea anulable al padrón del Módulo 3. Se
escribe al asignar el chofer, con el transportista que el chofer tiene **en ese momento**, y no se
mueve nunca más por sí sola. Reasignar el chofer la vuelve a escribir con el transportista del nuevo.

**Motivo**: lo fija la clarificación y FR-028. Vale desarmar por qué una referencia y no una copia del
nombre, porque parece lo contrario de lo que pide "congelar":

- Lo que hay que congelar es **a quién apunta el viaje**, y eso lo congela la referencia: si el chofer
  Gómez pasa de Transporte Sur a G&T, el viaje sigue apuntando a Transporte Sur (US3 esc. 10).
- Lo que **no** hay que congelar son los datos de ese transportista: si le corrigen la razón social,
  el viaje muestra la corregida. Una copia del nombre dejaría el viaje mostrando un nombre mal escrito
  para siempre.
- Y hay un tercer motivo, práctico: el filtro por transportista (FR-041) y el agrupamiento del reporte
  (FR-046) se apoyan en el mismo padrón de siempre, con `TransportistaId` comparado por igualdad. Con
  un nombre copiado habría que agrupar por texto y dos grafías del mismo transportista serían dos
  filas del reporte.

**Es anulable**: un viaje sin chofer asignado todavía no tiene transportista, y por eso el filtro por
transportista no lo devuelve. Es el comportamiento que la spec declara esperado.

**El transportista del vehículo no se compara con el del chofer** (FR-029). Un chofer de un
transportista puede manejar un vehículo de otro y no hay ninguna validación al respecto: el
transportista del viaje sale siempre del chofer.

---

## 10. Dos permisos: gestión y consulta

**Decisión**: `viajes.gestionar` y `viajes.consultar`.

| Rol | `viajes.gestionar` | `viajes.consultar` |
|---|---|---|
| Tráfico | ✅ | ✅ |
| Administrador del sistema | ✅ | ✅ |
| Administración de la empresa | — | ✅ |
| Gerencia | — | ✅ |

**Motivo**: es el precedente [004] —un módulo con dos niveles de acceso adentro lleva dos permisos, no
un permiso y un chequeo de rol en el endpoint— aplicado a la distinción que pide FR-050 y FR-051. Los
endpoints de lectura exigen `viajes.consultar`; los de escritura, `viajes.gestionar`. Quien gestiona
tiene los dos: son permisos, no niveles ordenados, y el que gestiona también consulta.

Esto es lo que hace que FR-052 y SC-012 se cumplan **en el servidor**: Gerencia no ve las acciones
porque el menú y la pantalla resuelven por permiso, y si alguien invoca el endpoint a mano recibe
`403` de la política de autorización, sin que el caso de uso tenga que enterarse.

**El padrón de clientes comparte los dos permisos** (FR-053): se administra con `viajes.gestionar` y
se consulta con `viajes.consultar`. No lleva permiso propio, por el mismo criterio con el que el
padrón de personas quedó dentro del permiso del Módulo 2.

**Tres entradas de menú**, todas atadas a `viajes.consultar`, porque las tres pantallas se pueden
mirar sin poder tocar nada: *Viajes* (`/viajes`), *Clientes* (`/clientes`) y *Totales*
(`/viajes/totales`).

---

## 11. El importe: el primer dinero del sistema

**Decisión**: `decimal(18,2)` en la base, `decimal` en C#, `number` en el JSON, y un formateador
compartido nuevo en `frontend/src/compartido/moneda.ts`.

**Motivo**: el Principio II exige pesos argentinos con punto de miles, coma decimal y símbolo `$`.
Ningún módulo anterior manejó plata, así que no hay dónde reutilizar. Es exactamente el caso del
`compartido/fechas.ts` del Módulo 3: un formato que va a usar todo el sistema no puede quedar escrito
a mano en cada pantalla, porque la primera que lo escriba distinto va a ser la que nadie revise.

```ts
const FORMATO = new Intl.NumberFormat('es-AR', {
  style: 'currency', currency: 'ARS', minimumFractionDigits: 2,
})
export function formatearPesos(monto: number): string { return FORMATO.format(monto) }
```

**`decimal` y no `double`**: un total por cliente es una suma de importes que alguien va a comparar
contra una planilla. Un flotante binario no representa `1234.10` exactamente y las diferencias se
acumulan al sumar. `decimal(18,2)` en SQL Server y `decimal` en C# es la correspondencia exacta.

**Cero es válido y negativo no** (FR-013). El cero se admite en el alta —el importe puede no estar
definido todavía— y tiene su consecuencia al rendir (FR-038). El negativo se rechaza en la aplicación
y además con un `CHECK (Importe >= 0)` en la tabla: es la misma clase de garantía que la unicidad, y
cuesta una línea de migración.

---

## 12. Paginación, orden y el filtro de estado por defecto

**Decisión**: 20 filas por página del lado del servidor, con `{ items, total, pagina, tamanioPagina }`
—`PaginaDe<T>`, tal como está—, y orden **fecha del viaje descendente, número descendente**.

**Motivo**: la convención [003] pide un orden total que no permita que dos filas se intercambien entre
páginas. `Fecha` no es total —muchos viajes comparten día— y `Numero` sí lo es, así que la pareja
alcanza y termina en una columna única. Es la primera vez que el criterio no termina en `Id`, y no
hace falta que termine: `Numero` tiene índice único propio, y es además el que ve el usuario.

**El filtro de estado por defecto excluye los anulados como predicado único** (FR-044), no como un
filtrado posterior:

```csharp
consulta = filtros.Estado is { } estado
    ? consulta.Where(viaje => viaje.Estado == estado)
    : consulta.Where(viaje => viaje.Estado != EstadoViaje.Anulado);
```

Es el precedente [004] sobre filtros complementarios: la exclusión es una garantía de la consulta y no
algo que alguien pueda olvidar aguas abajo. Y la pantalla dice explícitamente qué está mostrando
(FR-049), porque un listado no oculta filas en silencio.

**El total del listado no se muestra en una fila de totales** (FR-046a): los totales viven sólo en su
pantalla. El `total` de la paginación es la cantidad de coincidencias, que es otra cosa.

---

## 13. El padrón de clientes: qué se copia del Módulo 3 y qué no

`Cliente` se parece mucho a `Transportista`: razón social, CUIT único, teléfono, email obligatorio sin
unicidad, baja lógica con rechazo por dependencias. La pregunta obligada es si comparten algo.

**Decisión**: comparten **la regla del CUIT** —`ValidadorCuit` y `NormalizadorDocumentoNumerico` del
Módulo 3, consumidos tal cual— y nada más. `Cliente` es una tabla y una entidad propias.

**Motivo**: es el precedente [004] —se comparte la regla, no necesariamente la tabla— con menos duda
todavía que allá, porque acá no hay ninguna tentación de tabla común: un cliente y un transportista
son dos cosas distintas del negocio que casualmente se identifican con el mismo número. Unirlos
obligaría a un discriminador y a que la baja de uno mirara las dependencias del otro.

Lo que sí se copia con nombre propio es el **rechazo del CUIT que pertenece a un cliente dado de
baja** (FR-007): código de error distinto del duplicado común, exactamente como
`patente_de_vehiculo_dado_de_baja` en el Módulo 4, y por el mismo motivo: quien lo intenta no
encuentra al cliente en el listado por defecto y necesita que se le diga que lo reactive.

Las diferencias con `Transportista`, que las hay:

| | `Transportista` (M3) | `Cliente` (M5) |
|---|---|---|
| Dirección | no tiene | opcional, hasta 200 caracteres |
| Tipo de persona | física / jurídica | no lo lleva: el módulo no lo usa |
| Rechazo de baja | por choferes y vehículos **activos** | por viajes **`pendiente` o `en curso`** (FR-006) |

La última fila terminó siendo **la misma regla que el Módulo 3**, y llegó ahí por una corrección. La
spec pedía originalmente rechazar por viajes "no anulados", que incluye los rendidos: como un cliente
que dejó de operar tiene rendidos por definición, la baja quedaba prohibida justo para el caso que la
historia de usuario ponía como su motivo. Mirando sólo los `pendiente` y `en curso` la restricción
protege lo único que hay que proteger —que no quede trabajo comprometido colgando de un cliente
inactivo— y se alinea con el criterio del Módulo 3: se rechaza por **dependientes vivos**, no por
historial.

---

## 14. Lo que se decide acá porque la spec no lo dice, y no la contradice

Detalles de implementación que la spec deja abiertos y que conviene fijar antes de escribir código:

- **`Estado` se guarda como `tinyint`** con `Pendiente = 0`, `EnCurso = 1`, `Rendido = 2`,
  `Anulado = 3`. Los tres índices filtrados dependen de esos números (§2).
- **Las claves foráneas a `Clientes`, `Choferes`, `Vehiculos` y `Transportistas` van con
  `DeleteBehavior.Restrict`.** Ninguna de esas entidades se borra físicamente en ningún módulo, así
  que el `Restrict` no se ejerce nunca; está para que un borrado accidental futuro falle en la base en
  vez de llevarse viajes por delante.
- **El historial se borra en cascada con el viaje**, que tampoco se borra nunca. Es la única cascada
  del módulo y existe porque un cambio de estado sin su viaje no significa nada.
- **`MotivoAnulacion` es `null` mientras el viaje no esté anulado** y se escribe en la misma operación
  que el estado. No hay ningún camino que lo escriba sin anular ni que anule sin escribirlo.
- **El chofer y el vehículo dados de baja se señalan como inactivos en el listado y en la ficha**
  (FR-030) leyendo su `Activo`; la asignación no se toca. La palabra "(inactivo)" acompaña siempre al
  nombre, nunca sólo un color (FR-049).
- **La lista de asignables no pagina.** Son dos desplegables sobre padrones de decenas de filas.
- **La fecha del viaje es `date`, no `datetime`**: la spec habla de días, nunca de horas.
  `FechaHoyArgentina` decide qué es "hoy" para marcar la carga retroactiva (FR-016).

---

## 15. Riesgos y cómo se cubren

| Riesgo | Cómo se cubre |
|---|---|
| Los índices filtrados llevan el valor numérico del enum escrito a mano | Test de integración que inserta viajes en los cuatro estados y verifica que cada índice acepta y rechaza donde corresponde (§2) |
| La regla de habilitación corre en C# sobre entidades cargadas; la lista de asignables filtra en SQL | Convención [003]: un test compara las dos sobre el mismo dato. Acá el punto de contacto es chico —la lista filtra por `Activo` y estado operativo guardado, que son columnas— pero el test va igual |
| Dos operadores ponen en curso el mismo chofer a la vez | Índice único filtrado. El test lanza las dos operaciones en paralelo y verifica que una gana y la otra recibe `chofer_ocupado` con el número del viaje que lo ocupa (SC-005) |
| Un viaje queda guardado con asignación bloqueada por un cambio de fecha | FR-022a: el `PUT` revalida contra la fecha nueva y rechaza. Test que mueve la fecha de un viaje asignado a un día en que la VTV está vencida y verifica que **no cambió nada**: ni la fecha ni la asignación (SC-004) |
| Un documento corregido o eliminado en los Módulos 3 o 4 deja un viaje ya guardado con documentación vencida a su fecha | **No se cubre, y está declarado**: este módulo no administra esos padrones y no puede impedirlo. SC-004 se acotó a las operaciones propias y las *Assumptions* lo dicen. Revisar los viajes afectados al cambiar un documento exigiría tocar los Módulos 3 y 4, y es una spec aparte |
| Un viaje arranca con un chofer o un vehículo dado de baja | FR-025: el pase a `en curso` exige que los dos sigan activos y obliga a reasignar. Lo que **no** se revalida es la documentación: se controló al asignar, y volver a mirarla dejaría en tierra un viaje planificado con la unidad en regla (US4 esc. 14 y 15) |
| La rendición de un viaje con importe en cero se ejecuta sin confirmar | El `409` sin confirmación es del backend, no de la pantalla. Test que lo pide sin confirmar y verifica que el viaje sigue `en curso` (SC-007a) |
| Un viaje rendido se modifica por algún camino | Un solo lugar decide: los casos de uso de edición, asignación, cambio de estado y anulación consultan el estado antes de tocar nada. Test por cada uno de los cinco caminos, con rol *Administrador del sistema* (FR-018, SC-013) |
| El total de la pantalla de totales no coincide con la suma del listado | Las dos consultas excluyen los anulados con el mismo predicado. Test que carga viajes anulados y no anulados y compara los dos números (SC-008) |
| La búsqueda con `COLLATE` no usa índice | Aceptado y anotado (§8): `LIKE '%…%'` tampoco lo usaría. Se revisa si el listado supera el segundo con el volumen real |
