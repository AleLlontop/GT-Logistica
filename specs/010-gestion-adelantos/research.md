# Research: Gestión de adelantos de sueldo (Módulo 10)

Las decisiones técnicas del módulo, cada una con la alternativa descartada y el motivo. Cada sección
responde una pregunta que el diseño tenía abierta después de leer la spec.

La spec llegó con **ocho preguntas resueltas** en su sesión de clarificación, más una decisión de alcance
tomada durante la planificación (§10b). No queda ningún `NEEDS CLARIFICATION` de producto. Lo que sigue
es **cómo** se construye lo que la spec ya decidió.

**Es el primer módulo que aplica las convenciones del 009 sin inventar mecanismo nuevo**: el ciclo de vida
se cierra con `UPDATE` condicionales sobre la fila, el rechazo y la anulación se confirman desde su diálogo y el estado
guardado queda atado por un `CHECK`. Lo nuevo de este módulo es **§1**: una regla sobre datos de tres
módulos anteriores que la pantalla y el guardado tienen que aplicar igual.

---

## §1 — Quién puede recibir un adelanto, escrito una sola vez

La spec pide lo mismo en dos lugares:

- **FR-002**: el desplegable ofrece, para *Chofer*, las personas con ficha activa cuyo transportista es
  G&T Logística S.A.; para *Empleado*, las personas activas de tipo empleado sin ficha.
- **FR-009**: el guardado verifica lo mismo, también en una invocación directa, y **dice el motivo**:
  inexistente, dada de baja, de otro tipo, chofer de un transportista externo (US1 esc. 10 y 11).

**Decisión**: **una regla pura de dominio, `ElegibilidadDeBeneficiario.Evaluar(tipo, persona,
cuitEmpresaEmisora)`**, que devuelve `null` o el motivo. Las dos consultas cargan la misma proyección de la
persona —activa, tipo, si tiene ficha, si la ficha está activa, CUIT del transportista— y le aplican esa
regla:

- **El desplegable** proyecta las personas activas y **filtra en memoria** con la regla.
- **El guardado** proyecta la persona pedida por `Id` y evalúa la regla; el motivo sale de ahí.

Lo que el desplegable ofrece y lo que el guardado acepta **no se pueden separar, porque son la misma
función** (convención [006]: dos caminos al mismo resultado llaman al mismo armador).

**Por qué en memoria, cuando la convención [003] pide escribir el predicado en el árbol de la
consulta**: [003] protege una consulta que **tiene** que resolverse en SQL —una que se pagina, o que corre
sobre una tabla grande— de evaluarse en memoria sin que nadie lo note. Acá la evaluación en memoria es la
decisión y no un accidente:

- el desplegable **no se pagina**: son las personas activas de una única empresa, decenas;
- la regla tiene que **clasificar el motivo** para FR-009, y eso en SQL no se escribe: habría una versión
  del predicado en la consulta y otra en C# para el mensaje, con un test que las compare (convención
  [003]). Una función sola no necesita ese test.

**Límite anotado**: si el padrón creciera a miles de personas, el filtro pasa a SQL con el test de
comparación. No es el caso de esta versión (Principio I).

### Chofer lo decide la ficha; empleado, el padrón sin ficha

- **Chofer** es quien tiene **ficha de chofer activa** en el Módulo 3. `Persona.Tipo` no se mira: la ficha es
  la única fuente de verdad sobre quién es chofer (research §1 del Módulo 3). Una persona cargada como
  empleado y registrada después como chofer aparece bajo *Chofer* (spec §Edge Cases).
- **Propio** es el chofer cuyo transportista tiene el CUIT de la empresa emisora, **la misma comparación**
  con la que el Módulo 9 reconoce a un externo (su research §5), sobre dos CUIT ya normalizados.
- **Empleado** es la persona activa de `Tipo = Empleado` **sin ficha, activa o no**. Así nadie aparece bajo
  los dos tipos, y quien tiene la ficha dada de baja no aparece bajo ninguno.

### `TipoBeneficiario` es un enum propio, no `TipoIntegrante`

