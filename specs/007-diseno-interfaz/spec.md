# Feature Specification: Rediseño de la aplicación (Módulo 7)

**Feature Branch**: `007-diseno-interfaz`

**Created**: 2026-08-17

**Status**: Draft

**Input**: User description: "ux-ui" — precisado por quien conduce el producto como: *"quiero hacer
todo un rediseño, está todo bastante horrible y genérico (toda la aplicación web)"*.

## Punto de partida

Los seis módulos de negocio están implementados y validados: 42 pantallas, 20 de ellas con tablas de
datos, todas con marcado semántico, textos en español rioplatense y anuncios accesibles puestos
módulo por módulo. Lo que nunca se construyó es el **diseño**. No falta una mano de pintura: faltan
las tres capas.

El relevamiento encontró esto. Es el único tramo técnico del documento —el diagnóstico de dónde se
parte—; de acá en adelante todo se describe por lo que se ve en pantalla:

**No hay capa visual.** Todo el sistema corre sobre una única hoja de estilos de 136 líneas rotulada
*"Estilos mínimos del Módulo 1"*, escrita cuando la única pantalla era la de ingreso.

- De las **31 clases** que las pantallas aplican a sus elementos, la hoja define **8**. Las otras
  **23 no existen**: `campo__error` se escribe en 65 lugares, `acciones` en 19, `formulario__error`
  en 5, `paginacion` y `filtros` en 4 cada una, y ninguna produce ningún efecto. Tampoco `atenuada`,
  `atenuado`, `con-error`, `documento--historico`, `documento--reemplazado` ni las siete variantes de
  `estado--` que choferes y flota generan para los semáforos de documentación
- Hay **promesas de specs anteriores que hoy no se ven**. La convención de los Módulos 3 a 6 dice que
  *"todo elemento atenuado lleva además la palabra que lo explica"*. La palabra está; el atenuado no.
  Una factura anulada y una vigente se ven idénticas salvo por el texto de una columna. Los
  semáforos de documentación tienen la clase del color escrita desde el Módulo 3 y no tienen color
- Una regla global pinta **todos** los botones como botón primario azul. Como la celda que abre la
  ficha de cada fila es un botón, cada listado muestra una columna de botones azules gruesos; y en
  los formularios *Guardar* y *Cancelar* son visualmente idénticos

**No hay estructura ni arquitectura de información.**

- El menú es una **lista plana de 14 entradas** en una barra horizontal, sin agrupar, mezclando
  operación diaria (*Viajes*, *Facturas*) con catálogos de configuración (*Tipos de documentación*,
  *Tipos de vehículo*). *Totales* y *Totales facturados* quedan una al lado de la otra, y el propio
  código del servidor comenta el choque de nombres como un problema conocido
- **Dos pantallas no tienen entrada en el menú**: los paneles de vencimientos de choferes y de flota.
  Se llega a ellas sólo desde adentro del módulo
- La pantalla de inicio es un saludo —*"Hola, {usuario}"* y la lista de roles— y nada más. Después de
  ingresar, nadie sabe adónde ir salvo mirando la barra
- El encabezado pone la marca, el usuario, *Cambiar contraseña* y *Cerrar sesión* en una sola línea
  sin jerarquía: cerrar la sesión pesa visualmente lo mismo que la acción principal de la pantalla

**No hay vocabulario de componentes.** Cada módulo resolvió por su cuenta lo mismo que los otros:
**nueve** componentes de confirmación distintos y **cuatro** controles de paginación distintos para
el mismo trabajo. Los listados son tablas crudas con los filtros sueltos encima; las fichas son
secuencias de párrafos sin agrupar.

Esto es lo que se lee como **genérico**: no es que el diseño sea feo, es que no hay uno. Esta feature
lo hace.

## Encuadre: qué autoriza esta spec

Esta feature es un **rediseño de la aplicación entera**, no una capa de estilo sobre lo construido.

Sobre el Principio III de la constitución —*Cero Alcance Fantasma*—: no se lo suspende ni hace falta.
Ese principio prohíbe **construir lo que la spec no pide**; no le pone techo a lo que una spec puede
pedir. Lo que este documento hace es pedir el rediseño completo, explícitamente, para que
construirlo esté dentro de alcance y no fuera.

**Se rediseña, sin límite previo:**

1. La **identidad visual** completa: color, tipografía, escala, espaciado, bordes, profundidad,
   iconografía, densidad
2. La **estructura de la aplicación**: navegación, agrupación de las secciones, ubicación de la
   sesión, pantalla de inicio, encabezado de cada pantalla
