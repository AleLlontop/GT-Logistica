# Research — Adopción del sistema de diseño gt-ui (Módulo 8)

**Feature**: `008-diseno-gt-ui` · **Fecha**: 2026-08-25 · **Spec**: [spec.md](./spec.md)

> **Este documento es el *cómo se llegó*, no lo que rige.** Registra mediciones, alternativas
> descartadas y redacciones de la spec que después cambiaron — por eso habla en pasado de cosas que
> hoy son otras. **Lo que rige está en [spec.md](./spec.md)**; acá está por qué.

Once preguntas. Las tres primeras son las que podían hundir la feature —el contraste, el motor de
estilos y las dos tipografías—; las ocho siguientes fijan decisiones que los módulos futuros heredan.

---

## §1. Los cuatro valores de contraste: qué se recalibra y contra qué fondo

**Decisión**: se recalibran los cuatro valores que FR-007 nombra, **cada uno medido sobre el fondo
donde ese token efectivamente aparece**, no sobre un fondo único de referencia. Y se agrega una regla
que la spec no pedía pero que la medición volvió obligatoria: **ningún texto va directo sobre el
lienzo salvo `ink` e `ink-soft`**. Los dos hallazgos volvieron a la spec como FR-007a y FR-007b: acá
queda el cómo se llegó, allá queda lo que rige.

### Cómo se llegó

Lo primero fue reproducir las mediciones que la spec cita, porque de eso depende contra qué fondo se
recalibra. Medidas sobre los cuatro fondos del sistema:

| Token | sobre blanco | sobre `surface-soft` | sobre `surface-mute` | sobre `canvas` |
|---|---|---|---|---|
| `faint` `#9AA0AA` | **2,63** | 2,48 | 2,35 | 2,27 |
| `dim` `#BFC4CD` | **1,75** | 1,65 | 1,57 | 1,51 |
| encabezado `#8D93A1` | 3,08 | **2,90** | 2,75 | 2,65 |
| `muted` `#6E7280` | 4,79 | 4,51 | 4,28 | 4,13 |

Los tres números que la spec cita —2,63 · 1,75 · 2,90— aparecen **cada uno en una columna distinta**,
y siempre en la del fondo donde ese token se usa: `faint` y `dim` sobre la tarjeta blanca, el
encabezado de columna sobre `surface-soft`, que es exactamente lo que `tokens.md` le asigna
(*"surface-soft: campos en reposo, encabezado de tabla, pie de tabla"*). La cuarta, 4,32:1 del badge
*Anulado*, reproduce sobre `#F2F3F6`, su propio fondo. **La base de medición de la spec es "cada
token sobre su fondo real"**, y es la correcta: medir todo contra un fondo único castigaría tokens
que nunca aparecen ahí.

### Los cuatro valores recalibrados

Se bajó la luminosidad en HSL —matiz y saturación intactos— hasta cruzar 4,5:1, con margen para que
el redondeo a 8 bits no lo devuelva por debajo:

| Token | Antes | Después | Medición | Fondo de medición |
|---|---|---|---|---|
| `faint` | `#9AA0AA` | **`#6E7683`** | 2,63:1 → **4,58:1** | blanco (metadata y ayudas sobre tarjeta) |
| `dim` | `#BFC4CD` | **`#6B768A`** | 1,75:1 → **4,58:1** | blanco (vacío y placeholders) |
| encabezado de columna | `#8D93A1` | **`#6B7181`** | 2,90:1 → **4,59:1** | `surface-soft` `#F7F8FA` |
| texto del badge *Anulado* | `#6E7280` | **`#6A6E7C`** | 4,32:1 → **4,58:1** | `#F2F3F6` |

### El hallazgo incómodo: la jerarquía de tonos no sobrevive, y eso cambia una decisión

Recalibrados los cuatro, la rampa neutra queda así (luminancia relativa, de claro a oscuro):

| Token | Valor | L | sobre blanco |
|---|---|---|---|
| `dim` (nuevo) | `#6B768A` | 0,1792 | 4,58:1 |
| `faint` (nuevo) | `#6E7683` | 0,1791 | 4,58:1 |
| `muted` | `#6E7280` | 0,1691 | 4,79:1 |
| encabezado (nuevo) | `#6B7181` | 0,1652 | 4,88:1 |
| `ink-soft` | `#3C4048` | 0,0510 | 10,40:1 |