Tienen los mismos dos valores, y reusar el del Módulo 2 ahorraría un archivo. No se reusa porque **no
dicen lo mismo**: `Persona.Tipo` es un dato informativo que el operador carga en el padrón, y el tipo del
adelanto se decide con la ficha. Con un solo enum, validar "es chofer" con `Persona.Tipo == Chofer` compila
y está mal. El sistema ya tiene el precedente: `TipoPersona` y `TipoIntegrante` son dos enums porque son
dos ejes (`GT.Domain/Choferes/TipoPersona.cs`). Los números son los mismos, `1` y `2`, para que nadie tenga
que traducir al leer la base.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Predicado en el árbol de la consulta para el desplegable **y** un clasificador en C# para el motivo, con un test que los compare | Son dos escrituras de la misma regla sostenidas por un test. Tiene sentido cuando la consulta tiene que ir a SQL; un desplegable de decenas de filas no lo necesita |
| Validar con "la persona está en la lista del desplegable" y un mensaje genérico | Pierde el motivo que piden US1 esc. 10 —"ya no está activa"— y esc. 11 |
| Endpoints de elegibilidad en los Módulos 2 o 3 | Modifica módulos anteriores para una regla que es de este módulo |
| Reusar `TipoIntegrante` | Invita a validar con `Persona.Tipo`, que no decide quién es chofer |

---

## §2 — Cómo se cierran las carreras

| Carrera | Qué no puede pasar |
|---|---|
| Aprobar contra rechazar | que el adelanto quede en los dos estados, o que el segundo pise al primero (FR-026) |
| Dos aprobaciones, o dos rechazos | que el historial registre dos veces la misma resolución |
| Dos anulaciones | que se registren dos anulaciones con motivos distintos (FR-032) |

**Decisión**: **cada operación empieza con un `UPDATE` condicional sobre la fila del adelanto** y verifica
una fila afectada, dentro de su transacción. Es la convención [009] tal cual:

```sql
UPDATE Adelantos SET Estado = 1                          WHERE Id = @id AND Estado = 0;  -- aprobar
UPDATE Adelantos SET Estado = 2, MotivoRechazo = @motivo WHERE Id = @id AND Estado = 0;  -- rechazar
UPDATE Adelantos SET Estado = 3, MotivoAnulacion = @motivo WHERE Id = @id AND Estado = 1;  -- anular
```

Bajo el aislamiento por defecto, la segunda transacción se bloquea sobre la fila y, al desbloquearse,
reevalúa el `WHERE` contra el dato confirmado: cero filas. Se deshace, **se relee** y se responde `409`
con el estado en que quedó, que es lo que piden US4 esc. 7 y FR-026.

**No hace falta `Version`**, a diferencia del Módulo 9: allá dos ediciones veían el mismo estado y el estado
no las distinguía. Acá no hay edición (FR-035), y cada operación mueve el estado, así que la condición
sobre el estado alcanza para las cuatro carreras.

**Segunda red**: `IX_CambiosDeAdelanto_Operacion`, único sobre `(AdelantoId, Operacion)`. No es lo que
cierra la carrera —el `UPDATE` ya la cerró antes de insertar—, pero si alguien reescribiera un caso de uso
sin la condición, la segunda entrada de la misma operación no entraría.

### La carrera que no se cierra, y por qué está bien

Una **baja de la persona entre la validación y el `INSERT`** del registro deja un adelanto sobre alguien
que se dio de baja un instante después. No se cierra porque:

- la baja es una operación del Módulo 2 que no toca ninguna fila de este módulo, así que no hay condición
  que poner ni índice que la exprese;
- cerrarla exigiría bloquear la fila de `Personas` desde este módulo (`UPDLOCK`), SQL a mano sobre una
  tabla ajena;
- **el resultado es un estado que la spec ya admite**: un adelanto cuya persona se dio de baja después
  (spec §Edge Cases), que se sigue resolviendo y anulando.

La consulta previa cubre el caso que sí se ve en pantalla: la persona que otro usuario dio de baja
**mientras el formulario estaba abierto** (US1 esc. 10).

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Token de concurrencia (`rowversion`) | Detecta lo mismo que la condición de estado, pero con un valor binario que viaja al cliente y un mensaje genérico en vez de "ya está aprobado" |
| Sólo el índice único del historial | Con aprobar contra rechazar las dos operaciones son distintas y el índice no las ve |
| Aislamiento `Serializable` | Maquinaria para una fila |

---

## §3 — Qué se guarda y qué se deriva