3. La **disposición interna de las 42 pantallas**: cómo se arma un listado, un formulario y una
   ficha, y qué recibe el peso visual en cada una
4. El **vocabulario de componentes**: un solo botón, un solo campo, una sola tabla, un solo diálogo,
   una sola paginación, un solo indicador de estado, usados por los seis módulos, en reemplazo de los
   nueve componentes de confirmación y las cuatro paginaciones que hoy conviven

**Se conserva, y es deliberado:**

1. **Qué hace cada pantalla**: los datos que muestra, las operaciones que ofrece y los pasos de cada
   flujo. El rediseño cambia cómo se ve y cómo se llega, no qué pasa
2. **Quién puede hacer qué**: los permisos, y el hecho de que el servidor es la única fuente de
   verdad de qué opciones existen para cada usuario
3. **Las 42 direcciones**: las URL no cambian, porque el servidor las nombra al armar el menú
4. **Los textos operativos** que las specs anteriores fijaron palabra por palabra: mensajes de error,
   de confirmación, de estado vacío, etiquetas de campo y verbos de botón. El rediseño sí escribe los
   textos **nuevos** que introduzca —títulos de sección, rótulos de agrupación—, pero no reescribe
   los que ya están. Son lo único del producto que nadie llamó genérico, y son el contrato contra el
   que corren los seis quickstarts y los 41 archivos de test que prueban que el rediseño no rompió
   nada
5. **Los formatos**: pesos argentinos, fechas y español rioplatense quedan como están
6. **El piso de accesibilidad**, que en esta feature sube y en ningún caso baja
7. **El documento PDF de la factura**, que tiene su propio diseño definido en el Módulo 6

## Clarifications

### Session 2026-08-17

- Q: ¿En qué equipos se opera el sistema? Define si las 20 pantallas con tabla necesitan una forma
  alternativa de mostrarse en pantallas angostas. → A: **Escritorio y notebook solamente, de 1280 px
  para arriba.** Tablet y celular quedan fuera de alcance (FR-042, FR-044).
- Q: ¿G&T Logística tiene colores institucionales, logotipo o tipografía de marca que la interfaz
  deba respetar? → A: **Los tiene, y se decide deliberadamente no usarlos.** Quien conduce el
  producto considera que esa tipografía y esos colores empeorarían la interfaz. El sistema define
  identidad propia y el encabezado lo identifica por su nombre escrito, no por un logotipo. **Queda
  anotado como decisión, no como omisión**: si alguien revisa esta spec más adelante y no encuentra
  la marca de la empresa, es porque se resolvió dejarla afuera (FR-006, FR-007).
- Q: ¿El alcance es vestir lo construido o rediseñar? → A: **Rediseñar la aplicación entera.** El
  encuadre de arriba fija qué se rediseña y qué se conserva. La primera versión de esta spec estaba
  escrita como capa visual y se descartó por angosta.
- Q: Si el menú lo calcula el servidor y el frontend dibuja lo que recibe, ¿cómo se agrupan las 14
  entradas sin romper esa regla? → A: **El frontend agrupa, el servidor sigue decidiendo.** La
  pantalla ubica cada opción recibida en su sección según su código, y muestra únicamente las que
  llegaron; una sección sin opciones autorizadas no aparece. Decidir **dónde va** una opción es
  presentación; decidir **si existe** sigue siendo del servidor, que es lo que la regla del Módulo 2
  protege. Un código que la pantalla no conozca cae en la última sección y se ve igual, así que un
  módulo futuro aparece en el menú sin tocar el frontend (FR-011, FR-012).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Un lenguaje visual único para todo el sistema (Priority: P1)

El sistema tiene una identidad propia: una paleta, una escala tipográfica, una de espaciado, un
tratamiento de superficie y un juego de componentes. Cualquier pantalla de cualquier módulo se
reconoce como parte del mismo producto, y quien construya el módulo siguiente arma su pantalla con
las piezas que ya existen en lugar de inventarlas.

**Why this priority**: es la base de las otras seis historias. Sin un vocabulario definido, cada
pantalla que se rediseñe después vuelve a decidir sus valores desde cero y el resultado es otra vez
inconsistente — que es exactamente cómo se llegó a los nueve diálogos y las cuatro paginaciones.

**Independent Test**: se abre una pantalla de cada módulo y se comprueba que comparten paleta,
tipografía, espaciado y componentes; y que el mismo control —un botón primario, un campo, un
indicador de estado— se ve idéntico en las seis.

**Acceptance Scenarios**:

