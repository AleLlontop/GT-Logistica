# Quickstart: Gestión de viajes (Módulo 5)

Cómo levantar el sistema con este módulo y comprobar, **operando la aplicación**, que las 7 historias
de usuario y los 15 criterios de éxito se cumplen. Sin leer código, sin mirar logs y sin consultas SQL
(Principio IV de la constitución).

El detalle de tablas y campos está en [data-model.md](./data-model.md); los textos exactos de pantalla,
en [contracts/README.md](./contracts/README.md).

---

## Requisitos previos

- Podman (desarrollo local) o Docker (CI).
- `.env` completo. Si venís de cualquier módulo anterior ya lo tenés: **este módulo no agrega ninguna
  variable de entorno, ningún volumen y ninguna dependencia**.
- **Los Módulos 3 y 4 recorridos**, o al menos: un transportista activo, **dos choferes activos** —uno
  con documentación en regla y otro con un documento vencido— y **dos vehículos activos y
  disponibles** —uno en regla y otro con un documento próximo a vencer—. Este módulo los consume y no
  tiene pantallas para crearlos.

Si no los tenés, cargalos primero desde **Choferes** y **Flota**: el recorrido de los pasos 8 a 10 los
necesita.

---

## Levantar el sistema

```bash
podman compose up -d          # o: docker compose up -d
```

La migración `Modulo5Viajes` se aplica sola al arrancar: crea la secuencia de numeración y las tres
tablas nuevas, y no toca ninguna de las existentes. El sembrador agrega los permisos
`viajes.gestionar` —para *Tráfico* y *Administrador del sistema*— y `viajes.consultar` —para los
cuatro roles—.

---

## Preparar las cuentas del recorrido

Hacen falta **tres** cuentas para verificar los dos niveles de acceso (FR-051, FR-052).

Entrá como `admin` y, desde **Gestión de usuarios**, asegurate de tener:

- un usuario con el rol *Tráfico* —por ejemplo `mlopez`, si venís del Módulo 3 ya está—;
- un usuario con el rol *Gerencia* —creá `rgerencia` si no existe—.

Se usan en los pasos 1 y 17.

---

## Recorrido de validación

### 1. El módulo aparece en el menú, y no ofrece lo mismo a los tres roles (FR-050, FR-051)

Ingresá como `admin`: en el menú tienen que estar **Viajes**, **Clientes** y **Totales**, además de
las entradas de los módulos anteriores.

Ingresá como el usuario de *Tráfico*: las tres entradas también están.

Ingresá como el usuario de *Gerencia*: las tres entradas están —tiene `viajes.consultar`— y **ninguna
de Choferes, Flota ni Usuarios**.

### 2. El padrón de clientes arranca vacío (US1 esc. 1, FR-009)

Como `admin` o *Tráfico*, entrá a **Clientes**. Tiene que decir, con todas las letras:

> `Todavía no hay clientes cargados. Registrá el primero para poder empezar a cargar viajes.`

No una tabla vacía sin explicación.

### 3. No se puede registrar un viaje sin clientes (US2 esc. 3)

Entrá a **Viajes** → *Nuevo viaje*. El formulario no te deja completar el alta y muestra el aviso con
el enlace a **Clientes**.

Volvé atrás.

### 4. Cargar dos clientes (User Story 1, SC-001)

En **Clientes** → *Nuevo cliente*, cargá:

| Razón social | CUIT | Teléfono | Email |
|---|---|---|---|
| Distribuidora del Litoral | 30-71234567-8 | 341 4001122 | compras@litoral.com.ar |
| Agroinsumos del Sur | 30712345670 | 291 4553311 | admin@agrosur.com.ar |

Comprobá de paso:

- **CUIT con guiones**: el primero se escribió con guiones y se guardó igual. Se normaliza a sólo
  dígitos antes de validar (FR-004).
