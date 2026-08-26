# Feature Specification: Adopción del sistema de diseño gt-ui (Módulo 8)

**Feature Branch**: `008-diseno-gt-ui`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: *"vamos a actualizar la interfaz ya hecha, lee el contenido de la carpeta
gt-ui para seguir el nuevo diseño"*

## Punto de partida

El Módulo 7 hizo el trabajo estructural. Antes de él no había diseño: una hoja de 136 líneas, 23
clases escritas en el marcado que ninguna hoja definía, una regla global que pintaba todos los
botones de azul, catorce entradas planas de menú y nueve componentes de confirmación distintos.
Después de él hay **un vocabulario de componentes** —un botón, un campo, una tabla, un diálogo, una
paginación, un indicador de estado—, un menú agrupado en cinco secciones y un encabezado común a las
42 pantallas. Las 285 pruebas de los seis módulos de negocio quedaron en verde.

Lo que el Módulo 7 no tuvo es **una identidad visual escrita por fuera de él**. Eligió sus colores,
su tipografía, sus radios y su densidad sobre la marcha, mientras resolvía la estructura. El
resultado es correcto y sobrio, y no se parece a nada en particular.

Desde entonces existe `.claude/skills/gt-ui/`: un sistema de diseño documentado, con seis principios,
una paleta con valores, una escala tipográfica, radios, sombras, espaciado, reglas de movimiento,
specs de componente y **cuatro imágenes de referencia** —dos listados, un formulario de alta y una
ficha de solo lectura— que muestran el aire, el ritmo y la jerarquía que el texto no alcanza a
describir. El segundo listado, `choferes- padron.png`, es el que fija cómo se entra a una fila y
dónde viven las acciones secundarias.
La constitución lo hizo obligatorio en su Principio VI: *"Toda interfaz DEBE seguir el sistema de
diseño documentado en `.claude/skills/gt-ui/SKILL.md` y sus referencias"*.

Hoy la aplicación **no lo sigue**. La distancia no es de matiz:

- **El fondo, el papel y la profundidad son otros.** El sistema corre sobre `#f4f6f9` con tarjetas de
  borde sólido y sombras de 1 px. gt-ui pide un lienzo `#EDEEF1` con **dos orbes radiales tenues**
  detrás de todo, tarjetas como islas —blanco al 94 %, hairline de negro con alfa, sombra difusa de
  34 px con spread negativo— y prohíbe explícitamente el gris sólido de borde y las sombras duras
- **La tipografía es otra.** Corre Geist Variable para todo. gt-ui pide **Plus Jakarta Sans** para el
  texto y **Geist Mono** para identificadores, patentes, comprobantes y números de remito. Hoy no hay
  ninguna familia monoespaciada en el sistema: un CUIT, una patente y un número de comprobante se
  escriben con la misma fuente que una razón social
- **La jerarquía de acciones es otra.** Los botones son rectángulos de radio 4 px con relleno del
  acento `#12507b`. gt-ui pide **pastillas de radio completo**, el primario en `ink` —casi negro— con
  el **ícono anidado en su propio círculo interno**, el secundario en gris claro sin borde, y un
  terciario de *volver* con la flecha en círculo, arriba a la izquierda
- **La estructura de página es otra.** Hoy hay una barra superior con la marca y las acciones de
  cuenta, y debajo un menú pegado al borde izquierdo. gt-ui no tiene barra superior: la navegación es
  una **isla flotante** de 266 px, con radio 26 px, separada 18 px de los bordes de la ventana, con
  la marca arriba y el bloque de usuario y las acciones de cuenta al pie
- **Las tablas son otras.** Hoy tienen alternancia de fondo, columnas de guiones donde el dato falta,
  y *Origen* y *Destino* en dos columnas cuando son una sola ruta. gt-ui prohíbe la alternancia —con
  divisores hairline alcanza—, prohíbe la columna de `—` y pide fusionar las columnas que nombran un
  solo concepto
- **La forma de entrar a una fila es otra.** Hoy cada fila termina en una columna *Acciones* con
  botones sueltos, y el identificador es un enlace subrayado. gt-ui pide que la fila **entera**
  navegue, que lo que se busca con la vista sea el enlace —el número del viaje como token, el
  apellido del chofer subrayado con el DNI en mono debajo— y que lo secundario viva en un menú `···`
  al final de la fila
- **Las fichas son de una columna.** gt-ui pide dos: principal flexible y un aside de 330 px donde va
  el dato de más valor —el importe— en 32 px ExtraBold

Nada de esto es un defecto del Módulo 7: es la diferencia entre haber ordenado la interfaz y haberla
**vestido con la identidad que el proyecto adoptó después**. Esta feature la viste.

## Encuadre: qué autoriza esta spec

Esta feature adopta `.claude/skills/gt-ui/` como la apariencia real del sistema, en las 42 pantallas.

Sobre el Principio III de la constitución —*Cero Alcance Fantasma*—: rige igual que siempre. Este
documento pide la adopción completa del sistema de diseño para que hacerla esté dentro de alcance;
lo que gt-ui no documenta, y esta spec no pide, no se construye.

**Se adopta, y es el alcance:**

1. Los **tokens** de `references/tokens.md`: paleta, luz ambiental, tipografía, radios, sombras,
   espaciado y movimiento
2. La **estructura de página**: lienzo con orbes, sidebar como isla flotante, columna principal
   transparente, tarjetas como islas
3. La **jerarquía de acciones** de cuatro niveles: primario, secundario, terciario y destructivo
4. Las **specs de componente** de `references/componentes.md`: botones, campos, formularios en
   secciones numeradas, badges de estado, token de identificador, tablas, barra de filtros, tarjetas,
   callout de estado bloqueado, estado vacío, ficha de dos columnas y sidebar
5. Las **reglas de contenido** de `references/contenido.md` que se pueden cumplir **sin reescribir
   los textos que las specs anteriores fijaron**: ausencia de emoji, identificadores en mono, importes
   y fechas con su formato, y que ningún mensaje de error se vea antes de interactuar
6. El **aire, el ritmo y la jerarquía** que muestran las cuatro imágenes de referencia

**Se conserva, y es deliberado:**

1. **Qué hace cada pantalla**: los datos que muestra, las operaciones que ofrece y los pasos de cada
   flujo. Esta feature cambia cómo se ve, no qué pasa
2. **Los textos operativos** que las specs de los Módulos 1 a 6 fijaron palabra por palabra: mensajes
   de error, de confirmación, de estado vacío, etiquetas de campo y verbos de botón. Son el contrato
   contra el que corren los seis quickstarts y las 285 pruebas. Los **nuevos** textos que esta feature
   introduzca —rótulos de columna fusionada, encabezados de sección numerada— sí los escribe ella
