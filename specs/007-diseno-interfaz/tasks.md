# Tasks: Rediseño de la aplicación (Módulo 7)

**Input**: Documentos de diseño en `/specs/007-diseno-interfaz/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md)

**Tests**: esta feature **no escribe tests de pantalla nuevos**. Los 41 archivos existentes son la
red de seguridad y se ejecutan una y otra vez a lo largo del recorrido. Sí lleva **tres tests
nuevos**, y sólo tres: los que cubren comportamiento que ninguna prueba existente mira —el título de
la pestaña (T027), las dos entradas de menú del backend (T030) y el mapa de secciones (T033)—.

**Organization**: las tareas se agrupan por historia de usuario. Las siete historias son las siete
etapas de [research §12](./research.md) y van **en orden**: cada una deja el sistema en un estado
coherente.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1 … US7)
- Toda descripción lleva la ruta exacta del archivo

## Path Conventions

- Frontend: `frontend/src/`
- Backend: `backend/src/GT.Application/`
- El grueso es frontend. El backend recibe **dos entradas de menú** y su test (T029, T030)

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: incorporar las dependencias y dejar el proyecto listo. Nada visible cambia.

- [X] T001 Instalar en `frontend/package.json` las dependencias de runtime: `tailwindcss@4.3.3`, `@tailwindcss/vite@4.3.3`, `@radix-ui/react-dialog@1.1.23`, `class-variance-authority@0.7.1`, `clsx@2.1.1`, `tailwind-merge@3.6.0` y `lucide-react@1.31.0` (plan §Technical Context)
- [X] T002 Registrar el complemento de Tailwind en `frontend/vite.config.ts` **sin tocar** el proxy de `/api` —que es lo que permite que la cookie de sesión viaje con `SameSite=Strict`— ni el bloque `test` de vitest
- [X] T003 Elegir la familia tipográfica evaluando las cuatro candidatas de [research §4](./research.md) contra sus cuatro criterios, instalar su paquete `@fontsource-variable/…@5.3.0` y **dejar anotada la elegida y el motivo en `research.md` §4**
- [X] T004 [P] Declarar el idioma español del documento y el título base `Sistema Integral de Gestión` en `frontend/index.html`, que hoy dice `lang="en"` y `<title>frontend</title>` (FR-010)
- [X] T005 [P] Reemplazar `frontend/public/favicon.svg`, que hoy es el ícono violeta genérico de la herramienta con la que se creó el proyecto, por uno propio del producto (FR-010)
- [X] T006 Ejecutar `cd frontend && npm test` y `npm run build` **antes de tocar una sola pantalla**: es la línea de base contra la que se compara todo lo que sigue

**Checkpoint**: las dependencias están instaladas y la suite sigue en verde.

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: el sistema de tokens y las utilidades que **todas** las historias necesitan.
**Bloquea todo lo demás.**

⚠️ **Al terminar esta fase la aplicación se ve peor que antes**: T008 borra la hoja del Módulo 1 y
todavía ninguna pantalla usa las piezas nuevas. Es esperado y dura hasta la Fase 4. Lo que prueba que
nada se rompió es que la suite sigue en verde (T012).

- [X] T007 Escribir en `frontend/src/index.css` el `@import` de Tailwind y el bloque `@theme` con **todos** los tokens de [data-model §1](./data-model.md): los quince colores medidos, `--font-sans`, los tres `--radius-*`, los dos `--shadow-*` y `--container-lectura`. Respetar los espacios de nombre de Tailwind v4: un token fuera de `--color-*`, `--font-*`, `--radius-*` o `--shadow-*` **no genera ninguna utilidad**
- [X] T008 Borrar de `frontend/src/index.css` las 136 líneas del Módulo 1 —incluida la regla global que pinta de azul **todos** los `button`, origen del defecto que hace que la celda que abre una ficha se vea como la acción principal— y dejar en su lugar sólo las reglas base: importación de la fuente, fondo de página, anillo de foco de 3 px y cifras tabulares para los números que se comparan en vertical
- [X] T009 [P] Escribir `frontend/src/compartido/ui/cn.ts` combinando `clsx` y `tailwind-merge`
- [X] T010 [P] Escribir `frontend/src/compartido/ui/iconos.ts` reexportando de `lucide-react` únicamente los íconos que el sistema usa, para que la importación quede en un solo lugar y el empaquetado pueda descartar el resto
- [X] T011 Verificar a mano, con la aplicación levantada, que **ningún** color, tamaño ni separación del `@theme` quedó sin generar su utilidad correspondiente
- [X] T012 Ejecutar `cd frontend && npm test` con la hoja vieja ya borrada. **Tiene que seguir en verde**: ninguna prueba mira estilos, y ésta es la primera confirmación de que la red de seguridad de toda la feature funciona

**Checkpoint**: los tokens existen, la hoja vieja no, y la suite sigue en verde.

---

## Phase 3: User Story 1 - Un lenguaje visual único para todo el sistema (Priority: P1) 🎯

**Goal**: las catorce primitivas de [data-model §2](./data-model.md), con las firmas que fija
[contracts §1](./contracts/README.md). Al terminar, **nada visible cambia todavía**: ninguna pantalla
las usa aún.

**Independent Test**: se importa cada primitiva en una pantalla de prueba y se comprueba que un
botón primario, un campo con error y un indicador de estado se ven igual sin importar desde qué
módulo se los use.

### Las primitivas sin dependencias entre sí

- [X] T013 [P] [US1] `frontend/src/compartido/ui/Boton.tsx` con las cuatro variantes de contracts §1 declaradas con `class-variance-authority`. **`variante` es obligatoria y sin valor por defecto**: es lo que vuelve imposible repetir el defecto de origen
- [X] T014 [P] [US1] `frontend/src/compartido/ui/Campo.tsx`: etiqueta, obligatorio, error, ayuda y los cuatro anchos (`corto` · `medio` · `largo` · `completo`). **Envuelve controles nativos y no los reemplaza**, y conserva la asociación `id` ↔ etiqueta ↔ error, que es lo que consultan los 138 `getByLabelText` de la suite
- [X] T015 [P] [US1] `frontend/src/compartido/ui/Estado.tsx` con los cinco juegos de [data-model §3](./data-model.md). **`texto` es un parámetro obligatorio**: la primitiva no puede dibujarse sin la palabra, y así FR-032 no depende de que alguien se acuerde
- [X] T016 [P] [US1] `frontend/src/compartido/ui/Aviso.tsx` con los cuatro tonos y el `rol` (`status` o `alert`) obligatorio, que **reemplaza** a los `role=` escritos a mano sin eliminarlos
- [X] T017 [P] [US1] `frontend/src/compartido/ui/EstadoVacio.tsx` con sus cuatro casos —vacío, sin coincidencias, cargando, error—, recibiendo el texto desde afuera porque los mensajes los fijan las specs de cada módulo
- [X] T018 [P] [US1] `frontend/src/compartido/ui/Paginacion.tsx` con la **misma firma** que las cuatro que va a reemplazar, incluido `nombrePlural`, que es lo que permite decir "20 de 73 choferes" y no "20 de 73 elementos"
- [X] T019 [P] [US1] `frontend/src/compartido/ui/Filtros.tsx` como contenedor del bloque de filtros
- [X] T020 [P] [US1] `frontend/src/compartido/ui/Listado.tsx`: encabezado, filtros, tabla y paginación como una sola pieza, con el estilo de `table`, `thead`, `tr` y `td` bajo su propio contenedor
- [X] T021 [P] [US1] `frontend/src/compartido/ui/Ficha.tsx`: encabezado con identidad, estado y acciones, y secciones reconocibles
- [X] T022 [P] [US1] `frontend/src/compartido/ui/Historial.tsx` como secuencia en el tiempo, distinguible de una tabla de datos

### El diálogo y el encabezado, que tienen dependencias

- [X] T023 [US1] `frontend/src/compartido/ui/Dialogo.tsx` sobre `@radix-ui/react-dialog`: superficie, fondo, portal, **retención de foco**, `Escape` y devolución del foco al origen (FR-036)
- [X] T024 [US1] Mudar `DialogoConfirmacion` de `frontend/src/modules/usuarios/componentes/` a `frontend/src/compartido/ui/`, reescrito sobre `Dialogo` y **conservando su firma exacta**, incluida `etiquetaConfirmar` que trajo el Módulo 5. Los cinco envoltorios que ya delegan en él sólo cambian de dónde lo importan
- [X] T025 [US1] Actualizar en los cinco envoltorios la ruta de importación: `modules/choferes/componentes/ConfirmacionBaja.tsx`, `modules/flota/componentes/ConfirmacionBajaVehiculo.tsx`, `modules/flota/componentes/ConfirmacionEliminarDocumento.tsx`, `modules/viajes/componentes/ConfirmacionBajaCliente.tsx` y `modules/viajes/componentes/ConfirmacionRendicion.tsx`
- [X] T026 [US1] `frontend/src/compartido/ui/EncabezadoDePantalla.tsx`: título, acción principal y vuelta atrás; además **fija el título de la pestaña** como `{título} · Sistema Integral de Gestión` (FR-008, research §9)
- [X] T027 [US1] Escribir `frontend/src/compartido/ui/EncabezadoDePantalla.test.tsx`: al montarse con un título, el título del documento pasa a incluirlo. Es el primero de los tres tests nuevos de la feature
- [X] T028 [US1] Ejecutar `cd frontend && npm test`: las primitivas están escritas y **la suite sigue en verde porque todavía ninguna pantalla las usa**

**Checkpoint**: existe el vocabulario completo. El sistema todavía se ve sin estilo.

---

## Phase 4: User Story 2 - Entrar y saber adónde ir (Priority: P1)

**Goal**: el marco. Navegación agrupada en cinco secciones, encabezado del sistema, encabezado de
pantalla en las 42, pantalla de inicio e ingreso.

**Al terminar esta fase el sistema entero ya cambió de aspecto**, porque el marco lo comparten las 42
pantallas.

**Independent Test**: se ingresa con `admin` y con un usuario de Tráfico y se comprueba que cada uno
ve sus secciones agrupadas, que las secciones sin opciones autorizadas no aparecen, y que desde el
inicio se llega a todo lo que puede usar.

### Las dos entradas que faltan en el menú

- [X] T029 [US2] Agregar al catálogo de `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs` las dos entradas de [contracts §2](./contracts/README.md): `vencimientos-choferes` bajo `ChoferesGestionar` y `vencimientos-flota` bajo `FlotaGestionar`, con las etiquetas `Vencimientos de choferes` y `Vencimientos de flota` — nombradas así para no repetir el choque que el propio archivo comenta entre *Totales* y *Totales facturados* (FR-013)
- [X] T030 [US2] Escribir en `backend/tests/GT.UnitTests/` el test de las dos entradas nuevas: aparecen con su permiso y **no** aparecen sin él. Es el segundo de los tres tests nuevos
- [X] T031 [US2] Ejecutar `cd backend && dotnet test`: verde antes de seguir

### La navegación agrupada

- [X] T032 [US2] Escribir `frontend/src/compartido/seccionesDeMenu.ts` con el mapa estático `código` → sección de [data-model §4](./data-model.md), y sus tres reglas: sólo se dibuja lo que llegó del servidor, una sección sin opciones no se dibuja, y un código desconocido cae en **Administración**
- [X] T033 [US2] Escribir `frontend/src/compartido/seccionesDeMenu.test.ts`: un código inventado —el de un módulo futuro— aparece igual y cae en la última sección; una sección sin opciones autorizadas no aparece. Es el tercero y último de los tres tests nuevos, y cubre lo que el quickstart declara que no puede verificar a mano
- [X] T034 [US2] Rediseñar `frontend/src/compartido/Menu.tsx` para agrupar por sección, distinguiendo **la opción activa y la sección que la contiene** por algo más que el color (FR-011, FR-012, FR-014). El componente sigue sin tener lógica propia de permisos
- [X] T035 [US2] Rediseñar `frontend/src/compartido/Layout.tsx`: marca, usuario y acciones de cuenta con una jerarquía en la que *Cerrar sesión* **no pese lo mismo** que la acción principal de la pantalla (FR-016)

### Las pantallas del marco

- [X] T036 [US2] Rediseñar `frontend/src/modules/autenticacion/paginas/PantallaInicio.tsx`: deja de ser sólo un saludo y ofrece los accesos que los permisos habilitan, armados **con lo que la sesión ya trae** y sin pedirle al servidor ningún dato nuevo. Con el menú vacío, explica la situación y se ve terminada (FR-015)
- [X] T037 [US2] Rediseñar `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx` con las primitivas, conservando textos y comportamiento

### El encabezado en las 42 pantallas

- [X] T038 [P] [US2] Aplicar `EncabezadoDePantalla` a las nueve pantallas de usuarios y personas en `frontend/src/modules/usuarios/`
- [X] T039 [P] [US2] Aplicar `EncabezadoDePantalla` a las nueve pantallas de choferes, transportistas y tipos de documentación en `frontend/src/modules/choferes/`
- [X] T040 [P] [US2] Aplicar `EncabezadoDePantalla` a las seis pantallas de flota y tipos de vehículo en `frontend/src/modules/flota/`
- [X] T041 [P] [US2] Aplicar `EncabezadoDePantalla` a las nueve pantallas de viajes y clientes en `frontend/src/modules/viajes/`
- [X] T042 [P] [US2] Aplicar `EncabezadoDePantalla` a las siete pantallas de facturación en `frontend/src/modules/facturacion/`
- [X] T043 [US2] Recorrer las 42 pantallas de `frontend/src/modules/` verificando que **todas** tienen encabezado y que la pestaña del navegador cambia al navegar (FR-008, FR-016)
- [X] T044 [US2] Ejecutar `cd frontend && npm test`

**Checkpoint**: el sistema se ve como un producto. Las pantallas por dentro todavía no.

---

## Phase 5: User Story 3 - Trabajar sobre un listado (Priority: P1)

**Goal**: las quince pantallas de listado con la misma anatomía, los cinco bloques de filtros
unificados y las cuatro paginaciones reemplazadas por una.

**Independent Test**: se abre el listado de facturas con una anulada, una pagada y una vencida, y se
comprueba que las tres se distinguen, que los totales se comparan verticalmente y que filtros, tabla
y paginación se leen como una sola pieza.

### La paginación única

- [X] T045 [US3] Reemplazar las cuatro copias por la primitiva y **borrar** `modules/choferes/componentes/Paginacion.tsx`, `modules/flota/componentes/Paginacion.tsx`, `modules/viajes/componentes/Paginacion.tsx` y `modules/facturacion/componentes/Paginacion.tsx` (FR-022)

### Los cinco bloques de filtros

- [X] T046 [P] [US3] Rediseñar `frontend/src/modules/usuarios/componentes/FiltrosUsuarios.tsx` con la primitiva `Filtros`
- [X] T047 [P] [US3] Rediseñar `frontend/src/modules/choferes/componentes/FiltrosTransportistas.tsx`
- [X] T048 [P] [US3] Rediseñar `frontend/src/modules/flota/componentes/FiltrosFlota.tsx`
- [X] T049 [P] [US3] Rediseñar `frontend/src/modules/viajes/componentes/FiltrosViajes.tsx`
- [X] T050 [P] [US3] Rediseñar `frontend/src/modules/facturacion/componentes/FiltrosFacturas.tsx`

### Los quince listados

- [X] T051 [P] [US3] `frontend/src/modules/usuarios/paginas/ListadoUsuarios.tsx`
- [X] T052 [P] [US3] `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.tsx`
- [X] T053 [P] [US3] `frontend/src/modules/choferes/paginas/ListadoChoferes.tsx`
- [X] T054 [P] [US3] `frontend/src/modules/choferes/paginas/PanelVencimientos.tsx`
- [X] T055 [P] [US3] `frontend/src/modules/choferes/transportistas/ListadoTransportistas.tsx`
- [X] T056 [P] [US3] `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`
- [X] T057 [P] [US3] `frontend/src/modules/flota/paginas/ListadoFlota.tsx`
- [X] T058 [P] [US3] `frontend/src/modules/flota/paginas/PanelVencimientosFlota.tsx`
- [X] T059 [P] [US3] `frontend/src/modules/flota/tiposVehiculo/ListadoTiposVehiculo.tsx`
- [X] T060 [P] [US3] `frontend/src/modules/viajes/paginas/ListadoViajes.tsx` — **sin agregarle `<tfoot>`**: hay un test que verifica que no lo tiene
- [X] T061 [P] [US3] `frontend/src/modules/viajes/paginas/TotalesPeriodo.tsx`
- [X] T062 [P] [US3] `frontend/src/modules/viajes/clientes/ListadoClientes.tsx`
- [X] T063 [P] [US3] `frontend/src/modules/facturacion/paginas/ListadoFacturas.tsx` — **las filas siguen siendo `<tr>`**: hay un test que busca una fila por su ancestro
- [X] T064 [P] [US3] `frontend/src/modules/facturacion/paginas/PanelVencimientos.tsx`
- [X] T065 [P] [US3] `frontend/src/modules/facturacion/paginas/TotalesFacturados.tsx`

### Lo que los quince tienen que cumplir

- [X] T066 [US3] Verificar en los quince listados de `frontend/src/modules/` que las columnas de importe se alinean con cifras tabulares y que los separadores de miles y decimales caen en la misma vertical (FR-020)
- [X] T067 [US3] Verificar que **la celda que abre la ficha se ve como acceso a un detalle** y no como la acción principal, en los listados de choferes, flota, viajes, facturas, usuarios, personas y clientes de `frontend/src/modules/` (FR-022)
- [X] T068 [US3] Verificar en `frontend/src/modules/` que las filas atenuadas por regla —factura anulada, viaje anulado, cliente o chofer dado de baja— **se ven efectivamente atenuadas**, conservan su palabra y siguen siendo legibles con `--color-texto-tenue` (FR-021)
- [X] T069 [US3] Verificar en los quince listados de `frontend/src/modules/` que los estados de vacío, sin coincidencias y cargando se distinguen entre sí, y que el control que declara qué se está mostrando sigue diciendo lo mismo palabra por palabra (FR-023, FR-024)
- [X] T070 [US3] Ejecutar `cd frontend && npm test`

**Checkpoint**: se puede trabajar sobre cualquier listado del sistema.

---

## Phase 6: User Story 4 - Cargar un formulario y ver qué falta (Priority: P1)

**Goal**: los dieciséis formularios y sus componentes, con campos agrupados, obligatorios señalados,
errores donde se los busca y acciones siempre en el mismo lugar.

**Independent Test**: se envía el alta de un viaje con tres campos vacíos y se comprueba que los tres
errores se ubican sin leer el formulario entero y que guardar se distingue de cancelar.

- [X] T071 [P] [US4] `frontend/src/modules/usuarios/paginas/FormularioUsuario.tsx`
- [X] T072 [P] [US4] `frontend/src/modules/usuarios/paginas/CambiarPassword.tsx`
- [X] T073 [P] [US4] `frontend/src/modules/usuarios/personas/paginas/FormularioPersona.tsx`
- [X] T074 [P] [US4] `frontend/src/modules/usuarios/componentes/SelectorPersona.tsx`
- [X] T075 [P] [US4] `frontend/src/modules/usuarios/paginas/PanelRoles.tsx` y `frontend/src/modules/usuarios/componentes/PermisosDelRol.tsx`
- [X] T076 [P] [US4] `frontend/src/modules/choferes/paginas/FormularioChofer.tsx`
- [X] T077 [P] [US4] `frontend/src/modules/choferes/transportistas/FormularioTransportista.tsx`
- [X] T078 [P] [US4] `frontend/src/modules/choferes/documentacion/FormularioDocumento.tsx`, con su campo de archivo
- [X] T079 [P] [US4] `frontend/src/modules/flota/paginas/FormularioVehiculo.tsx`
- [X] T080 [P] [US4] `frontend/src/modules/flota/tiposVehiculo/FormularioTipoVehiculo.tsx`
- [X] T081 [P] [US4] `frontend/src/modules/flota/documentacion/FormularioDocumentoVehiculo.tsx`
- [X] T082 [P] [US4] `frontend/src/modules/viajes/paginas/FormularioViaje.tsx`
- [X] T083 [P] [US4] `frontend/src/modules/viajes/paginas/AsignacionViaje.tsx`
- [X] T084 [P] [US4] `frontend/src/modules/viajes/clientes/FormularioCliente.tsx`, que sirve al alta y a la edición
- [X] T085 [US4] `frontend/src/modules/facturacion/paginas/AltaFactura.tsx` con sus tres componentes: `componentes/SelectorDeViajes.tsx` —tabla dentro de un formulario, cuyo anuncio tiene que seguir dentro de un `[role="status"]`—, `componentes/ResumenDeImportes.tsx` y `componentes/VistaPreviaDocumento.tsx`, cuyo marco se integra a la pantalla **sin intentar estilar el PDF que muestra adentro**
- [X] T086 [P] [US4] `frontend/src/modules/facturacion/paginas/CorreccionFactura.tsx`
- [X] T087 [P] [US4] `frontend/src/modules/facturacion/paginas/EmpresaEmisora.tsx` y `componentes/CargaDeLogo.tsx`
- [X] T088 [US4] Verificar en los dieciséis formularios de `frontend/src/modules/` que los campos obligatorios se distinguen, que el ancho de cada campo acompaña a su dato —un CUIT y una patente no ocupan lo mismo que una razón social— y que las acciones están en el mismo lugar con la primaria distinguida de la secundaria (FR-026, FR-028, FR-030)
- [X] T089 [US4] Ejecutar `cd frontend && npm test`, prestando atención a las **28 llamadas a `selectOptions`** de diez archivos: son las que confirman que los `<select>` siguen siendo nativos

**Checkpoint**: se carga cualquier formulario del sistema sabiendo qué falta.

---

## Phase 7: User Story 5 - Abrir una ficha y leerla (Priority: P2)

**Goal**: las cinco fichas con encabezado que identifica, muestra estado y reúne las acciones —hoy
están al pie—, secciones navegables e historial como secuencia en el tiempo.

**Independent Test**: se abre la ficha de una factura vencida con historial y la de un viaje rendido,
y en las dos se identifica de un vistazo qué registro es, en qué estado está y qué se puede hacer.

- [X] T090 [P] [US5] `frontend/src/modules/usuarios/paginas/DetalleUsuario.tsx`
- [X] T091 [P] [US5] `frontend/src/modules/choferes/paginas/FichaChofer.tsx`, con su tabla de documentación y las clases `documento--historico` y `documento--reemplazado` que hoy no producen ningún efecto
- [X] T092 [P] [US5] `frontend/src/modules/flota/paginas/FichaVehiculo.tsx`, distinguiendo **el estado derivado que se muestra del guardado que se edita** (FR-033)
- [X] T093 [P] [US5] `frontend/src/modules/viajes/paginas/FichaViaje.tsx`
- [X] T094 [P] [US5] `frontend/src/modules/facturacion/paginas/FichaFactura.tsx`, con sus siete secciones ya existentes
- [X] T095 [US5] Subir las acciones del pie al encabezado en las cinco fichas —`DetalleUsuario.tsx`, `FichaChofer.tsx`, `FichaVehiculo.tsx`, `FichaViaje.tsx` y `FichaFactura.tsx`—: es **el único movimiento de estructura de toda la feature** y lo único que una revisión debería encontrar más allá de estilos (FR-031, contracts §3)
- [X] T096 [US5] Aplicar la primitiva `Historial` en `frontend/src/modules/viajes/paginas/FichaViaje.tsx` y `frontend/src/modules/facturacion/paginas/FichaFactura.tsx` (FR-034)
- [ ] T097 [US5] Verificar que una ficha de registro inmutable —viaje rendido, factura anulada— comunica visualmente que no ofrece acciones de escritura, y que un motivo de anulación de 500 caracteres se lee como párrafo (FR-033)
- [X] T098 [US5] Ejecutar `cd frontend && npm test`

**Checkpoint**: se lee cualquier ficha de un vistazo.

---

## Phase 8: User Story 6 - Que un aviso, una confirmación o un estado se noten (Priority: P2)

**Goal**: un solo diálogo, un solo indicador de estado, avisos y rechazos diferenciados sin depender
del color.

⚠️ **Es el único punto de la feature donde una dependencia toca algo que la suite verifica**: varios
de los 41 archivos abren diálogos. Por eso T099 a T104 van primero y T105 corre la suite enseguida.
Si el entorno de test diera problemas con Radix, el repliegue está escrito en [research §8](./research.md).

**Independent Test**: se disparan las bajas de un chofer, un vehículo, un cliente y un usuario y las
anulaciones de un viaje y de una factura, y los seis son el mismo diálogo con distinto contenido.

### Los cuatro diálogos con campos adentro

- [X] T099 [P] [US6] Reenganchar `frontend/src/modules/facturacion/componentes/ConfirmacionAnulacion.tsx` al contenedor `Dialogo`, reemplazando su `role="dialog"` propio
- [X] T100 [P] [US6] Reenganchar `frontend/src/modules/facturacion/componentes/ConfirmacionesDeEmision.tsx`, que lleva los dos diálogos de FR-032 del Módulo 6
- [X] T101 [P] [US6] Reenganchar `frontend/src/modules/facturacion/componentes/RegistrarCobro.tsx`
- [X] T102 [P] [US6] Reenganchar `frontend/src/modules/viajes/componentes/ConfirmacionAnulacion.tsx`, que lleva el campo de motivo
- [ ] T103 [US6] Verificar con teclado que en los nueve componentes de confirmación de `frontend/src/modules/*/componentes/` el foco **cicla dentro** y no se escapa al contenido de atrás, que `Escape` cierra y que el foco vuelve al elemento de origen (FR-035, FR-036)
- [ ] T104 [US6] Verificar que los seis diálogos de baja y anulación se ven como el mismo componente con distinto contenido (FR-034)
- [X] T105 [US6] Ejecutar `cd frontend && npm test`. **Si algo se rompe, es acá**: es el momento de la feature con más riesgo

### Estados y avisos

- [X] T106 [US6] Aplicar la primitiva `Estado` a los estados de documentación en `modules/choferes/servicios/estados.ts` y `modules/flota/servicios/estados.ts`, cuyas clases `estado--*` están escritas desde el Módulo 3 y **nunca tuvieron color**
- [X] T107 [P] [US6] Aplicar `Estado` a los estados de viaje y de factura en `frontend/src/modules/viajes/` y `frontend/src/modules/facturacion/`, conservando las palabras de `NombresDeEstado` y de los `TEXTO_ESTADO_*`, que FR-004 congela
- [X] T108 [P] [US6] Aplicar `Aviso` a los mensajes de resultado y de rechazo de `frontend/src/modules/`, conservando cada `role="status"` y `role="alert"` donde ya estaba
- [ ] T109 [US6] Verificar que los tres paneles de vencimientos —choferes, flota y facturas— muestran un mismo estado igual, y que ninguna distinción de estado depende sólo del color (FR-035, FR-040)
- [ ] T110 [US6] Verificar que la aparición de un aviso **no desplaza bruscamente** el contenido que se estaba leyendo (FR-037)

**Checkpoint**: nada del sistema se comunica sólo con color, y hay un solo diálogo.

---

## Phase 9: User Story 7 - Densidad, foco y ancho (Priority: P3)

**Goal**: el ajuste final sobre las estructuras ya definidas.

**Independent Test**: se abre el listado de facturas —ocho columnas, el más ancho— a 1280 px y al
200 % de zoom, y se recorre entero con el teclado.

- [ ] T111 [US7] Revisar la densidad de las quince pantallas de listado de `frontend/src/modules/`: cuánta información entra sin marear, con la escala de espaciado de Tailwind y sin valores sueltos
- [ ] T112 [US7] Verificar que el listado de facturas se lee **a 1280 px sin desplazamiento horizontal de la página** (FR-042, SC-010)
- [ ] T113 [US7] Aplicar desplazamiento contenido en la tabla, en `frontend/src/compartido/ui/Listado.tsx`, a los listados que no entren, sin arrastrar al resto de la pantalla, y evitar que un texto largo en una celda empuje las columnas de importe fuera de la vista (FR-044)
- [ ] T114 [US7] Verificar el 200 % de zoom en las 42 pantallas: el texto no se corta, no se superpone y no aparece desplazamiento horizontal (FR-043)
- [ ] T115 [US7] Verificar que el contenido respeta `--container-lectura` en un monitor de 2560 px (FR-017)
- [ ] T116 [US7] Recorrer el alta de una factura **entera con el teclado**, sin mouse, desde el menú hasta emitir, viendo el foco en todo momento —incluidos la tabla de selección de viajes y los diálogos— (FR-039, SC-009)
- [ ] T117 [US7] Verificar que las transiciones que el rediseño haya incorporado respetan la preferencia de movimiento reducido del sistema operativo (FR-041)

**Checkpoint**: el rediseño está terminado. Falta comprobar que no se llevó nada puesto.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: la validación, que en esta feature es la parte importante.

- [X] T118 Ejecutar `cd backend && dotnet test` y `cd frontend && npm test` y dejar la suite entera en verde
- [X] T119 Ejecutar `cd frontend && npm run build` y `npm run lint`
- [ ] T120 Medir el contraste con las herramientas del navegador sobre los diez pares de [data-model §1.1](./data-model.md): 4,5:1 para texto y 3:1 para lo no textual que comunica (SC-008)
- [ ] T121 [P] Convertir a **escala de grises** capturas del listado de facturas, de un formulario con errores y de un panel de vencimientos, según el paso 42 de `specs/007-diseno-interfaz/quickstart.md`, y comprobar que no se pierde información (SC-012)
- [X] T122 [P] Buscar en `frontend/src/` valores arbitrarios entre corchetes: cualquiera es un valor fuera del `@theme` y hay que llevarlo a un token o justificarlo (FR-008)
- [ ] T123 Recorrer los 42 pasos de `specs/007-diseno-interfaz/quickstart.md` con las tres cuentas —`admin`, `trafico` y `gerencia`— y anotar cualquier discrepancia
- [ ] T124 **Recorrer enteros los seis quickstarts anteriores** —`specs/001-autenticacion-usuarios/quickstart.md`, `002-gestion-usuarios-roles`, `003-gestion-choferes`, `004-gestion-flota`, `005-gestion-viajes` y `006-gestion-facturacion`— y verificar que ninguno encuentra una diferencia de comportamiento: los mismos textos, los mismos pasos, los mismos resultados. Es SC-001 y es la prueba principal de toda la feature. Una sola diferencia alcanza para que el rediseño no esté terminado
- [X] T125 Actualizar `specs/README.md` con la fila del Módulo 7 y con lo que el recorrido haya encontrado
- [X] T126 Actualizar `AGENTS.md` con las decisiones transversales de esta feature en la sección *Decisiones transversales ya tomadas*, una línea por decisión con la referencia `[007]`. Confirmar contra lo realmente implementado las cinco candidatas que anota `plan.md` §Etapa 8 —la variante obligatoria y tipada, el límite del árbol accesible al incorporar una biblioteca, agrupar no es autorizar, la suite congelada como prueba de que el comportamiento no cambió, y separar contenedor de contenido— y **descartar las que no resulten transversales**: no se agregan entradas por completar la lista

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias, arranca de inmediato
- **Foundational (Fase 2)**: depende de la Fase 1 — **bloquea todas las historias**
- **US1 (Fase 3)**: depende de la Fase 2 — **bloquea a todas las demás historias**, porque son sus piezas
- **US2 (Fase 4)**: depende de US1
- **US3 (Fase 5)**, **US4 (Fase 6)**, **US5 (Fase 7)**, **US6 (Fase 8)**: dependen de US1. Son independientes entre sí: tocan archivos distintos
- **US7 (Fase 9)**: depende de US3 a US6, porque se verifica sobre lo que ellas construyen
- **Polish (Fase 10)**: depende de todo

### User Story Dependencies

A diferencia de los módulos anteriores, acá **el orden importa** y las historias no son
intercambiables: US1 es el vocabulario y sin él ninguna otra puede escribirse. De US2 en adelante el
orden es por conveniencia —US2 primero porque cambia el aspecto de las 42 pantallas de una vez—, y
US3 a US6 podrían reordenarse sin romper nada.

### Dentro de cada historia

Las tareas marcadas `[P]` tocan archivos distintos y no se pisan. Las que no lo están, o dependen de
una anterior, o son una verificación que necesita ver el conjunto terminado.

### Parallel Opportunities

- **Fase 3**: T013 a T022 son diez primitivas en diez archivos, todas en paralelo
- **Fase 4**: T038 a T042 son cinco módulos, uno por persona
- **Fase 5**: T046 a T050 (cinco filtros) y T051 a T065 (quince listados) — es la fase con más paralelismo del proyecto
- **Fase 6**: T071 a T087, dieciséis formularios en archivos distintos
- **Fase 7**: T090 a T094, cinco fichas
- **Fase 8**: T099 a T102, los cuatro diálogos con campos

## Parallel Example: User Story 3

```bash
# Los cinco bloques de filtros, juntos:
T046  FiltrosUsuarios.tsx
T047  FiltrosTransportistas.tsx
T048  FiltrosFlota.tsx
T049  FiltrosViajes.tsx
T050  FiltrosFacturas.tsx

# Los quince listados, juntos:
T051 … T065
```

---

## Implementation Strategy

### MVP primero

El MVP de esta feature **no es US1**: US1 no se ve. El primer incremento con valor es
**US1 + US2**, que deja el sistema entero con identidad, navegación agrupada y encabezado en las 42
pantallas. Es lo que se puede mostrar.

Hay que tener presente que entre T008 y el final de la Fase 4 la aplicación **se ve peor que al
empezar**: la hoja vieja ya no está y las pantallas todavía no usan las piezas nuevas. No conviene
frenar en el medio.

### Entrega incremental

1. Fases 1 y 2 → dependencias y tokens
2. Fase 3 (US1) → el vocabulario
3. Fase 4 (US2) → **primer incremento mostrable**
4. Fase 5 (US3) → lo que más se usa
5. Fases 6 a 8 (US4, US5, US6) → el resto de las pantallas
6. Fase 9 (US7) → el ajuste
7. Fase 10 → la validación, que es lo que decide si está terminado

### Estrategia con varios desarrolladores

Después de la Fase 3, US3 a US6 se reparten sin conflicto: tocan archivos distintos. Lo que **no** se
reparte es la Fase 3: las primitivas las decide una sola persona, porque son las que van a hacer que
las 42 pantallas se vean como un producto y no como seis.

---

## Notes

- **La regla operativa de todo el recorrido** ([contracts §4](./contracts/README.md)): reordenar,
  envolver, cambiar clases y mover bloques **no rompe ningún test**. Quitar o renombrar una etiqueta,
  un nombre accesible, un rol o un texto visible, **sí**
- **Las tres únicas líneas de la suite atadas a la estructura**: el anuncio de `SelectorDeViajes`
  tiene que seguir dentro de un `[role="status"]` (T085), las filas del listado de facturas tienen
  que seguir siendo `<tr>` (T063) y el listado de viajes no puede tener `<tfoot>` (T060)
- **Los `<select>` no se reemplazan.** Diez archivos de test hacen 28 llamadas a `selectOptions`
  sobre los selectores de 17 pantallas (T089)
- Los textos no se reescriben (FR-004). Los únicos textos nuevos de la feature son los cinco rótulos
  de sección y las dos etiquetas de menú de [contracts §2](./contracts/README.md)