- **CUIT mal formado**: probá `30-71234567-9` (dígito verificador incorrecto) y `123`. Los dos se
  rechazan con el motivo puntual **en el campo**, y no se crea nada (US1 esc. 4).
- **CUIT duplicado**: intentá cargar un tercer cliente con `30712345678`. Se rechaza informando el
  duplicado y no crea nada (US1 esc. 3, SC-003).
- **Editar sin conflicto**: abrí *Distribuidora del Litoral*, cambiale la razón social a
  `Distribuidora del Litoral S.A.` y guardá conservando su CUIT. No hay conflicto (US1 esc. 5).

### 5. Registrar viajes (User Story 2, SC-001, SC-002)

En **Viajes** → *Nuevo viaje*, cargá el primero:

- Cliente: `Distribuidora del Litoral S.A.`
- Fecha: hoy
- Origen: `Rosario` · Destino: `Córdoba`
- Remito: `5567` · Detalle: `Insumos agrícolas, 12 pallets` · Importe: `450000`

Al guardar, el viaje aparece en el listado como **Pendiente**, con un **número que asignó el sistema**.
Anotalo: es el `N`.

Comprobá:

- **El número no se edita**: abrí el viaje y editalo. El número se muestra y no hay forma de tocarlo,
  en ningún estado (US2 esc. 4, SC-002).
- **No hay chofer ni vehículo en el formulario**: no están ni en el alta ni en la edición. Se asignan
  después, desde su propia acción (US3 esc. 14, FR-019a).
- **Campos obligatorios**: intentá guardar un viaje sin cliente, sin origen, sin destino o sin fecha.
  Cada uno se marca con su motivo puntual y no se crea nada (US2 esc. 2).
- **Importe negativo**: probá `-100`. Se rechaza diciendo que el importe no puede ser negativo
  (US2 esc. 6).
- **Importe en cero**: se acepta. Cargá un segundo viaje con importe `0` para el paso 13
  (US2 esc. 7).
- **Sin remito**: se acepta; el remito se puede cargar después (US2 esc. 9).
- **Remito duplicado**: cargá un tercer viaje con remito `5567`. Se rechaza **nombrando el número del
  viaje que ya lo usa** —`N`— y no guarda (US2 esc. 8, SC-003).

### 6. Origen igual a destino, fecha pasada y fecha futura (US2 esc. 10, 11 y 12)

- Cargá un viaje con origen y destino `Rosario`. **Se guarda**, y la advertencia llega junto con la
  confirmación, sin ningún paso extra: existen servicios dentro de la misma localidad.
- Cargá un viaje con fecha de **la semana pasada**. Se acepta y el listado lo marca
  `Carga retroactiva` junto a la fecha, con la palabra y no sólo con un color.
- Cargá un viaje con fecha de **el mes que viene**. Se acepta como planificado, en `Pendiente`.

### 7. La numeración no se reutiliza (US2 esc. 5, FR-011)

Anulá uno de los viajes recién cargados —paso 14 explica la pantalla— y registrá otro. **El número
nuevo es el siguiente**, no el del anulado. El número de un viaje anulado no vuelve nunca.

### 8. Asignar chofer y vehículo habilitados (User Story 3, SC-001)

Abrí el viaje `N` → *Asignar chofer y vehículo*.

Comprobá primero **qué ofrece la pantalla**:

- Ningún chofer dado de baja, aunque tenga viajes históricos (US3 esc. 2).
- Ningún vehículo dado de baja ni **fuera de servicio** (US3 esc. 3).
- Arriba dice: `La documentación se valida contra la fecha del viaje: {fecha}`.

Asigná el chofer **en regla** y el vehículo **en regla**. Se guarda sin objeción, y los dos aparecen en
el listado y en la ficha, junto con el **transportista** del chofer (US3 esc. 1 y 9).

### 9. El bloqueo por documentación vencida (User Story 3, SC-004)