3. **El nombre del sistema**, que es *Sistema Integral de Gestión*. La imagen de referencia dice
   *Sistema Integral de Transporte*; las imágenes son la fuente de la jerarquía y del aire, nunca de
   los valores ni de los textos
4. **Las 42 direcciones**, porque el servidor las nombra al armar el menú
5. **Quién puede hacer qué**: los permisos, y que el servidor sigue siendo la única fuente de verdad
   de qué opciones existen para cada usuario
6. **Los controles nativos**: `input`, `select` y `textarea` se estilan, no se sustituyen. Es la
   convención [007] y la razón por la que las 285 pruebas siguen sirviendo
7. **El documento PDF de la factura**, que tiene su propio diseño definido en el Módulo 6
8. **El piso de accesibilidad**: ninguna pantalla queda menos operable con teclado ni menos legible
   con lector de pantalla de lo que está hoy

## Clarifications

### Session 2026-08-25

- Q: gt-ui fija tres neutros claros —`faint` para ayudas y metadata, `dim` para el vacío y `#8D93A1`
  para los encabezados de columna— que **no llegan al contraste mínimo** que el Módulo 7 midió y dejó
  escrito: 2,63:1, 1,75:1 y 2,90:1 contra los 4,5:1 que pide el texto. ¿Qué manda? → **Se recalibran
  los cuatro valores que fallan y el resto de gt-ui queda intacto.** Se oscurecen hasta llegar a
  4,5:1 y el cambio **vuelve a la skill**, no queda como excepción escondida en el código. El aire se
  conserva: son cuatro tonos apenas más oscuros sobre una paleta que ya cumple en todo lo demás
  (FR-007, FR-008). **La jerarquía no**, y eso se descubrió midiendo: recalibrados, los cuatro tonos
  claros caen dentro de la misma banda de luminancia y son perceptualmente el mismo gris. Por eso
  `faint` queda como el único tono atenuado de texto y `dim` se degrada a token no textual — la
  distinción entre secundario y metadata pasa al tamaño y al peso (FR-007a, FR-007b).
- Q: gt-ui pide fusionar las columnas que nombran un solo concepto y que el identificador clickeable
  sea la forma de abrir una ficha. ¿Hasta dónde llega la reestructuración de las 21 tablas? → **Hasta
  el final: se revisan las 21 y se fusiona todo par que nombre un solo concepto. La columna
  *Acciones* desaparece** (FR-040 a FR-047).
- Q: Si *Acciones* desaparece, ¿cómo se entra a una fila y dónde quedan editar, ver vencimientos y
  dar de baja? → **Lo que se busca con la vista es lo clickeable.** En el padrón de choferes es el
  apellido y nombre, subrayado para que se lea como enlace, con el DNI en mono debajo —lo que además
  fusiona dos columnas en una—. **La fila entera navega también.** Lo secundario va en un menú `···`
  de 28 px al final de la fila, **siempre en el DOM y siempre enfocable**: un menú que sólo aparece
  al pasar el mouse queda inalcanzable con teclado y en pantalla táctil. Lo que sí aparece al pasar
  el mouse es el chevron `›`, que confirma que la fila navega —en los listados de índice, según
  acota la respuesta siguiente— (FR-040 a FR-045).
- Q: ¿La fila que navega con su enlace único rige en las 21 tablas, o sólo donde hay una ficha a la
  que ir? → **Sólo donde hay ficha.** El **enlace de fila, la fila navegable y el chevron** rigen en los
  listados de índice; las tablas sin destino propio —el selector de viajes con casillas, los dos
  paneles de totales, el detalle dentro de una ficha, el historial— quedan afuera: inventarles un
  destino sería alcance fantasma. El **menú `···` y la desaparición de `Acciones`** se rigen por otro
  criterio, independiente del primero: alcanzan a **toda** tabla que hoy tenga acciones de fila,
  tenga ficha o no —por eso los cinco archivos de FR-069 incluyen listados sin ficha—. Lo demás
  —tokens, divisores hairline, importes alineados, celdas que dicen qué falta— alcanza a las 21
  (FR-040 a FR-046, SC-006, SC-007).
- Q: De las cinco fichas de solo lectura sólo dos tienen importe. ¿Qué va en el aside de 330 px de las
  otras tres? → **El dato de más valor de esa ficha**, que es el importe cuando lo hay y, cuando no,
  lo que la ficha ya muestra y más se consulta: el semáforo de documentación del chofer, el estado con
  su semáforo en el vehículo, los roles en el detalle de usuario. FR-053 se generaliza y el importe
  queda como su caso particular. El aside **nunca se dibuja vacío** y **no se agrega ningún dato que
  la ficha no reciba hoy** (FR-050, FR-053).
- Q: ¿A partir de qué tamaño un formulario se agrupa en secciones numeradas, o van todos? → **Se numera
  cuando hay dos o más secciones.** Un formulario que es un solo grupo de campos —registrar cobro,
  tipo de vehículo, panel de roles, ingreso, persona, asignación, cambio de contraseña— no lleva chip
  ni título de sección: va directo dentro de su isla, con la misma barra de acciones al pie. Un chip
  numerado sobre un único grupo no agrupa nada y obliga a inventar un título que nadie pidió. El plan
  DEBE listar los 16 formularios diciendo cuáles quedan de una sección y cuáles se agrupan, con el
  título de cada una (FR-029).
- Q: ¿La barra de acciones fija al pie del viewport rige también en los formularios dentro de un
  diálogo y en las pantallas sin sesión? → **Sólo en los formularios de página completa.** En un
  diálogo —*Registrar cobro*— y en las pantallas sin sesión —ingreso, cambio de contraseña forzado—
  la barra va al pie de **su propio contenedor**, con la misma composición: leyenda de obligatorios a
  la izquierda, acciones a la derecha, el primario último. Lo que FR-030 persigue es que la acción
  principal siga alcanzable en un formulario que se desplaza; un diálogo y una tarjeta de ingreso no
  se desplazan, así que anclarla a la ventana sólo la separaría del formulario al que pertenece
  (FR-030, SC-003).