| Dato | Dónde | Por qué |
|---|---|---|
| `Estado` | **columna** | Aprobar, rechazar y anular son hechos: no se derivan de nada. Un `CHECK` lo ata a los motivos (data-model §Adelantos) |
| `TipoBeneficiario` | **columna** | Es el elegido al registrar y **no se recalcula** (spec §Clarifications, FR-020). Derivarlo de la ficha cambiaría adelantos viejos cuando alguien pasa de empleado a chofer |
| Apellido, nombre, DNI | **se leen del padrón** | FR-020. No es un comprobante que congele lo impreso (convención [006]) |
| `MotivoRechazo`, `MotivoAnulacion` | **columnas** en `Adelantos` | Se muestran en el detalle y en la entrada del historial, que los lee de ahí: hay un solo rechazo y una sola anulación posibles, y no se copian |
| Fecha y usuario de cada operación | **historial** | `CambiosDeAdelanto`, escrito en la misma transacción |
| Total adelantado | **derivado** al leer | §5 |

**Dos columnas de motivo y no una `MotivoDeCierre`.** Una sola alcanzaría —un rechazado nunca se anula—,
pero obligaría a mirar el estado para saber qué motivo es. Con dos, el `CHECK` dice exactamente qué columna
lleva texto en cada estado, y la liquidación de haberes que venga no hereda una columna ambigua.

---

## §4 — Qué se confirma y dónde

La convención [005] fija el criterio: la confirmación vive en el backend **cuando el paso no se puede
deshacer**. La [009] fija las dos formas de llegar a `confirmado: true`.

| Operación | ¿Se deshace? | Confirmación |
|---|---|---|
| Registrar | sí: se rechaza con motivo | ninguna |
| **Aprobar** | sí: se anula con motivo | **ninguna** (spec §Assumptions: resolver "sin tener que cargar nada de nuevo") |
| **Rechazar** | **no**: `rechazado` es final y el adelanto no se vuelve a presentar | **backend**: sin `confirmado: true` responde `409 rechazo_requiere_confirmacion` sin cambiar nada (FR-024) |
| **Anular** | **no**, y revierte algo que ya contaba como adelantado | **backend**: sin `confirmado: true` responde `409 anulacion_requiere_confirmacion` sin cambiar nada (FR-029) |

**El rechazo y la anulación viajan confirmados desde su diálogo**, la segunda forma de la convención
[009]: no hay ningún número que sólo el servidor sepa y haya que mostrar antes. Cada diálogo pide el motivo
y su botón nombra la consecuencia —*Rechazar adelanto*, *Anular adelanto*—; un segundo diálogo sería pedir
dos veces lo mismo. El `409` queda para quien invoca la acción sin pasar por la pantalla (US4 esc. 8, US5
esc. 7).

**El rechazo se confirma porque no se deshace** (spec §Clarifications, decisión de alcance durante la
planificación). Aunque el dinero nunca contó como adelantado, el adelanto queda cerrado con un motivo que
ya no se corrige, y [005] mide la reversibilidad del paso. La primera versión de este plan lo dejaba sin
confirmación, con el argumento de que registrar un adelanto nuevo restituye lo perdido; se descartó.

**Aprobar sigue sin confirmación**: se deshace con la anulación, que sí la pide.

**Orden de validación**, igual en los dos: existencia, estado, motivo, confirmación. Pedir confirmación
para algo que igual se va a rechazar es pedir una decisión que no existe (Módulo 9, data-model §Anular).

---

## §5 — Cómo se calcula el total adelantado

**Decisión**: **una suma en SQL sobre la misma consulta filtrada del listado**, antes de paginar, y un
campo más en la respuesta del listado:

```csharp
var total = await consulta.CountAsync(cancelacion);
var totalAdelantado = await consulta
    .Where(adelanto => adelanto.Estado == EstadoAdelanto.Aprobado)
    .SumAsync(adelanto => adelanto.Importe, cancelacion);
```

- **Toda la selección y no la página** (FR-016): sale de `consulta`, no de las 20 filas.
- **Sólo los `aprobado`** (spec §Assumptions): los pendientes no están firmes y los rechazados y anulados no
  se entregaron. Con el filtro de estado en `pendiente`, el total da `$ 0,00` y es correcto: entre esos no
  hay aprobados.
