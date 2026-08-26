# Contratos — Adopción del sistema de diseño gt-ui (Módulo 8)

**Feature**: `008-diseno-gt-ui` · **Spec**: [../spec.md](../spec.md) · **Data model**: [../data-model.md](../data-model.md)

Esta feature **no expone ninguna interfaz nueva hacia afuera**: no toca el backend, no agrega
endpoints, no cambia ningún DTO y no cambia ninguna de las 42 direcciones (FR-067). El backend sigue
siendo la única fuente de verdad de qué opciones de menú existen y de qué puede hacer cada usuario.

La interfaz que sí tiene contrato es la **interna**: las primitivas de `frontend/src/compartido/ui/`
que las 42 pantallas consumen. Es lo que hay que acordar antes de escribir código, porque un cambio
de firma acá se paga en 42 lugares.

Las firmas van en TypeScript porque es el lenguaje en que el contrato se hace cumplir: una prop
obligatoria y tipada **no compila** si falta, y ésa es toda la garantía de la convención [007].

---

## 1. Lo que NO cambia de firma

Estas primitivas del Módulo 7 cambian por dentro —tokens, forma, densidad— y **conservan su firma
exacta**. Ninguna pantalla que las use necesita tocarse.

| Primitiva | Firma que se conserva |
|---|---|
| `Campo` | `{ id, etiqueta, obligatorio?, error?, ayuda?, ancho?, children, className? }` |
| `Aviso` | `{ tono, rol, children, className? }` |
| `Dialogo` | `{ titulo, onCerrar, children, className? }` |
| `DialogoConfirmacion` | `{ titulo, mensaje, etiquetaConfirmar?, onConfirmar, onCancelar }` |
| `EstadoVacio` | `{ caso, children, accion?, className? }` |
| `Paginacion` | `{ pagina, total, tamanioPagina, onCambiarPagina, nombrePlural }` |
| `Listado` / `TablaDesplazable` | `{ children, className? }` |
| `EncabezadoDePantalla` | `{ titulo, accionPrincipal?, volverA?, resumen?, className? }` |

**Son ocho.** `Boton`, `Estado`, `Filtros` y `Ficha` **sí** cambian de firma y están en §2. Las cuatro
cambian por la misma razón: la spec les pide una **estructura interna** —un ícono anidado, una forma
de estado, una franja de resumen, una segunda columna— y una estructura no se puede agregar desde una
cadena de clases. Donde el Módulo 7 hubiera puesto un selector de descendiente, esta feature pone una
prop: es lo que dice la convención [007] y lo que T035a viene a retirar de `EncabezadoDePantalla`.

Dos que merecen decirse en voz alta:

- **`Paginacion` no cambia ni una coma de su texto.** Cuatro pruebas lo verifican palabra por palabra
  (`'Página 2 de 4, mostrando 21 a 40 de 73 choferes'`) y su `role="status"` es lo que anuncia el
  cambio de página sin que la pantalla cambie (FR-061).
- **`Dialogo` conserva su `onOpenAutoFocus` que lleva el foco al diálogo y no a su primer control.**
  Es lo que fijó el Módulo 2 y ratificó la convención [007]: manda la spec sobre el valor por defecto
  de Radix (FR-062).

---

## 2. Lo que cambia de firma

### 2.1 `Estado` — suma `forma`, obligatoria

```ts
type Forma = 'pastilla' | 'punto'

interface Props {
  /** El valor tal como lo devuelve el API, en camelCase. Un valor desconocido cae en neutro. */
  valor: string
  /** La palabra que ya está en pantalla. Obligatoria: el color nunca comunica solo (FR-055). */
  texto: string
  /**
   * `pastilla` para el estado **del que trata la pantalla**; `punto` para un estado de alta y baja
   * que sólo acompaña y no debe competir con él (FR-055).
   *
   * Obligatoria y sin valor por defecto, por la misma razón que `variante` en `Boton`: una forma
   * que se elige sola es una decisión que nadie revisa.
   */
  forma: Forma
  /** El dato accesorio del estado —comprobante, fecha, motivo—. Va **debajo**, en menor jerarquía,
   *  y nunca repite la palabra del estado (FR-057). */
  detalle?: ReactNode
  className?: string
}
```

**Todas las llamadas existentes tienen que agregar `forma`.** Es un cambio que rompe la compilación a
propósito: obliga a decidir, en cada uno de los usos, si ese estado es el tema de la pantalla o si
sólo acompaña.

### 2.2 `Boton` — suma `icono`, opcional (FR-018)