- Q: ¿Qué comportamiento de teclado tiene el menú `···` de una fila una vez abierto? → **Abre con
  `Enter` o `Espacio`, el foco pasa al primer ítem, las flechas recorren los ítems, `Escape` lo cierra
  y devuelve el foco al `···`, y el foco que sale del menú lo cierra.** No retiene el foco ni bloquea la
  pantalla de atrás: sigue sin ser un diálogo. Un ítem que dispara una confirmación le entrega el foco
  al diálogo que ya existe, con el comportamiento que fijó el Módulo 2 (FR-044, SC-008, SC-013).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - La aplicación se ve como el sistema de diseño que el proyecto adoptó (Priority: P1)

Quien conduce el producto abre cualquier pantalla del sistema —el listado de viajes, el alta de un
chofer, la ficha de una factura— y lo que ve es lo que muestran las imágenes de referencia de gt-ui:
el mismo lienzo con su luz ambiental, la misma isla de navegación flotando a la izquierda, las mismas
tarjetas de bordes suaves, la misma tipografía, la misma pastilla oscura para la acción principal. No
hay una pantalla que se haya quedado con la apariencia anterior.

**Why this priority**: es la feature. Sin esto no hay nada más. Y es lo primero que se puede mirar y
juzgar sin abrir ningún archivo, que es lo que exige el Principio IV de la constitución.

**Independent Test**: se recorre el sistema pantalla por pantalla con las imágenes de referencia al
lado y se verifica que fondo, tipografía, radios, sombras, densidad y jerarquía coinciden.

**Acceptance Scenarios**:

1. **Given** la aplicación con sesión abierta, **When** se abre cualquiera de las 42 pantallas,
   **Then** el fondo es el lienzo de gt-ui con sus dos orbes radiales detrás del contenido, y la
   navegación es una isla flotante separada de los bordes de la ventana
2. **Given** cualquier pantalla, **When** se compara su tipografía con la escala de gt-ui, **Then**
   el texto usa Plus Jakarta Sans y los identificadores —CUIT, DNI, patente, comprobante, número de
   remito— usan Geist Mono
3. **Given** cualquier tarjeta del sistema, **When** se la observa, **Then** no tiene borde gris
   sólido de 1 px ni sombra dura: tiene hairline de negro con alfa y sombra difusa
4. **Given** una pantalla cualquiera, **When** se cuentan sus botones **primarios** —sin contar la
   acción destructiva, que es un nivel aparte con su propio relleno—, **Then** hay a lo sumo uno, y
   es la acción principal de esa pantalla

---

### User Story 2 - La acción principal se distingue de la escapatoria y de la navegación (Priority: P1)

Un operador de Tráfico entra a dar de alta un chofer. La pantalla tiene arriba a la izquierda un
*volver* discreto, y al pie una barra con *Cancelar* en gris claro y *Guardar chofer* como pastilla
oscura con el ícono en su círculo. No hay ambigüedad sobre cuál es la acción que completa la tarea, y
tampoco sobre cómo salir sin completarla.

**Why this priority**: es el Principio 1 de gt-ui y una de las dos condiciones que la constitución
exige declarar en el plan. Y es la diferencia operativa concreta con la feature anterior: el Módulo 7
separó *Guardar* de *Cancelar*, pero los dejó a los dos como rectángulos del mismo tamaño y peso.

**Independent Test**: se abre cada uno de los formularios y se verifica que hay exactamente una
pastilla rellena, que es la que guarda, y que existe una salida visible sin desplazar la pantalla.

**Acceptance Scenarios**:

1. **Given** un formulario de alta o edición de página completa, **When** se lo abre, **Then** la
   barra de acciones está fija al pie del viewport y no al final del scroll, con la leyenda de campos
   obligatorios a la izquierda y las acciones a la derecha, el primario último; en un diálogo o en una
   pantalla sin sesión, esa misma barra va al pie de su propio contenedor
2. **Given** un formulario largo, **When** se desplaza hasta la mitad, **Then** la acción principal
   sigue visible y alcanzable sin bajar hasta el final
3. **Given** una pantalla de solo lectura sin ninguna acción de escritura disponible, **When** se la
   abre, **Then** no hay ningún botón relleno inventado para llenar el lugar
4. **Given** una acción destructiva —anular, dar de baja—, **When** se la observa, **Then** es la
   única cosa roja de la pantalla

---

### User Story 3 - Los listados se leen de un vistazo (Priority: P2)

Un usuario de Gerencia abre el listado de viajes para revisar el período. Arriba, el buscador ocupa
todo el ancho y debajo van los filtros como desplegables compactos que muestran su valor actual, con
el que está filtrando marcado. Una franja dice cuántos resultados hay, cuánto suman y por qué
criterio están ordenados. La tabla no tiene rayas alternadas: filas separadas por una línea muy
tenue, encabezados en mayúsculas chiquitas, los importes a la derecha en negrita y en una sola línea,
los estados como pastillas de color con su palabra, y donde no hay dato dice qué falta en lugar de un
guión.

**Why this priority**: 20 de las 42 pantallas tienen tabla, y son las que más tiempo se miran. Va
después de las dos primeras porque depende de los tokens y del vocabulario de botones.

**Independent Test**: se abre cada listado, se aplica un filtro, se pagina y se verifica la barra de
filtros, la franja de resumen, el pie de paginación y el tratamiento de las celdas sin dato.

**Acceptance Scenarios**:

1. **Given** un listado, **When** se lo abre, **Then** el buscador está primero y a todo el ancho, y
   los filtros debajo muestran su valor actual sin necesidad de desplegarlos
2. **Given** un listado con un filtro aplicado, **When** se lo mira, **Then** el control que está
   filtrando se distingue de los que no, y sigue habiendo un texto que declara qué se está mostrando
3. **Given** una fila con un dato ausente, **When** se la mira, **Then** la celda dice qué falta
   —*Sin asignar*, en el tono atenuado— y no muestra un guión
4. **Given** una columna de importes, **When** se la recorre en vertical, **Then** los números están
   a la derecha, en negrita, en una sola línea, y los separadores de miles y la coma decimal quedan
   alineados entre filas
5. **Given** una fila cualquiera, **When** se busca cómo abrir su ficha, **Then** lo clickeable es el
   dato que se estaba buscando con la vista —el número del viaje como token, el apellido del chofer
   subrayado— y la fila entera lleva al mismo lugar
6. **Given** una fila, **When** se pasa el mouse por encima, **Then** aparece el chevron que confirma
   que navega, y el menú `···` del final ya estaba visible antes de pasar
7. **Given** un listado, **When** se lo recorre sólo con el tabulador, **Then** se llega al menú `···`
   de cada fila y se lo puede abrir sin tocar el mouse
8. **Given** el menú `···` de una fila abierto con teclado, **When** se recorren sus ítems con las
   flechas y se aprieta `Escape`, **Then** el menú se cierra y el foco vuelve al `···` que lo abrió