**Los cuatro tonos claros caen dentro de una banda de luminancia de 0,165 a 0,179.** Numéricamente
siguen ordenados; perceptualmente son el mismo gris. No es un defecto del método: es aritmética. Por
debajo de `ink-soft` y por encima de 4,5:1 no hay lugar para cuatro niveles distinguibles, porque
`muted` —que ya cumplía— está en 4,79:1 y deja un hueco de 0,29 puntos de contraste donde habría que
meter `dim`, `faint` y el encabezado.

FR-007 pedía, en su redacción original, recalibrar *"conservando su papel en la jerarquía"*. Cumplir
las dos mitades de esa frase con los cuatro tokens **como colores de texto** es imposible. La salida
no es bajar el piso de contraste —eso contradice al Módulo 7 y a SC-009— sino aceptar lo que el número
dice, que es lo que la spec terminó fijando en FR-007a:

1. **`faint` es el único tono atenuado de texto.** Se lo usa donde la spec dice *"en el tono
   atenuado"*: la celda que dice qué falta (FR-038), la fila atenuada (FR-060), la metadata, las
   ayudas y los placeholders.
2. **`dim` deja de ser color de texto.** Queda para lo **no textual**: el separador `·` entre
   metadata, el relleno del ícono del estado vacío, el `#` del token de identificador. Ahí el piso
   aplicable es 3:1 cuando comunica y ninguno cuando es decorativo, y su valor original `#BFC4CD`
   alcanza. Donde `tokens.md` lo asignaba a texto —placeholders, *"Sin asignar"*— manda `faint`. Se
   registra el valor recalibrado igual, para el caso en que alguien lo necesite como texto.
3. **La distinción entre texto secundario y metadata deja de vivir en el tono y pasa a vivir en el
   tamaño y el peso** —13–14 px Regular contra 11,5–12 px Regular—, que es lo que la escala
   tipográfica de `tokens.md` ya define y que sobrevive a cualquier piso de contraste.

Esto no es una desviación inventada: es a lo que llegó el Módulo 7 por su cuenta. Su hoja tiene
exactamente **tres** tonos de texto —`texto` 15,12:1, `texto-suave` 6,28:1, `texto-tenue` 4,84:1—, no
cinco. La paleta de gt-ui describe cinco porque fue escrita mirando, no midiendo.

### El quinto valor, que la spec no listó

`muted` `#6E7280` cumple sobre blanco (4,79:1) y sobre `surface-soft` (4,51:1), y **falla sobre
`surface-mute` (4,28:1) y sobre el lienzo (4,13:1)**. No entra en el alcance de FR-007 —que nombra
cuatro— y no se lo recalibra, porque hacerlo sería alcance fantasma. Lo que sí se hace es convertir el
riesgo en una **regla verificable**, que no cambia ningún valor:

> **Ningún texto va directo sobre el lienzo ni sobre `surface-mute` salvo `ink` e `ink-soft`.** Todo
> el contenido vive dentro de una isla (FR-015), y sobre el lienzo sólo quedan el título de página
> —`ink`, 16,27:1— y su bajada —`ink-soft`, 8,96:1—.

Es coherente con FR-011: la columna principal es transparente, así que el lienzo **es** un fondo de
texto y hay que tratarlo como tal.

**Alternativas descartadas**:

- *Bajar el piso a 3:1 para metadata, tratándola como "texto no esencial"* — WCAG no tiene esa
  categoría; su excepción es para texto decorativo o inactivo, y una fecha de vencimiento no es
  ninguna de las dos. Contradice explícitamente lo que el Módulo 7 midió y dejó escrito.
- *Aclarar los fondos para ganar contraste* — cambiaría el lienzo `#EDEEF1`, que es la decisión visual
  más característica de gt-ui. Se corrige el texto, no el fondo.
- *Dejar los cinco tonos y documentar la excepción en el código* — es exactamente lo que FR-008
  prohíbe: la skill diciendo una cosa y la aplicación haciendo otra.

**Vuelve a la skill** (FR-008): los cuatro valores nuevos con su medición y su fondo al lado, más la
degradación de `dim` a token no textual y la regla del lienzo.

---

## §2. Motor de estilos: Tailwind v4 con `@theme`, sin `tailwind.config.js`