- **La respuesta es `{ items, total, pagina, tamanioPagina, totalAdelantado }`**, en un DTO propio del
  módulo. Es la forma de la convención [003] con un campo más; `PaginaDe<T>` no se toca.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Endpoint aparte `/adelantos/total` con los mismos filtros | Dos pedidos que la pantalla tiene que mantener sincronizados; un filtro que cambia entre los dos da un total de otra selección |
| Sumar en el frontend | Suma sólo la página visible, que es lo que FR-016 prohíbe |
| Columna de acumulado por persona | Un dato guardado sin nada que lo necesite, y FR-012 dice que no hay topes que controlar |

---

## §6 — Tabla de códigos HTTP

La regla [005] sin excepciones nuevas:

| Situación | Código | Código de error |
|---|---|---|
| Tipo, persona, fecha, motivo o importe faltante o mal formado (FR-006 a FR-008) | `400` | `datos_invalidos` con `campo` |
| Fecha posterior a hoy o anterior al primer día del mes anterior (FR-005) | `400` | `fecha_fuera_de_rango`, con `desde` y `hasta` |
| Empresa emisora sin configurar, con tipo *Chofer* (FR-002a) | `400` | `empresa_emisora_no_configurada` |
| Persona inexistente, dada de baja, de otro tipo o chofer externo (FR-009) | `400` | `beneficiario_no_elegible`, con `motivo` |
| Motivo de rechazo o de anulación vacío (FR-024, FR-029) | `400` | `motivo_requerido` |
| Rango de filtro con *desde* posterior a *hasta* (FR-015) | `400` | `rango_invalido` |
| **Adelanto que no está `pendiente`** al aprobar o rechazar (FR-021) | `409` | `adelanto_no_resoluble`, con `estado` |
| **Adelanto que no está `aprobado`** al anular (FR-027) | `409` | `adelanto_no_anulable`, con `estado` |
| **Confirmación pendiente** (FR-024, FR-029) | `409` | `rechazo_requiere_confirmacion` / `anulacion_requiere_confirmacion` |
| Adelanto inexistente | `404` | `adelanto_no_encontrado` |

**`beneficiario_no_elegible` es `400` y no `409`**, aunque la causa pueda ser una baja que hizo otro usuario:
se corrige **cambiando el campo**, eligiendo otra persona, que es lo que [005] manda a `400`. Es el mismo
criterio con el que el Módulo 9 respondió `400 transportista_no_liquidable` a un transportista dado de baja.

---

## §7 — Permisos y menú

**Dos permisos nuevos**, por permiso y nunca por rol (FR-041, convención [004]):

| Código | Qué habilita | Roles |
|---|---|---|
| `adelantos.gestionar` | registrar, aprobar, rechazar, anular y el desplegable de beneficiarios | Administración de la empresa, Administrador del sistema |
| `adelantos.consultar` | listado, detalle y el filtro de personas | los dos anteriores **más** Gerencia |

Aprobar **no** lleva un permiso aparte (spec §Clarifications, FR-022). Módulo del permiso en el catálogo:
`Adelantos`.

**Menú**: dos entradas nuevas en `CatalogoOpcionesMenu`, en la sección *Operación* con una línea cada una
en `seccionesDeMenu.ts`, junto a viajes, facturas y liquidaciones:

| Código | Texto | Ruta | Permiso |
|---|---|---|---|
| `consultar-adelanto` | Consultar adelanto | `/adelantos` | `adelantos.consultar` |
| `registrar-adelanto` | Registrar adelanto | `/adelantos/nuevo` | `adelantos.gestionar` |

Los textos son los nombres del enunciado. Los códigos siguen el patrón del Módulo 9 —verbo y objeto— y
ninguno es `liquidaciones`, el código desconocido de `seccionesDeMenu.test.ts`.

**Un test del Módulo 2 va a cambiar su aserción**: `AsignarRolesTests.Gerencia_RecibeSuPrimerPermisoConElModulo5`
recorre los módulos de permisos de Gerencia, y `adelantos.consultar` le suma uno. Es el mismo test que ya
actualizaron los Módulos 6 y 9 por la misma razón, y sigue protegiendo lo mismo: Gerencia no recibe
ningún permiso de gestión.