---

### User Story 4 - Una ficha pone adelante el dato que importa y explica lo que no se puede hacer (Priority: P2)

Un usuario abre la ficha de un viaje rendido. El título lleva el número como token y el estado como
pastilla en la misma línea, y debajo el contexto: cliente, ruta, fecha. Cruzando la pantalla hay un
aviso que dice que el viaje está cerrado para edición **y explica cómo revertirlo**. A la izquierda,
el recorrido y el historial como línea de tiempo con el paso actual marcado. A la derecha, en grande,
el importe.

**Why this priority**: es la tercera de las tres formas de pantalla que el sistema tiene, y la que
gt-ui documenta con más detalle. Va en P2 junto con los listados porque las dos dependen de lo mismo.

**Independent Test**: se abre una ficha de cada módulo, incluida una de un registro inmutable, y se
verifica el encabezado, las dos columnas, el dato destacado en el aside y el callout con su salida.

**Acceptance Scenarios**:

1. **Given** la ficha de un registro con estado, **When** se la abre, **Then** el estado está en el
   encabezado junto a la identidad, y no como una fila más de una lista de datos
2. **Given** la ficha de un registro que no admite escritura, **When** se la abre, **Then** el aviso
   dice qué pasa y ofrece la alternativa, o explica por qué no hay ninguna
3. **Given** una ficha con un importe, **When** se la abre, **Then** el importe es el elemento de
   mayor tamaño del cuerpo de la pantalla
4. **Given** una ficha sin ningún importe —un chofer, un vehículo, un usuario—, **When** se la abre,
   **Then** el aside no está vacío: lleva el dato que más se consulta de esa ficha, destacado, y es un
   dato que la pantalla ya mostraba
5. **Given** una ficha con historial de estados, **When** se lo mira, **Then** se lee como una línea
   de tiempo cronológica con el paso actual marcado, y no como una tabla de cuatro columnas

---

### User Story 5 - Nada de lo que ya funcionaba dejó de funcionar (Priority: P1)

Quien valida el sistema recorre los seis quickstarts de los módulos anteriores, con las tres cuentas
—`admin`, un usuario de Tráfico y uno de Gerencia—, y todo hace lo mismo que hacía: los mismos
mensajes, las mismas confirmaciones, los mismos rechazos, las mismas direcciones, los mismos
permisos. Las 285 pruebas siguen en verde.

**Why this priority**: es P1 y no una nota al pie. Un rediseño que rompe una operación no es un
rediseño: es una regresión con mejor tipografía. La convención [007] dice que congelar los textos
convierte a la suite existente en la prueba de que el comportamiento no cambió.

**Independent Test**: se corre la suite completa y se recorre un quickstart de punta a punta.

**Acceptance Scenarios**:

1. **Given** la suite de pruebas del frontend, **When** se la corre después del cambio, **Then** los
   285 casos de los 43 archivos pasan sin haber modificado ninguna aserción sobre textos, roles ni
   etiquetas accesibles
2. **Given** cualquier pantalla, **When** se la opera sólo con teclado, **Then** el foco se ve
   siempre y llega a todo lo que se podía alcanzar antes
3. **Given** una operación que muestra su resultado sin cambiar de pantalla, **When** se la ejecuta,
   **Then** el resultado se sigue anunciando, como exige la convención [003]

---

### Edge Cases

- **Un estado que gt-ui no nombra.** gt-ui define cuatro estados de viaje. El sistema tiene cinco
  juegos de estados y unos veinte valores: documentación en regla, próxima a vencer y vencida;
  vehículo disponible, en viaje y fuera de servicio; usuario activo, inactivo y bloqueado. Cada valor
  tiene que caer en una pastilla con color **y palabra**, y un valor desconocido tiene que dibujarse
  igual, en el tono neutro, sin romper la pantalla
- **Una pantalla sin navegación.** El ingreso y el cambio de contraseña forzado no muestran el
  sidebar. Tienen que verse parte del mismo sistema igual, sobre el mismo lienzo
- **Un menú vacío.** Un usuario cuyos roles todavía no habilitan ninguna opción no ve navegación: la
  isla no puede quedar como un rectángulo vacío flotando
- **Un menú largo.** gt-ui fija el umbral: pasadas las quince entradas, el ítem activo deja de ser
  relleno pleno y pasa a fondo suave con texto de acento
- **Una fila sobre la que no se puede hacer nada.** Un usuario sin permiso de escritura ve las filas
  y entra a las fichas, pero su menú `···` no tiene nada adentro: entonces no se dibuja
- **Un clic sobre el menú de una fila que navega.** Abrir el `···`, o tocar cualquier control de la
  fila, no puede disparar la navegación de la fila
- **Una tabla que no entra a lo ancho.** Se desplaza la tabla dentro de su isla, nunca la pantalla
- **Alguien pidió menos movimiento.** Las entradas al viewport y las transiciones se apagan si el
  sistema operativo lo pide
- **Una fila atenuada.** Un viaje anulado o una factura anulada siguen llevando la palabra que lo
  explica además del tono, y el tono tiene que seguir siendo legible: atenuado no es borroso

## Requirements *(mandatory)*

### Fundamentos visuales

- **FR-001**: El sistema DEBE definir sus colores, tipografía, radios, sombras, espaciado y curvas de
  movimiento a partir de los valores de `references/tokens.md`, en un solo lugar, de modo que ninguna
  pantalla declare un color, un tamaño ni una separación por su cuenta
- **FR-002**: El fondo de la aplicación DEBE ser el lienzo de gt-ui, con los **dos orbes radiales**
  detrás de todo el contenido, sin capturar el puntero y sin quedar dentro de un contenedor con
  desplazamiento
- **FR-003**: El texto DEBE usar **Plus Jakarta Sans**, y los identificadores —DNI, CUIT, patente,
  número de comprobante, número de remito, número de viaje— DEBEN usar **Geist Mono**. Ninguna de las
  familias que gt-ui prohíbe puede quedar como fuente efectiva de ningún texto
- **FR-004**: Ningún borde del sistema puede ser un gris sólido: los bordes DEBEN ser negro con alfa,
  con el valor fuerte reservado para lo que comunica —borde de campo, contorno de control— y el suave
  para lo que sólo separa
- **FR-005**: Ninguna sombra del sistema puede ser dura: todas DEBEN ser difusas, con spread negativo
  y alfa baja, según los cuatro valores de `tokens.md`
- **FR-006**: Las animaciones DEBEN usar únicamente la curva de gt-ui y animar únicamente posición y
  opacidad, y DEBEN apagarse cuando el sistema operativo pide menos movimiento