1. **Given** una pantalla de cada uno de los seis módulos, **When** se las compara, **Then** usan la
   misma paleta, la misma escala tipográfica y el mismo espaciado, sin valores sueltos fuera de ellas
2. **Given** un botón que ejecuta la acción principal y uno que cancela, **When** se los mira en
   cualquier pantalla del sistema, **Then** se distinguen entre sí y son iguales a los de las demás
   pantallas
3. **Given** los indicadores de estado de documentación, de viaje, de factura y de vehículo, **When**
   se los compara, **Then** son el mismo recurso visual con distinto valor, y ninguno depende sólo
   del color
4. **Given** cualquier pantalla, **When** se la mira, **Then** ningún color, tamaño de texto ni
   separación queda fuera del sistema definido

---

### User Story 2 - Entrar y saber adónde ir (Priority: P1)

Después de ingresar, la aplicación muestra dónde está parado el usuario y qué puede hacer. Las
secciones están agrupadas por para qué sirven —lo que se opera todos los días, lo que se administra,
lo que se configura— en lugar de una fila de catorce nombres. La pantalla de inicio deja de ser un
saludo y ofrece los accesos que los permisos de esa persona habilitan.

**Why this priority**: es el primer contacto y lo que hoy peor está. Catorce entradas planas con dos
que se llaman casi igual, y dos pantallas a las que no se llega desde el menú, es un problema de
orientación, no de estética.

**Independent Test**: se ingresa con una cuenta de Tráfico y con `admin`, y se comprueba que cada una
ve sus secciones agrupadas, que ninguna ve una opción que no le corresponde, y que desde el inicio se
llega a todo lo que puede usar.

**Acceptance Scenarios**:

1. **Given** una sesión de `admin` con todas las opciones, **When** se mira la navegación, **Then**
   las opciones aparecen agrupadas por sección y no como una lista plana
2. **Given** una sesión de un usuario de Tráfico, **When** se mira la navegación, **Then** ve
   únicamente las opciones que sus permisos habilitan, y las secciones que quedan sin ninguna opción
   no se muestran
3. **Given** una sesión abierta en el listado de viajes, **When** se mira la navegación, **Then** la
   opción correspondiente se distingue de las demás por algo más que el color, y también se distingue
   la sección que la contiene
4. **Given** una sesión recién iniciada, **When** se mira la pantalla de inicio, **Then** ofrece los
   accesos que los permisos de esa persona habilitan, sin pedirle al servidor ningún dato que hoy no
   le pida
5. **Given** un usuario cuyos roles todavía no habilitan ninguna opción, **When** ingresa, **Then**
   la pantalla de inicio se ve completa y explica la situación, sin quedar a medio armar
6. **Given** cualquier pantalla, **When** se mira su parte superior, **Then** el título de la
   pantalla pesa más que las acciones de sesión, y las acciones de cuenta no compiten con la acción
   principal
7. **Given** los paneles de vencimientos de choferes y de flota, **When** se busca cómo llegar,
   **Then** se llega desde la navegación como a cualquier otra pantalla
8. **Given** cualquier pantalla, **When** se mira la pestaña del navegador, **Then** dice el nombre
   del sistema y el de la pantalla, y muestra un ícono propio del producto

---

### User Story 3 - Trabajar sobre un listado (Priority: P1)

Las 20 pantallas con tabla son donde Tráfico y Administración pasan el día. Se rediseñan como una
unidad: una barra con el título y la acción principal, los filtros presentados como un bloque
resuelto, la tabla legible en diagonal, y los estados de carga, vacío y sin coincidencias tratados
como parte del listado y no como texto suelto.

**Why this priority**: es el uso principal del sistema. Una tabla sin líneas ni alternancia, con una
columna de botones azules y los importes alineados a la izquierda, obliga a seguir la fila con el
dedo.

**Independent Test**: se abre el listado de facturas con al menos una anulada, una pagada y una
vencida, y se comprueba que las tres se distinguen, que los totales se comparan verticalmente y que
los filtros, la paginación y los estados vacíos se ven como parte de una sola pieza.

**Acceptance Scenarios**:

1. **Given** cualquier listado, **When** se lo mira, **Then** el título, la acción principal, los
   filtros, la tabla y la paginación se leen como una sola pieza y no como bloques apilados
2. **Given** un listado con diez o más filas, **When** se lo mira, **Then** cada fila se separa
   visualmente de la siguiente sin necesidad de contar columnas
3. **Given** un listado con columna de importes, **When** se lo mira, **Then** los importes quedan
   alineados de modo que sus separadores de miles y decimales caen en la misma vertical
