---

description: "Task list for 008-diseno-gt-ui"
---

# Tasks: Adopción del sistema de diseño gt-ui (Módulo 8)

**Input**: Design documents from `/specs/008-diseno-gt-ui/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: **no se escriben pruebas nuevas.** La spec no las pide y el criterio es el opuesto: las 285
pruebas que ya existen son la prueba de que el comportamiento no cambió (FR-068, convención [007]). El
único cambio autorizado sobre la suite son los pasos de interacción de FR-069, y están en T082–T086.

**Organization**: por historia de usuario, para que cada una se implemente y se verifique sola.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ir en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1…US5)
- Todo camino es relativo a la raíz del repositorio
- Un ID con **letra** —`T035a`, `T089a`— es una tarea agregada después de la numeración original, en el
  lugar que le corresponde. No falta ninguna: la letra evita renumerar 120 tareas para insertar una

## Path Conventions

Aplicación web; **sólo se toca `frontend/`**. El backend no se toca: ninguna entidad, endpoint ni DTO
cambia. La única escritura fuera de `frontend/` es sobre `.claude/skills/gt-ui/`, que FR-008 exige.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dejar el proyecto con las dos tipografías nuevas y con una línea de base verde contra la
cual medir todo lo que sigue.

- [X] T001 Ejecutar `cd frontend && npm test` **antes de tocar nada** y anotar el resultado: 285 pruebas en 43 archivos en verde. Es la línea de base de FR-068 y sin ella no se puede distinguir una regresión propia de una previa
- [X] T002 Agregar `@fontsource-variable/plus-jakarta-sans` y `@fontsource/geist-mono` a `frontend/package.json`, y retirar `@fontsource-variable/geist` (contracts §4)
- [X] T003 Reemplazar en `frontend/src/index.css` el `@import '@fontsource-variable/geist'` por los dos imports nuevos
- [X] T004 Verificar con la herramienta de desarrollo del navegador —panel *Computed* → *Rendered Fonts*— que la familia efectiva es *Plus Jakarta Sans* y no un respaldo del sistema. `system-ui` resuelve a Segoe UI en Windows y taparía la familia propia en silencio (FR-003, research §3)

**Checkpoint**: las dos familias cargan, la suite sigue en verde y el aspecto todavía es el del Módulo 7.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: los tokens de gt-ui en un solo lugar y el renombrado que hace que el proyecto vuelva a
compilar. **Toca la capa de la que dependen las 42 pantallas.**

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa y la suite en verde.

- [X] T005 Reemplazar el bloque `@theme` de `frontend/src/index.css` por los tokens de gt-ui con los nombres de `references/tokens.md` —`canvas`, `surface`, `surface-soft`, `surface-mute`, `ink`, `ink-soft`, `muted`, `faint`, `dim`, `dim-texto`, `line`, `line-strong`, `brand`, `estado-*`, `danger`— dentro de los espacios de nombre de Tailwind v4 (`--color-*`, `--font-*`, `--radius-*`, `--shadow-*`). **No** se crea `tailwind.config.js`: una segunda fuente de tokens es exactamente lo que FR-001 prohíbe (FR-001, research §2, data-model §1.1)
- [X] T006 Cargar en ese `@theme` los **cuatro valores recalibrados** con su medición en comentario al lado: `faint` `#6E7683` (4,58:1 sobre blanco), `dim-texto` `#6B768A` (4,58:1 sobre blanco), encabezado de columna `#6B7181` (4,59:1 sobre `surface-soft`) y texto del badge *Anulado* `#6A6E7C` (4,58:1 sobre `#F2F3F6`). El valor recalibrado de `dim` se registra bajo el nombre `dim-texto`, porque `dim` deja de ser color de texto (FR-007, FR-007a, data-model §1.1, §1.2)
- [X] T007 Dejar escrito en `frontend/src/index.css`, como comentario junto a los tokens, las dos reglas que la medición volvió obligatorias: **`dim` no pinta texto** —para texto atenuado manda `faint`— y **sobre el lienzo y sobre `surface-mute` sólo va texto en `ink` o `ink-soft`** (FR-007a, FR-007b, research §1)
- [X] T007a **Enumerar ahora los dos conjuntos que la regla de FR-007b tiene que cubrir**, para no descubrirlos en Phase 8: (1) qué texto queda **sobre el lienzo** una vez que la columna principal se vuelve transparente —hoy es el título de página y su bajada, y hay que confirmar que no hay nada más en `Layout`—, y (2) qué texto va **sobre `surface-mute`**, cuyo único uso como fondo es el relleno de la pastilla secundaria. Los dos son conjuntos chicos y cerrados: si alguno pide un tono que no sea `ink` o `ink-soft`, **se vuelve a la spec y no se escribe una excepción en el código** (FR-007b, SC-009a, FR-011, research §1)
- [X] T008 Declarar en `@theme` los radios (`pastilla` 9999px, `island` 26px, `card` 20px, `field` 14px, `token` 7px), las cuatro sombras difusas con spread negativo, la curva `cubic-bezier(0.32,0.72,0,1)` y las dos familias tipográficas (data-model §1.3)
- [X] T009 Conservar en `frontend/src/index.css` las tres reglas base del Módulo 7 que siguen vigentes: `:focus-visible` con anillo visible, `font-variant-numeric: tabular-nums` y el bloque `prefers-reduced-motion` (FR-006, FR-025, FR-039)
- [X] T010 Retirar las utilidades `@utility atenuada` / `@utility atenuado` de `frontend/src/index.css` y reapuntarlas a `faint`, que pasa a ser el único tono atenuado de texto (FR-007a, research §1)
- [X] T011 Barrer los nombres de token del Módulo 7 —`pagina`, `superficie`, `superficie-alterna`, `superficie-hundida`, `texto`, `texto-suave`, `texto-tenue`, `acento`, `acento-oscuro`, `acento-fondo`, `borde`, `borde-fuerte`, `exito`, `advertencia`, `error`, `chico`, `medio`, `grande`, `tarjeta`, `dialogo`— en `frontend/src/compartido/` y `frontend/src/modules/`, reemplazándolos por los nombres nuevos. Es mecánico: las pantallas casi no declaran color porque el Módulo 7 ya las limpió
- [X] T012 Ejecutar `cd frontend && npm run build` y `npm test`. **Este es el punto de control más barato de la feature**: si la suite no está en verde acá, el problema es de la capa de tokens y no de las 42 pantallas, y se arregla antes de seguir (research §9)
- [X] T013 [P] Escribir los cuatro valores recalibrados en `.claude/skills/gt-ui/references/tokens.md`, **cada uno con su medición y el fondo sobre el que se midió al lado**, más la degradación de `dim` a token no textual y la regla del lienzo (FR-008, FR-007a, FR-007b, contracts §6)
- [X] T014 [P] Registrar `references/choferes- padron.png` en la lista de referencias de `.claude/skills/gt-ui/SKILL.md`, con una línea que diga qué fija: cómo se entra a una fila y dónde viven las acciones secundarias (FR-008)