- **FR-007**: El sistema DEBE seguir cumpliendo el contraste mínimo que el Módulo 7 fijó —4,5:1 para
  texto, 3:1 para lo no textual que comunica—. Los **cuatro valores de gt-ui que no lo alcanzan** se
  recalibran hasta cumplirlo, **midiendo cada uno sobre el fondo donde ese token efectivamente
  aparece** y no contra un fondo único: `faint` (2,63:1), `dim` (1,75:1), el gris de encabezado de
  columna (2,90:1) y el texto del badge *Anulado* sobre su fondo (4,32:1). El resto de la paleta se
  toma tal cual está: ya cumple
- **FR-007a**: La jerarquía de tonos **no sobrevive entera a ese piso**, y manda el contraste. Por
  debajo de `ink-soft` y por encima de 4,5:1 no entran cuatro niveles distinguibles, así que:
  **`faint` queda como el único tono atenuado de texto** —la celda que dice qué falta, la fila
  atenuada, la metadata, las ayudas y los placeholders— y **`dim` deja de ser color de texto**, y pasa
  a token no textual: el separador entre metadata, el relleno del ícono del estado vacío, el `#` del
  token de identificador. Su valor recalibrado se registra igual, bajo el nombre `dim-texto`, para
  quien lo necesite como texto. **La distinción entre texto secundario y metadata pasa a vivir en el
  tamaño y el peso**, que es donde la escala tipográfica de gt-ui ya la define y donde sobrevive a
  cualquier piso de contraste
- **FR-007b**: Ningún texto puede ir directo **sobre el lienzo ni sobre `surface-mute`** salvo en
  `ink` o `ink-soft`. Todo el resto del contenido vive dentro de una isla (FR-015). Es lo que cubre a
  `muted`, que cumple sobre blanco y sobre `surface-soft` pero **no** sobre esos dos fondos, y que
  esta feature deliberadamente **no** recalibra: la regla no cambia ningún valor y se comprueba
  operando
- **FR-008**: Los cuatro valores recalibrados DEBEN **volver a `references/tokens.md`**, con su
  medición anotada al lado. La skill sigue siendo la fuente única: una excepción que viva sólo en el
  código deja al sistema de diseño diciendo una cosa y a la aplicación haciendo otra

### Estructura de página

- **FR-009**: La navegación DEBE presentarse como una **isla flotante** de 266 px separada de los
  bordes de la ventana, con la marca arriba, las secciones en el medio y el bloque de usuario con las
  acciones de cuenta al pie
- **FR-010**: La barra superior actual DEBE desaparecer: la identificación del sistema y las acciones
  de cuenta —*Cambiar contraseña*, *Cerrar sesión*— pasan al pie de la isla de navegación, y siguen
  estando disponibles desde cualquier pantalla con sesión abierta
- **FR-011**: La columna principal DEBE ser transparente, con el espaciado que fija `tokens.md`, de
  modo que el lienzo y su luz se vean a través de ella
- **FR-012**: Los agrupamientos del menú DEBEN llevar su rótulo en el estilo de eyebrow, y la opción
  abierta DEBE distinguirse por **fondo y peso** además de por color. Si el menú supera las quince
  entradas, el ítem activo usa fondo suave en lugar de relleno pleno
- **FR-013**: El frontend DEBE seguir dibujando exactamente las opciones que llegan del servidor, y
  una opción con un código desconocido DEBE seguir apareciendo, en la última sección
- **FR-014**: Las pantallas sin sesión —ingreso y cambio de contraseña forzado— DEBEN usar el mismo
  lienzo, la misma tipografía y el mismo vocabulario de componentes, sin navegación
- **FR-015**: Toda superficie de contenido DEBE presentarse como isla: blanco al 94 %, hairline y
  sombra difusa, con el radio de tarjeta de `tokens.md`

### Jerarquía de acciones

- **FR-016**: El sistema DEBE ofrecer **cuatro** niveles de acción —primario, secundario, terciario y
  destructivo— y cada uno DEBE verse como lo describe `references/componentes.md`
- **FR-017**: Cada pantalla DEBE tener **a lo sumo un** botón primario, y DEBE ser su acción
  principal. Una pantalla de solo lectura sin acción accionable no lleva ninguno. La acción
  **destructiva es un nivel propio y no cuenta como segundo primario**: se ve rellena porque es
  grave, no porque compita, y las cuatro fichas que la tienen —chofer, vehículo, viaje y factura—
  llevan primario y destructivo a la vez. Lo que la regla prohíbe es un segundo **primario**, no un
  segundo relleno
- **FR-018**: El botón primario DEBE llevar su ícono **anidado en un círculo interno** pegado al borde
  derecho, nunca suelto al lado del texto
- **FR-019**: El encabezado de una pantalla PUEDE llevar acciones secundarias junto al primario
  —*Ver vencimientos* al lado de *Nuevo chofer*—, y DEBEN verse como secundarias: pastilla gris
  claro, nunca un segundo relleno pleno compitiendo
- **FR-020**: El rojo DEBE quedar reservado a lo destructivo: ningún otro elemento del sistema puede
  usarlo como relleno de acción
- **FR-021**: Declarar la variante DEBE seguir siendo obligatorio para dibujar un botón: la garantía
  de la convención [007] no puede debilitarse al cambiar la apariencia
- **FR-022**: El *volver* DEBE presentarse como pastilla blanca con la flecha en círculo, arriba a la
  izquierda, fuera de la línea de decisión de la pantalla
- **FR-023**: Un elemento que navega DEBE seguir siendo un enlace y un elemento que ejecuta DEBE
  seguir siendo un botón, se vean como se vean

### Campos y formularios

- **FR-024**: Todo campo DEBE tener declarados y visibles sus cuatro estados: reposo, foco, error y
  deshabilitado
- **FR-025**: El anillo de foco DEBE ser visible por sí solo y no depender de un cambio de color de
  borde, en los controles propios y en los nativos
- **FR-026**: El ancho de un campo DEBE acompañar al dato que recibe: un DNI, un CUIT, una patente o
  una fecha no ocupan lo mismo que una razón social o un domicilio
- **FR-027**: Los campos obligatorios DEBEN estar marcados de forma visible **y** en su nombre
  accesible, y la pantalla DEBE llevar la leyenda que explica la marca
- **FR-028**: Ningún mensaje de error puede verse antes de que la persona haya interactuado con el
  formulario. Los mensajes que hoy aparecen al enviar o al volver del servidor **siguen apareciendo
  igual**: esta feature no cambia cuándo se valida, sólo prohíbe el error visible en el primer dibujo