**Decisión**: los tokens de gt-ui se declaran en `@theme` dentro de `src/index.css`, reemplazando a
los del Módulo 7. **No** se crea el `tailwind.config.js` que muestra `tokens.md`.

**Rationale**: el proyecto ya corre Tailwind v4 (`tailwindcss` y `@tailwindcss/vite` 4.3.3), donde la
configuración es CSS-first y `@theme` genera las mismas utilidades que generaría el config de JS. El
snippet de `tokens.md` está escrito para v3. Adoptar ese archivo introduciría una segunda fuente de
tokens al lado de la que ya existe — exactamente lo que FR-001 prohíbe. Los nombres van dentro de los
espacios de nombre de v4 (`--color-*`, `--font-*`, `--radius-*`, `--shadow-*`): un token fuera de
ellos queda como variable suelta y no genera ninguna utilidad.

**El mapeo de nombres es 1 a 1 con `tokens.md`** —`canvas`, `surface`, `ink`, `muted`, `faint`, `dim`,
`line`, `brand`, `estado-*`, `danger`— y **no** se conservan los del Módulo 7 (`pagina`, `superficie`,
`texto`, `acento`). Renombrar es lo que hace que la skill y el código digan lo mismo, que es el punto
de FR-008. El costo es acotado y mecánico: los nombres viejos aparecen sólo en `index.css`, en las
primitivas de `compartido/ui/`, en `clases.ts`, en `Layout` y en `Menu` — las pantallas casi no
declaran color, porque el Módulo 7 ya las había limpiado.

**Alternativa descartada**: *mantener los nombres del Módulo 7 mapeados a los valores de gt-ui* —
dejaría `--color-acento` con el valor de `brand`, y quien leyera `tokens.md` buscaría `brand` en el
código sin encontrarlo.

---

## §3. Las dos familias tipográficas

**Decisión**: `@fontsource-variable/plus-jakarta-sans` para el texto y `@fontsource/geist-mono` para
los identificadores, empaquetadas con la aplicación. **`@fontsource-variable/geist` se retira**:
ninguna pantalla queda usándola.

**Rationale**: la spec asume que *"las dos familias tipográficas se empaquetan con la aplicación,
igual que la actual: no se cargan desde un servicio externo"*. Fontsource es el mecanismo que el
proyecto ya usa para eso. Plus Jakarta Sans está publicada en versión variable, que es un solo archivo
para los cinco pesos que la escala de gt-ui usa (Regular, Medium, SemiBold, Bold, ExtraBold); en
estático serían diez archivos contando cursivas.

**Riesgo declarado**: `tokens.md` prohíbe Inter, Roboto, Arial, Helvetica y Open Sans **como fuente
efectiva**, no sólo como primera opción de la pila. La pila de respaldo se escribe
`'Plus Jakarta Sans Variable', system-ui, sans-serif`, y hay que **verificar con la herramienta de
desarrollo que la familia resuelta sea la propia** (FR-003): `system-ui` resuelve a Segoe UI en
Windows y en algunos Linux a un alias que termina en una de las prohibidas. La verificación es una
tarea, no un supuesto.

---

## §4. Qué se puede cambiar sin romper 285 pruebas

**Decisión**: se cambian libremente clases, envoltorios, orden de bloques y elementos contenedores.
**No** se cambian textos, roles, etiquetas accesibles ni la asociación `label`↔control.

**Rationale**: la suite consulta por rol, por texto y por etiqueta —138 `getByLabelText`, 109
`getByRole`, 28 `selectOptions`—, nunca por clase ni por estructura del DOM. Es la convención [007], y
es lo que permitió al Módulo 7 reestructurar 42 pantallas con la suite en verde. Dos consecuencias
concretas para esta feature:

- **Fusionar dos columnas en una es seguro** mientras los dos textos sigan en el DOM. `Ruta` con
  `Rosario → Córdoba` conserva `Rosario` y `Córdoba` como texto; lo que se pierde es el `<th>`
  `Origen`, y ninguna prueba consulta encabezados de columna por nombre (verificado: cero
  coincidencias de `getByRole('columnheader')` en los 43 archivos).
- **Mover una acción de fila dentro de un menú `···` no es seguro**, porque el botón deja de estar en
  el DOM hasta que el menú se abre. Ése es el único cambio que obliga a tocar la suite, y FR-069 lo
  acota a 20 líneas en 5 archivos (§8).

---

## §5. El menú `···` de una fila: se escribe, no se instala