### Qué ve quien llega sin permiso

`RutaProtegida` sólo exige sesión, así que un usuario de *Tráfico* que escribe `/adelantos` entra a la
pantalla, la carga responde `403`, y la pantalla mostraría `No pudimos traer los adelantos. Volvé a
intentar en unos minutos.`: un error que no existe y un reintento que nunca va a funcionar.

**Decisión** (spec §Clarifications, FR-043, US6 esc. 5): **la pantalla distingue el `403` y muestra un aviso
de que falta permiso y a quién pedírselo**, en lugar de su contenido (textos en `contracts/README.md`
§Pantallas). El listado y el detalle lo deciden con el `403` de su carga, detectado con `esSinPermiso` de
`servicioAdelantos.ts`. *Registrar adelanto* no carga nada al abrir —los beneficiarios se piden al elegir
el tipo—, así que recibe `puedeGestionar` de la sesión, como el detalle, y el `403` del guardado sigue
siendo la restricción (convención [005]).

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Redirigir a `/` desde `App.tsx` | Esconde el motivo: la persona escribió una dirección y aparece en el inicio sin explicación |
| Resolverlo en `RutaProtegida` o en `clienteHttp` para todo el sistema | Cambia lo que muestran los Módulos 5, 6 y 9, que no está entre los tres cambios enumerados (spec §Assumptions). Queda como candidato para una spec futura |
| Decidir las tres pantallas con los permisos de la sesión | El listado y el detalle ya tienen una carga que responde; la sesión sólo hace falta donde no hay nada que preguntarle al servidor |

---

## §8 — Pantallas y endpoints

**Tres pantallas y dos diálogos**:

| Pantalla | Ruta |
|---|---|
| Listado de adelantos | `/adelantos` |
| Registrar adelanto | `/adelantos/nuevo` |
| Detalle de adelanto | `/adelantos/:id` |
| *Diálogo* Rechazar adelanto | sobre el detalle |
| *Diálogo* Anular adelanto | sobre el detalle |

**Aprobar no tiene diálogo**: es un botón del detalle que ejecuta y relee (§4).

**Ocho endpoints**, todos bajo `/api/adelantos`:

| Método y ruta | Permiso |
|---|---|
| `GET /beneficiarios?tipo=` | gestionar |
| `GET /personas` | consultar |
| `GET /?personaId=&desde=&hasta=&estado=&pagina=` | consultar |
| `GET /{id:int}` | consultar |
| `POST /` | gestionar |
| `POST /{id:int}/aprobacion` | gestionar |
| `POST /{id:int}/rechazo` | gestionar |
| `POST /{id:int}/anulacion` | gestionar |

- **Cada cambio de estado es un recurso propio** (convención [004]), y no hay `PUT`: un adelanto no se
  modifica (FR-035).
- **`/beneficiarios` y `/personas` son literales en el mismo prefijo que `/{id:int}`**: la restricción de
  tipo es lo que las hace alcanzables (convención [005]).
- **`/beneficiarios` exige `gestionar`** porque sólo lo usa el registro. **`/personas` exige `consultar`**
  porque lo usa el filtro del listado, que Gerencia ve, y devuelve apellido, nombre y DNI que Gerencia ya ve
  en cada fila.

---

## §9 — Trampas que ya conocemos

1. **Los valores de enum escritos a mano en SQL.** `CK_Adelantos_Estado` lleva `0` a `3` de
   `EstadoAdelanto`, y `CK_Adelantos_TipoBeneficiario` lleva `1` y `2`. Reordenar un enum no falla al
   compilar. Va un test que intenta guardar una fila inválida en cada estado y verifica el rechazo, con una
   fila que viole **sólo** esa restricción (convención [009]: con dos violadas, SQL Server nombra una sola).
2. **`LEN(NULL)` en un `CHECK` deja pasar la fila.** `LEN(NULL) > 0` es `UNKNOWN`, y un `CHECK` sólo rechaza
   lo que es `FALSE`. Escrito como `[Estado] = 2 AND LEN([MotivoRechazo]) > 0`, un rechazado **sin**
   motivo pasaría: la rama del rechazo da `UNKNOWN`, las demás `FALSE`, y el `OR` entero da `UNKNOWN`. Cada
   `LEN` va detrás de su `IS NOT NULL`. Lo cubre el mismo test de restricciones con el motivo en `NULL`.
