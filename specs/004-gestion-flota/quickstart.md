# Quickstart: Gestión de flota (Módulo 4)

Cómo levantar el sistema con este módulo y comprobar, **operando la aplicación**, que las 6 historias
de usuario y los 14 criterios de éxito se cumplen. Sin leer código, sin mirar logs y sin consultas SQL
(Principio IV de la constitución).

El detalle de tablas y campos está en [data-model.md](./data-model.md); los textos exactos de pantalla,
en [contracts/README.md](./contracts/README.md).

---

## Requisitos previos

- Podman (desarrollo local) o Docker (CI).
- `.env` completo. Si venís del Módulo 3 ya lo tenés: **este módulo no agrega ninguna variable**. Los
  adjuntos de la documentación de vehículos van al mismo volumen que los de choferes
  (`GT_ARCHIVOS_RUTA`).
- El Módulo 3 recorrido, o al menos **un transportista activo cargado**. Sin transportistas no se
  puede registrar ninguna unidad (US2 esc. 7), y este módulo no tiene pantalla para crearlos.

---

## Levantar el sistema

```bash
podman compose up -d          # o: docker compose up -d
```

La migración se aplica sola al arrancar. Además de crear las tres tablas nuevas y agregarle el ámbito
al catálogo de tipos de documentación, siembra los permisos `flota.gestionar` —para *Tráfico* y
*Administrador del sistema*— y `flota.tipos.gestionar` —sólo para *Administrador del sistema*—.

---

## Preparar dos usuarios para el recorrido

Este módulo es el primero que **distingue niveles de acceso adentro**, así que hacen falta las dos
cuentas para verificarlo.

Entrá como `admin` y, desde **Gestión de usuarios**, asegurate de tener un usuario con el rol
*Tráfico* —por ejemplo `mlopez`, si venís del Módulo 3 ya está—. Se usan en el paso 12.

---

## Recorrido de validación

### 1. El módulo aparece en el menú, y no igual para los dos roles (FR-039)

Ingresá como `admin`: en el menú tienen que estar **Flota** y **Tipos de vehículo**, además de las
entradas de los módulos anteriores.

Ingresá como `mlopez` (Tráfico): tiene que estar **Flota** y **no** tiene que estar **Tipos de
vehículo**.

Con la sesión de `mlopez`, escribí `/tipos-vehiculo` en la barra de direcciones: el sistema lo rechaza.
El menú oculta, pero quien decide es el servidor.

### 2. Los tipos de documentación viejos quedaron con ámbito chofer (FR-017c)

Como `admin`, abrí **Tipos de documentación**. Los tipos que ya tenías del Módulo 3 —licencia,
psicofísico, los que hayas cargado— tienen que figurar todos con ámbito **Chofer**, sin excepciones y
sin que nadie los haya tocado.

Abrí un chofer con documentación cargada y comprobá que su estado sigue siendo el mismo de antes: la
migración no cambió el comportamiento de ningún documento existente.

### 3. El catálogo de tipos de vehículo arranca vacío (US1 esc. 1, FR-036)

Como `admin`, abrí **Tipos de vehículo**. Tiene que verse el mensaje *"Todavía no hay tipos de vehículo
cargados. Cargá el primero para poder registrar unidades."*, **no** una tabla vacía sin explicación.

### 4. No se puede registrar un vehículo sin tipo cargado (US2 esc. 6)

Como `mlopez`, abrí **Flota → Registrar unidad**. El sistema tiene que informar que primero hay que
cargar un tipo de vehículo y **no** dejar completar el alta.

### 5. Cargar tipos de vehículo (User Story 1, SC-001)

Como `admin`, en **Tipos de vehículo** cargá dos: `Tractor` y `Semirremolque`.

- Intentá cargar `Tractor` de nuevo: tiene que rechazarlo diciendo que ya existe (US1 esc. 3).
- Cargá un tercero, `Utilitario`, y dalo de baja: queda inactivo, deja de ofrecerse y **no desaparece
  de la lista de tipos** (US1 esc. 4).