- **FR-029**: Un formulario con **dos o más** grupos de campos DEBE agruparse en **secciones
  numeradas**, cada una con su chip, su título y, cuando ayuda, una línea que explique qué se pide. Un
  formulario que es un **solo** grupo NO lleva chip ni título de sección: sus campos van directo dentro
  de la isla, con la misma barra de acciones al pie. El plan DEBE listar los 16 formularios indicando
  cuáles quedan de una sección y cuáles se agrupan, con el título de cada sección
- **FR-030**: La barra de acciones de un formulario de **página completa** DEBE quedar **fija al pie
  del viewport**, con la leyenda de obligatorios a la izquierda y las acciones a la derecha, el
  primario último. En un formulario que vive **dentro de un diálogo** y en las **pantallas sin
  sesión**, la barra va al pie de su propio contenedor con esa misma composición: no se ancla a la
  ventana
- **FR-031**: Los controles nativos DEBEN estilarse y **no** sustituirse por listas dibujadas

### Listados y tablas

- **FR-032**: Un listado DEBE presentarse como una sola pieza: buscador, filtros, franja de resumen,
  tabla y pie de paginación dentro del mismo marco
- **FR-033**: El buscador DEBE ir **primero y a todo el ancho**, y los filtros debajo como
  desplegables compactos que muestran su valor actual sin desplegarse
- **FR-034**: El filtro que está filtrando DEBE distinguirse de los que no, y el listado DEBE seguir
  declarando por escrito qué se está mostrando: nunca oculta filas en silencio
- **FR-035**: Debajo de los filtros DEBE ir una franja de resumen con la cantidad de resultados, el
  total o las señales que la pantalla tenga —*1 con documentación vencida*— y el criterio de orden
- **FR-036**: Las filas DEBEN separarse **sólo** con divisores hairline: la alternancia de fondo que
  el Módulo 7 introdujo se retira
- **FR-037**: Los encabezados de columna DEBEN ir en el estilo de eyebrow sobre fondo suave, y DEBEN
  seguir siendo encabezados de tabla de verdad, con su ámbito declarado
- **FR-038**: Ninguna tabla puede mostrar una columna de guiones: donde falta el dato, la celda DEBE
  decir qué falta, en el tono atenuado
- **FR-039**: Los importes DEBEN ir a la derecha, en negrita, en una sola línea, con el símbolo y el
  número juntos y las cifras alineadas en vertical entre filas

#### Cómo se entra a una fila

Son **21 tablas en 20 pantallas**: la ficha de factura tiene dos —el detalle de viajes incluidos y el
historial—. Esta subsección tiene **dos alcances distintos**, y el plan DEBE clasificar las 21 según
los dos, para que la revisión pueda contarlas:

- **FR-040 a FR-043** —enlace de fila, identificador debajo, fila navegable y chevron— rigen en los
  **listados de índice**: las tablas cuyas filas tienen una ficha a la que ir. Una tabla sin destino
  propio —el selector de viajes con casillas, los paneles de totales, el detalle dentro de una ficha,
  el historial— NO lleva enlace de fila ni fila navegable: no se le inventa un destino
- **FR-044 a FR-046** —menú `···` y desaparición de la columna `Acciones`— rigen en **toda** tabla que
  hoy ofrezca acciones sobre sus filas, tenga ficha o no

Todo el resto de esta sección —tokens, divisores hairline, encabezados en eyebrow, importes
alineados, celdas que dicen qué falta, desplazamiento dentro de la isla— alcanza a las 21 tablas sin
excepción.

- **FR-040**: El enlace de una fila DEBE ser **lo que se busca con la vista**, no una columna de
  acceso aparte: en el padrón de choferes es el apellido y nombre, en el listado de viajes es el
  número. Va subrayado cuando es texto, para que se lea como enlace, y como **token** —recuadro con
  el numeral atenuado y el número en mono— cuando lo que se busca es un identificador
- **FR-041**: Debajo del enlace DEBE ir, en mono y en menor jerarquía, el identificador que acompaña
  al dato —el DNI bajo el apellido, la patente bajo el nombre del chofer—. Eso es lo que fusiona dos
  columnas en una
- **FR-042**: Donde hay enlace de fila, la **fila entera** DEBE navegar al mismo destino que él
- **FR-043**: Al pasar el mouse por una fila **que navega** DEBE aparecer un chevron `›` que lo
  confirma. Es lo único que puede aparecer al pasar el mouse, y una fila que no navega no lo lleva
- **FR-044**: Las acciones secundarias de una fila —editar, ver vencimientos, dar de baja— DEBEN
  vivir en un **menú `···` de 28 px** al final de la fila. El disparador del menú DEBE estar
  **siempre en el DOM y siempre enfocable**: uno que aparezca sólo al pasar el mouse queda
  inalcanzable con teclado y en pantalla táctil. Abierto, el menú DEBE comportarse así: se abre con
  `Enter` o `Espacio`, el foco pasa al primer ítem, las flechas recorren los ítems, `Escape` lo cierra
  y **devuelve el foco al `···`**, y el foco que sale del menú lo cierra. NO retiene el foco ni bloquea
  la pantalla de atrás: no es un diálogo. Un ítem que dispara una confirmación le entrega el foco al
  diálogo que ya existe, con el comportamiento de FR-062
- **FR-045**: Cuando el usuario no tiene ninguna acción secundaria disponible sobre una fila, el
  menú `···` de esa fila NO DEBE dibujarse. Ocultarlo sigue siendo una cortesía: la restricción es
  el rechazo del servidor
- **FR-046**: La columna **`Acciones` desaparece** de todas las tablas que la tienen, tengan ficha o
  no: sus acciones pasan al menú `···`
- **FR-047**: Se DEBEN revisar las **21 tablas** y fusionar todo par de columnas que nombre un solo
  concepto. Como mínimo, y el plan DEBE enumerarlas una por una para que la revisión pueda contarlas:
  1. Listado de viajes: `Origen`+`Destino` → **Ruta**; `Chofer`+`Vehículo` → **Asignación**
  2. Selector de viajes de una factura y detalle de la ficha de factura: `Origen`+`Destino` → **Ruta**
  3. Padrón de choferes: `Apellido y nombre`+`DNI` → **Chofer**
  4. Padrón de personas: `Nombre`+`Apellido`+`DNI` → **Persona**
  5. Listado de flota: `Marca`+`Modelo` → **Vehículo**
  6. Transportistas y clientes: nombre o razón social + `CUIT` → una sola columna; `Teléfono`+`Email`
     → **Contacto**
  7. Historial de estados de viaje y de factura: `Estado anterior`+`Estado nuevo` → la transición,
     que además deja de ser tabla y pasa a línea de tiempo (FR-054). **Cuenta como fusión igual**: el
     par de columnas desaparece, aunque lo que lo reemplaza no sea una columna sino una entrada de
     línea de tiempo
  8. Paneles de vencimientos de choferes y de flota: `Documento`+`Vencimiento` → el documento con su
     fecha debajo
