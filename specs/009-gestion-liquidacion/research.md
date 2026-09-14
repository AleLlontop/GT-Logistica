# Research: Gestión de liquidación a transportistas (Módulo 9)

Decisiones técnicas del módulo, con la alternativa que se descartó y por qué. Cada sección responde a
una pregunta que el diseño tenía abierta después de leer la spec.

La spec llegó con **siete preguntas resueltas** en su sesión de clarificación y con las cinco
decisiones de alcance confirmadas, así que acá no queda ningún `NEEDS CLARIFICATION` de producto. Lo
que sigue es **cómo** se construye lo que la spec ya decidió.

---

## §1 — Dónde vive el vínculo viaje ↔ liquidación

**Decisión**: **una tabla propia, `LiquidacionViajes`**, con una fila por viaje agrupado y una columna
`Vigente`. Un **índice único filtrado sobre `ViajeId` donde `Vigente = 1`** es la garantía de que un
viaje no esté en dos liquidaciones vigentes (FR-014, SC-002).

```sql
CREATE UNIQUE INDEX IX_LiquidacionViajes_ViajeVigente
    ON LiquidacionViajes (ViajeId) WHERE [Vigente] = 1;
```

- **Generar y editar insertan filas con `Vigente = 1`.** Si dos operadores toman el mismo viaje, el
  índice corta la segunda inserción; el repositorio traduce la violación a una excepción de la capa de
  aplicación, que se convierte en el rechazo que nombra el viaje y la liquidación que lo tiene
  (convención [003]).
- **Quitar un viaje en la edición borra su fila.** La liquidación es `pendiente` y sin pagos: no hay
  nada que conservar en el vínculo: qué se quitó queda registrado en el historial de la edición (§3b).
- **Anular pone `Vigente = 0` en todas las filas de la liquidación.** El viaje queda libre para otra
  liquidación y la anulada **sigue sabiendo qué viajes agrupaba** (FR-028).

**Por qué no `Viajes.LiquidacionId`, que es lo que hizo el Módulo 6 con `FacturaId`**. Hay dos motivos,
y cualquiera de los dos alcanza:

1. **La spec prohíbe agregarles datos a los módulos anteriores.** *Assumptions* dice que este módulo
   "no les agrega pantallas, campos, estados ni reglas", y sus dos únicas excepciones son de
   presentación en el frontend (§11b). Una columna en `Viajes` es un campo nuevo en la tabla principal
   del Módulo 5, con su migración sobre una tabla ajena.
2. **Una columna escalar pierde el dato que FR-028 exige.** Al anular, `LiquidacionId` tendría que
   volver a nulo para liberar el viaje, y la anulada dejaría de saber qué agrupaba. El Módulo 6 no tuvo
   ese problema porque el detalle de la factura anulada queda impreso en su documento regenerado; acá
   no hay documento.

**Por qué el índice filtrado sí es la herramienta, cuando el Módulo 6 la descartó**: la convención
[006] dice que *la convención nombra el objetivo, no el mecanismo*. Allá el dato era una columna
escalar y la unicidad ya era estructural; acá el dato es una relación de muchos a muchos en el tiempo
—un viaje pasa por varias liquidaciones anuladas antes de quedar en una vigente—, y la unicidad **no**
es estructural. Es exactamente el caso para el que [005] escribió el índice único filtrado.

**`Vigente` repite un dato**, y hay que decirlo: vale `0` exactamente cuando la liquidación está
`anulada`. Se acepta porque un índice filtrado sólo puede mirar columnas de su propia tabla, y es lo
único que permite que la garantía viva en la base. Lo que impide que discrepe:

- se escribe **en la misma transacción** que cambia el estado (data-model §Anular), y ningún otro
  camino lo toca;
- un test de integración recorre liquidaciones en los tres estados y verifica que
  `Vigente = (Estado <> anulada)` fila por fila.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| `Viajes.LiquidacionId` + `UPDATE` condicional (patrón del Módulo 6) | Modifica el Módulo 5, que la spec excluye, y pierde los viajes de la anulada (FR-028) |
| Dos tablas: `LiquidacionViajes` sólo con los vigentes (índice único sin filtro) y `ViajesDeLiquidacionAnulada` como copia al anular | No repite ningún dato, pero convierte cada anulación en un movimiento de filas entre tablas y obliga a leer de dos lugares según el estado. Es más código para sostener lo mismo que una columna y un test |
| Validar la exclusividad sólo con una consulta previa | Deja la carrera abierta: dos operadores que generan a la vez para el mismo fletero pasan los dos la consulta. La garantía tiene que estar en la base (convención [005]) |