3. **`SumAsync` sobre un conjunto vacío.** En SQL, `SUM` sin filas es `NULL`. Un listado filtrado sin
   aprobados tiene que dar `$ 0,00` y no fallar: va un caso de integración que lo verifica.
4. **Las rutas literales junto a `{id:int}`** (§8).
5. **`ExecuteUpdateAsync` no pasa por el rastreador.** Los casos de uso **releen el detalle** al terminar
   (convención [006]).
6. **"Hoy" es el de Argentina en el servidor.** La fecha admitida se evalúa con
   `FechaHoyArgentina.Desde(reloj.GetUtcNow())`: un adelanto cargado a las 22 del 30/09 es del 30/09, y el
   piso de ese día es el 01/08. La pantalla propone el día local del navegador; si difiere, manda el
   servidor, y el rechazo trae `desde` y `hasta` para decir el rango.
7. **El motivo se recorta antes de medirlo y de guardarlo**, en el servidor y en la pantalla: `"   "` es
   vacío (FR-006, US4 esc. 3, US5 esc. 3).
8. **Un importe más grande que `decimal(18,2)`** no llega a la base como un `500`: `ImporteValido` lo
   rechaza como formato incorrecto (FR-008).
9. **El piso de la fecha cruza el año.** `PrimeraFechaAdmitida(01/01/2027)` es `01/12/2026` (spec §Edge
   Cases). Se construye con `AddMonths(-1)` sobre el primer día del mes, nunca restando uno al mes a mano.
   Lo mismo en la pantalla con `date-fns`.

---

## §10 — Qué se reutiliza sin tocar

| Pieza | De dónde viene | Se usa para |
|---|---|---|
| `Persona`, `Chofer`, `Transportista`, `EmpresaEmisora` | Módulos 2, 3 y 6 | se **leen**; no se les agrega columna, navegación ni método |
| `ErrorResponse`, políticas por permiso, `PermisoHandler` | Módulos 1 y 2 | los dos permisos |
| `CatalogoOpcionesMenu`, `SembradorInicial` | Módulos 1 y 2 | las dos entradas y el reparto a roles |
| `TimeProvider`, `FechaHoyArgentina` | Módulos 3 y 5 | el instante del historial y el día en curso |
| `ConversorInstanteUtc` | Módulo 2 | los instantes salen con `Z` (convención [002]) |
| `compartido/moneda.ts` | Módulo 5 | importes en pantalla |
| `formatearFecha`, `formatearInstante` de `compartido/fechas.ts` | Módulo 3 | fechas e instantes en pantalla. El archivo **sí** suma dos funciones (§10b) |
| `compartido/ui/*` | Módulos 7 y 8 | `Listado`, `Filtros`, `Paginacion`, `EstadoVacio`, `Estado`, `FilaNavegable`, `EnlaceDeFila`, `SeccionNumerada`, `BarraDeAcciones`, `Ficha`, `AsideDeFicha`, `LineaDeTiempo`, `Dialogo`, `Campo`, `Boton`, `Aviso`, `Callout` |

**Dependencias nuevas: ninguna. Variables de entorno nuevas: ninguna. Cambios de infraestructura:
ninguno.**

**Dos puntos de extensión del frontend compartido** reciben líneas, sin cambiar lo que ya hacen:

- `compartido/seccionesDeMenu.ts`: los dos códigos de menú en *Operación* (§7).
- `compartido/ui/Estado.tsx`: `aprobado` con el tono `rendido` y `rechazado` con el tono `anulado`.
  `pendiente` y `anulado` ya están. Sin estas dos líneas los dos estados caerían en `neutro`, que no es un
  error pero pinta igual a un aprobado que a un estado sin información. Que `rechazado` y `anulado`
  compartan tono está bien: los dos son finales y no cuentan, y la palabra los distingue (FR-039).

---

## §10b — "Hoy en `yyyy-MM-dd`" pasa a `compartido/fechas`

**Decisión** (spec §Clarifications, decisión de alcance): `enIso(fecha: Date): string` y
`hoyEnIso(): string` en **`frontend/src/compartido/fechas.ts`**, con sus casos en `fechas.test.ts`. Este
módulo lo usa para proponer la fecha del adelanto, y **las tres copias locales que ya existían pasan a
importarlo** (spec §Assumptions, cambios 1 a 3):