4. **Given** el listado de facturas con una factura anulada, **When** se lo mira, **Then** esa fila
   se ve atenuada respecto de las demás, conserva la palabra que lo explica y su texto sigue siendo
   legible
5. **Given** cualquier listado, **When** se mira la celda que abre la ficha, **Then** se ve como
   acceso a un detalle y no como la acción principal de la pantalla
6. **Given** los listados de choferes, flota, viajes y facturas, **When** se comparan sus controles
   de paginación, **Then** son el mismo control
7. **Given** un listado vacío, uno filtrado sin coincidencias y uno cargando, **When** se los mira,
   **Then** los tres se distinguen entre sí y ninguno se confunde con una fila de datos
8. **Given** el control que declara qué se está mostrando, **When** se lo mira, **Then** se lee como
   parte del listado y sigue diciendo con todas las letras qué filas se están ocultando

---

### User Story 4 - Cargar un formulario y ver qué falta (Priority: P1)

Las altas y ediciones —viaje, factura, chofer, vehículo, cliente, usuario, persona, documento— se
rediseñan con los campos agrupados por sentido, cada uno con el ancho que su dato pide, los
obligatorios señalados, los errores donde se los busca, y las acciones siempre en el mismo lugar con
la que guarda distinguida de la que cancela.

**Why this priority**: es la otra mitad del uso diario, y donde un error visual cuesta un dato mal
cargado. Hoy el mensaje de error de un campo no tiene ningún tratamiento y *Guardar* y *Cancelar* son
el mismo botón.

**Independent Test**: se envía el alta de un viaje con tres campos vacíos y se comprueba que los tres
errores se ubican sin leer el formulario entero, y que la acción de guardar se distingue de la de
cancelar.

**Acceptance Scenarios**:

1. **Given** un formulario con más de seis campos, **When** se lo mira, **Then** los campos están
   agrupados por sentido y cada grupo se reconoce como tal
2. **Given** un formulario de alta, **When** se lo mira antes de completarlo, **Then** se distingue
   cuáles campos son obligatorios
3. **Given** un formulario con errores por campo, **When** se muestra el rechazo, **Then** cada error
   se ve junto a su campo, el campo queda marcado por algo más que el color, y el texto del error
   mantiene el contraste exigido
4. **Given** un formulario con un error general —un duplicado, un conflicto de estado—, **When** se
   muestra el rechazo, **Then** el mensaje se destaca del resto y se distingue de un error de campo
5. **Given** cualquier formulario del sistema, **When** se miran sus acciones, **Then** están en el
   mismo lugar que en todos los demás y la que guarda se distingue de la que cancela
6. **Given** un control deshabilitado —guardar sin cambios, emitir sin viajes seleccionados—,
   **When** se lo mira, **Then** se ve deshabilitado y no se confunde con uno disponible
7. **Given** un campo de CUIT, uno de patente y uno de razón social en el mismo formulario, **When**
   se los mira, **Then** el ancho de cada uno acompaña al dato que recibe

---

### User Story 5 - Abrir una ficha y leerla (Priority: P2)

Las fichas —viaje, factura, chofer, vehículo, cliente, usuario— se rediseñan con un encabezado que
identifica al registro, muestra su estado y reúne sus acciones, y el cuerpo dividido en secciones
reconocibles: los datos, la documentación, el historial. Hoy son secuencias de párrafos donde el
número, el estado y el botón de anular pesan lo mismo.

**Why this priority**: la ficha es donde se decide operar, pero se llega a ella desde el listado.
Rediseñarla antes que el listado dejaría el camino a medio hacer.

**Independent Test**: se abre la ficha de una factura vencida con historial y la de un viaje rendido,
y se comprueba que en las dos se identifica de un vistazo qué registro es, en qué estado está y qué
se puede hacer con él.

**Acceptance Scenarios**:

1. **Given** la ficha de cualquier registro, **When** se la abre, **Then** el encabezado dice qué
   registro es, en qué estado está y qué acciones ofrece, sin necesidad de recorrer la pantalla
2. **Given** una ficha con datos, documentación e historial, **When** se la mira, **Then** las
   secciones se distinguen entre sí y se puede saltar a la que interesa
3. **Given** la ficha de un registro inmutable —un viaje rendido, una factura anulada—, **When** se
   la mira, **Then** se entiende que no ofrece acciones de escritura, y por qué
4. **Given** la ficha de un vehículo cuyo estado guardado difiere del derivado, **When** se la mira,
   **Then** se distingue cuál es el valor que se muestra y cuál el que se edita
5. **Given** un historial de varias líneas, **When** se lo mira, **Then** se lee como una secuencia
   en el tiempo y no como una tabla más