- Editá `Utilitario` ya inactivo: el formulario avisa que está inactivo y suma **Dar de alta**. Dale
  de alta y comprobá que vuelve a ofrecerse al registrar una unidad (US1 esc. 6, FR-009).

### 6. Cargar tipos de documentación de vehículo (FR-017)

Como `admin`, en **Tipos de documentación** cargá dos con ámbito **Vehículo**:

| Nombre | Días de aviso | Ámbito |
|---|---|---|
| VTV | 30 | Vehículo |
| Seguro del vehículo | 15 | Vehículo |

El campo **Ámbito** es obligatorio: intentá guardar sin elegirlo y tiene que rechazarlo.

### 7. Registrar un vehículo (User Story 2, SC-001, SC-003, SC-003a)

Como `mlopez`, en **Flota → Registrar unidad** cargá `AB123CD`, marca `Scania`, modelo `R450`, tipo
`Tractor`, transportista `G&T Logística S.A.`.

Cuatro cosas para comprobar en este paso:

1. **El estado operativo sólo ofrece "Fuera de servicio"**, con la explicación de que una unidad sin
   documentación no puede quedar disponible (US2 esc. 8, FR-014a).
2. Sin elegir tipo, o sin elegir transportista, el sistema lo rechaza nombrando el campo (US2 esc. 3 y
   4).
3. Con la patente vacía o con `HOLA` el sistema marca el campo con el motivo puntual: la patente tiene
   que tener formato `ABC123` o `AB123CD` (US2 esc. 5, FR-004).
4. Guardado, la unidad aparece en el listado con su tipo, su transportista, estado **Fuera de
   servicio** y documentación **Sin documentación** (US2 esc. 1 y 8).

### 8. La patente se normaliza y no admite duplicados (US2 esc. 2, SC-002)

Con `AB123CD` ya registrada, intentá registrar `ab 123 cd` y después `AB-123-CD`. Las dos veces tiene
que rechazarlo diciendo que esa patente ya está registrada, y **no** crear ningún vehículo.

Registrá ahora una segunda unidad escribiendo la patente en minúsculas y con guiones —`ef-456-gh`— y
comprobá que en el listado figura como `EF456GH` (FR-003).

Registrá una tercera, `IJ789KL`, asignada a un transportista terciarizado distinto: se usa en el
paso 11.

### 9. Cargar documentación y ver el estado calculado (User Story 3, SC-004)

Abrí la ficha de `AB123CD` → **Agregar documento**. Cargá tres, y elegí las fechas de vencimiento **en
relación al día de hoy** para no depender del calendario:

| Tipo | Vencimiento | Estado que tiene que mostrar |
|---|---|---|
| VTV | hoy + 90 días | **Vigente** (faltan más de 30, que son sus días de aviso) |
| Seguro del vehículo | hoy + 10 días | **Próxima a vencer** (entra en la ventana de 15) |
| VTV | hoy − 5 días | **Vencida** |

Comprobá al pasar:

- **En ningún momento el formulario deja elegir ni editar el estado** (US3 esc. 6, FR-021, SC-004).
- El selector de tipo **sólo ofrece VTV y Seguro del vehículo**: los tipos de chofer no aparecen
  (US3 esc. 12, FR-017a).
- Con una fecha de vencimiento anterior o igual a la de emisión, lo rechaza (US3 esc. 2).
- Un archivo que no sea PDF, JPG o PNG, o de más de 10 MB, lo rechaza indicando el motivo y **no
  guarda el documento** (US3 esc. 8, FR-025).
- Guardá el seguro **sin archivo**: el documento queda cargado, la ficha lo muestra con *"Sin archivo
  adjunto"*, y el estado general del vehículo **no cambia por eso** (US3 esc. 13, FR-016a).
- Corregí el número de un documento y guardá: queda actualizado con las mismas validaciones y el
  estado se recalcula (US3 esc. 9).

### 10. Renovación, historial y eliminación (US3 esc. 7, 10 y 11, SC-010)