```ts
interface Props extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className'> {
  variante: Variante          // obligatoria y sin defecto, como hoy (FR-021)
  tamanio?: Tamanio
  /**
   * El ícono de la acción. La primitiva lo **anida en un círculo interno** pegado al borde derecho
   * (FR-018); nunca se dibuja suelto al lado del texto. Sólo lo rinden `primario` y `destructivo`:
   * en `secundario` se ignora, y `terciario` dibuja su flecha por su cuenta.
   */
  icono?: ReactNode
  className?: string
}
```

**Por qué es una prop y no un selector.** El círculo interno es un `<span>` que envuelve al ícono:
una cadena de clases no puede crear un elemento. La alternativa era `[&>svg:last-child]` sobre los
hijos, que es exactamente la regla que selecciona elementos interactivos a secas que la convención
[007] prohíbe y que T035a retira de `EncabezadoDePantalla` en esta misma feature. Con la prop, un
botón que pasa el ícono adentro de `children` se ve mal **y se ve en la revisión**; con el selector se
vería mal en silencio.

Es **opcional y aditiva**: ninguna llamada existente deja de compilar, y los 109 `getByRole` de la
suite siguen encontrando un `<button>` nativo con su nombre accesible intacto.

### 2.3 `Filtros` — suma `resumen`, opcional (FR-035)

```ts
interface Props {
  children: ReactNode
  /** El texto que declara qué se está mostrando. Un listado nunca oculta filas en silencio (FR-034). */
  declaracion?: ReactNode
  /**
   * La **franja de resumen** (FR-035): cantidad de resultados, el total o las señales que la pantalla
   * tenga —*1 con documentación vencida*— y el criterio de orden. Va **debajo de los filtros y encima
   * de la tabla**, y la arma cada listado porque los tres datos son suyos.
   */
  resumen?: ReactNode
  className?: string
}
```

**`declaracion` y `resumen` no son lo mismo y por eso son dos props.** `declaracion` responde *qué se
está mostrando* —es la garantía de [003] contra ocultar filas en silencio— y `resumen` responde
*cuántos, cuánto suman y en qué orden*. Meterlos en una sola prop obligaría a cada listado a decidir
el orden visual de dos cosas que la spec ya ordenó.

### 2.4 `Ficha` — pasa a dos columnas (FR-050, FR-051, FR-052)

Hoy `Ficha.tsx` exporta `FichaEncabezado`, `FichaSeccion` y `FichaCuerpo`, y las consumen las cinco
fichas. `FichaSeccion` **no cambia**. Las otras dos sí:

```ts
interface FichaEncabezadoProps {
  identidad: ReactNode
  /** El token del identificador, **en la misma línea** que el título y la pastilla (FR-051). */
  token?: ReactNode
  estado?: ReactNode
  /** El contexto del registro, debajo de la línea de identidad (FR-051). */
  resumen?: ReactNode
  acciones?: ReactNode
  nota?: ReactNode
}

interface FichaCuerpoProps {
  children: ReactNode          // la columna principal, flexible
  /**
   * El aside fijo de 330 px. **Obligatorio**: FR-050 dice que nunca queda vacío, y `AsideDeFicha`
   * exige a su vez un `destacado`. Una ficha que no sabe cuál es su dato de más valor no compila.
   */
  aside: ReactNode
  className?: string
}
```