6. **Given** un motivo de anulación de 500 caracteres, **When** se lo mira, **Then** se lee como
   párrafo y no como una línea estirada

---

### User Story 6 - Que un aviso, una confirmación o un estado se noten (Priority: P2)

Los nueve componentes de confirmación se reemplazan por uno solo. Los avisos de resultado, los
rechazos y los indicadores de estado comparten tratamiento en los seis módulos, se distinguen entre
sí sin depender del color, y ninguno pasa inadvertido ni desplaza de golpe lo que se estaba leyendo.

**Why this priority**: los nueve diálogos ya funcionan y ya anuncian correctamente; lo que falta es
que sean uno solo y que se noten. Es corrección de coherencia sobre algo que opera.

**Independent Test**: se disparan las bajas de un chofer, un vehículo, un cliente y un usuario y las
anulaciones de un viaje y de una factura, y se comprueba que los seis son el mismo diálogo con
distinto contenido.

**Acceptance Scenarios**:

1. **Given** las seis confirmaciones del sistema, **When** se las compara, **Then** son el mismo
   componente: misma disposición, mismo tratamiento del texto, mismas posiciones de las acciones
2. **Given** un diálogo abierto, **When** se lo mira, **Then** se distingue con claridad del
   contenido que quedó detrás
3. **Given** un diálogo abierto, **When** se lo recorre con el teclado, **Then** el foco se ve en
   todo momento y no se escapa detrás del diálogo
4. **Given** un guardado exitoso anunciado sin cambio de pantalla, **When** aparece el aviso,
   **Then** se ve como confirmación y se distingue de un rechazo por algo más que el color
5. **Given** un rechazo del servidor, **When** aparece el mensaje, **Then** se destaca sin que su
   aparición desplace bruscamente el contenido que la persona estaba leyendo
6. **Given** los paneles de vencimientos de choferes, de flota y de facturas, **When** se los
   compara, **Then** un mismo estado se ve igual en los tres

---

### User Story 7 - Densidad, foco y ancho (Priority: P3)

El último ajuste: cuánta información entra por pantalla sin marear, que el foco del teclado se vea
siempre, que el sistema aguante el zoom al 200 % y que a 1280 px ninguna pantalla obligue a
desplazarse de costado.

**Why this priority**: se verifica sobre las estructuras que definen las historias anteriores, así
que va al final. El rango de anchos es angosto y conocido, lo que lo vuelve la historia más acotada.

**Independent Test**: se abre el listado de facturas —ocho columnas, el más ancho del sistema— a
1280 px y al 200 % de zoom, y se recorre entero con el teclado.

**Acceptance Scenarios**:

1. **Given** el listado de facturas a 1280 px de ancho, **When** se lo mira, **Then** las ocho
   columnas se leen sin desplazamiento horizontal de la página
2. **Given** cualquier pantalla con el navegador al 200 % de zoom, **When** se la mira, **Then** el
   texto no se corta ni se superpone y no aparece desplazamiento horizontal de la página
3. **Given** una tabla que aun así no entra en el ancho disponible, **When** se la mira, **Then** el
   desplazamiento queda contenido en la tabla y el resto de la pantalla no se mueve
4. **Given** cualquier pantalla en un monitor de 2560 px, **When** se la mira, **Then** el contenido
   respeta su ancho máximo de lectura en lugar de estirarse de borde a borde
5. **Given** cualquier elemento con el que se pueda interactuar, **When** se llega a él con el
   teclado, **Then** el foco se ve, incluido dentro de tablas y de diálogos

---

### Edge Cases

- **Textos largos en celdas**: una razón social de 200 caracteres, o un origen y un destino largos en
  la misma fila, no pueden romper el ancho de la tabla ni empujar las columnas de importe fuera de la
  vista
- **Menú sin opciones**: un usuario cuyos roles no habilitan nada ve una aplicación completa y una
  pantalla de inicio que explica la situación, no un armazón vacío
- **Una sección de navegación con una sola opción autorizada**: se muestra igual, sin quedar como un
  grupo a medio llenar
- **Una opción de menú cuyo código la pantalla no conoce** —un módulo futuro—: aparece igual, en la
  última sección, sin necesidad de tocar el frontend
- **Listado vacío vs. filtrado sin coincidencias**: las specs anteriores fijaron dos textos distintos
  a propósito; los dos tratamientos tienen que dejar clara la diferencia
- **El aviso que aparece mientras se está leyendo otra cosa**: no puede desplazar de golpe el
  contenido bajo el cursor