En la ficha de `AB123CD` tenés dos VTV: una vencida hace 5 días y una que vence en 90.

- La que vence en 90 días tiene que figurar como la vigente del tipo; la vencida tiene que aparecer
  **atenuada y con la palabra "Histórico"**, y **no** afectar el estado general (FR-024).
- Pedí eliminar la VTV histórica: tiene que pedir confirmación **advirtiendo que no se puede
  deshacer**. Cancelá: **nada cambia** (US3 esc. 11, SC-009). Confirmá: el documento y su archivo
  desaparecen de la ficha (US3 esc. 10).

### 11. Consultar y filtrar la flota (User Story 4, SC-003b, SC-006)

En **Flota**, con las tres unidades cargadas:

- Sin filtros, el listado muestra patente, marca, modelo, tipo, transportista, estado y estado de
  documentación de cada una (US4 esc. 1).
- Filtrá por el transportista terciarizado: tiene que ver **sólo** `IJ789KL`. Filtrá por G&T Logística
  S.A.: **sólo** la flota propia (US4 esc. 3, SC-003b).
- Combiná transportista + tipo + estado + estado de documentación: el listado muestra únicamente lo
  que cumple **todas** las condiciones a la vez (US4 esc. 2).
- Con un filtro aplicado, el control tiene que **decir explícitamente qué está filtrando** (US4
  esc. 10, FR-037).
- Con un filtro que no coincide con nada, tiene que verse *"Ningún vehículo coincide con los filtros
  aplicados."*, no una tabla vacía (US4 esc. 7, FR-036).
- Abrí la ficha de una unidad: patente, marca, modelo, tipo, transportista, estado y **todos** sus
  documentos con tipo, número, fechas y estado (US4 esc. 5). El archivo adjunto se abre desde ahí
  (US4 esc. 6): tocá **Abrir archivo** y comprobá que el PDF **se ve en la pestaña nueva**, no que se
  descarga para abrirlo después. Lo mismo vale para la documentación de un chofer, que usa el mismo
  mecanismo.

### 12. La documentación vencida saca la unidad de "disponible", sola (User Story 4 esc. 4 y 11, SC-006)

Este es el paso central del módulo y conviene hacerlo despacio.

1. En la ficha de `EF456GH`, cargá **un solo** documento: `Seguro del vehículo` con vencimiento
   **hoy + 60 días**. Su estado general pasa a **En regla**.
2. Editá la unidad y ponela en **Disponible**: ahora sí lo acepta.
3. Filtrá el listado por estado **Disponible**: `EF456GH` aparece.
4. Ahora corregí ese seguro y ponele vencimiento **ayer**. **No toques el estado operativo del
   vehículo.**
5. Volvé al listado: `EF456GH` figura como **Fuera de servicio**, y filtrando por **Disponible** ya no
   aparece — sin que nadie haya editado el estado de la unidad (US4 esc. 11, FR-014).
6. Corregí el seguro y devolvele el vencimiento a hoy + 60 días. La unidad vuelve a figurar como
   **Disponible**, otra vez sin que nadie la edite (SC-010).

Con `AB123CD`, que tiene documentación vencida, probá editarla y ponerla en **Disponible**: el sistema
lo impide **e informa qué documentación se lo impide** (US6 esc. 7, FR-014a).

### 13. El panel de vencimientos (User Story 5, SC-005, SC-007)

Abrí **Flota → Vencimientos**, a **un solo paso** desde el módulo (SC-007).

- Tienen que figurar las unidades activas con al menos un documento próximo a vencer o vencido,
  diciendo **de qué documento se trata** y **en cuántos días vence o hace cuántos venció** (US5 esc. 1).
- Seleccioná una fila: llega a la ficha del vehículo con la documentación visible (US5 esc. 2).
- **Todo vehículo que el filtro "disponible" excluyó por documentación vencida o ausente tiene que
  estar acá** (US5 esc. 4, SC-006). Comprobalo contra la lista del paso 12.