**Decisión**: se escribe una primitiva propia `MenuDeFila`, con el comportamiento de teclado que
FR-044 fija. **No** se adopta `@radix-ui/react-dropdown-menu`.

**Rationale**: es la convención [007] aplicada tal cual — *"se toma lo que agrega comportamiento y no
lo que reemplaza un control nativo que los tests operan"*. Radix Dropdown Menu aporta foco y flechas,
pero convierte cada ítem en un `div[role="menuitem"]`; las acciones de fila de hoy son `<button>` y
`<Link>`, y las 20 líneas de FR-069 los consultan como `getByRole('button', { name: 'Dar de baja' })`.
Con Radix habría que reescribir esas aserciones, y FR-069 autoriza **agregar un paso de interacción,
no cambiar una aserción**. La primitiva propia mantiene los ítems como `button` y `a` de verdad, y las
20 líneas sólo necesitan un `click` sobre el `···` delante.

Lo que hay que escribir es acotado y está enteramente especificado por FR-044: abrir con `Enter` o
`Espacio`, foco al primer ítem, flechas para recorrer, `Escape` cierra y devuelve el foco al
disparador, el foco que sale cierra. No retiene el foco ni bloquea el fondo — **no es un diálogo**, y
por eso `@radix-ui/react-dialog`, que sí está instalado, no sirve acá.

**Alternativas descartadas**: *dejar las acciones visibles en la fila y no hacer menú* — contradice
FR-046 y SC-007. *Mostrar el `···` sólo al pasar el mouse* — FR-044 lo prohíbe explícitamente, y con
razón: quedaría inalcanzable con teclado y en pantalla táctil.

---

## §6. La fila navegable sin romper lo que tiene adentro

**Decisión**: la fila lleva un `onClick` que navega, y **el enlace real sigue siendo el `<a>` de la
celda**. Todo control interactivo dentro de la fila detiene la propagación.

**Rationale**: FR-042 pide que la fila entera navegue y FR-023 pide que lo que navega siga siendo un
enlace. Envolver la fila en un `<a>` no es válido —un `<a>` no puede contener `<td>`— y ponerle
`role="link"` al `<tr>` rompería `getByRole('row')`. La combinación que cumple las dos cosas es: el
`<a>` de la celda es el destino real, accesible por teclado y anunciado por el lector de pantalla; el
`onClick` del `<tr>` es una comodidad de mouse que replica ese destino. El `<tr>` **no** recibe
`tabIndex`: agregarlo duplicaría cada fila en el recorrido del tabulador, que es justo lo que SC-008 y
SC-013 no quieren.

La regla de la spec —*"un clic sobre el menú `···`, o sobre cualquier control de la fila, opera ese
control y no navega"*— se implementa con `stopPropagation` en la celda del `···` y en cualquier celda
con control propio (la casilla del selector de viajes, el enlace *Abrir archivo* de las fichas).

---

## §7. Los veinte valores de estado sobre cuatro tonos

**Decisión**: se conserva el mapa `TONO_POR_VALOR` que ya existe en `compartido/ui/Estado.tsx` y se
reasignan sus tonos a los cuatro de gt-ui más neutro. **La palabra sigue llegando desde afuera** y un
valor desconocido sigue cayendo en neutro.

**Rationale**: la primitiva ya resuelve el problema —`texto` es obligatorio, el valor desconocido no
rompe— y lo único que cambia es la tabla de colores. Los cuatro tonos de gt-ui (`rendido`,
`pendiente`, `facturado`, `anulado`) son **cuatro roles cromáticos**, no cuatro estados de viaje:
verde de cierre, ámbar de espera, índigo de emitido y gris de anulado. Los veinte valores del sistema
caen sobre esos cuatro más neutro sin forzar nada (el mapeo completo va en `data-model.md` §3).

FR-055 agrega una distinción que hoy no existe: **la pastilla se reserva para el estado del que trata
la pantalla**, y un estado de alta y baja que sólo acompaña va como punto y palabra sin pastilla. Eso
es una prop nueva en `Estado` —`forma: 'pastilla' | 'punto'`—, obligatoria como ya lo es `texto`, por
la misma razón que la variante del botón es obligatoria: una forma con valor por defecto se elige sola
y nadie se entera.

---

## §8. Las 20 líneas de la suite que FR-069 autoriza