- **La vista previa del documento de factura**: es un PDF ya diseñado dentro de un marco; el marco se
  integra a la pantalla sin intentar estilar el documento
- **Escala de grises y daltonismo**: toda distinción que dependa del color tiene que seguir
  distinguiéndose sin él
- **Preferencia de movimiento reducido**: si el rediseño incorpora transiciones, las respeta
- **Un listado con una sola fila** y **uno con el máximo por página**: los dos se ven terminados
- **Una ficha sin historial y una con veinte líneas**: las dos se ven terminadas

## Requirements *(mandatory)*

### Functional Requirements

#### Límites del rediseño

- **FR-001**: El rediseño NO cambia qué hace cada pantalla: los datos que muestra, las operaciones
  que ofrece y los pasos de cada flujo quedan como están. Nada de lo que hoy pide confirmación deja
  de pedirla, y nada que hoy navegue al guardar deja de navegar
- **FR-002**: El rediseño NO cambia quién puede hacer qué. El servidor sigue siendo la única fuente
  de verdad de qué opciones de menú existen para cada usuario, y ninguna pantalla decide visibilidad
  por permisos por su cuenta
- **FR-003**: Las 42 direcciones del sistema no cambian
- **FR-004**: Los textos operativos ya fijados por las specs de los seis módulos —mensajes de error,
  de confirmación, de estado vacío, etiquetas de campo y verbos de botón— no se reescriben. El
  rediseño sí escribe los textos nuevos que introduzca, como títulos de sección o rótulos de
  agrupación
- **FR-005**: Todo lo que hoy anuncia un lector de pantalla lo sigue anunciando igual: los avisos de
  resultado que aparecen sin cambiar de pantalla, el encabezado y el título de cada tabla, el nombre
  de cada control y la etiqueta de cada campo. Ningún elemento con significado se reemplaza por uno
  decorativo para lograr un efecto visual

#### Identidad y sistema de diseño

- **FR-006**: La interfaz **no adopta la identidad institucional de G&T Logística**. La empresa tiene
  colores y tipografía de marca, y la decisión explícita de quien conduce el producto es no llevarlos
  a la aplicación. El sistema define identidad propia
- **FR-007**: El encabezado identifica al sistema por su nombre escrito, no por un logotipo. El logo
  que la empresa emisora carga en el Módulo 6 existe para el documento de la factura y no se lleva a
  ninguna pantalla
- **FR-008**: El sistema define una **paleta** —fondo, superficies, texto principal, texto
  secundario, bordes, acento, y los colores de éxito, advertencia y error—, una **escala
  tipográfica** y una **escala de espaciado**, y ningún elemento de ninguna pantalla usa un valor
  fuera de ellas
- **FR-009**: El sistema define un **juego de componentes** que los seis módulos comparten: botón en
  sus variantes, campo, selector, tabla, indicador de estado, diálogo, aviso, paginación y bloque de
  filtros. Los nueve componentes de confirmación y los cuatro controles de paginación existentes
  quedan reemplazados por uno de cada clase
- **FR-010**: El documento declara que su contenido está en español, el título de la pestaña
  identifica al sistema y a la pantalla abierta y cambia al navegar, y el ícono de pestaña es propio
  del producto

#### Estructura y navegación

- **FR-011**: Las opciones de menú se presentan **agrupadas por sección** según para qué sirven, no
  como una lista plana. La agrupación la resuelve la pantalla a partir del código de cada opción
  recibida
- **FR-012**: La navegación muestra únicamente las opciones que el servidor autorizó. Una sección sin
  opciones autorizadas no se muestra, y una opción cuyo código la pantalla no conozca se muestra
  igual, en la última sección
- **FR-013**: Los paneles de vencimientos de choferes y de flota se alcanzan desde la navegación,
  como cualquier otra pantalla
- **FR-014**: La navegación distingue la opción de la pantalla abierta **y** la sección que la
  contiene, por algo más que el color
- **FR-015**: La pantalla de inicio ofrece los accesos que los permisos de esa persona habilitan, sin
  pedirle al servidor ningún dato que hoy no le pida. Con la sesión sin opciones habilitadas, explica
  la situación y se ve terminada
- **FR-016**: Toda pantalla tiene un encabezado con el mismo tratamiento: título, y la acción
  principal cuando la hay. Las acciones de sesión y de cuenta no compiten visualmente con ellas
- **FR-017**: El contenido tiene un ancho máximo de lectura: en un monitor ancho no se estira de
  borde a borde

#### Listados

- **FR-018**: Un listado se presenta como una pieza única —encabezado, filtros, tabla y paginación—,
  con la misma anatomía en las 20 pantallas que lo usan