**`aside` es obligatorio a propósito**, por la misma razón que `variante` en `Boton` y `forma` en
`Estado`: las cinco fichas tienen su dato destacado decidido en [data-model §8](../data-model.md#8-el-dato-de-más-valor-del-aside-fr-053),
y un opcional dejaría que una se olvidara del suyo sin que nada falle. El `estado` sale de la lista de
datos y sube al encabezado (FR-052): eso **sí** toca las cinco fichas, y es el cambio de estructura
que T095–T100 enumeran.

---

## 3. Primitivas nuevas

### 3.1 `MenuDeFila` — el `···` (FR-044, FR-045)

```ts
interface ItemDeMenu {
  /** El texto del ítem. Verbo en infinitivo + objeto cuando hay ambigüedad (FR-065). */
  etiqueta: string
  /** Un ítem que navega es un enlace; uno que ejecuta es un botón (FR-023). Se pasa uno u otro. */
  a?: string
  onSeleccionar?: () => void
  /** Los destructivos van al final y en `danger` (FR-020). */
  destructivo?: boolean
}

interface Props {
  /** Nombre accesible del disparador: `Acciones de Gómez, Ramona`. Nunca sólo "Acciones". */
  etiqueta: string
  items: ItemDeMenu[]
}
```

**Contrato de comportamiento**, que es lo que de verdad hay que cumplir:

1. El disparador está **siempre en el DOM y siempre es enfocable**. No aparece al pasar el mouse.
2. `items` vacío → **no se dibuja nada** (FR-045). Ocultarlo es cortesía; la restricción es el
   rechazo del servidor.
3. Abre con clic, `Enter` o `Espacio`. Al abrir, el foco pasa al **primer ítem**.
4. `ArrowDown` / `ArrowUp` recorren los ítems y ciclan.
5. `Escape` cierra y **devuelve el foco al disparador**.
6. El foco que sale del menú lo cierra.
7. **No retiene el foco ni bloquea la pantalla de atrás**: no es un diálogo.
8. Un ítem que dispara una confirmación le entrega el foco al `Dialogo` que ya existe (FR-062).
9. Los ítems son `<button>` y `<a>` **de verdad**, no `div[role="menuitem"]`: es lo que permite que
   las 20 líneas de FR-069 sólo agreguen un clic y no cambien ninguna aserción.
10. El clic sobre el disparador **no dispara la navegación de la fila** (`stopPropagation`).

### 3.2 `EnlaceDeFila` y `TokenDeIdentificador` (FR-040, FR-041)

```ts
interface EnlaceDeFilaProps {
  a: string
  /** Lo que se busca con la vista: el apellido, la razón social. Va subrayado. */
  children: ReactNode
  /** El identificador que lo acompaña: el DNI, el CUIT, la patente. En mono, menor jerarquía. */
  identificador?: ReactNode
}

interface TokenDeIdentificadorProps {
  /** Cuando lleva `a` es un enlace; sin `a` es una etiqueta. */
  a?: string
  numero: ReactNode
}
```

**Exactamente un `EnlaceDeFila` o un `TokenDeIdentificador` con `a` por fila** en los 9 listados de
índice (SC-006). Las 12 tablas sin destino propio no llevan ninguno.

### 3.3 `FilaNavegable` (FR-042, FR-043)

```ts
interface Props {
  /** El mismo destino que el enlace de la fila. */
  a: string
  children: ReactNode
}
```

Renderiza un `<tr>` con `onClick` que navega y un chevron `›` que aparece al pasar el mouse. **No
recibe `tabIndex`**: el destino accesible por teclado es el `<a>` de la celda, y duplicar la fila en
el recorrido del tabulador es exactamente lo que SC-013 no quiere (research §6).

### 3.4 `SeccionNumerada` y `BarraDeAcciones` (FR-029, FR-030)

```ts
interface SeccionNumeradaProps {
  numero: number
  titulo: string
  /** Una línea que explica qué se pide, cuando ayuda. */
  explicacion?: string
  children: ReactNode
}

interface BarraDeAccionesProps {
  /**
   * `viewport` para un formulario de página completa —fija al pie de la ventana—.
   * `contenedor` para un formulario dentro de un diálogo y para las pantallas sin sesión.
   * Obligatoria: es la decisión que FR-030 distingue y no tiene un valor por defecto correcto.
   */
  anclaje: 'viewport' | 'contenedor'
  /** La leyenda de obligatorios, a la izquierda. */
  leyenda?: ReactNode
  /** Las acciones, a la derecha, con el primario último. */
  children: ReactNode
}
```

`SeccionNumerada` se usa **sólo en los 9 formularios de dos o más grupos**. Los 7 de un solo grupo no
la usan: un chip numerado sobre un único grupo no agrupa nada.

### 3.5 `AsideDeFicha`, `LineaDeTiempo`, `Callout`, `Isla`, `Lienzo`

```ts
interface AsideDeFichaProps {
  /** El dato de más valor. Nunca vacío (FR-050, FR-053). */
  destacado: ReactNode
  children?: ReactNode
}

interface LineaDeTiempoProps { children: ReactNode }
interface EntradaProps {
  cuando: ReactNode
  /** La transición completa, no dos columnas: `Pendiente → Facturado`. */
  que: ReactNode
  quien?: ReactNode
  motivo?: ReactNode
  /** El paso actual va marcado. */
  actual?: boolean
}

interface CalloutProps {
  tono: 'rendido' | 'pendiente' | 'facturado' | 'anulado' | 'danger'
  titulo: string
  /** Qué pasa **y** la salida. Si no hay salida, por qué (FR-058). */
  children: ReactNode
}

interface IslaProps { children: ReactNode; className?: string }
```

`Lienzo` no recibe props: son los dos orbes en `fixed inset-0 -z-10 pointer-events-none`. Va una sola
vez, en la raíz, **nunca dentro de un contenedor con desplazamiento** (FR-002).

---

## 4. Lo que se retira

| Pieza | Reemplazo |
|---|---|
| `Historial` / `HistorialEntrada` | `LineaDeTiempo` / `EntradaDeLineaDeTiempo` (FR-054) |
| `clasesDeEnlaceDeFila` | `EnlaceDeFila` y `TokenDeIdentificador` |
| `@fontsource-variable/geist` | `@fontsource-variable/plus-jakarta-sans` + `@fontsource/geist-mono` |
| Los tokens del Módulo 7 (`pagina`, `superficie`, `texto`, `acento`, …) | Los nombres de `tokens.md` (research §2) |
| La barra superior de `Layout` | El pie de la isla de navegación (FR-010) |

`clasesDeFormulario` y `clasesDeControl` **se conservan**: siguen siendo la forma de estilar controles
nativos sin sustituirlos (FR-031), y siguen sin ser una regla global sobre `input`.

---

## 5. Los textos: qué se congela y qué escribe esta feature

**Se congela** (FR-066, FR-068): todo mensaje de error, de confirmación y de estado vacío; toda
etiqueta de campo; todo verbo de botón que ya existe; el texto de `Paginacion`; los nombres de estado
de `NombresDeEstado` y los `TEXTO_ESTADO_*`.

**Los escribe esta feature** (FR-065), en español rioplatense con voseo, sin emoji y sin muletillas de
producto:

| Qué | Dónde | Valor |
|---|---|---|
| Rótulos de columna fusionada | 15 fusiones en 12 tablas | *Ruta*, *Asignación*, *Chofer*, *Persona*, *Vehículo*, *Transportista*, *Cliente*, *Contacto*, *Documento* |
| Títulos de sección numerada | 9 formularios | ver [data-model §6](../data-model.md#6-los-16-formularios-y-sus-secciones-fr-029) |
| Nombre accesible del `···` | 8 tablas | `Acciones de {identidad de la fila}` |
| Leyenda de obligatorios | 16 formularios | *Los campos con \* son obligatorios* |
| **Placeholders de campo** | ~20 campos, en la columna *Vacío* de la tabla de estados del plan | `30.123.456` · `20-30123456-4` · `30-71234567-8` · `EF456GH` · `0000-00000111` · `71234567890123` · `0001` · `30` · `22644,63` · `+54 9 221 555-0148` · `nombre@empresa.com.ar` · `Rosario` · `Córdoba` · `Distribuidora del Litoral` |
| **Textos de *Seleccioná*** | los `select` sin valor elegido | *Seleccioná un tipo* · *Seleccioná un transportista* · *Seleccioná un cliente* · *Seleccioná un chofer* · *Seleccioná un vehículo* |
| **Chip *Opcional*** | los campos no obligatorios | *Opcional* |

**Los placeholders son texto nuevo y hay que contarlos como tal.** Los introduce la tabla de estados
de campo del plan, que el Principio VI obliga a declarar, y hasta esta versión no figuraban en ninguna
lista de textos nuevos: se habrían escrito al implementar, sin que nadie los revisara. La regla que
los gobierna sale de `contenido.md` y es una sola: **un placeholder es un ejemplo real del formato,
nunca una instrucción**. Por eso `30.123.456` y no *Ingresá el DNI*, y por eso el voseo de esta feature
aparece en *Seleccioná …* y no en un imperativo dentro del campo. Ninguno reemplaza a la etiqueta, que
FR-066 congela.

**El nombre del sistema sigue siendo *Sistema Integral de Gestión***, no el *Sistema Integral de
Transporte* que dice la imagen de referencia: las imágenes son la fuente del aire y de la jerarquía,
nunca de los textos ni de los valores.

---

## 6. Lo que la skill recibe de vuelta (FR-008)

`.claude/skills/gt-ui/` no es sólo entrada de esta feature: también es salida. Se escribe en ella:

1. Los **cuatro valores recalibrados** en `references/tokens.md`, con su medición y el fondo sobre el
   que se midió, al lado de cada uno.
2. La **degradación de `dim` a token no textual** y la regla de que `faint` es el único tono atenuado
   de texto (research §1).
3. La regla de que **sobre el lienzo y sobre `surface-mute` sólo va texto en `ink` o `ink-soft`**.
4. `references/choferes- padron.png` en la lista de referencias de `SKILL.md`, que hoy no figura —es
   la imagen que fija cómo se entra a una fila y dónde viven las acciones secundarias, y sin
   registrarla la próxima feature no la encuentra.