---

## §2 — Cómo se cierran las otras tres carreras

La exclusividad del viaje (§1) no es la única carrera del módulo. Hay cuatro más, y **las cuatro son
sobre la misma fila**, la de la liquidación:

| Carrera | Qué no puede pasar |
|---|---|
| Dos órdenes de pago simultáneas | que lo pagado supere el total (FR-043, SC-007) |
| Una edición mientras alguien registra un pago | que se cambien los viajes de una liquidación con pagos (FR-045, FR-048) |
| Una anulación mientras alguien registra un pago | que quede un pago sobre una liquidación anulada (FR-053) |
| Dos ediciones de la misma liquidación | que la segunda pise a la primera sin aviso, o que aplique una diferencia de viajes calculada sobre una composición que ya cambió y deje el total distinto de la suma (FR-048, spec §Clarifications) |

**Decisión**: **cada operación de escritura empieza con un `UPDATE` condicional sobre la fila de la
liquidación** y verifica que haya afectado exactamente una fila, dentro de su transacción:

```sql
-- Pagar (FR-037, FR-041, FR-043)
UPDATE Liquidaciones
   SET ImportePagado = ImportePagado + @importe,
       Estado = CASE WHEN ImportePagado + @importe = ImporteTotal THEN 1 ELSE 0 END
 WHERE Id = @id AND Estado = 0 AND ImportePagado + @importe <= ImporteTotal;

-- Editar (FR-045, FR-048)
UPDATE Liquidaciones SET ImporteTotal = @nuevoTotal, Version = Version + 1
 WHERE Id = @id AND Estado = 0 AND ImportePagado = 0 AND Version = @versionAbierta;

-- Anular (FR-053, FR-057)
UPDATE Liquidaciones SET Estado = 2, MotivoAnulacion = @motivo
 WHERE Id = @id AND Estado = 0 AND ImportePagado = 0;
```

Bajo el aislamiento por defecto de SQL Server, la segunda transacción se bloquea sobre la fila que la
primera está modificando y, al desbloquearse, **reevalúa el `WHERE` contra el dato ya confirmado**:
afecta cero filas y se rechaza. Es el mismo mecanismo que el Módulo 6 usa para marcar viajes (su
research §4), aplicado a otra fila.

**La carrera entre dos ediciones necesita un dato más: `Version`.** Las condiciones de estado y de pago
no la detectan, porque las dos ediciones ven la liquidación `pendiente` y sin pagos. Además, cada
edición calcula qué viajes quita y agrega comparando el conjunto que manda con la composición que lee;
si otra edición cambió esa composición entremedio, la diferencia queda calculada sobre un dato viejo.
La decisión (spec §Clarifications, FR-048) es **rechazar la segunda**:

- el detalle devuelve `version` y la pantalla de edición la manda de vuelta al guardar;
- el `UPDATE` de la edición exige `Version = @versionAbierta` y la sube en uno;
- cero filas con estado y pagos en regla significa que otro la editó: `409 liquidacion_modificada`.