- **FR-019**: Las filas de una tabla se distinguen entre sí a simple vista, y el encabezado de
  columnas se distingue del cuerpo
- **FR-020**: Las columnas de importe se alinean de modo que los números se comparen en vertical, y
  las de fecha llevan un tratamiento uniforme en todo el sistema
- **FR-021**: Una fila atenuada por regla —anulada, dada de baja— se ve efectivamente atenuada,
  conserva la palabra que lo explica y mantiene el contraste mínimo de FR-038
- **FR-022**: La celda que abre la ficha de una fila se presenta como acceso a un detalle y no como
  la acción principal de la pantalla
- **FR-023**: Los estados de listado vacío, de filtrado sin coincidencias y de carga tienen
  tratamiento propio, se distinguen entre sí y ninguno se confunde con una fila de datos
- **FR-024**: El control que declara qué filas se están mostrando se integra al listado y sigue
  diciendo con todas las letras qué se está ocultando

#### Formularios

- **FR-025**: Los campos de un formulario de más de seis campos se agrupan por sentido, y cada grupo
  se reconoce como tal
- **FR-026**: Los campos obligatorios se distinguen de los opcionales antes de intentar guardar
- **FR-027**: El error de un campo se muestra junto a su campo, el campo queda marcado por algo más
  que el color, y el texto del error cumple el contraste de FR-038. El error general del formulario
  se destaca del resto y se distingue de un error de campo
- **FR-028**: Las acciones están en el mismo lugar en todos los formularios del sistema, y la que
  guarda se distingue de la que cancela
- **FR-029**: Un control deshabilitado se ve deshabilitado y no se confunde con uno disponible
- **FR-030**: El ancho de cada campo acompaña al dato que recibe

#### Fichas

- **FR-031**: Toda ficha tiene un encabezado que identifica al registro, muestra su estado y reúne
  sus acciones, con la misma anatomía en los seis módulos
- **FR-032**: El cuerpo de una ficha se divide en secciones reconocibles y navegables
- **FR-033**: Una ficha de un registro inmutable comunica visualmente que no ofrece acciones de
  escritura, y en la ficha de un vehículo se distingue el valor derivado que se muestra del valor
  guardado que se edita
- **FR-034**: El historial se presenta como una secuencia en el tiempo, distinguible de las tablas de
  datos

#### Estados, avisos y confirmaciones

- **FR-035**: Todos los estados del sistema comparten un mismo recurso visual, aplicado igual en el
  listado, en la ficha y en los paneles de vencimientos, y ninguna distinción de estado se comunica
  sólo por el color
- **FR-036**: Todas las confirmaciones del sistema son el mismo componente, se distinguen con
  claridad del contenido que queda detrás, y el foco del teclado se ve en todo momento dentro de ellas
- **FR-037**: Un aviso de resultado exitoso y un mensaje de rechazo se distinguen entre sí por algo
  más que el color, y la aparición de cualquiera de los dos no desplaza bruscamente el contenido que
  la persona está leyendo

#### Accesibilidad

- **FR-038**: Todo el texto cumple una relación de contraste de al menos 4,5:1 contra su fondo (3:1
  para texto grande), y todo elemento no textual que comunique información, al menos 3:1
- **FR-039**: El foco del teclado se ve en el 100 % de los elementos interactivos, incluidos los que
  están dentro de tablas y de diálogos
- **FR-040**: Ninguna información del sistema se comunica únicamente por el color
- **FR-041**: Si el rediseño incorpora transiciones, respeta la preferencia de movimiento reducido
  del sistema operativo

#### Anchos y densidad

- **FR-042**: El sistema se ve correctamente **de 1280 px de ancho para arriba**: escritorio y
  notebook. Tablet y celular quedan **fuera de alcance**
- **FR-043**: Con el navegador al 200 % de zoom, ninguna pantalla obliga a desplazarse
  horizontalmente para leer un texto
- **FR-044**: Una tabla que no entra en el ancho disponible contiene su propio desplazamiento
  horizontal, sin arrastrar al resto de la pantalla, y un texto largo dentro de una celda no rompe el
  ancho de la tabla

### Alcance: pantallas comprendidas

Las 42 rutas hoy implementadas, agrupadas por módulo:

- **Autenticación (2)**: ingreso, inicio
- **Usuarios y roles (9)**: listado, alta, detalle, edición y asignación de roles de usuario;
  listado, alta y edición de personas; cambio de contraseña propia
- **Choferes (9)**: listado, alta, ficha, edición y panel de vencimientos de choferes; listado, alta
  y edición de transportistas; tipos de documentación