Volvé a *Asignar* y elegí el chofer con **un documento vencido**. El sistema **rechaza** la asignación
y el mensaje **nombra el documento** —tipo y número— que la bloquea. Nada se guardó: el viaje conserva
la asignación anterior (US3 esc. 4).

Probá también el camino de la **fecha futura**: en el viaje planificado del mes que viene, intentá
asignar un vehículo cuya VTV vence **antes** de esa fecha. Se rechaza igual, porque la validación corre
contra la fecha del viaje y no contra el día en que se carga (US3 esc. 6).

### 10. La advertencia que no bloquea, y la carga retroactiva (US3 esc. 5 y 13, SC-014)

- Asigná al viaje `N` el vehículo con un documento **próximo a vencer**. **La asignación se guarda** y
  la advertencia llega con el resultado, nombrando el documento afectado. Reasignar es reversible, así
  que no hace falta confirmar nada (FR-015a).
- En el viaje con **fecha de la semana pasada**, asigná una unidad cuyo documento estaba vigente
  entonces pero venció después. **Se acepta**: la documentación se evalúa contra la fecha del viaje.
  Es lo que permite asentar un viaje real con la unidad que efectivamente lo hizo (SC-014).

### 11. Cambiar la fecha de un viaje asignado (US2 esc. 14, FR-022a, SC-004)

Con el viaje `N` ya asignado, editá sus datos y movele la fecha a un día en que la documentación de la
unidad esté **vencida**.

El sistema **rechaza el cambio de fecha** indicando qué documento de qué unidad lo impide, y **no
guarda nada**: ni la fecha, ni el resto de los campos que hayas tocado, ni la asignación. Volvé al
viaje y comprobalo.

### 12. El ciclo de vida y la exclusividad (User Story 4, SC-005, SC-006)

Con el viaje `N` asignado y en `Pendiente`:

1. **Poner en curso.** El viaje pasa a `En curso`.
2. **Un viaje sin asignar no arranca.** Abrí otro viaje `pendiente` sin chofer ni vehículo: la acción
   *Poner en curso* está deshabilitada, con el motivo a la vista (US4 esc. 2).
3. **El chofer ocupado.** Asigná el **mismo chofer** a un segundo viaje pendiente e intentá ponerlo en
   curso. Se rechaza **indicando el número del viaje que lo ocupa** (US4 esc. 3).
4. **El vehículo ocupado.** Lo mismo con el vehículo (US4 esc. 4).
4b. **El ocupado también se verifica al reasignar.** Poné un tercer viaje `en curso` con otra unidad y
   después intentá **reasignarle** el chofer del viaje `N`, que sigue andando. Se rechaza con el mismo
   mensaje: la exclusividad vale en los dos caminos, no sólo al arrancar (US3 esc. 16, FR-026a).
5. **Dos pendientes con el mismo chofer, mismo día, se aceptan.** Un viaje `pendiente` no ocupa a
   nadie (US4/US3 esc. 12).
6. **No se puede saltear.** En un viaje `pendiente`, la acción *Rendir* no se ofrece (US4 esc. 10).

### 13. Rendir, y la confirmación del importe en cero (US4 esc. 5, 6 y 7, SC-007a)

Poné `En curso` el viaje con **importe cero** del paso 5 y pedí *Rendir*.

- **No lo rinde de una.** Muestra el diálogo: el viaje va a quedar cerrado con importe $ 0,00 y después
  no se va a poder corregir.
- **Cancelá.** El viaje sigue `En curso` con su importe en cero. Completale el importe y rendilo: ahora
  rinde directo, sin diálogo.

Rendí también el viaje `N`. Comprobá que:

- El historial registró el cambio.
- **Su chofer y su vehículo quedaron libres**: el segundo viaje del paso 12 ahora **sí** se puede poner
  en curso (US4 esc. 5).
- **La asignación se conserva** en el listado y en la ficha: liberar no es borrar (FR-037).

### 14. El viaje rendido es inmutable, para todos (US4 esc. 8 y 9, SC-013)