| # | Archivo | Módulo | Cambio | Qué fecha propone |
|---|---|---|---|---|
| 1 | `modules/facturacion/paginas/FichaFactura.tsx` | 6 | se borra `hoyEnIso` local y se importa | *Fecha de cobro* |
| 2 | `modules/facturacion/paginas/AltaFactura.tsx` | 6 | se borra `enIso` local y se importa; `sumarDias` la sigue usando | *Fecha de facturación* y el vencimiento a 30 días |
| 3 | `modules/liquidaciones/paginas/DetalleLiquidacion.tsx` | 9 | se borra `hoyEnIso` local y se importa | *Fecha de pago* |

**Por qué no cambia nada visible**: las tres copias son el mismo algoritmo —año, mes y día **locales**,
con ceros a la izquierda— escrito en tres formatos de línea. La compartida conserva exactamente eso,
**incluido su caso borde**: no pasa por `toISOString()`, que a las 22 en Argentina ya es el día siguiente en
UTC. Ése es el caso que `fechas.test.ts` fija, con el reloj en las 23:30 locales.

**Cómo se verifica**, con la regla de la convención [009]: **ninguna de las tres suites mira la fecha
propuesta**. `DialogoOrdenDePago.test.tsx` la verifica, pero recibe `hoy` por parámetro y no pasa por la
función de la pantalla. Así que antes de cada cambio se **agrega** un caso que la busca por su etiqueta
—`Fecha de cobro`, `Fecha de facturación`, `Fecha de pago`— con el reloj fijado, se lo ve en verde con la
copia local, y recién después se cambia la importación. Agregar un caso no es modificar uno: el resto de
cada suite sigue sin tocarse y es la prueba de que el refactor no cambió nada.

**Por qué ahora**: es la cuarta necesidad. La convención [009] pide llevarlo a `compartido` con la tercera,
y el sistema ya la había pasado sin hacerlo; una cuarta copia dejaría cuatro lugares donde corregir un error
de zona horaria que el Módulo 3 ya pagó una vez.

**Por qué no el piso de la fecha**: `primeraFechaAdmitida(hoy)` sólo la usa este módulo, así que vive en
`servicioAdelantos.ts`. Si otra pantalla la necesita, se discute con la segunda.

**Alternativa descartada**: una cuarta copia en `RegistrarAdelanto.tsx`, sin tocar los Módulos 6 y 9.
Descartada por decisión de alcance: el costo de tocar tres pantallas con sus suites como red es menor que
el de mantener cuatro copias.

---

## §11 — Qué no se puede verificar a mano, y qué se hace con eso

El Principio IV obliga a declararlo:

| Qué | Por qué no a mano | Test que lo cubre |
|---|---|---|
| Aprobar y rechazar el mismo adelanto en el mismo instante (FR-026) | hace falta que las dos transacciones se crucen; el recorrido prueba la segunda operación rechazada **en secuencia**, con dos navegadores | `ResolucionConcurrenteTests` |
| Dos anulaciones en el mismo instante (FR-032) | ídem | `AnulacionConcurrenteTests` |
| Los `CHECK` rechazan filas inválidas, incluido el motivo en `NULL` | ninguna pantalla permite intentarlo | `RestriccionesDeAdelantoTests` |
| Una invocación directa sin `confirmado` no rechaza ni anula | la pantalla siempre lo manda | `AprobacionYRechazoTests`, `AnulacionTests` |
| Una invocación directa con persona de otro tipo o chofer externo se rechaza (US1 esc. 11) | el desplegable no los ofrece | `RegistroTests` |
| El desplegable y el guardado aplican la misma regla | a mano se ve el resultado, no que sea la misma función | `ElegibilidadDeBeneficiarioTests` (unitario) y `BeneficiariosTests` (integración) |
| Las rutas literales no las captura `{id:int}` | si fallara, el desplegable y el filtro no cargarían, sin decir por qué | `RutasAdelantosTests` |
| El piso de la fecha el 1 de enero | habría que esperar a enero | `ReglasDeAdelantoTests` con el día por parámetro |

Todo lo demás se comprueba operando la aplicación, en el recorrido de `quickstart.md`.