- **FR-048**: Las filas DEBEN mantener altura pareja; un dato secundario que la haría crecer va como
  metadata en menor jerarquía
- **FR-049**: Cuando una tabla no entra a lo ancho, DEBE desplazarse ella dentro de su isla, sin
  arrastrar a la pantalla

### Fichas

- **FR-050**: Una ficha de solo lectura DEBE armarse en dos columnas: principal flexible y aside fijo
  de 330 px. El aside NUNCA queda vacío: siempre aloja el dato que FR-053 define
- **FR-051**: El encabezado de una ficha DEBE llevar, en la misma línea, el título, el token del
  identificador y la pastilla de estado; y debajo, el contexto del registro
- **FR-052**: El estado de un registro DEBE vivir en el encabezado, nunca como una fila más de una
  lista de datos
- **FR-053**: El **dato de más valor de la ficha** DEBE ir en el aside, en la jerarquía más alta del
  cuerpo de la pantalla. Cuando hay importe, es el importe, en el tamaño de cifra destacada. Cuando
  no lo hay, es el dato que la ficha **ya muestra** y que más se consulta —el semáforo de
  documentación del chofer, el estado con su semáforo en el vehículo, los roles en el detalle de
  usuario—. No se trae ningún dato que la ficha no reciba hoy: el backend no se toca
- **FR-054**: Un historial de estados DEBE leerse como **línea de tiempo cronológica** con el paso
  actual marcado, y no como una tabla de columnas

### Estados, avisos y vacíos

- **FR-055**: Ninguna información puede comunicarse sólo por color: todo estado DEBE llevar su
  palabra. La **pastilla** se reserva para el estado del que trata la pantalla —la documentación en
  el padrón de choferes—; un estado de alta y baja que sólo acompaña va como punto y palabra, sin
  pastilla, para no competir con él
- **FR-056**: Un valor de estado que el sistema de diseño no nombre DEBE dibujarse igual, en el tono
  neutro, sin romper la pantalla
- **FR-057**: El dato accesorio de un estado —número de comprobante, fecha de vencimiento, motivo—
  DEBE ir **debajo**, en menor jerarquía, sin repetir la palabra del estado
- **FR-058**: Un mensaje que impide una acción DEBE decir qué pasa **y ofrecer la salida**; si no hay
  salida, DEBE decir por qué
- **FR-059**: Un estado vacío DEBE explicar la causa en un solo lugar, y DEBE distinguir *"todavía no
  hay ninguno"* de *"tu filtro no encontró nada"*, conservando los textos que cada módulo ya escribió
- **FR-060**: Un elemento atenuado DEBE seguir llevando la palabra que lo explica, y su tono DEBE
  seguir siendo legible
- **FR-061**: Todo resultado que aparece sin que la pantalla cambie DEBE seguir anunciándose, como
  exige la convención [003]
- **FR-062**: Los diálogos DEBEN seguir reteniendo el foco, cerrarse con `Escape` y mover el foco al
  diálogo —no a su primer control—, como fijó el Módulo 2 y ratificó la convención [007]

### Contenido

- **FR-063**: No puede haber emoji en ninguna parte de la interfaz
- **FR-064**: Las fechas y los importes DEBEN conservar su formato argentino, y los identificadores
  DEBEN escribirse en mono
- **FR-065**: Los textos **nuevos** que introduzca esta feature —los rótulos de las columnas
  fusionadas de FR-047, los encabezados de sección numerada, los ítems del menú `···`— DEBEN
  escribirse en español rioplatense con voseo, sin muletillas de producto, y los botones nuevos DEBEN
  nombrar su objeto y no sólo su verbo. **Esta última regla alcanza únicamente a los botones nuevos**:
  un botón que ya está en pantalla diciendo `Guardar` **se queda diciendo `Guardar`**, aunque nombre
  sólo su verbo. FR-066 tiene precedencia y el criterio no se aplica retroactivamente
- **FR-066**: Los textos que los Módulos 1 a 6 fijaron NO DEBEN reescribirse

### No regresión

- **FR-067**: Las 42 direcciones DEBEN seguir siendo las mismas
- **FR-068**: Las **285 pruebas de los 43 archivos DEBEN seguir pasando**, sin modificar ninguna
  aserción sobre textos, roles ni etiquetas accesibles
- **FR-069**: Mover las acciones de fila al menú `···` (FR-044) obliga a que las pruebas que hoy las
  tocan **abran el menú primero**. Eso es un paso de interacción, no una aserción, y es el único
  cambio autorizado sobre la suite. Está acotado a **20 líneas en 5 archivos**, que el plan DEBE
  enumerar: `TiposDocumentacion.test.tsx`, `ListadoTiposVehiculo.test.tsx`, `ListadoUsuarios.test.tsx`,
  `ListadoPersonas.test.tsx` y `ListadoClientes.test.tsx`. **Lo que de verdad acota el permiso es la
  lista de archivos**: si al implementar aparece una línea 21 dentro de esos cinco, se agrega. Una
  prueba fuera de esos cinco archivos es señal de que algo se rompió, no de que la lista estaba
  incompleta
- **FR-070**: Toda pantalla DEBE seguir operándose con teclado, alcanzando lo mismo que alcanza hoy.
  En particular, todas las acciones que hoy viven en la columna `Acciones` DEBEN seguir siendo
  alcanzables con teclado desde la fila
- **FR-071**: El título de la pestaña del navegador DEBE seguir nombrando la pantalla y el sistema
- **FR-072**: La resolución mínima soportada sigue siendo 1280 px de ancho: tablet y celular siguen
  fuera de alcance

## Key Entities

El sistema de diseño no tiene datos, pero sí un vocabulario cerrado. Estas son sus piezas:

- **Token**: un valor con nombre —un color, un tamaño, un radio, una sombra, una curva—. Vive en un
  solo lugar y es lo único que las pantallas pueden nombrar
- **Nivel de acción**: primario, secundario, terciario o destructivo. Es un atributo obligatorio de
  todo botón; no existe el botón sin nivel