Abrí la ficha del viaje `N`, ya rendido. **No hay ninguna acción**: ni editar, ni reasignar, ni volver
a `en curso`, ni anular. La ficha lo dice, para que no parezca que faltan botones.

Repetilo **como `admin`**, con el rol *Administrador del sistema*: pasa exactamente lo mismo. Un viaje
rendido es inmutable para todos los roles (FR-018).

### 15. Anular un viaje (User Story 6, SC-007)

En un viaje `Pendiente` o `En curso`, pedí *Anular*:

1. **Sin motivo escrito, el botón de confirmar no se habilita** (US6 esc. 2).
2. **Cancelá**: el viaje queda exactamente igual, con su estado, su asignación y su historial sin
   cambios (US6 esc. 3).
3. Escribí el motivo y confirmá. El viaje queda `Anulado`, el historial lo registra, y su chofer y su
   vehículo quedan libres (US6 esc. 4).
4. **Desaparece del listado sin filtros**, y **reaparece con su motivo visible** al filtrar por
   `Anulado` (US6 esc. 5).
5. **No hay forma de devolverlo** a `pendiente` ni a `en curso` (US6 esc. 7).

### 16. El historial completo (US4 esc. 13, SC-006)

Abrí la ficha de cualquier viaje. El historial muestra **cada** cambio con estado anterior, estado
nuevo, usuario y fecha y hora, **empezando por el alta**. Se lee desde la ficha, sin consultas
técnicas.

### 17. Consultar, filtrar y buscar (User Story 5)

En **Viajes**:

- **Filtros combinados**: cliente + rango de fechas + estado + transportista a la vez. El listado
  muestra sólo los que cumplen **todas** las condiciones (US5 esc. 2).
- **Búsqueda sin acentos**: escribí `cordoba` sin acento. Aparecen los viajes con destino `Córdoba`.
  Probá también `CÓRDOBA` en mayúsculas y `litoral` para la razón social (US5 esc. 3 y 4).
- **Sin filtro de estado no se ven los anulados**, y el control **dice cuál está mostrando**:
  `Todos menos anulados`. Ninguna fila queda oculta en silencio (US5 esc. 9, FR-049).
- **Sin resultados**: aplicá un filtro que no coincida con nada. Sale el mensaje explícito, no una
  tabla vacía (US5 esc. 8).
- **Paginación**: con más de 20 viajes que cumplan los filtros, ves 20 filas, el total de coincidencias
  y cómo avanzar (US5 esc. 7).
- **La ficha completa** muestra todo lo que enumera FR-045 (US5 esc. 6).
- **El camino directo (SC-011)**: desde el ingreso, *Viajes → filtrar por cliente → leer el estado en
  la fila*. Comprobá que responde en qué estado está un viaje de un cliente concreto **sin pasar por
  ninguna otra pantalla y sin necesitar el número del viaje**.

**Como el usuario de *Gerencia*** (US7 esc. 6 y 7, SC-012):

- Abrí el listado y una ficha: los consultás bien, y **no ves** ninguna acción de alta, modificación,
  asignación, cambio de estado ni anulación.
- Entrá a **Clientes**: lo consultás y no podés tocar nada.

### 18. El transportista no se mueve cuando el chofer cambia (US5 esc. 5, SC-010)

Andá al Módulo 3 y **cambiale el transportista** al chofer del viaje `N`.

Volvé a **Viajes**: el viaje sigue figurando bajo el transportista de antes, en el listado **y** al
filtrar por transportista. Pero si en el Módulo 3 le corregís la **razón social** a ese transportista,
el viaje muestra la corregida: lo que queda fijo es a quién apunta el viaje, no sus datos (FR-028).

### 19. El chofer dado de baja no se borra del viaje (US3 esc. 11, FR-030)

Dale de baja en el Módulo 3 al chofer de un viaje **`pendiente`** ya asignado. Volvé al viaje:
**conserva la asignación** y la muestra señalada como `(inactivo)`, con la palabra y no sólo con un
color. No se borró ni se reasignó sola.