**Checkpoint**: los tokens de gt-ui son los del sistema, la skill y el código dicen lo mismo, y las 285 pruebas siguen pasando.

---

## Phase 3: User Story 1 - La aplicación se ve como el sistema de diseño (Priority: P1) 🎯 MVP

**Goal**: el lienzo con sus dos orbes, la navegación como isla flotante, las tarjetas como islas y las
dos tipografías, en las 42 pantallas de una vez.

**Independent Test**: se recorre el sistema pantalla por pantalla con las cuatro imágenes de
referencia al lado y se verifica que fondo, tipografía, radios, sombras, densidad y jerarquía
coinciden. Ninguna quedó con la apariencia anterior.

- [X] T015 [P] [US1] Crear `frontend/src/compartido/ui/Lienzo.tsx`: los dos orbes radiales en un contenedor `fixed inset-0 -z-10 pointer-events-none`, con los valores exactos de `tokens.md` (FR-002)
- [X] T016 [P] [US1] Crear `frontend/src/compartido/ui/Isla.tsx`: blanco al 94 %, hairline `line`, sombra difusa y radio de tarjeta. Nunca borde gris sólido ni sombra dura (FR-004, FR-005, FR-015)
- [X] T017 [US1] Montar `Lienzo` una sola vez en la raíz, en `frontend/src/App.tsx`, fuera de todo contenedor con desplazamiento. Las 42 rutas **no cambian** (FR-002, FR-067)
- [X] T018 [US1] Reescribir `frontend/src/compartido/Layout.tsx`: **se retira la barra superior**. La marca sube a la isla de navegación y el bloque de usuario con *Cambiar contraseña* y *Cerrar sesión* baja a su pie. La columna principal queda **transparente** con `px-10 py-6` (FR-009, FR-010, FR-011)
- [X] T019 [US1] Reescribir `frontend/src/compartido/Menu.tsx` como isla flotante de 266 px, radio 26 px, separada 18 px de los bordes: rótulos de sección en estilo eyebrow, ítem activo distinguido por **fondo y peso** además de color, y cambio a fondo suave con texto de acento si el menú supera las quince entradas (FR-009, FR-012)
- [X] T020 [US1] Verificar en `Menu.tsx` que se siguen dibujando **exactamente** las opciones que llegan del servidor y que una con código desconocido sigue apareciendo en la última sección. La lista vacía sigue sin dibujar navegación (FR-013)
- [X] T021 [P] [US1] Revestir `frontend/src/compartido/ui/EncabezadoDePantalla.tsx` con la escala tipográfica de gt-ui —título de página 34 px ExtraBold— conservando su firma y el efecto que fija el título de la pestaña (FR-071, contracts §1)
- [X] T022 [P] [US1] Revestir `frontend/src/compartido/ui/Aviso.tsx`, `Dialogo.tsx`, `DialogoConfirmacion.tsx` y `EstadoVacio.tsx` con los tokens y radios nuevos, **sin tocar sus firmas** ni el `onOpenAutoFocus` que lleva el foco al diálogo y no a su primer control (FR-062, contracts §1)
- [X] T023 [P] [US1] Revestir `frontend/src/compartido/ui/Paginacion.tsx`. **Su texto no cambia ni una coma** —cuatro pruebas lo verifican palabra por palabra— y conserva su `role="status"` (FR-061, contracts §1)
- [X] T024 [US1] Aplicar `Geist Mono` a los identificadores del sistema —DNI, CUIT, CUIL, patente, número de comprobante, número de remito, número de viaje— en las pantallas que los muestran. Hoy no hay ninguna familia monoespaciada: un CUIT se escribe con la misma fuente que una razón social (FR-003, FR-064)
- [X] T025 [US1] Revestir `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx` y `frontend/src/modules/usuarios/paginas/CambiarPassword.tsx`: mismo lienzo, misma tipografía y mismo vocabulario de componentes, **sin navegación** (FR-014)
- [X] T026 [US1] Revestir `frontend/src/modules/autenticacion/paginas/PantallaInicio.tsx` sobre el lienzo, sin inventarle una acción primaria que no tiene (FR-017)
- [ ] T027 [US1] Recorrer las **42 pantallas** de [data-model §4](./data-model.md#4-las-42-pantallas) verificando fondo, tipografía, radios y sombras. Ninguna con la apariencia anterior, ninguna con borde gris sólido de 1 px ni sombra dura (SC-001)

**Checkpoint**: la aplicación se ve como gt-ui de punta a punta. Es el MVP: se puede mirar y juzgar sin abrir un archivo.

---

## Phase 4: User Story 2 - La acción principal se distingue (Priority: P1)

**Goal**: cuatro niveles de acción con la forma de gt-ui, un solo primario por pantalla, y los 16
formularios con sus secciones y su barra de acciones.

**Independent Test**: se abre cada uno de los 16 formularios y se verifica que hay exactamente una
pastilla rellena, que es la que guarda, y que existe una salida visible sin desplazar la pantalla.

- [X] T028 [US2] Reescribir las variantes de `estilosDeBoton` en `frontend/src/compartido/ui/clases.ts` con la forma de gt-ui: `primario` pastilla `ink`, `secundario` pastilla `surface-mute` sin borde, `terciario` pastilla blanca con flecha en círculo, `destructivo` idéntico al primario en `bg-danger`. La variante `texto` se conserva como enlace (FR-016, FR-020, data-model §2)
- [X] T028a [US2] Sumar la prop **`icono`** a `frontend/src/compartido/ui/Boton.tsx`, que la primitiva **anida en un círculo interno** `bg-white/[0.14]` pegado al borde derecho, nunca suelto al lado del texto. Sólo lo rinden `primario` y `destructivo`. Es una prop y **no** un `[&>svg:last-child]` sobre los hijos: una cadena de clases no puede crear el `<span>` del círculo, y el selector sería la misma regla sobre elementos interactivos a secas que T035a retira de `EncabezadoDePantalla`. Opcional y aditiva: ninguna llamada existente deja de compilar (FR-018, FR-021, contracts §2.2)
- [X] T029 [US2] Verificar que `variante` sigue siendo **obligatoria y sin valor por defecto** en `frontend/src/compartido/ui/Boton.tsx`: un botón sin declarar su nivel no compila. La garantía de [007] no puede debilitarse al cambiar la apariencia (FR-021)
- [X] T030 [US2] Revestir `frontend/src/compartido/ui/Campo.tsx` con el contrato de campo del plan —reposo `surface-soft` con borde `line-strong`, anillo de foco visible por sí solo, error con borde y fondo `danger`, deshabilitado atenuado— conservando su firma y la asociación `id`↔etiqueta↔error (FR-024, FR-025, FR-027)
- [X] T031 [US2] Definir en `Campo` los tres anchos —corto, medio, largo— que la prop `ancho` ya acepta, con los valores de gt-ui (FR-026)
- [X] T031a [US2] **Asignar el `ancho` campo por campo** en los 19 formularios, según la tabla de estados de campo del plan: DNI, CUIT, CUIL, patente, fecha, importe, punto de venta y días de aviso **cortos**; nombre, apellido, teléfono, email, marca, modelo y comprobante **medios**; razón social, domicilio, origen, destino y los `select` de transportista, cliente, chofer y vehículo **largos**. Se hace acá y no en T031 porque el ancho lo decide el dato, y el dato lo conoce el sitio de llamada (FR-026)
- [X] T031b [US2] **Escribir los placeholders** de la columna *Vacío* de la tabla del plan, con los valores de [contracts §5](./contracts/README.md#5-los-textos-qué-se-congela-y-qué-escribe-esta-feature): `30.123.456`, `20-30123456-4`, `30-71234567-8`, `EF456GH`, `0000-00000111`, `71234567890123`, `0001`, `30`, `22644,63`, `+54 9 221 555-0148`, `nombre@empresa.com.ar`, `Rosario`, `Córdoba`, `Distribuidora del Litoral`; los *Seleccioná …* de los `select` sin valor; el chip *Opcional*. **Un placeholder es un ejemplo real del formato, nunca una instrucción**, y nunca reemplaza a la etiqueta, que FR-066 congela. Son textos nuevos de esta feature y los revisa T118 (FR-024, FR-065, contracts §5)
- [X] T032 [US2] Actualizar `clasesDeControl` y `clasesDeFormulario` en `frontend/src/compartido/ui/clases.ts` a los tokens nuevos. Los controles nativos **se estilan, no se sustituyen**, y siguen sin una regla global sobre `input` (FR-031)
- [X] T033 [P] [US2] Crear `frontend/src/compartido/ui/SeccionNumerada.tsx`: chip numerado, título y línea de explicación opcional (FR-029, contracts §3.4)
- [X] T034 [P] [US2] Crear `frontend/src/compartido/ui/BarraDeAcciones.tsx` con `anclaje` **obligatoria**: `viewport` para los formularios de página completa, `contenedor` para los de diálogo y las pantallas sin sesión. Leyenda de obligatorios a la izquierda, acciones a la derecha, primario último (FR-030, contracts §3.4)
- [X] T035 [US2] Aplicar el *volver* terciario —pastilla blanca con flecha en círculo, arriba a la izquierda, fuera de la línea de decisión— en `EncabezadoDePantalla` (FR-022)
- [X] T035a [US2] **Retirar de `EncabezadoDePantalla` el bloque de selectores por descendiente** —`[&_button]`, `[&_a]`, `[&_button[type=submit]]`— que hoy decide la jerarquía por el tipo del botón en vez de por una variante declarada. Es el antipatrón de [007] adentro de una primitiva, y es lo que hace que en las fichas *Editar*, *Dar de baja* y *Registrar cobro* se dibujen idénticas. La firma de la primitiva **no cambia** (FR-019, FR-021, research §10, contracts §1)
- [X] T035b [US2] **Designar la acción principal de las cinco fichas**, que hoy no tienen ninguna, **con el verbo exacto que ya está en pantalla**: `/usuarios/:id` → `Editar`, `/choferes/:id` → `Cargar documento`, `/flota/:id` → **`Agregar documento`** (no *Cargar*: son distintos y los dos están congelados), `/viajes/:id` → `Asignar chofer y vehículo` / `Reasignar chofer y vehículo`, `/facturas/:id` → `Registrar cobro`. El resto pasa a secundaria y lo destructivo a destructivo: `Anular` en viaje y factura, `Dar de baja` / `Reactivar` en chofer y vehículo. **Ningún verbo se reescribe** (FR-017, FR-019, FR-020, FR-066, research §11)
- [X] T035c [US2] **Mudar el *volver* de `accionPrincipal` a `volverA`** en las **seis** pantallas donde hoy viaja como una acción más: `/choferes/vencimientos` (*Volver al listado de choferes*), `/flota/vencimientos` (*Volver al listado de flota*), `/tipos-vehiculo` (*Volver a la flota*), `/viajes/:id`, `/facturas/:id` y `/usuarios/:id` (*Volver al listado*). La prop ya existe y no la usa nadie (FR-022, research §11)
- [X] T035d [US2] **Crear el *volver*** en las **tres** pantallas que no tienen ninguno: `/facturas/vencimientos` —que hoy no tiene ninguna acción de encabezado—, `/choferes/:id` y `/flota/:id`. Es texto nuevo y lo escribe esta feature, siguiendo la forma de los que ya existen (FR-022, FR-065, research §11)
- [X] T035e [US2] Verificar que **`DetalleUsuario` pasó a usar `EncabezadoDePantalla`** como las otras cuatro fichas: hoy es la única que arma su propio `<nav aria-label="Acciones sobre el usuario">`. Sus cuatro controles —`Editar`, `Roles`, `Restablecer contraseña`, `Volver al listado`— conservan texto y rol; lo que cambia es dónde viven (FR-019, FR-022, FR-066, research §11)
- [X] T036 [US2] Verificar que lo que navega sigue siendo un `<a>` y lo que ejecuta sigue siendo un `<button>`, se vean como se vean, en las primitivas y en los enlaces que hoy usan `clasesDeBoton` (FR-023)
- [X] T037 [P] [US2] Aplicar `BarraDeAcciones` con anclaje `contenedor` en `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx` — un solo grupo, sin sección numerada
- [X] T038 [P] [US2] Ídem en `frontend/src/modules/usuarios/paginas/CambiarPassword.tsx` — un solo grupo
- [X] T039 [P] [US2] Ídem con anclaje `contenedor` en `frontend/src/modules/facturacion/componentes/RegistrarCobro.tsx` — diálogo, un solo grupo
- [X] T040 [P] [US2] Aplicar `BarraDeAcciones` con anclaje `viewport` en `frontend/src/modules/usuarios/personas/paginas/FormularioPersona.tsx` — un solo grupo, sin chip
- [X] T041 [P] [US2] Ídem en `frontend/src/modules/usuarios/paginas/PanelRoles.tsx` — un solo grupo
- [X] T042 [P] [US2] Ídem en `frontend/src/modules/flota/tiposVehiculo/FormularioTipoVehiculo.tsx` — un solo grupo
- [X] T043 [P] [US2] Ídem en `frontend/src/modules/viajes/paginas/AsignacionViaje.tsx` — un solo grupo
- [X] T044 [P] [US2] Agrupar `frontend/src/modules/usuarios/paginas/FormularioUsuario.tsx` en **2 secciones**: *1. Acceso* y *2. Estado y permisos*, con `BarraDeAcciones` al pie del viewport (FR-029, data-model §6)
- [X] T045 [P] [US2] Agrupar `frontend/src/modules/choferes/paginas/FormularioChofer.tsx` en **3 secciones**: *1. Identidad*, *2. Contacto*, *3. Dependencia*
- [X] T046 [P] [US2] Agrupar `frontend/src/modules/choferes/transportistas/FormularioTransportista.tsx` en **2 secciones**: *1. Identidad fiscal*, *2. Contacto*
- [X] T047 [P] [US2] Agrupar `frontend/src/modules/flota/paginas/FormularioVehiculo.tsx` en **2 secciones**: *1. Identificación*, *2. Dependencia y estado*
- [X] T048 [P] [US2] Agrupar `frontend/src/modules/viajes/paginas/FormularioViaje.tsx` en **3 secciones**: *1. Cliente y fecha*, *2. Recorrido*, *3. Importe*
- [X] T049 [P] [US2] Agrupar `frontend/src/modules/viajes/clientes/FormularioCliente.tsx` en **2 secciones**: *1. Identidad fiscal*, *2. Contacto*. Sirve al alta y a la edición
- [X] T050 [US2] Agrupar `frontend/src/modules/facturacion/paginas/AltaFactura.tsx` en **4 secciones**: *1. Cliente y comprobante*, *2. Período*, *3. Viajes a facturar*, *4. Datos del comprobante*, con `componentes/SelectorDeViajes.tsx` dentro de la tercera y `componentes/ResumenDeImportes.tsx` y `componentes/VistaPreviaDocumento.tsx` integrados **sin intentar estilar el PDF que muestran adentro**
- [X] T051 [P] [US2] Agrupar `frontend/src/modules/facturacion/paginas/CorreccionFactura.tsx` en **2 secciones**: *1. Datos que no se modifican*, *2. Datos corregibles*
- [X] T052 [P] [US2] Agrupar `frontend/src/modules/facturacion/paginas/EmpresaEmisora.tsx` en **4 secciones**: *1. Identidad fiscal*, *2. Datos de facturación*, *3. Contacto y cobro*, *4. Logo*, con `componentes/CargaDeLogo.tsx` en la cuarta
- [X] T053 [P] [US2] Aplicar el contrato de campo y la barra de acciones al pie de su contenedor en los **tres formularios en línea** que no son pantalla propia: `frontend/src/modules/choferes/documentacion/FormularioDocumento.tsx`, `frontend/src/modules/flota/documentacion/FormularioDocumentoVehiculo.tsx` y el alta de `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`. Ninguno lleva sección numerada (data-model §6)
- [X] T054 [US2] Recorrer las 42 pantallas contando botones **primarios** —la pastilla `ink` rellena—: **a lo sumo uno por pantalla**, y en toda pantalla con formulario es el que guarda. **La acción destructiva no entra en el conteo**: es un cuarto nivel y en `/choferes/:id`, `/flota/:id`, `/viajes/:id` y `/facturas/:id` convive con el primario, que es lo correcto y no una infracción. Los cuatro paneles de solo lectura —`/choferes/vencimientos`, `/flota/vencimientos`, `/viajes/totales`, `/facturas/vencimientos`— **no llevan ninguno**, y tampoco `/`. **`/facturas/totales` sí lleva**: su *Ver totales* ejecuta la consulta, y es la asimetría con `/viajes/totales` que el relevamiento destapó (FR-017, FR-020, SC-002, research §10, tabla de acción primaria del plan)
- [ ] T055 [US2] Verificar en los 16 formularios que hay salida visible sin desplazar la pantalla y que la acción principal es alcanzable desde cualquier punto del desplazamiento — anclada al viewport en los de página completa, al pie de su contenedor en el diálogo y en las dos pantallas sin sesión (SC-003)
- [X] T056 [US2] Verificar que el rojo quedó reservado a lo destructivo: ningún otro elemento del sistema lo usa como relleno de acción (FR-020)
- [X] T056a [US2] Recorrer los **16 encabezados con acciones** —los 15 que usan `accionPrincipal` más `DetalleUsuario`— y confirmar que ninguno tiene dos **primarios** compitiendo: el primario es uno solo, lo que lo acompaña se lee como secundario, lo destructivo se lee como destructivo y el *volver* está arriba a la izquierda. Las **dos secundarias reales** del sistema son las dos *Ver vencimientos* de `/choferes` y `/flota` (FR-019, FR-022, SC-002, research §10)

**Checkpoint**: se abre cualquier formulario del sistema sabiendo cuál es la acción que completa la tarea y cómo salir sin completarla.

---

## Phase 5: User Story 3 - Los listados se leen de un vistazo (Priority: P2)

**Goal**: las 21 tablas con divisores hairline, encabezados en eyebrow, importes alineados y celdas
que dicen qué falta; 15 fusiones de columna repartidas en 12 tablas; el enlace de fila en 9 listados;
las 8 superficies de filtro con el buscador arriba; y la desaparición de las 10 columnas `Acciones`,
que dejan **8 menús `···`** —los dos números difieren a propósito: `ListadoChoferes` y `ListadoFlota`
pierden la columna sin dejar ningún `···` detrás—. **6 de esos 8 menús se hacen en esta fase**; los
dos restantes son las tablas de documentación de `FichaChofer` y `FichaVehiculo`, y van en Phase 6
(T098, T099).

**Independent Test**: se abre cada listado, se aplica un filtro, se pagina, se entra a una fila con el
mouse y se recorre el menú `···` sólo con el tabulador.

### Primitivas del listado

- [X] T057 [US3] Reescribir `frontend/src/compartido/ui/Listado.tsx`: **se retira la alternancia de fondo** —con divisores hairline alcanza—, encabezados en eyebrow 9,5 px sobre `surface-soft` conservando `th scope`, filas `px-5 py-[13px]` de altura pareja, y desplazamiento horizontal contenido dentro de la isla (FR-036, FR-037, FR-048, FR-049)
- [X] T058 [US3] Reescribir `frontend/src/compartido/ui/Filtros.tsx` como **el contenedor de las tres franjas**: buscador arriba a todo el ancho, filtros debajo, resumen al pie. La declaración por escrito de qué se está mostrando se conserva. La primitiva **da el lugar y el estilo; no decide el orden de sus hijos** — eso lo hacen las ocho superficies de T059a–T059h (FR-032, FR-033, FR-034, contracts §2.3)
- [X] T059 [US3] Agregar a `Filtros.tsx` la prop **`resumen`** con la franja: cantidad de resultados, total o señales de la pantalla, y criterio de orden. Es prop y no contenido de `declaracion` porque los tres datos los tiene el listado y el orden visual lo fija la spec (FR-035, contracts §2.3)

#### Las ocho superficies de filtro

> **El buscador no vive en la primitiva.** `Filtros.tsx` recibe los controles como hijos, así que
> *"el buscador primero y a todo el ancho"* (FR-033) y *"el que está filtrando se distingue"*
> (FR-034) se cumplen **acá**, archivo por archivo. Sin estas ocho tareas, T058 reestila un
> contenedor y las dos FR quedan sin cumplir en las ocho pantallas que filtran.
>
> Cuatro de las ocho usan la primitiva; **cuatro no**: dos repiten a mano el `<section
> aria-label="Filtros">` con el blob de `[&_input]`, `[&_select]`, `[&_button]` —el mismo antipatrón
> de [007] que T035a retira de `EncabezadoDePantalla`— y dos filtran con marcado suelto en la
> pantalla. Las ocho pasan a usar `Filtros`, y el blob desaparece con ellas.

- [X] T059a [P] [US3] `frontend/src/modules/viajes/componentes/FiltrosViajes.tsx`: el buscador —hoy **último**, después de tres `select` y dos fechas— sube a la primera franja y va a todo el ancho; los demás quedan como desplegables compactos, con el que filtra distinguido. Pasa `resumen` con cantidad, total del período y criterio de orden (FR-033, FR-034, FR-035)
- [X] T059b [P] [US3] `frontend/src/modules/usuarios/componentes/FiltrosUsuarios.tsx`: tiene **dos** campos de búsqueda; los dos van a la franja de arriba y los dos `select` debajo (FR-033, FR-034)
- [X] T059c [P] [US3] `frontend/src/modules/facturacion/componentes/FiltrosFacturas.tsx`: **no tiene buscador de texto** —son cinco `select` y dos fechas—. La franja de arriba queda vacía y no se dibuja: **no se inventa un buscador** que la pantalla no tiene (Principio III). Los siete controles se distinguen entre filtrando y no (FR-034)
- [X] T059d [P] [US3] `frontend/src/modules/flota/componentes/FiltrosFlota.tsx`: cuatro `select`, mismo caso que T059c — sin buscador, sin inventarlo (FR-034)
- [X] T059e [P] [US3] `frontend/src/modules/choferes/componentes/FiltrosTransportistas.tsx`: **hoy no usa la primitiva** —repite el `<section aria-label="Filtros">` con el blob de selectores por descendiente—. Pasa a usar `Filtros`, el buscador *Nombre o CUIT* a la franja de arriba, y **el blob se borra** (FR-032, FR-033, [007])
- [X] T059f [P] [US3] `frontend/src/modules/choferes/paginas/ListadoChoferes.tsx`: ídem — sus filtros están en línea en la pantalla con el mismo blob repetido. Se extraen a `Filtros` con el buscador primero (FR-032, FR-033)
- [X] T059g [P] [US3] `frontend/src/modules/viajes/clientes/ListadoClientes.tsx`: el `<form>` de filtros en línea —buscador por razón social y la casilla *Mostrar sólo los activos*— pasa a `Filtros`, buscador arriba y casilla debajo. **Los dos textos se conservan**: los consulta la suite (FR-033, FR-066)
- [X] T059h [P] [US3] `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.tsx`: su único buscador en línea pasa a la franja de arriba de `Filtros`, a todo el ancho (FR-033)
- [X] T060 [P] [US3] Crear `frontend/src/compartido/ui/MenuDeFila.tsx` con las diez reglas de comportamiento de [contracts §3.1](./contracts/README.md#31-menudefila--el----fr-044-fr-045): disparador siempre en el DOM y enfocable, abre con `Enter`/`Espacio`, foco al primer ítem, flechas para recorrer, `Escape` cierra devolviendo el foco al disparador, el foco que sale cierra, **no retiene el foco ni bloquea el fondo**, ítems como `button` y `a` de verdad, `items` vacío no dibuja nada, y el clic no dispara la navegación de la fila (FR-044, FR-045)
- [X] T061 [P] [US3] Crear `frontend/src/compartido/ui/EnlaceDeFila.tsx` y `frontend/src/compartido/ui/TokenDeIdentificador.tsx`: el dato que se busca con la vista, subrayado, con su identificador en mono debajo; y el recuadro con `#` en `dim` y el número en mono (FR-040, FR-041)
- [X] T062 [P] [US3] Crear `frontend/src/compartido/ui/FilaNavegable.tsx`: `<tr>` con `onClick` que navega y chevron `›` al pasar el mouse. **Sin `tabIndex`** — el destino accesible por teclado es el `<a>` de la celda (FR-042, FR-043, research §6)
- [X] T063 [US3] Agregar la prop **obligatoria** `forma: 'pastilla' | 'punto'` a `frontend/src/compartido/ui/Estado.tsx` y remapear `TONO_POR_VALOR` a los cuatro tonos de gt-ui más neutro, según [data-model §3](./data-model.md#3-pastilla-de-estado). `texto` sigue siendo obligatorio y el valor desconocido sigue cayendo en neutro (FR-055, FR-056)
- [X] T064 [US3] Agregar a `Estado.tsx` la prop `detalle`: el dato accesorio —comprobante, fecha, motivo— va **debajo**, en menor jerarquía, sin repetir la palabra del estado (FR-057)
- [X] T065 [US3] Actualizar **todas** las llamadas a `Estado` de `frontend/src/modules/` para pasar `forma`. El cambio rompe la compilación a propósito: obliga a decidir en cada uso si ese estado es el tema de la pantalla o si sólo acompaña

### Las tablas, una por una

- [X] T066 [P] [US3] `frontend/src/modules/viajes/paginas/ListadoViajes.tsx`: fusionar `Origen`+`Destino` → **Ruta** y `Chofer`+`Vehículo` → **Asignación**; enlace de fila = el número como `TokenDeIdentificador` → `/viajes/:id`; fila navegable con chevron; importes a la derecha en negrita y en una línea
- [X] T067 [P] [US3] `frontend/src/modules/choferes/paginas/ListadoChoferes.tsx`: fusionar `Apellido y nombre`+`DNI` → **Chofer** con el DNI en mono debajo; enlace de fila → `/choferes/:id`; **la columna `Acciones` desaparece y no queda `···`** — contenía sólo *Ver ficha*, que es ahora el enlace de fila (research §8)
- [X] T068 [P] [US3] `frontend/src/modules/flota/paginas/ListadoFlota.tsx`: fusionar `Marca`+`Modelo` → **Vehículo**; enlace de fila = la patente como token mono → `/flota/:id`; **`Acciones` desaparece sin `···`**, mismo caso que T067
- [X] T069 [P] [US3] `frontend/src/modules/facturacion/paginas/ListadoFacturas.tsx`: enlace de fila = el número de comprobante como token mono → `/facturas/:id`; fila navegable; importes alineados
- [X] T070 [P] [US3] `frontend/src/modules/usuarios/paginas/ListadoUsuarios.tsx`: enlace de fila = el nombre de usuario → `/usuarios/:id`; **`Acciones` desaparece** y *Editar*, *Roles* y *Dar de baja* pasan al `···`
- [X] T071 [P] [US3] `frontend/src/modules/viajes/clientes/ListadoClientes.tsx`: fusionar `Razón social`+`CUIT` → **Cliente** con el CUIT en mono debajo, y `Teléfono`+`Email` → **Contacto**; enlace de fila → `/clientes/:id`; **`Acciones` desaparece** y *Editar* y *Dar de baja* / *Dar de alta* pasan al `···`. Sin permiso de gestión el `···` no se dibuja (FR-045)
- [X] T072 [P] [US3] `frontend/src/modules/choferes/paginas/PanelVencimientos.tsx`: fusionar `Documento`+`Vencimiento` → **Documento** con la fecha debajo; enlace de fila → `/choferes/:id`
- [X] T073 [P] [US3] `frontend/src/modules/flota/paginas/PanelVencimientosFlota.tsx`: fusionar `Documento`+`Vencimiento` → **Documento**; enlace de fila = la patente → `/flota/:id`
- [X] T074 [P] [US3] `frontend/src/modules/facturacion/paginas/PanelVencimientos.tsx`: enlace de fila = el número → `/facturas/:id`; importes alineados. Sin fusiones: no tiene par de columnas que nombre un solo concepto
- [X] T075 [P] [US3] `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.tsx`: fusionar `Nombre`+`Apellido`+`DNI` → **Persona** con el DNI en mono debajo; **`Acciones` desaparece** y *Editar* y *Dar de baja* pasan al `···`. **Sin enlace de fila**: su único destino es la edición y no se le inventa una ficha (data-model §5)
- [X] T076 [P] [US3] `frontend/src/modules/choferes/transportistas/ListadoTransportistas.tsx`: fusionar `Nombre`+`CUIT` → **Transportista** y `Teléfono`+`Email` → **Contacto**; **`Acciones` desaparece** y sus acciones pasan al `···`. Sin enlace de fila
- [X] T077 [P] [US3] `frontend/src/modules/flota/tiposVehiculo/ListadoTiposVehiculo.tsx`: **`Acciones` desaparece** y *Editar* y *Dar de baja* pasan al `···`. Sin enlace de fila: edita en la misma pantalla
- [X] T078 [P] [US3] `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`: **`Acciones` desaparece** y *Editar* y *Dar de baja* pasan al `···`. Sin enlace de fila
- [X] T079 [P] [US3] `frontend/src/modules/facturacion/componentes/SelectorDeViajes.tsx`: fusionar `Origen`+`Destino` → **Ruta**; importes alineados. **Sin enlace de fila ni chevron**: sus filas llevan casilla, y el clic sobre la casilla no navega
- [X] T080 [P] [US3] Aplicar tokens, divisores hairline, encabezados en eyebrow e importes alineados en `frontend/src/modules/viajes/paginas/TotalesPeriodo.tsx` y `frontend/src/modules/facturacion/paginas/TotalesFacturados.tsx`. **Sin enlace de fila**: son paneles de totales (data-model §5)
- [X] T081 [US3] Recorrer las 21 tablas eliminando toda **columna de guiones**: donde falta el dato, la celda dice qué falta —*Sin asignar*— en `faint` (FR-038, SC-004)

### Los cinco archivos de prueba que FR-069 autoriza

> Se agrega **un paso de interacción** —abrir el `···`— delante de la acción. **Ninguna aserción cambia.**

- [X] T082 [US3] `frontend/src/modules/choferes/documentacion/TiposDocumentacion.test.tsx`: abrir el `···` antes de las líneas **129** y **149** (*Editar*)
- [X] T083 [US3] `frontend/src/modules/flota/tiposVehiculo/ListadoTiposVehiculo.test.tsx`: abrir el `···` antes de las líneas **123, 138, 160, 168, 186**; revisar **157, 158 y 188** —son `queryByRole(...).not.toBeInTheDocument()`— y confirmar que siguen pasando por el motivo correcto
- [X] T084 [US3] `frontend/src/modules/usuarios/paginas/ListadoUsuarios.test.tsx`: abrir el `···` antes de las líneas **104, 118, 139** (*Dar de baja*)
- [X] T085 [US3] `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.test.tsx`: abrir el `···` antes de las líneas **94 y 114** (*Dar de baja*)
- [X] T086 [US3] `frontend/src/modules/viajes/clientes/ListadoClientes.test.tsx`: abrir el `···` antes de las líneas **90 y 91**; las líneas **109, 110 y 111** —la prueba sin permiso de gestión— **siguen pasando sin tocarlas**, porque FR-045 hace que el `···` no se dibuje y los botones siguen ausentes por la razón correcta. Mirarlas para confirmarlo (research §8)
- [X] T087 [US3] Ejecutar `cd frontend && npm test` y verificar que **ningún archivo fuera de esos cinco** necesitó tocarse. Uno más es señal de que algo se rompió, no de que la lista estaba corta (FR-069, SC-011)

### Verificación de la historia

- [X] T088 [US3] Verificar que en **cada listado de índice** hay exactamente un enlace por fila, que es el dato que se busca con la vista, y que las 12 tablas sin ficha de destino no tienen ninguno (SC-006)
- [X] T089 [US3] Verificar que **ninguna de las 21 tablas tiene columna `Acciones`**: las 10 que la tenían la perdieron, 8 dejando su `···` y 2 sin dejar nada (FR-046, SC-007, data-model §5)
- [X] T089a [US3] Contra la tabla de [data-model §5](./data-model.md#columnas-que-se-fusionan-fr-047), confirmar las **15 fusiones en 12 tablas** y que las **9 tablas restantes** —nombradas ahí una por una— se miraron y no tienen ningún par de columnas que nombre un solo concepto (FR-047)
- [ ] T090 [US3] Recorrer con el tabulador un listado con acciones de fila: se llega al `···` de cada fila, se abre sin mouse, las flechas recorren los ítems y `Escape` cierra devolviendo el foco al `···`. Sin pasar el mouse, todos los `···` están visibles (SC-008)
- [ ] T091 [US3] Verificar en las tablas con importes que los separadores de miles y la coma decimal quedan alineados en vertical entre todas las filas (FR-039, SC-005)
- [X] T091a [US3] Vaciar cada listado por los **dos caminos** —uno sin ningún registro y otro con un filtro que no encuentra nada— y verificar que `EstadoVacio` **distingue los dos casos** con el texto que cada módulo ya escribió, y que la causa se explica **en un solo lugar**. La prop `caso` de la primitiva es la que lo decide, y su firma no cambia (FR-059, contracts §1)

**Checkpoint**: se abre un listado, se filtra, se pagina y se entra a una fila —con el mouse o sólo con el teclado— sin buscar dónde hacer clic.

---

## Phase 6: User Story 4 - Una ficha pone adelante el dato que importa (Priority: P2)

**Goal**: las cinco fichas en dos columnas, con el estado en el encabezado, el dato de más valor en el
aside, el historial como línea de tiempo y el callout que ofrece la salida.

**Independent Test**: se abre una ficha de cada módulo, incluida una de un registro inmutable, y se
verifica el encabezado, las dos columnas, el dato destacado del aside y el callout con su salida.

- [X] T092 [P] [US4] Crear `frontend/src/compartido/ui/AsideDeFicha.tsx`: columna fija de 330 px con `destacado` obligatorio. **Nunca se dibuja vacío** (FR-050, FR-053)
- [X] T093 [P] [US4] Crear `frontend/src/compartido/ui/LineaDeTiempo.tsx` con `EntradaDeLineaDeTiempo`: cronológica, con el paso actual marcado y la transición completa en una sola línea —`Pendiente → Facturado`—, no en dos columnas (FR-054)
- [X] T094 [P] [US4] Crear `frontend/src/compartido/ui/Callout.tsx`: dice qué pasa **y ofrece la salida**; si no hay salida, dice por qué (FR-058)
- [X] T095 [US4] Reescribir `frontend/src/compartido/ui/Ficha.tsx` con la firma de [contracts §2.4](./contracts/README.md#24-ficha--pasa-a-dos-columnas-fr-050-fr-051-fr-052): `FichaCuerpo` pasa a dos columnas con **`aside` obligatorio** —principal flexible y 330 px fijos—, y `FichaEncabezado` suma `token` para llevar título, identificador y pastilla de estado **en la misma línea**, con el contexto debajo. `FichaSeccion` **no cambia**. El `aside` obligatorio rompe la compilación de las cinco fichas a propósito, igual que `variante` y `forma`: una ficha que no declara su dato de más valor no compila (FR-050, FR-051, FR-052)
- [X] T096 [P] [US4] `frontend/src/modules/viajes/paginas/FichaViaje.tsx`: dos columnas; aside con el **importe** en cifra destacada de 32 px ExtraBold; historial de 4 columnas → `LineaDeTiempo`; `Callout` para el viaje rendido con la salida por Totales; **sin primario** cuando el viaje es inmutable
- [X] T097 [P] [US4] `frontend/src/modules/facturacion/paginas/FichaFactura.tsx`: dos columnas; aside con el **importe total**; fusionar `Origen`+`Destino` → **Ruta** en la tabla de viajes incluidos; historial → `LineaDeTiempo`; `Callout` para la factura anulada; **sin primario** en ese caso
- [X] T098 [P] [US4] `frontend/src/modules/choferes/paginas/FichaChofer.tsx`: dos columnas; aside con el **semáforo de documentación** y el documento más próximo a vencer; **`Acciones` desaparece** de la tabla de documentación y *Corregir* y *Eliminar* pasan al `···`
- [X] T099 [P] [US4] `frontend/src/modules/flota/paginas/FichaVehiculo.tsx`: dos columnas; aside con el **estado operativo y su semáforo** más la patente en mono; **`Acciones` desaparece** de la tabla de documentación y sus acciones pasan al `···`
- [X] T100 [P] [US4] `frontend/src/modules/usuarios/paginas/DetalleUsuario.tsx`: dos columnas; aside con los **roles** como chips
- [X] T101 [US4] Verificar en las cinco fichas que **no se trajo ningún dato que la ficha no reciba hoy**: el backend no se toca (FR-053)
- [X] T102 [US4] Retirar `frontend/src/compartido/ui/Historial.tsx` una vez que las dos fichas usan `LineaDeTiempo`, y borrar sus importaciones (contracts §4)
- [X] T103 [US4] Retirar `clasesDeEnlaceDeFila` de `frontend/src/compartido/ui/clases.ts` una vez que los listados usan `EnlaceDeFila` y `TokenDeIdentificador` (contracts §4)

**Checkpoint**: una ficha se abre y el dato que se vino a buscar está adelante; lo que no se puede hacer está explicado con su alternativa.

---

## Phase 7: User Story 5 - Nada de lo que ya funcionaba dejó de funcionar (Priority: P1)

**Goal**: la verificación consolidada de que el rediseño no es una regresión con mejor tipografía.

**Independent Test**: se corre la suite completa y se recorre un quickstart de punta a punta.

> **Esta historia no espera a esta fase para empezar.** Su primera verificación es T001 y se repite en
> cada checkpoint —T012, T027, T056, T087—. Lo que se consolida acá es lo que sólo puede comprobarse
> con todo el trabajo hecho.

- [X] T104 [US5] Ejecutar `cd frontend && npm test`: **285 pruebas en 43 archivos en verde** (FR-068, SC-011)
- [X] T105 [US5] Revisar el diff completo de la suite: los únicos cambios son pasos de interacción en los cinco archivos de FR-069. **Ninguna aserción sobre textos, roles ni etiquetas accesibles cambió** (SC-011)
- [X] T106 [US5] Verificar que las **42 direcciones** siguen siendo las mismas y que el título de la pestaña sigue nombrando la pantalla y el sistema (FR-067, FR-071)
- [ ] T107 [US5] Recorrer los **seis quickstarts** de los Módulos 1 a 6 con las tres cuentas —`admin`, Tráfico y Gerencia—: mismos mensajes, mismas confirmaciones, mismos rechazos, mismos permisos (SC-012)
- [ ] T108 [US5] Operar el sistema **de punta a punta sólo con teclado**, con el foco visible en cada paso, verificando que se alcanza todo lo que se alcanzaba antes —en particular todas las acciones que vivían en la columna `Acciones` (FR-070, SC-013)
- [X] T109 [US5] Verificar que todo resultado que aparece sin que la pantalla cambie se sigue anunciando con `role="status"` (FR-061, convención [003])

**Checkpoint**: el rediseño está completo y ninguna operación cambió de comportamiento.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: lo que atraviesa a todas las historias y lo que le queda al proyecto después.

- [ ] T110 Medir el contraste con herramienta —no a ojo— en las 42 pantallas: **4,5:1 para texto y 3:1 para lo no textual que comunica**. Prestar atención a los cuatro valores recalibrados y a los fondos sobre los que se los midió (SC-009)
- [X] T111 Verificar que **ningún texto va directo sobre el lienzo ni sobre `surface-mute` salvo `ink` e `ink-soft`**. Es la regla que cubre a `muted`, que falla 4,13:1 sobre el lienzo y que esta feature deliberadamente no recalibra (FR-007b, SC-009a, research §1)
- [X] T112 Verificar que **ninguna pantalla muestra un mensaje de error en su primer dibujo**, antes de que alguien la haya tocado. Esta feature no cambió cuándo se valida (FR-028, SC-010)
- [ ] T113 Verificar con un filtro de escala de grises sobre un listado que los estados de las filas **se siguen distinguiendo**: cada uno lleva su palabra además del color (FR-055, SC-014)
- [ ] T113a Verificar que todo elemento atenuado —un viaje anulado, una factura anulada— **lleva la palabra que lo explica** además del tono, y que el tono sigue siendo legible: atenuado no es borroso (FR-060)
- [ ] T114 Activar *Reducir movimiento* en el sistema operativo y recorrer el sistema: **nada anima** (FR-006, SC-015)
- [X] T115 Verificar que las animaciones que quedan usan **sólo** la curva de gt-ui y animan **sólo** `transform` y `opacity`, con `IntersectionObserver` y nunca un listener de scroll (FR-006)
- [X] T116 [P] Verificar que **no hay emoji** en ninguna parte de la interfaz (FR-063)
- [X] T117 [P] Verificar que las fechas y los importes conservan su formato argentino —`compartido/fechas` y `compartido/moneda`— y que los identificadores están en mono (FR-064)
- [X] T118 [P] Revisar los textos **nuevos** que introdujo esta feature —9 rótulos de columna, 24 títulos de sección, el nombre accesible de los `···`, la leyenda de obligatorios, los **~20 placeholders**, los *Seleccioná …* y el chip *Opcional*— en español rioplatense con voseo, sin muletillas de producto. Los placeholders además se revisan contra su regla propia: **ejemplo del formato, nunca instrucción** (FR-065, contracts §5)
- [X] T119 [P] Verificar que el nombre del sistema sigue siendo *Sistema Integral de Gestión* y no el *Sistema Integral de Transporte* de la imagen de referencia
- [ ] T120 Recorrer los bordes de [quickstart.md](./quickstart.md#los-bordes-que-hay-que-probar-a-propósito) **a 1280 px de ancho, que es la resolución mínima soportada**: menú vacío, menú largo, pantalla sin navegación, estado desconocido, fila sin acciones, tabla que no entra a lo ancho, fila atenuada y clic sobre un control de la fila. Tablet y celular siguen fuera de alcance: no se los prueba ni se los adapta (FR-049, FR-056, FR-060, FR-072)
- [X] T121 Ejecutar `cd frontend && npm run lint` y `npm run build`, y limpiar los tokens, clases e importaciones que quedaron sin uso
- [ ] T122 Recorrer las **16 tareas de validación manual que el Módulo 7 dejó pendientes**, ahora sobre el resultado de este módulo y no sobre el anterior (supuesto de la spec)
- [X] T123 Actualizar `AGENTS.md` con las decisiones transversales de esta feature, una línea por decisión y con referencia `[008]`. Las candidatas están en la sección *Mantenimiento* de [plan.md](./plan.md#mantenimiento); **sólo entra lo que de verdad sirva a una feature futura**, no una entrada por entrada

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup — **BLOQUEA todas las historias**. Nada se ve como gt-ui hasta que los tokens estén
- **US1 (Phase 3)**: depende de Foundational
- **US2 (Phase 4)**: depende de Foundational. Puede ir en paralelo con US1 si hay gente, pero conviene después: `clases.ts` se apoya en los tokens ya asentados
- **US3 (Phase 5)**: depende de Foundational. Independiente de US2
- **US4 (Phase 6)**: depende de Foundational. Comparte `Estado` con US3 (T063), así que si van en paralelo, T063 va primero
- **US5 (Phase 7)**: verificación consolidada — depende de que las demás estén hechas, pero **su suite se corre en cada checkpoint**, no sólo acá
- **Polish (Phase 8)**: depende de todo lo anterior

### User Story Dependencies

- **US1 (P1)** — sin dependencias sobre otras historias
- **US2 (P1)** — sin dependencias sobre otras historias
- **US3 (P2)** — T063 (`Estado` con `forma`) lo comparte con US4; quien llegue primero lo hace
- **US4 (P2)** — ídem. T097 fusiona una columna que también toca US3: va en US4 porque la tabla vive dentro de la ficha
- **US5 (P1)** — depende de todas, por naturaleza: es la que verifica que ninguna rompió nada

### Within Each User Story

- Primitivas antes que pantallas — **siempre**. Es lo que hace barata la feature (research §9)
- Dentro de US3: primitivas (T057–T065) → filtros (T059a–T059h) → tablas (T066–T081) → suite (T082–T087) → verificación (T088–T091a). Las ocho superficies de filtro van **después de T058 y T059**, que son las que les dan el contenedor y la prop `resumen`
- Dentro de US4: primitivas (T092–T095) → fichas (T096–T100) → retiros (T102, T103)
- Los retiros —`Historial`, `clasesDeEnlaceDeFila`— van **al final** de su historia: retirar antes rompe la compilación

### Parallel Opportunities

- **Phase 2**: T013 y T014 —la escritura de vuelta sobre la skill— van en paralelo entre sí y con T005–T012
- **Phase 3**: T015, T016, T021, T022 y T023 son archivos distintos
- **Phase 4**: los 16 formularios (T037–T052) son 16 archivos distintos, más T053 para los tres formularios en línea — es la fase con más paralelismo de la feature. T050 no lleva `[P]` porque toca cuatro archivos del módulo de facturación
- **Phase 5**: las tres primitivas nuevas (T060, T061, T062) en paralelo; después las ocho superficies de filtro (T059a–T059h) en paralelo entre sí, y las 15 tablas (T066–T080) también. **T059f, T059g y T059h tocan archivos de pantalla que además tienen tabla** —`ListadoChoferes`, `ListadoClientes`, `ListadoPersonas`—: no van en paralelo con T067, T071 y T075 respectivamente
- **Phase 6**: las tres primitivas (T092–T094) en paralelo; después las cinco fichas (T096–T100) en paralelo
- **Phase 8**: T116–T119 son verificaciones independientes

---

## Parallel Example: User Story 3

```bash
# Las tres primitivas nuevas del listado, juntas:
Task: "Crear frontend/src/compartido/ui/MenuDeFila.tsx con las diez reglas de contracts §3.1"
Task: "Crear frontend/src/compartido/ui/EnlaceDeFila.tsx y TokenDeIdentificador.tsx"
Task: "Crear frontend/src/compartido/ui/FilaNavegable.tsx"

# Después, las tablas — quince archivos distintos:
Task: "ListadoViajes.tsx: Ruta + Asignación, enlace de fila, fila navegable"
Task: "ListadoChoferes.tsx: Chofer con DNI en mono, enlace de fila, Acciones desaparece"
Task: "ListadoFlota.tsx: Vehículo, patente como token, Acciones desaparece"
Task: "ListadoFacturas.tsx: número como token, fila navegable"
Task: "ListadoUsuarios.tsx: enlace de fila, Editar/Roles/Dar de baja al ···"
# … y las diez restantes
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1: Setup — las dos tipografías y la línea de base verde
2. Phase 2: Foundational — **crítico, bloquea todo**
3. Phase 3: US1 — el lienzo, la isla de navegación y las tarjetas
4. **PARAR Y VALIDAR**: recorrer las 42 pantallas con las cuatro imágenes al lado
5. Es demostrable: se mira y se juzga sin abrir un archivo, que es lo que pide el Principio IV

### Incremental Delivery

1. Setup + Foundational → los tokens son los de gt-ui y la skill dice lo mismo que el código
2. US1 → la aplicación se ve como gt-ui → **MVP**
3. US2 → la acción principal se distingue en los 16 formularios
4. US3 → los listados se leen de un vistazo
5. US4 → las fichas ponen adelante el dato que importa
6. US5 + Polish → la verificación consolidada y lo que le queda al proyecto

Cada incremento se corre contra la suite antes de seguir. **Es la red que hace viable reestructurar 42 pantallas.**

### Parallel Team Strategy

1. El equipo hace Setup + Foundational junto — no tiene sentido paralelizarlo, es un solo archivo
2. Una vez que T012 está en verde:
   - Persona A: US1 (estructura de página)
   - Persona B: US2 (los 16 formularios — la fase más paralelizable)
   - Persona C: US3 (los listados), empezando por T063 si US4 va a arrancar en paralelo
   - Persona D: US4 (las fichas)
3. US5 la corre quien termine, sobre el resultado de todas

---

## Notes

- **No se escriben pruebas nuevas**: las 285 que ya existen son la prueba de que el comportamiento no cambió (FR-068, convención [007])
- **Ningún texto de los Módulos 1 a 6 se reescribe** (FR-066). Los nuevos los escribe esta feature y están listados en contracts §5
- **El backend no se toca** en ninguna tarea
- Reordenar, envolver, cambiar clases y mover bloques no rompe la suite; **esconder un control adentro de un menú sí** — por eso T082–T086 existen y están acotadas
- Correr `npm test` después de cada grupo lógico, no sólo en los checkpoints
- Parar en cualquier checkpoint para validar la historia sola