- **Flota (6)**: listado, alta, ficha, edición y panel de vencimientos de vehículos; tipos de
  vehículo
- **Viajes (9)**: listado, alta, ficha, edición, asignación y totales por período; listado, alta y
  ficha de clientes
- **Facturación (7)**: listado, alta, ficha, corrección, panel de vencimientos, totales facturados y
  configuración de la empresa emisora
- **Común a todas**: navegación, encabezado de pantalla, diálogo de confirmación, control de
  paginación y bloque de filtros

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Recorrer completos los quickstarts de los seis módulos no encuentra **ninguna**
  diferencia de comportamiento respecto de lo que cada uno describe: los mismos datos, los mismos
  pasos y los mismos resultados. Es la prueba de que el rediseño no se llevó nada puesto
- **SC-002**: Mostrando capturas de una pantalla de cada módulo sin sus textos, alguien externo las
  identifica como el mismo producto
- **SC-003**: Una persona que nunca vio el sistema encuentra, en menos de 15 segundos y desde la
  pantalla de inicio, dónde se cargan los viajes y dónde se emiten las facturas
- **SC-004**: Una persona que nunca vio el sistema identifica, sin hacer clic y en menos de 5
  segundos, en qué sección está parada
- **SC-005**: En el listado de facturas con al menos una anulada entre diez filas, esa fila se señala
  correctamente en menos de 3 segundos, y su texto sigue siendo legible
- **SC-006**: En un formulario rechazado con tres campos con error, los tres se ubican en menos de 10
  segundos sin leer el formulario completo
- **SC-007**: Abriendo la ficha de una factura, se responde en menos de 5 segundos qué número es, en
  qué estado está y qué se puede hacer con ella
- **SC-008**: El 100 % del texto de las 42 pantallas alcanza 4,5:1 de contraste (3:1 en texto grande)
  y el 100 % de los elementos no textuales que comunican información alcanza 3:1, medido con una
  herramienta de contraste sobre las pantallas reales
- **SC-009**: El alta de una factura se completa de punta a punta usando solamente el teclado, viendo
  en todo momento dónde está el foco
- **SC-010**: A 1280 px de ancho, y con el navegador al 200 % de zoom, ninguna de las 42 pantallas
  obliga a desplazarse horizontalmente para leer un texto
- **SC-011**: Mirando capturas de los controles de paginación y de las confirmaciones de los seis
  módulos sin sus textos, no es posible decir de qué módulo es cada una: existe una sola de cada
- **SC-012**: Ninguna captura del sistema convertida a escala de grises pierde información: todo
  estado, error y elemento atenuado se sigue distinguiendo
- **SC-013**: Ningún elemento que el sistema señala como distinto —atenuado, con error, con estado,
  vigente o histórico— se ve igual que uno que no lo está

## Assumptions

- **El rediseño alcanza a la aplicación entera**, los seis módulos y las 42 pantallas. Rediseñar de a
  un módulo dejaría el producto desparejo, que es el problema que esta feature viene a resolver
- **Se rediseña lo que se ve, no lo que hace.** Las pantallas siguen existiendo, con las mismas
  direcciones y las mismas operaciones; lo que cambia es cómo se ven, cómo se agrupan y cómo se llega
- **Los seis quickstarts y los tests existentes son la red de seguridad.** Se conservan los textos
  operativos precisamente para que sigan sirviendo de prueba de que el comportamiento no cambió
- **Escritorio y notebook, de 1280 px para arriba.** Tablet y celular quedan fuera por decisión
  tomada en la clarificación: nadie opera el sistema desde la calle
- **La identidad de la empresa queda afuera a propósito.** Existe, se conoce, y se resolvió no
  llevarla a la aplicación
- **Sin modo oscuro.** Nadie lo pidió y el Principio I manda no anticipar necesidades hipotéticas. Si
  aparece, es una spec futura
- **Sin cambios de idioma ni de formato.** El español rioplatense, el formato de moneda argentino y
  el de fecha quedan como están
- **La impresión queda fuera.** El único documento que se imprime es el PDF de la factura, con su
  propio diseño definido en el Módulo 6
- **El PDF de la vista previa no se estila**: se integra su marco a la pantalla
- **Se asume navegador actualizado**, sin soporte para versiones anteriores
- **Los datos de prueba de los quickstarts alcanzan** para ver en pantalla todos los estados que hay
  que distinguir; si falta alguno, se carga a mano durante la validación
- **La validación de contraste requiere una herramienta de medición**, la única excepción al
  Principio IV en esta spec: todo el resto se verifica mirando y operando la aplicación