Ahora intentá **ponerlo en curso**: se rechaza indicando que el chofer está dado de baja, y arranca
recién después de reasignarlo (US4 esc. 14, FR-025). Un viaje que **ya estaba `en curso`** cuando le
dieron de baja al chofer sigue su camino normal hasta rendirse: el control es para arrancar, no para
interrumpir.

### 20. La baja de un cliente con viajes (US1 esc. 6 a 10, SC-009)

- Intentá dar de baja a `Distribuidora del Litoral S.A.`, que tiene viajes `pendiente` o `en curso`.
  **Se rechaza informando cuántos** dependen de él (US1 esc. 8).
- Ahora rendí o anulá todos sus viajes y volvé a intentarlo: **la baja procede**. Un cliente que dejó
  de operar tiene historial por definición, y el historial no lo bloquea (US1 esc. 6, SC-009).
- La baja pide confirmación explícita; **cancelá** y no cambia nada; confirmá y queda inactivo, deja
  de ofrecerse al registrar viajes y **no se borra** (US1 esc. 6 y 7).
- Intentá registrar un cliente nuevo con **el CUIT del que diste de baja**. Se rechaza indicando que
  hay que darlo de alta de nuevo (US1 esc. 10).
- **Dalo de alta de nuevo**: vuelve al listado por defecto, se ofrece otra vez y sus viajes históricos
  quedan intactos (US1 esc. 9).

### 21. Los totales del período (User Story 7, SC-008)

Entrá a **Totales**.

- **Sin rango elegido no calcula nada** y dice que falta elegirlo (US7 esc. 2).
- Elegí un rango que cubra los viajes cargados. Aparecen **dos cuadros**: uno por cliente y otro por
  transportista, con cantidad de viajes e importe acumulado (US7 esc. 1).
- **Los anulados no cuentan**: un cliente con 10 viajes en el período de los cuales 2 están anulados
  figura con **8** y con la suma de esos 8 (US7 esc. 3).
- **Cuadra con el listado**: filtrá el listado por ese cliente y ese rango, sumá los importes de las
  filas mostradas y comparalo con el total. Coinciden (US7 esc. 4, SC-008).
- **Un rango sin viajes** muestra el mensaje explícito de "sin resultados" (US7 esc. 5).
- **El listado no tiene fila de total**: los totales viven sólo acá (FR-046a).

### 22. El viaje demorado (US4 esc. 12, FR-039)

Este es el único paso que depende del calendario. Si tenés un viaje que pasó a `En curso` hace más de
**5 días corridos**, el listado lo muestra destacado como **Demorado**, con la palabra que lo explica,
y su estado **sigue siendo** `En curso`: el sistema no le cambia el estado a ningún viaje por sí solo.

Si no podés esperar cinco días, el test automatizado lo fija sin depender de la fecha (ver abajo).

---

## Tests automatizados

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

Los de integración levantan la aplicación contra el SQL Server del compose.

Hay ocho escenarios que los tests cubren mejor que el recorrido manual:

- **El viaje demorado** (FR-039): a mano hay que esperar cinco días. El test fija el instante del
  cambio de estado y verifica el borde exacto —a los 5 días todavía no, pasados los 5 sí— y que el
  estado guardado no cambia.
- **La concurrencia de la exclusividad** (SC-005, que exige 0%): dos operaciones simultáneas que ponen
  en curso el mismo chofer. Una gana y la otra recibe `chofer_ocupado` con el número del viaje que lo
  ocupa. A mano es imposible de provocar; la garantía la da un índice único filtrado en la base y este
  test es el que la ejercita.
- **El remito duplicado en simultáneo** (SC-003): lo mismo con dos altas al mismo tiempo.
- **Los valores numéricos de los tres índices filtrados**: inserta un viaje en cada uno de los cuatro
  estados y verifica que cada índice acepta y rechaza donde corresponde. Es lo que protege contra un
  reordenamiento futuro de `EstadoViaje`, que no fallaría al compilar (research §2).