**Decisión**: se verificó el inventario contra el código y la lista de cinco archivos de FR-069 es
correcta y completa.

Diez tablas tienen hoy columna `Acciones`. De ellas, **cinco** tienen pruebas que operan esas
acciones, y son exactamente las cinco que FR-069 nombra:

| Archivo | Líneas | Acciones que quedan detrás del `···` |
|---|---|---|
| `TiposDocumentacion.test.tsx` | 129, 149 | *Editar* |
| `ListadoTiposVehiculo.test.tsx` | 123, 138, 157, 158, 160, 168, 186, 188 | *Editar*, *Dar de baja*, *Dar de alta* |
| `ListadoUsuarios.test.tsx` | 104, 118, 139 | *Dar de baja* |
| `ListadoPersonas.test.tsx` | 94, 114 | *Dar de baja* |
| `ListadoClientes.test.tsx` | 90, 91, 109, 110, 111 | *Editar*, *Dar de baja*, *Dar de alta* |

**Son 20 líneas.** FR-069 decía 21 antes de este relevamiento y se lo corrigió con el número medido;
lo que no cambió —porque es lo que de verdad acota el permiso— es la lista de cinco archivos. Siete
de esas líneas son `queryByRole(...).not.toBeInTheDocument()`, y de ésas, **las tres de la prueba sin permiso
de gestión (`ListadoClientes` 109–111) siguen pasando sin tocarlas** — FR-045 dice que sin acciones
disponibles el `···` no se dibuja, así que los botones siguen ausentes por la razón correcta. Se
enumeran igual, porque hay que **mirarlas** para confirmar que pasan por el motivo bueno y no por
casualidad. Si al implementar aparece una línea 21 que este relevamiento no vio, se agrega a la lista;
lo que FR-069 prohíbe es tocar una prueba **fuera de estos cinco archivos**, y eso sigue firme.

Las otras cinco tablas con `Acciones` —`ListadoChoferes`, `ListadoFlota`, `ListadoTransportistas`,
`FichaChofer`, `FichaVehiculo`— no tienen pruebas que toquen sus acciones de fila (verificado: cero
coincidencias). `FichaChofer` y `FichaVehiculo` no tienen archivo de prueba.

**Caso aparte, y es una simplificación**: en `ListadoChoferes` y `ListadoFlota` la columna `Acciones`
contiene **únicamente** *Ver ficha*, que es justo lo que FR-040 convierte en enlace de fila. En esas
dos tablas la columna desaparece y **no queda ningún `···`**: no hay acción secundaria que alojar.

---

## §9. En qué orden se toca la aplicación

**Decisión**: tokens → estructura de página → primitivas → pantallas, con **las primitivas antes que
las pantallas**.

**Rationale**: el Módulo 7 dejó el trabajo hecho para que esto sea barato. Las 42 pantallas casi no
declaran estilo: lo declaran las primitivas de `compartido/ui/`, `clases.ts`, `Layout` y `Menu`.
Cambiar los tokens y las primitivas cambia el aspecto de las 42 pantallas de una vez, y recién después
queda lo que sí es pantalla por pantalla y que **no es estilo sino estructura**: las columnas
fusionadas (FR-047), el enlace de fila (FR-040), el menú `···` (FR-044), las secciones numeradas
(FR-029) y el aside de las cinco fichas (FR-050).

Consecuencia práctica para el orden de tareas: **después de cambiar tokens y primitivas hay que poder
correr la suite y verla en verde.** Si no lo está, el problema es de esa capa y no de las 42
pantallas, y se arregla antes de seguir. Es el punto de control más barato de toda la feature.

---

## §10. El encabezado de pantalla: qué hay de verdad detrás de `accionPrincipal`

**Decisión**: la tabla de acción primaria del plan se corrige contra el código. El relevamiento
encontró que **`accionPrincipal` no contiene una acción principal**, y que cuatro filas de esa tabla
nombraban acciones que hoy no existen.

Se hizo la misma pasada que §8 hizo con la suite, y por la misma razón: una lista que la revisión va a
contar tiene que salir del código y no de la lectura de otra lista.

### Lo que se contó, y cerró

| Qué | Esperado | Medido | |
|---|---|---|---|
| Tablas (`<table`, sin tests) | 21 en 20 archivos | **21 en 20 archivos** | ✅ |
| Columnas `Acciones` (`<th scope="col">Acciones</th>`) | 10 | **10** | ✅ |