- Cargá la renovación de un documento alertado con vencimiento **fuera** de la ventana de aviso: la
  unidad deja de aparecer por ese documento, **sin que nadie borre ni edite el anterior** (US5 esc. 3,
  SC-010).
- Renová todo y volvé al panel: tiene que decir *"No hay vencimientos pendientes."* (US5 esc. 5).

### 14. Bajas con dependencias (User Story 1 esc. 5, User Story 6 esc. 12, SC-008)

- Como `admin`, intentá dar de baja el tipo `Tractor`: se rechaza informando **cuántos vehículos** lo
  están usando, y el tipo **no se borra** (US1 esc. 5, FR-010).
- Intentá dar de baja el tipo de documentación `VTV`: se rechaza informando cuántos documentos lo usan,
  **contando los de choferes y los de vehículos** (FR-017b).
- Intentá cambiarle el ámbito a `VTV`: se rechaza informando cuántos documentos ya lo usan (FR-017d).
  Creá un tipo nuevo sin documentos, cambiale el ámbito y comprobá que **eso sí se permite**.
- Como `mlopez`, en **Transportistas**, intentá dar de baja el transportista terciarizado dueño de
  `IJ789KL`: se rechaza informando **cuántos choferes y cuántos vehículos activos** dependen de él
  (US6 esc. 12, FR-008d). Ésta es la regla que el Módulo 3 no tenía.

### 15. Modificar, reasignar, dar de baja y reactivar (User Story 6, SC-003c, SC-009)

- Corregí la marca y el modelo de `IJ789KL` y guardá: queda actualizado y el sistema lo confirma
  (US6 esc. 1).
- Intentá ponerle la patente `AB123CD`: se rechaza. Guardá conservando su propia patente: **no** hay
  conflicto (US6 esc. 2).
- **Reasignala a G&T Logística S.A.**: pasa a la flota propia y **su documentación se conserva
  íntegra** (US6 esc. 3, SC-003c). Intentá dejarla sin transportista o asignarle uno inactivo: se
  rechaza (US6 esc. 4).
- Pedí darla de baja: pide **confirmación explícita**. Cancelá: nada cambia (US6 esc. 6, SC-009).
  Confirmá: desaparece del listado sin filtros y **vuelve a verse con el filtro `Dado de baja`**, con
  su documentación y sus archivos intactos (US6 esc. 5 y 8, FR-008).
- Con la unidad dada de baja, intentá registrar `IJ789KL` como nueva: se rechaza **indicando que hay
  que reactivar la unidad existente**, no diciendo simplemente que ya existe (US6 esc. 10, FR-008f).
- Reactivala desde su ficha, con confirmación: vuelve al listado por defecto y al panel de
  vencimientos si corresponde, con toda su documentación (US6 esc. 9, FR-008e).
- Para US6 esc. 11: dale de baja a la unidad, después dale de baja a su tipo de vehículo desde `admin`
  —si el tipo no tiene otros vehículos— e intentá reactivarla: el sistema tiene que **pedir un tipo
  activo** antes de completar la reactivación.

### 16. Un vehículo dado de baja no alerta ni ensucia la vista diaria (FR-031, FR-035)

Con una unidad dada de baja que tenga documentación vencida:

- **No** aparece en el listado sin filtros (FR-031).
- **No** aparece en el panel de vencimientos (FR-035).
- Su documentación se consulta filtrando por `Dado de baja` y está intacta (US6 esc. 8).
- Al reactivarla, **vuelve a alertar sola**, sin recargar nada (edge case de la spec).

### 17. Paginación (US4 esc. 9, FR-032)

Cargá vehículos hasta pasar de 20 —o filtrá de modo que califiquen más de 20—. El listado tiene que
mostrar la primera página con **20 filas**, el **total de coincidencias** y la forma de avanzar. Al
cambiar de página, ninguna unidad aparece dos veces ni desaparece.

---

## Tests automatizados

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

Los de integración levantan la aplicación contra el SQL Server del compose.