**Sólo la edición sube `Version`.** Un pago o una anulación ya hacen fallar una edición posterior por
su propia condición —`ImportePagado = 0`, `Estado = 0`—, con un motivo más útil para quien edita ("ya
tiene órdenes de pago") que "cambió mientras tanto".

**Alternativa descartada**: `rowversion` de SQL Server. Cambia con cualquier `UPDATE` de la fila, así
que también detectaría las ediciones, pero es un valor binario que viaja al cliente y vuelve, y hace
fallar la edición por un pago con el mensaje genérico en vez del específico. Un entero que sólo mueve
la edición dice exactamente lo que se quiere verificar.

**Esto obliga a guardar `ImporteTotal` e `ImportePagado` en la liquidación**, y es la decisión que más
conviene justificar: los dos son sumas que se podrían calcular al leer —de los viajes y de las órdenes—,
y la convención [003] prefiere derivar. Se guardan porque:

- **son la condición del `UPDATE`**: sin ellos en la fila, cerrar la carrera del pago exige bloquear a
  mano con `UPDLOCK` en SQL escrito a mano, que es más frágil y no existe en ningún otro lugar del
  sistema;
- **la base los puede verificar**: `CHECK (ImportePagado <= ImporteTotal)` convierte a FR-043 en una
  garantía de la base y no en una validación que alguien puede saltear;
- **no hay proceso que los mantenga**: cambian sólo en las tres operaciones de arriba, siempre en la
  misma transacción que la fila que los justifica.

Lo que impide que discrepen de la suma: un test de integración que, después de generar, editar,
pagar y anular, compara `ImporteTotal` con la suma de los viajes y `ImportePagado` con la suma de las
órdenes (convención [003]: una regla que vive en dos lados lleva un test que compara los dos).

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Total y pagado derivados + `SELECT … WITH (UPDLOCK)` | SQL a mano fuera de EF, un patrón que el sistema no tiene, y FR-043 queda sin `CHECK` en la base |
| Token de concurrencia (`rowversion`) sobre la liquidación | Sólo detecta el conflicto si la operación modifica la fila. Una edición que quita un viaje de $100.000 y agrega otro de $100.000 no cambia ninguna columna y pasaría sin chequeo |
| Nivel de aislamiento `Serializable` en las cuatro transacciones | Resuelve las carreras a costa de bloqueos por rango sobre `OrdenesDePago` y `LiquidacionViajes`, y de interbloqueos que hay que reintentar. Mucha maquinaria para cuatro filas |

---

## §3 — Qué se guarda y qué se deriva

| Dato | Dónde | Por qué |
|---|---|---|
| `Estado` (`pendiente`, `pagada`, `anulada`) | **columna** | `anulada` es un hecho que no se puede derivar de nada. `pagada` se podría derivar de `ImportePagado = ImporteTotal`, pero guardarlo evita escribir el filtro dos veces —dominio y SQL—, que es el costo que [006] documentó para `vencida`. **Un `CHECK` impide que discrepe** (data-model §Liquidaciones) |
| `ImporteTotal`, `ImportePagado` | **columna** | condición de los `UPDATE` que cierran las carreras (§2) |
| Lo que resta pagar | **derivado**: `ImporteTotal − ImportePagado` | es una resta sobre dos columnas de la misma fila; se proyecta en la consulta y no hay nada que guardar |
| Fecha de generación (FR-017) | **derivada** del historial | es el instante de la entrada `generacion` de `CambiosDeLiquidacion`, convertido a fecha de Argentina. Una columna aparte copiaría un hecho ya registrado y podría discrepar de él (convención [005]) |
| Motivo de anulación | **columna** en `Liquidaciones` | se muestra en el detalle y en la entrada `anulacion` del historial, que lo lee de la liquidación: hay una sola anulación posible y no se copia |
| Número de liquidación y de orden de pago | **columna**, de una secuencia | §4 |

**El `CHECK` del estado** es lo que vuelve seguro guardar `pagada`:

```sql
CHECK (
     ([Estado] = 0 AND [ImportePagado] <  [ImporteTotal] AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 1 AND [ImportePagado] =  [ImporteTotal] AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 2 AND [ImportePagado] =  0              AND [MotivoAnulacion] IS NOT NULL)
)
```

Con él, FR-030 —`pagada` cuando no resta nada— y FR-053 —sólo se anula sin pagos— dejan de depender de
que el código lo haga bien: la base rechaza cualquier fila que las contradiga. Los valores `0`, `1` y
`2` están escritos a mano, con la misma trampa que ya documentaron los Módulos 5 y 6 (§12).

---

## §3b — Cómo registra el historial qué viajes cambió cada edición

**Decisión**: una tabla hija, **`CambiosDeLiquidacionViajes`**, con una fila por viaje que una edición
quitó o agregó: la entrada del historial, el viaje y si fue agregado (spec §Clarifications, FR-033).

- **Se escribe en la misma transacción que la edición**, con la misma diferencia de viajes que la
  edición aplica sobre `LiquidacionViajes`: las dos salen del mismo cálculo y no pueden discrepar.
- **Guarda la referencia al viaje, no su número copiado.** El número de viaje no cambia nunca (FR-011
  del Módulo 5), así que la referencia alcanza para mostrarlo, y un viaje nunca se borra.
- **No guarda importes ni la composición completa**: con la composición actual y los cambios de cada
  edición, la de cualquier momento anterior se reconstruye (spec §Assumptions).

**Por qué no quitar el borrado de `LiquidacionViajes` y marcar las filas quitadas**: una fila quitada que
queda en la tabla con otra marca multiplica los estados de `Vigente` —vigente, anulada, quitada— y la
consulta de disponibles tendría que distinguirlos. El vínculo dice qué agrupa la liquidación; el
historial dice cómo llegó ahí. Son dos preguntas y dos tablas.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| Una columna de texto en `CambiosDeLiquidacion` con `#13, #21` | Pierde la referencia al viaje —no se puede consultar ni enlazar— y obliga a parsear texto para mostrar dos listas |
| Guardar la composición completa anterior en cada edición | Es lo que la opción C de la clarificación ofrecía y se descartó: repite datos que ya se reconstruyen |

---

## §4 — Cómo se numeran la liquidación y la orden de pago

**Decisión**: **dos secuencias de SQL Server**, `NumeroDeLiquidacion` y `NumeroDeOrdenDePago`, que
alimentan una columna `Numero int` por `DEFAULT NEXT VALUE FOR`, con índice único. Es exactamente el
mecanismo del número de viaje del Módulo 5, con su misma trampa (§12).

**Formato visible**: `LQ-{número}` y `OP-{número}`, sin ceros a la izquierda —`LQ-12`, `OP-3`—. **El
formato lo arma el backend y viaja ya armado en el JSON** (`"numero": "LQ-12"`), porque también lo
usan los mensajes de rechazo ("ya está en la liquidación LQ-12"): escribirlo en C# y en TypeScript
serían dos formatos que se pueden separar.

**Por qué con prefijo, cuando el viaje se muestra como `#13`**: el detalle de la liquidación muestra
**tres** clases de número a la vez —la liquidación, sus viajes y sus órdenes de pago—. Tres `#` en la
misma pantalla no se distinguen.

**Alternativas descartadas**:

| Alternativa | Por qué no |
|---|---|
| `LQ-2026-07`, con el período, como sugieren los ejemplos del enunciado | No es único: la spec admite varias liquidaciones del mismo transportista y período, y de distintos transportistas en el mismo período |
| `LQ-2026-0001`, correlativo por año | Obliga a reiniciar la numeración cada año, que una secuencia no hace sola. La spec dice que los números del enunciado son ilustrativos |
| El `Id` de la tabla | Es un detalle de la base: puede saltar ante una transacción deshecha igual que la secuencia, pero además no es un dato de negocio y no conviene exponerlo como tal |

---

## §5 — Cómo se reconoce a un transportista externo

**Decisión**: **una consulta, sin tocar el padrón**:

```csharp
contexto.Transportistas.Where(t => t.Activo && t.Cuit != cuitDeLaEmpresaEmisora)
```

Los dos CUIT se guardan **ya normalizados a once dígitos** —el del transportista desde el Módulo 3
(FR-003), el de la empresa emisora desde el Módulo 6 con el mismo normalizador—, así que la comparación
es exacta sin transformar nada.

- **Sin empresa emisora configurada** (la fila no existe, Módulo 6 research §12) no se ofrece ningún
  transportista y la respuesta lo declara con `empresaEmisoraConfigurada: false`. La generación se
  rechaza con `400 empresa_emisora_no_configurada` también si se la invoca directamente (FR-001a).
- **El filtro del listado** usa la misma consulta sin la condición de `Activo` (FR-022). Sin empresa
  emisora no puede haber liquidaciones —nunca se pudo generar una—, así que la lista vacía no oculta
  nada.
- **Al guardar** se verifica otra vez que el transportista sea externo y esté activo: la pantalla no es
  la garantía.
- **La edición exige lo mismo** (FR-045, spec §Clarifications): una liquidación cuyo transportista se
  dio de baja o dejó de ser externo se sigue pagando y anulando, pero no se edita. Por eso la consulta
  de disponibles puede seguir validando externo y activo sin excepciones: nunca la llama la edición de
  una liquidación de ese transportista, porque esa edición no abre.

La consulta vive en el repositorio de este módulo, que **lee** `Transportistas` y `EmpresaEmisora` sin
escribirles nada. No se agrega ningún método a los repositorios de los Módulos 3 y 6.

---

## §6 — Qué viajes están disponibles

**Decisión**: el predicado de FR-004 escrito **entero en el árbol de la consulta**, no extraído a un
método (convención [003]):

```csharp
contexto.Viajes.Where(viaje =>
    viaje.TransportistaId == transportistaId &&
    (viaje.Estado == EstadoViaje.Rendido || viaje.Estado == EstadoViaje.Facturado) &&
    viaje.Fecha.Month == mes &&
    viaje.Fecha.Year == anio &&
    !contexto.LiquidacionViajes.Any(vinculo => vinculo.ViajeId == viaje.Id && vinculo.Vigente))
```

- **`TransportistaId` del viaje, no el del chofer** (FR-005): es la referencia que el Módulo 5 fija al
  asignar y no mueve después.
- **`Rendido` o `Facturado`** (spec §Clarifications): un viaje ya facturado al cliente se liquida igual.
  Un viaje facturado cuya factura se anula vuelve a `rendido` en el Módulo 6 y sigue disponible o
  liquidado según tenga o no un vínculo vigente: la factura y la liquidación no se enteran una de la
  otra.
- **Los viajes liberados por una anulación vuelven solos**: su fila quedó con `Vigente = 0` y la
  subconsulta sólo mira las vigentes (SC-003).
- **Orden**: fecha del viaje y número, ascendentes, como la lista de facturables del Módulo 6.

**La misma consulta sirve a la edición**: el endpoint de disponibles recibe transportista y período, y
la pantalla de edición los toma de la liquidación. No hay un segundo endpoint de disponibles por
liquidación.

**Sin selección en la generación** (FR-008): el cuerpo del `POST` lleva igual la lista de `viajeIds`
que se mostraron, porque FR-011 exige guardar **exactamente lo que se revisó** y no lo que haya
disponible al momento de guardar. El servidor verifica que cada uno siga disponible; no verifica que la
lista esté completa, porque un viaje rendido entre la lectura y el guardado no tiene que entrar (spec
§Edge Cases).

---

## §7 — Dónde se confirma cada paso irreversible

La convención [005] fija el criterio: **la confirmación vive en el backend cuando el paso no se puede
deshacer**, y el primer intento responde `409` sin cambiar nada.

| Operación | ¿Se deshace? | Dónde se confirma |
|---|---|---|
| Generar | sí: se edita o se anula | no pide confirmación (spec §Assumptions) |
| Editar | sí: un viaje quitado se vuelve a agregar | no pide confirmación |
| **Registrar una orden de pago** | **no**: la orden no se modifica ni se borra, y puede dejar la liquidación `pagada`, que es final | **backend**: `409 pago_requiere_confirmacion` con el importe y lo que va a restar pagar calculados por el servidor; el segundo intento lleva `confirmado: true` |
| **Anular** | **no**: `anulada` es final | **backend**: sin `confirmado: true` responde `409 anulacion_requiere_confirmacion` sin cambiar nada |

**Dos formas de llegar a `confirmado: true`, y la diferencia es a propósito**:

- **La orden de pago hace el viaje de ida y vuelta.** FR-040 pide mostrar "lo que va a restar pagar
  después de ella", y ese número lo tiene que calcular el servidor sobre el saldo actual: calcularlo en
  la pantalla con la ficha que se cargó hace diez minutos mostraría un saldo que otro pago ya cambió. El
  primer envío vuelve con `409` y los dos importes; la pantalla los muestra y reenvía confirmado.
- **La anulación manda `confirmado: true` desde el diálogo.** El diálogo de anulación **es** la
  confirmación explícita: pide el motivo y su botón nombra la consecuencia, *Anular liquidación*. No hay
  ningún dato del servidor que mostrar antes, y un segundo diálogo después del primero sería pedir dos
  veces lo mismo. El `409` sigue ahí para quien invoca la acción sin pasar por la pantalla (FR-056).

**Diferencia con el Módulo 6, anotada**: allá la anulación de una factura la confirma sólo la pantalla,
porque su research §11 consideró que el motivo obligatorio ya cubría la irreversibilidad. Acá FR-056
exige la confirmación **también cuando se invoca directamente**, y la convención [005] es explícita:
paso irreversible, confirmación en el backend. No se cambia el Módulo 6.

---

## §8 — Tabla de códigos HTTP

La regla [005] sin excepciones nuevas:

| Situación | Código | Código de error |
|---|---|---|
| Transportista, mes, año, fecha o importe faltante o mal formado | `400` | `datos_invalidos` con `campo` |
| Período fuera de las opciones (FR-002) | `400` | `periodo_invalido` |
| Empresa emisora sin configurar (FR-001a) | `400` | `empresa_emisora_no_configurada` |
| Transportista inexistente, propio o dado de baja (FR-001) | `400` | `transportista_no_liquidable` |
| Sin viajes (FR-012) | `400` | `sin_viajes` |
| Total en $0,00 (FR-012a) | `400` | `total_en_cero` |
| Viaje que no es del transportista, del período o no está rendido ni facturado (FR-011) | `400` | `viaje_no_liquidable`, con cada viaje y su motivo |
| Fecha de pago fuera del rango (FR-038) | `400` | `fecha_de_pago_fuera_de_rango`, con `desde` y `hasta` |
| Importe de pago que supera lo que resta (FR-037) | `400` | `importe_supera_saldo`, con `restaPagar` |
| Motivo de anulación vacío (FR-055) | `400` | `motivo_requerido` |
| **Viaje ya en otra liquidación vigente** (FR-011, FR-014) | `409` | `viaje_ya_liquidado`, con cada viaje y la liquidación que lo tiene |
| **Liquidación pagada, anulada o con órdenes de pago** al editar o anular (FR-045, FR-054) | `409` | `liquidacion_no_editable` / `liquidacion_no_anulable`, con `motivo` (`pagada`, `anulada`, `conOrdenesDePago`) y, en el último caso, cantidad y suma |
| **Transportista dado de baja o que dejó de ser externo** al editar (FR-045) | `409` | `liquidacion_no_editable` con `motivo: transportistaNoLiquidable` |
| **Liquidación modificada por otra edición** desde que se abrió (FR-048) | `409` | `liquidacion_modificada` |
| **Orden de pago sobre una liquidación pagada o anulada** (FR-042) | `409` | `liquidacion_no_pagable` |
| **Confirmación pendiente** (FR-040, FR-056) | `409` | `pago_requiere_confirmacion` / `anulacion_requiere_confirmacion` |

**`importe_supera_saldo` es `400` y no `409`** aunque dependa del saldo, que es un estado: el caso normal
es un importe mal tipeado contra un saldo que la pantalla ya mostraba, y se corrige cambiando el campo.
Cuando la causa es otro pago simultáneo, el mensaje igual dice cuánto resta, que es lo que hace falta
para corregirlo. Es el mismo criterio que el Módulo 6 aplicó al número de comprobante duplicado.

---

## §9 — Permisos y menú

**Decisión**: **dos permisos nuevos**, por permiso y nunca por rol (FR-063, convención [004]):

| Código | Qué habilita | Roles |
|---|---|---|
| `liquidaciones.gestionar` | generar, editar, anular, registrar órdenes de pago | Administración de la empresa, Administrador del sistema |
| `liquidaciones.consultar` | listado y detalle | los dos anteriores **más** Gerencia |

**Menú**: dos entradas nuevas en `CatalogoOpcionesMenu`, que ya traduce permiso → opción sin código
nuevo:

| Código | Texto | Ruta | Permiso |
|---|---|---|---|
| `consultar-liquidacion` | Consultar liquidación | `/liquidaciones` | `liquidaciones.consultar` |
| `generar-liquidacion` | Generar liquidación | `/liquidaciones/nueva` | `liquidaciones.gestionar` |

Los textos son los nombres que usa el enunciado para las dos opciones.

**Sección del menú**: las dos van a `Operación`, junto a *Viajes* y *Facturas*, con **una línea cada una
en `seccionesDeMenu.ts`**. Sin esa línea caerían en `Administración`, que la propia tabla describe como
"quién entra al sistema".

**Por qué los códigos no son `liquidaciones`**: `seccionesDeMenu.test.ts` usa justamente
`liquidaciones` como ejemplo de **código desconocido** que tiene que caer en la última sección. Mapear
ese código rompería un test que prueba otra cosa —que un módulo futuro aparece sin tocar el frontend—.
Con códigos propios el test sigue probando lo mismo y no se toca.

---

## §10 — Pantallas y endpoints

**Cuatro pantallas y dos diálogos**:

| Pantalla | Ruta |
|---|---|
| Listado de liquidaciones | `/liquidaciones` |
| Generar liquidación | `/liquidaciones/nueva` |
| Detalle de liquidación | `/liquidaciones/:id` |
| Editar liquidación | `/liquidaciones/:id/editar` |
| *Diálogo* Registrar orden de pago | sobre el detalle |
| *Diálogo* Anular liquidación | sobre el detalle |

La orden de pago es un diálogo y no una pantalla, por el mismo criterio con el que el Módulo 6 registra
el cobro: son dos campos sobre un registro que ya se está mirando.

**Ocho endpoints**, todos bajo `/api/liquidaciones`:

| Método y ruta | Permiso |
|---|---|
| `GET /transportistas?incluirInactivos=` | consultar |
| `GET /disponibles?transportistaId=&mes=&anio=` | gestionar |
| `GET /` | consultar |
| `GET /{id:int}` | consultar |
| `POST /` | gestionar |
| `PUT /{id:int}` | gestionar |
| `POST /{id:int}/anulacion` | gestionar |
| `POST /{id:int}/ordenes-de-pago` | gestionar |

**`/transportistas` y `/disponibles` son literales en el mismo prefijo que `/{id:int}`**: la ruta con
identificador lleva la restricción de tipo o las dos literales quedan inalcanzables, sin fallar al
compilar ni al arrancar (convención [005]).

**`GET /transportistas` exige sólo `consultar`**: la usan el filtro del listado —que Gerencia ve— y la
generación. Devuelve razón social y CUIT de transportistas que Gerencia ya ve en cada fila del listado.

**El `PUT` recibe el conjunto final de `viajeIds`**, no una lista de altas y bajas: el servidor calcula
la diferencia contra lo que la liquidación tiene. Es lo que la pantalla ya tiene en la mano y deja un
solo cuerpo que validar.

---

## §11 — Qué se reutiliza sin tocar

| Pieza | De dónde viene | Se usa para |
|---|---|---|
| `PaginaDe<T>` | Módulo 3 | el listado |
| `ErrorResponse`, políticas por permiso, `PermisoHandler` | Módulos 1 y 2 | los dos permisos |
| `CatalogoOpcionesMenu`, `SembradorInicial` | Módulos 1 y 2 | las dos entradas y el reparto a roles |
| `TimeProvider` y `FechaHoyArgentina` | Módulos 3 y 5 | el instante del historial, el día en curso y la fecha de generación |
| `ConversorInstanteUtc` | Módulo 2 | los instantes salen con `Z` (convención [002]) |
| `Transportista`, `Viaje`, `EmpresaEmisora` | Módulos 3, 5 y 6 | se **leen**; no se les agrega columna, navegación ni método |
| `compartido/moneda.ts`, `compartido/fechas.ts` | Módulos 3 y 5 | importes y fechas en pantalla |
| `compartido/ui/*` | Módulos 7 y 8 | `Listado`, `Filtros`, `Paginacion`, `EstadoVacio`, `Estado`, `FilaNavegable`, `TokenDeIdentificador`, `SeccionNumerada`, `BarraDeAcciones`, `Ficha`, `AsideDeFicha`, `LineaDeTiempo`, `Dialogo`, `Campo`, `Boton`, `Aviso` |

**Dependencias nuevas: ninguna. Variables de entorno nuevas: ninguna. Cambios de infraestructura:
ninguno.** Tampoco hay archivos: la liquidación no genera documento (spec §Assumptions).

---

## §11b — El formato del CUIT pasa a `compartido`

**Decisión**: **`frontend/src/compartido/cuit.ts`**, con `formatearCuit(cuit: string): string` y su
`cuit.test.ts`, al lado de `moneda.ts` y `fechas.ts`. Este módulo lo usa en tres lugares —el desplegable
de generación, la fila del listado y el aside del detalle— y **las dos copias locales que ya existían
pasan a importarlo** (spec §Assumptions, cambios 1 y 2):

| Archivo | Módulo | Cambio |
|---|---|---|
| `modules/choferes/transportistas/ListadoTransportistas.tsx` | 3 | se borra la función local `formatearCuit` y se importa la compartida |
| `modules/viajes/clientes/ListadoClientes.tsx` | 5 | ídem |

**Por qué no cambia nada visible**: las dos copias son el mismo algoritmo escrito de dos formas —un
`if` con retorno temprano y un ternario—: once caracteres salen `XX-XXXXXXXX-X`; cualquier otro largo
sale sin tocar. La compartida conserva exactamente esa regla, **incluido el caso de largo distinto**, que
no tiene que "arreglarse" ahora: un CUIT guardado siempre tiene once dígitos (FR-003 del Módulo 3), y
cambiar el caso borde sería un cambio de comportamiento disfrazado de refactor.

**Cómo se verifica**: `cuit.test.ts` fija los dos casos, y **las suites existentes de las dos pantallas
no se tocan**. Consultan por texto, así que si el CUIT saliera distinto fallarían: seguir en verde sin
modificarlas es la prueba de que el comportamiento no cambió (convención [007]).

**Salvedad encontrada en `/speckit-analyze`**: `ListadoTransportistas.test.tsx` busca el CUIT con
guiones, pero `ListadoClientes.test.tsx` no lo mira en ningún caso. Para *Clientes* la red no existía:
antes del refactor se le agrega un caso que lo busca, se lo ve en verde con la función local, y recién
después se cambia la importación (T037). Agregar un caso no es modificar uno.

**Por qué en este módulo y no después**: es la tercera vez que se necesita. Con dos copias todavía se
podía discutir si era el mismo concepto; con tres, una copia más es la que deja al sistema con tres
lugares donde cambiar el formato.

**Alternativa descartada**: una tercera copia en `servicioLiquidaciones.ts`, que es lo que proponía la
primera versión de este plan para no tocar los Módulos 3 y 5. Descartada por decisión de alcance
(spec §Clarifications): el costo de tocar dos pantallas con sus tests como red es menor que el de
mantener tres copias.

---

## §12 — Trampas que ya conocemos

1. **Los valores de enum escritos a mano en SQL.** El `CHECK` del estado (§3) y los `UPDATE` condicionales
   (§2) usan `0`, `1` y `2` de `EstadoLiquidacion`. EF traduce los `UPDATE` desde el enum, pero el
   `CHECK` va como texto: reordenar el enum no falla al compilar y deja el `CHECK` protegiendo el estado
   equivocado. Va un test que intenta guardar una fila inválida en cada estado y verifica el rechazo.
2. **El número sale de la secuencia, no del constructor.** `Numero` sin `required` y con `private set`,
   y `ValueGeneratedOnAdd` en la configuración: si EF manda el `0` del constructor en el `INSERT`, el
   `DEFAULT` de la columna no se aplica nunca (Módulo 5, `Viaje.Numero`).
3. **Las rutas literales junto a `{id:int}`** (§10).
4. **`ExecuteUpdateAsync` no pasa por el rastreador.** Después del `UPDATE` condicional, la entidad
   rastreada —si la hay— no refleja el cambio. Los casos de uso **releen la ficha** al terminar para
   armar la respuesta (convención [006]: la respuesta de una escritura se relee).
5. **Transacción deshecha con entidades rastreadas.** Al rechazar por un viaje ya liquidado o por una
   liquidación que dejó de ser editable, se hace `rollback` **y** `ChangeTracker.Clear()`, igual que
   `RepositorioFacturas.EmitirAsync`: si no, un reintento en el mismo alcance arrastra las filas
   insertadas.
6. **El filtro de período sobre `Viaje.Fecha`.** `Fecha.Month` y `Fecha.Year` sobre `DateOnly` traducen
   a `DATEPART` en SQL Server —el Módulo 6 ya lo usa—, pero sólo escritos en el árbol de la consulta.
7. **Traducir la violación de índice por nombre.** La tabla `LiquidacionViajes` tiene un solo índice
   único filtrado y `Liquidaciones` otro por número. El repositorio distingue cuál se violó por el
   nombre en el mensaje de SQL Server (patrón de `RepositorioFacturas`), para que la carrera por un viaje
   no llegue como un error genérico.
8. **La fecha de generación en Argentina.** El historial guarda el instante en UTC. La fecha que se
   muestra y contra la que se valida la fecha de pago (FR-038) es la de Argentina: una liquidación
   generada a las 22 del 31/07 es del 31/07, no del 01/08. Se convierte con el mismo criterio que
   `FechaHoyArgentina`, en un solo lugar.

---

## §13 — Qué no se puede verificar a mano, y qué se hace con eso

El Principio IV obliga a declararlo en vez de fingir que sí:

| Qué | Por qué no a mano | Test que lo cubre |
|---|---|---|
| Dos operadores generan a la vez liquidaciones con un viaje en común (SC-002) | hace falta que las dos transacciones se crucen en el mismo instante | `GeneracionConcurrenteTests` |
| Dos órdenes de pago simultáneas que juntas superan el total (SC-007) | ídem | `PagoConcurrenteTests` |
| Una edición o una anulación que se cruza con un pago | ídem | `EdicionConcurrenteTests`, `AnulacionConcurrenteTests` |
| Que dos ediciones que se cruzan dentro de la misma transacción no dejen el total distinto de la suma | el recorrido prueba la segunda edición rechazada —guardando una después de la otra, paso 25—, pero no el cruce en el mismo instante | `EdicionConcurrenteTests` |
| `ImporteTotal` e `ImportePagado` coinciden con las sumas, y `Vigente` con el estado | a mano se ve el total en pantalla, pero no la fila contra la suma | `CoherenciaDeLiquidacionTests` |
| El `CHECK` del estado rechaza filas inválidas | no hay pantalla que permita intentarlo | `RestriccionesDeLiquidacionTests` |

Todo lo demás —incluidos los rechazos por viaje ya liquidado, por saldo y por estado— se comprueba
operando la aplicación, en el recorrido de `quickstart.md`.