- **Estado de campo**: reposo, foco, error o deshabilitado. Los cuatro se declaran para cada campo
- **Pastilla de estado**: la representación de un valor de estado, con su tono y su palabra. La
  palabra es obligatoria
- **Token de identificador**: la representación de un identificador que abre una ficha
- **Enlace de fila**: el dato que se busca con la vista, que es lo que se toca para entrar. Hay
  exactamente uno por fila
- **Menú de fila**: el `···` donde viven las acciones secundarias de una fila. Está siempre en el
  DOM, siempre es alcanzable con teclado y, abierto, se recorre con las flechas y se cierra con
  `Escape` devolviendo el foco a su disparador. No retiene el foco: no es un diálogo
- **Isla**: una superficie de contenido —la navegación, una tarjeta, un listado— con su radio, su
  hairline y su sombra difusa
- **Sección numerada**: la unidad de agrupación de un formulario, con su chip, su título y su
  explicación. Existe sólo cuando el formulario tiene dos o más grupos que distinguir

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Las **42 pantallas** se ven con la identidad de gt-ui; ninguna quedó con la apariencia
  anterior
- **SC-002**: En **cada** pantalla hay a lo sumo un botón **primario** —la pastilla oscura rellena—, y
  en toda pantalla con formulario ese botón es el que guarda. **La acción destructiva no entra en el
  conteo** (FR-017): en las cuatro fichas que la tienen conviven primario y destructivo, y es lo
  correcto. Lo que se cuenta es cuántas acciones se presentan como *la* principal, no cuántos
  rellenos hay
- **SC-003**: Los **16 formularios** ofrecen una salida visible sin desplazar la pantalla, y su acción
  principal es alcanzable desde cualquier punto del desplazamiento —anclada al viewport en los de
  página completa, al pie de su contenedor en los de diálogo y en las pantallas sin sesión—
- **SC-004**: Ninguna de las **21 tablas** muestra una celda con un guión donde falta un dato
- **SC-005**: Los importes de una columna se comparan en vertical: en las tablas con importes, los
  separadores de miles y la coma decimal quedan alineados entre todas las filas
- **SC-006**: En **cada listado de índice** hay **exactamente un** enlace por fila, es el dato que se
  busca con la vista, y se distingue del texto que no es accionable. Las tablas sin ficha de destino
  no tienen ninguno
- **SC-007**: Ninguna de las 21 tablas del sistema tiene columna `Acciones`
- **SC-008**: Recorriendo **sólo con el tabulador** un listado que tenga acciones de fila, se llega al
  menú `···` de cada fila y se abre sin tocar el mouse; sus ítems se recorren con las flechas y
  `Escape` lo cierra devolviendo el foco al `···`; y con las filas sin recorrer con el mouse, los
  `···` están todos visibles
- **SC-009**: Todo texto del sistema y todo elemento no textual que comunica información alcanza el
  contraste mínimo vigente, verificado con una herramienta de medición y no a ojo. Los cuatro valores
  recalibrados figuran en `tokens.md` con su medición **y el fondo sobre el que se midió** al lado, y
  con ellos la degradación de `dim` a token no textual (FR-007a) y la regla del lienzo (FR-007b)
- **SC-009a**: Ningún texto del sistema aparece directo sobre el lienzo ni sobre `surface-mute` en un
  tono que no sea `ink` o `ink-soft`
- **SC-010**: Ninguna pantalla muestra un mensaje de error en su primer dibujo, antes de que alguien
  la haya tocado
- **SC-011**: Las **285 pruebas** de los **43 archivos** pasan. Ninguna aserción sobre textos, roles
  ni etiquetas accesibles cambió, y los únicos pasos de interacción agregados son los de las 20
  líneas que FR-069 enumera, **todas dentro de sus cinco archivos**
- **SC-012**: Los seis quickstarts de los Módulos 1 a 6 se recorren completos con las tres cuentas y
  ninguna operación cambió de comportamiento
- **SC-013**: Todo el sistema se opera de punta a punta sólo con teclado, con el foco visible en cada
  paso
- **SC-014**: Una captura en escala de grises de cualquier listado permite distinguir los estados de
  sus filas
- **SC-015**: Con *menos movimiento* activado en el sistema operativo, ninguna pantalla anima nada

## Assumptions

- **El sistema de diseño se toma como está, salvo donde esta spec lo corrige.** `.claude/skills/gt-ui/`
  es la fuente. La única corrección autorizada es la de los cuatro valores de contraste (FR-007), y
  vuelve escrita a la skill (FR-008). `choferes- padron.png` todavía no figura en la lista de
  referencias de `SKILL.md`: registrarlo es parte del trabajo, para que la próxima feature lo
  encuentre sin que nadie se lo tenga que contar
- **Las imágenes son la fuente del aire, no de los valores.** Los cuatro PNG muestran ritmo, densidad
  y jerarquía. Los valores exactos salen de `tokens.md`, y los textos de las specs anteriores. Donde
  la imagen y el sistema no coinciden —el nombre *Sistema Integral de Transporte*, las opciones
  concretas del menú, los datos de ejemplo—, manda el sistema
- **Los cuatro estados que gt-ui nombra no son todos los del sistema.** Los cinco juegos de estados y
  sus veinte valores se mapean sobre el vocabulario de tonos de gt-ui, conservando en cada caso la
  palabra que ya está en pantalla y sumándole una forma además del color. Un valor sin mapeo cae en
  neutro
- **Esta feature no cambia cuándo se valida un formulario.** Hoy la validación ocurre al enviar y al
  volver del servidor, y así queda. Agregar validación al salir de cada campo sería comportamiento
  nuevo, y no está pedido
- **Un menú de fila no es un diálogo.** El `···` abre una lista de acciones sobre la fila: se recorre
  con las flechas y se cierra con `Escape` o al salir el foco, pero no lo retiene ni bloquea la
  pantalla de atrás. Las confirmaciones que esas acciones disparan siguen siendo los diálogos que ya
  existen, con las mismas palabras y el mismo comportamiento de foco
- **La fila que navega no se lleva por delante lo que tiene adentro.** Un clic sobre el menú `···`, o
  sobre cualquier control de la fila, opera ese control y no navega
- **El Módulo 7 queda cerrado por esta feature en lo visual, no en lo estructural.** Sus 16 tareas de
  validación manual pendientes se recorren sobre el resultado de este módulo, no sobre el anterior
- **Las dos familias tipográficas se empaquetan con la aplicación**, igual que la actual: no se
  cargan desde un servicio externo
- **El backend no se toca.** Ninguna pantalla necesita un dato que hoy no reciba