Las 21 tablas y las 10 columnas coinciden exactamente con [data-model §5](./data-model.md#5-las-21-tablas),
incluidas las dos de `FichaFactura` y la de `ListadoClientes`, que ya hoy es condicional
(`{puedeGestionar && <th …>}`) — o sea que FR-045 no inventa el criterio, lo generaliza.

### Lo que no cerró: `accionPrincipal` es un grupo estilado por descendiente

La prop recibe un fragmento con **una o varias** acciones, y `EncabezadoDePantalla` las estila con
selectores de descendiente —`[&_button]`, `[&_a]`, `[&_button[type=submit]]`— decidiendo cuál se
destaca por el **tipo del botón**, no por una variante declarada.

Es el antipatrón de la convención [007] sobreviviendo adentro de una primitiva: una regla que
selecciona elementos interactivos a secas. Y tiene una consecuencia que sólo se ve operando: **en las
cuatro fichas no hay ningún `type="submit"`** —son todos `type="button"`—, así que hoy *Editar*,
*Dar de baja* y *Registrar cobro* se dibujan **idénticas**. La ficha no tiene acción principal; tiene
una fila de botones iguales.

Para esta feature eso cambia el tamaño del trabajo: las primarias que la tabla del plan asigna a las
cinco fichas **hay que designarlas, no revestirlas**. La firma de la primitiva se conserva
(contracts §1); lo que se retira es el bloque de selectores por descendiente, que es lo que hoy
sustituye a la variante que FR-021 exige declarar.

### Las cuatro correcciones a la tabla del plan

| Pantalla | Decía | Es | Qué implica |
|---|---|---|---|
| `/viajes` | secundaria *Ver totales* | **no existe**: sólo *Nuevo viaje* | `/viajes/totales` se alcanza **únicamente desde el menú**. Agregarle un enlace sería alcance fantasma: la fila se corrige, no la pantalla |
| `/facturas` | secundarias *Ver vencimientos* y *Ver totales* | **no existen**: sólo *Nueva factura* | ídem para `/facturas/totales` y `/facturas/vencimientos` |
| `/facturas/totales` | primaria `ninguna` | **`Ver totales`**, un `type="submit"` que ejecuta la consulta por rango de fechas | Es una acción de verdad y es la principal de la pantalla |
| `/viajes/totales` | primaria `ninguna` | **`ninguna`**, correcto: su `<form>` hace `preventDefault` y no tiene botón | Los dos paneles de totales **no son simétricos**, y la tabla los trataba igual |

### El *volver* no está donde la firma lo espera

`EncabezadoDePantalla` tiene una prop `volverA` que **ninguna de estas pantallas usa**. El *volver*
viaja hoy adentro de `accionPrincipal`, como una acción más:

- `PanelVencimientos` (choferes) — *Volver al listado de choferes*
- `PanelVencimientosFlota` — *Volver al listado de flota*
- `ListadoTiposVehiculo` — *Volver a la flota*
- `FichaFactura` — un `<button>` que navega a `/facturas`
- `DetalleUsuario` — *Volver al listado*, dentro de su propio `<nav aria-label="Acciones sobre el usuario">`

FR-022 pide el terciario **arriba a la izquierda, fuera de la línea de decisión**. Para estas cinco no
es revestir: es **mudar el control de prop**, que es justo el cambio que la convención [007] declara
seguro —no toca texto, ni rol, ni etiqueta accesible—.

Dos más que la tabla no contemplaba:

- **`PanelVencimientos` de facturación no tiene ninguna acción de encabezado**, ni siquiera un volver.
  El *Volver a facturas* que la tabla le asigna **hay que crearlo**.
- **`DetalleUsuario` es la única ficha que no usa `accionPrincipal`**: arma su propio `<nav>`. Es la
  que más trabajo de encabezado tiene, no la que menos.

### Dónde quedan hoy los dos mecanismos

`ListadoFacturas` ya pasa `clasesDeBoton('primario')` a su enlace; las otras catorce dependen del
estilado por descendiente. Conviven dos formas de decidir la jerarquía en el mismo encabezado, y la
feature deja **una**: la variante declarada.

---

## §11. Los verbos de la tabla de acción primaria, contra el código

**Decisión**: la tabla del plan lleva el **texto literal que está hoy en pantalla**, no el nombre
conceptual de la acción. Se verificaron las 42 filas y **18 estaban mal**.

Es la corrección más importante que salió de este relevamiento, porque la tabla decía de sí misma
*"los verbos son los que ya están en pantalla y no se reescriben (FR-066)"* — y no lo eran. Un plan
que afirma lo contrario de lo que hace no se puede revisar leyéndolo.

### Los verbos que no coincidían

| Pantalla | Decía la tabla | Dice el código |
|---|---|---|
| `/usuarios/nuevo`, `/usuarios/:id/editar` | *Guardar usuario* · *Guardar cambios* | **`Guardar`** |
| `/usuarios/:id/roles` | *Guardar roles* | **`Guardar`** |
| `/personas/nueva`, `/personas/:id/editar` | *Guardar persona* · *Guardar cambios* | **`Guardar`** |
| `/facturacion/empresa` | *Guardar datos de la empresa* | **`Guardar`** |
| `/facturas/:id/editar` | *Guardar corrección* | **`Guardar cambios`** |
| `/flota/nuevo` | *Guardar vehículo* | **`Registrar unidad`** |
| `/viajes/:id/asignacion` | *Guardar asignación* | **`Asignar`** (y su salida es *Volver al viaje*, no *Cancelar*) |
| `/usuarios/:id` | *Editar usuario* | **`Editar`** |
| `/flota/:id` | *Cargar documento* · *Editar vehículo* | **`Agregar documento`** · **`Editar`** |
| `/viajes/:id` | *Editar viaje* · *Anular viaje* | **`Editar`** · **`Anular`** |
| `/facturas/:id` | *Corregir* · *Anular factura* | **`Corregir datos`** · **`Anular`** |

**`Cargar documento` y `Agregar documento` son dos textos distintos** —chofer usa el primero, vehículo
el segundo— y los dos están congelados. La tabla los había unificado.

### Por qué esto rompía la feature

`Guardar` a secas es el verbo de cinco formularios, y **once aserciones lo consultan por nombre**:

| Archivo | Aserciones `{ name: 'Guardar' }` |
|---|---|
| `FormularioUsuario.test.tsx` | 4 |
| `EmpresaEmisora.test.tsx` | 4 |
| `PanelRoles.test.tsx` | 3 |

Ninguno de los tres está entre los cinco archivos que FR-069 autoriza a tocar. `FichaFactura.test.tsx`
agrega lo suyo: consulta `{ name: 'Corregir datos' }` literal.

**El conflicto de fondo era entre dos requisitos.** FR-065 pide que un botón nombre su objeto y no
sólo su verbo; FR-066 congela los textos de los Módulos 1 a 6. La tabla lo había resuelto en silencio
hacia FR-065. Se resolvió al revés y se escribió: FR-065 **alcanza sólo a los textos nuevos**.

### Dos acciones más que la tabla tenía mal

- **`/facturas/:id` listaba una secundaria *Ver documento* que no existe en ninguna parte** — cero
  coincidencias en todo el frontend. Era alcance fantasma escrito en el plan (Principio III), y se
  retiró.
- **`/usuarios/:id` listaba una destructiva *Dar de baja* que esa pantalla no tiene** —es de
  `ListadoUsuarios`— y **omitía `Restablecer contraseña`**, que sí tiene y es la más delicada de la
  ficha. La fila quedó con sus cuatro controles reales y **sin destructiva**.

### El *volver*, completo

Corrige y completa lo que §10 dejó a medias —ahí faltaba `/viajes/:id`—:

| Situación | Pantallas |
|---|---|
| **Mudar** de `accionPrincipal` a `volverA` (6) | `/choferes/vencimientos`, `/flota/vencimientos`, `/tipos-vehiculo`, `/viajes/:id`, `/facturas/:id`, `/usuarios/:id` |
| **Crear**, porque no tienen ninguno (3) | `/facturas/vencimientos`, `/choferes/:id`, `/flota/:id` |

### Lo que la tabla ya tenía bien

Las 24 filas restantes coinciden: `Ingresar`, `Cambiar contraseña`, `Guardar chofer`,
`Guardar transportista`, `Guardar viaje`, `Guardar cliente`, `Guardar cambios` en las cinco ediciones
que sí lo dicen, `Registrar unidad`, `Emitir factura`, `Cargar tipo`, `Registrar cobro`, los seis
`Nuevo …` de los listados y las dos `Ver vencimientos`.