Hay ocho escenarios que los tests cubren mejor que el recorrido manual:

- **Los bordes del cálculo de estado**: vencer exactamente hoy —que es `proximaAvencer`, no
  `vencida`— y un tipo con 0 días de aviso. A mano dependen de la fecha del día; en un test se fijan.
- **La equivalencia entre la regla en C# y la consulta en SQL**: el mismo dato tiene que dar el mismo
  estado calculado en el dominio y en el filtro del listado (convención [003] de `CLAUDE.md`).
- **El estado operativo derivado** (FR-014): que una unidad guardada como `disponible` con un
  documento vencido se devuelva y se filtre como `fueraDeServicio`, y que vuelva sola al renovar. A
  mano se comprueba en el paso 12, pero el test lo fija sin depender del calendario.
- **Que el filtro `disponible` no devuelva nunca una unidad con documentación vencida o ausente**
  (SC-006, que exige 0%).
- **La normalización y validación de patentes** (FR-003, FR-004): mayúsculas, espacios, guiones y
  puntos; los dos formatos válidos y los inválidos conocidos.
- **El documento vigente de cada tipo** (FR-024): con dos documentos del mismo tipo manda el de
  vencimiento más lejano; **con la misma fecha de vencimiento**, manda el cargado último. El empate a
  mano es casi imposible de armar y es justo el caso que deja el listado inestable.
- **La migración del ámbito** (FR-017c): que los tipos de documentación preexistentes queden en
  `chofer` y que ningún documento de chofer cambie de estado por la migración.
- **La atomicidad de la carga** (FR-029): con el almacenamiento forzado a fallar, que **no** quede el
  documento creado; y que al corregir un documento con un archivo de reemplazo que falla, el documento
  conserve intacto el adjunto anterior. Es el único requisito del módulo **sin escenario de
  aceptación**, porque describe una falla que nadie puede provocar desde la pantalla: la spec lo
  declara explícitamente y su verificación vive acá.

---

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| El menú no muestra **Flota** | Esa cuenta no tiene el rol *Tráfico* ni *Administrador del sistema*. Es el comportamiento correcto (FR-039) |
| El menú muestra **Flota** pero no **Tipos de vehículo** | Es correcto: el catálogo de tipos es sólo del Administrador del sistema (FR-039) |
| El formulario de vehículo no deja completar nada | Falta un tipo de vehículo activo o un transportista activo. Cargá lo que falte primero (US2 esc. 6 y 7) |
| El alta no me deja elegir "Disponible" | Es lo esperado: una unidad recién registrada no tiene documentación, y sin documentación no puede quedar disponible (FR-013, US2 esc. 8) |
| Una unidad figura **Fuera de servicio** y yo la había dejado disponible | Tiene documentación vencida o le falta. El estado mostrado se deriva al consultar; al renovar vuelve sola (FR-014) |
| No encuentro un vehículo que sé que existe | Si lo diste de baja, el listado no lo muestra por defecto. Poné *Estado del vehículo* = `Dado de baja` (FR-031) |
| Al registrar una patente me dice que reactive una unidad | Esa patente pertenece a un vehículo dado de baja. La patente sigue ocupada; reactivá la unidad en vez de crear otra (FR-008f) |
| Un vehículo figura **En regla** y le veo un documento vencido en la ficha | Ese documento está marcado **Histórico**: hay una renovación posterior del mismo tipo, y es la que cuenta (FR-024) |
| No puedo elegir el tipo de documento que quiero | Ese tipo es de ámbito **chofer**, o está inactivo. Este módulo sólo ofrece los activos de ámbito vehículo (FR-017a) |
| No me deja crear un tipo de documentación con un nombre que sé que no usé | El nombre es único en **todo** el catálogo, no por ámbito. Si ya existe uno de chofer con ese nombre, usá otro (research §3) |
| El archivo adjunto no se abre | Se sirve por un endpoint que exige sesión y permiso: si expiró, volvé a ingresar (FR-038) |