- **Los cinco caminos de escritura sobre un viaje rendido** (SC-013): editar, asignar, poner en curso,
  rendir y anular, los cinco con rol *Administrador del sistema*. A mano se comprueba que los botones
  no están; el test comprueba que el guardado los rechaza igual si se los invoca.
- **La revalidación por cambio de fecha** (FR-022a, SC-004): que al rechazar el cambio **no quede
  guardado nada**, ni la fecha ni los otros campos del mismo `PUT`. A mano se ve el rechazo; el test
  verifica que la transacción no dejó rastro.
- **Los bordes de la habilitación contra la fecha del viaje** (FR-024): un documento que vence
  exactamente el día del viaje —que es `próximo a vencer`, no vencido— y un tipo con 0 días de aviso.
  Dependen del calendario a mano; en un test se fijan.
- **La equivalencia entre la regla en C# y la consulta en SQL** (convención [003]): el mismo dato tiene
  que dar el mismo veredicto en el dominio y en el filtro de asignables.

---

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| El menú no muestra **Viajes** | Esa cuenta no tiene ninguno de los dos permisos del módulo. Los cuatro roles tienen `viajes.consultar`; revisá que el usuario tenga rol asignado (FR-051) |
| Veo **Viajes** pero no hay botón de *Nuevo viaje* | Esa cuenta tiene sólo `viajes.consultar`. Es el comportamiento correcto para *Administración* y *Gerencia* (FR-052) |
| El formulario de viaje no me deja completar nada | No hay ningún cliente **activo** cargado. Cargá uno primero (US2 esc. 3) |
| No encuentro un viaje que sé que existe | Si lo anulaste, el listado no lo muestra por defecto. Poné *Estado* = `Anulado` (FR-044) |
| Al registrar un CUIT me dice que dé de alta a un cliente | Ese CUIT pertenece a un cliente dado de baja. El CUIT sigue ocupado; dalo de alta en vez de crear otro (FR-007) |
| No aparece el chofer que quiero asignar | Está dado de baja en el Módulo 3, y por eso no se ofrece. Su asignación en viajes viejos se conserva igual (FR-021, FR-030) |
| No aparece el vehículo que quiero asignar | Está dado de baja o su estado operativo **guardado** es `fuera de servicio`. Revisalo en **Flota** (FR-021) |
| Me rechaza una asignación por documentación y en Flota la unidad figura disponible | La validación corre contra **la fecha del viaje**, no contra hoy. Un viaje futuro puede caer después del vencimiento (FR-024, US3 esc. 6) |
| Me acepta asignar una unidad que hoy tiene la VTV vencida | Es un viaje con fecha pasada y la VTV estaba vigente **esa** fecha. Es lo esperado: la carga retroactiva tiene que poder decir la verdad (SC-014) |
| No me deja poner el viaje en curso y el chofer está libre | Fijate el vehículo: la exclusividad alcanza a los dos. El mensaje nombra el viaje que lo ocupa (FR-026) |
| *Poner en curso* está deshabilitado | Falta asignar chofer o vehículo. El motivo está a la vista, junto al botón (FR-025) |
| Pedí rendir y me pidió confirmar | El importe está en cero. Después de rendir no se puede corregir, así que la confirmación es previa (FR-038) |
| Un viaje rendido no tiene ningún botón | Es lo esperado, para todos los roles incluido el Administrador del sistema (FR-018, SC-013) |
| El total por transportista no cuadra con el del listado | Los viajes **sin chofer asignado** no tienen transportista y no aparecen en ese cuadro. Por cliente sí cuadran siempre (FR-046) |
| Un viaje sigue bajo un transportista que ya no es el del chofer | Es correcto: el viaje guarda el transportista que el chofer tenía **al asignarlo** (FR-028, SC-010) |
