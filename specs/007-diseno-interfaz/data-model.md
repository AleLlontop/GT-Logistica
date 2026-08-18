# Modelo del sistema de diseño — Módulo 7

**Fecha**: 2026-08-17 · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

Esta feature no toca datos: no hay entidades, ni tablas, ni migraciones. Lo que sí tiene un modelo
—valores fijos, relaciones y estados— es **el sistema de diseño**, y es lo que este documento define,
en el mismo lugar donde los otros módulos definen sus entidades.

Tres partes: los **tokens** (los valores permitidos), las **primitivas** (las piezas que los usan) y
el **vocabulario de estados** (cómo los cuatro juegos de estados del sistema se dicen con una sola
pieza).

---

## 1. Tokens

Son los únicos valores que puede usar cualquier pantalla. FR-008 lo dice al revés: nada fuera de esta
lista.

Se declaran en el bloque `@theme` de `index.css`. Los nombres **respetan los espacios de nombre de
Tailwind v4** —`--color-*`, `--font-*`, `--radius-*`, `--shadow-*`— porque es lo que hace que cada
token genere su utilidad: `--color-superficie` produce `bg-superficie`, `text-superficie` y
`border-superficie`. Un nombre fuera de esos espacios queda como variable CSS suelta y no genera
nada, que es el error fácil de cometer acá.

### 1.1 Color

Todos los pares de la paleta fueron **medidos**, no estimados. La columna de contraste indica el peor
caso sobre los tres fondos del sistema.

**Fondos y superficies**

| Token | Valor | Para qué |
|---|---|---|
| `--color-pagina` | `#f4f6f9` | Fondo de la aplicación |
| `--color-superficie` | `#ffffff` | Tarjetas, tablas, diálogos, formularios |
| `--color-superficie-alterna` | `#f7f9fb` | Fila alterna de tabla |
| `--color-superficie-hundida` | `#e9edf3` | Encabezado de tabla, bloque de filtros |

**Texto**

| Token | Valor | Contraste (peor caso) | Para qué |
|---|---|---|---|
| `--color-texto` | `#141821` | 15,12:1 | Texto principal |
| `--color-texto-suave` | `#4e5666` | 6,28:1 | Rótulos, texto secundario |
| `--color-texto-tenue` | `#5e6779` | 4,84:1 | **Lo atenuado**: filas anuladas, registros dados de baja |

`--color-texto-tenue` está calibrado en el límite justo: es el color más claro que sigue cumpliendo
4,5:1 **sobre los tres fondos**, incluida la fila alterna. Es lo que permite que una fila anulada se
vea atenuada sin volverse ilegible (FR-021).

**Acento**

| Token | Valor | Contraste | Para qué |
|---|---|---|---|
| `--color-acento` | `#12507b` | 7,27:1 · 8,54:1 con blanco encima | Acción principal, enlaces, foco, opción activa |
| `--color-acento-oscuro` | `#0d3b5c` | 11,68:1 con blanco encima | Estado presionado y apuntado |
| `--color-acento-fondo` | `#e6eef6` | 7,29:1 con el acento encima | Fondo de la sección activa del menú |

Es el azul que ya usaba la pantalla de ingreso, conservado a propósito: es el único rasgo visual del
sistema actual que no molesta, y sirve de punto de continuidad.

**Semánticos**

| Token | Texto | Fondo | Contraste texto/fondo | Para qué |
|---|---|---|---|---|
| Éxito | `#1b6b41` | `#e6f2ea` | 5,66:1 | Confirmaciones, estado en regla, pagada |
| Advertencia | `#7d5300` | `#fbf0da` | 5,98:1 | Próxima a vencer, avisos que no bloquean |
| Error | `#a4262c` | `#fbeeef` | 6,42:1 | Rechazos, vencida, campo con error |

El rojo es el que ya existía. Los otros dos se eligieron oscuros a propósito: un verde y un ámbar
claros no llegan a 4,5:1 y obligarían a comunicar el estado sólo por color, que es justo lo que
FR-040 prohíbe.

**Bordes**

| Token | Valor | Contraste | Para qué |
|---|---|---|---|
| `--color-borde` | `#c6cdd8` | — | Separadores que no comunican: líneas de tabla, divisiones |
| `--color-borde-fuerte` | `#7b8698` | 3,13:1 | Lo que **sí** comunica: borde de campo, contorno de control |

La distinción es la que exige FR-038. Un separador de tabla es decorativo porque la información —que
son filas distintas— la lleva además la alternancia de fondo; el borde de un campo de formulario
comunica dónde se escribe, y por eso está calibrado a 3:1 sobre los tres fondos.

### 1.2 Tipografía

| Token | Valor |
|---|---|
| `--font-sans` | La familia elegida en la etapa 1, variable, con la pila del sistema declarada detrás |

**La escala de tamaños no se inventa**: se adopta la de Tailwind, que ya está afinada y ya trae su
interlineado apareado. Definir una propia sería trabajo para llegar al mismo lugar.

**Regla firme**: todo número que se compara en vertical —importes, cantidades, fechas en columna— usa
cifras tabulares. Se declara una vez en las reglas base y **no depende de qué familia se elija**
(research §4).

### 1.3 Espaciado, radio, profundidad y foco

| Token | Valor |
|---|---|
| Espaciado | Se adopta la escala de Tailwind sobre su base de 4 px, sin redefinirla |
| `--radius-chico` / `--radius-medio` / `--radius-grande` | Campos y botones / tarjetas / diálogos |
| `--shadow-tarjeta` / `--shadow-dialogo` | Elevación de tarjeta / de diálogo |
| Foco | Contorno de 3 px en `--color-acento` con 2 px de separación, en las reglas base |

El anillo de foco es el que ya existe hoy y ya cumple: se conserva y se extiende a todo elemento
interactivo, incluidos los que están dentro de tablas y diálogos (FR-039).

### 1.4 Anchos

| Token | Valor | Para qué |
|---|---|---|
| `--container-lectura` | Ancho máximo del contenido | FR-017: en un monitor ancho el texto no se estira |
| — | 1280 px | FR-042: por debajo no se garantiza nada. Es un piso de verificación, no un token |

---

## 2. Primitivas

Catorce piezas en `frontend/src/compartido/ui/`, escritas siguiendo el patrón de shadcn/ui: **el
código es del repositorio**, con sus variantes declaradas mediante `class-variance-authority` y sus
clases compuestas con `clsx` + `tailwind-merge`. No hay hojas de estilos: cada primitiva lleva sus
utilidades adentro. **Ninguna pantalla define color, tamaño ni separación por su cuenta.**

| Primitiva | Variantes / estados | Qué reemplaza |
|---|---|---|
| `Boton` | primario · secundario · de texto · destructivo × normal, apuntado, presionado, deshabilitado, con foco | La regla global que pinta todos los botones de azul |
| `Campo` | texto · número · fecha · selector · área · casilla · archivo × normal, obligatorio, con error, deshabilitado, con foco. **Envuelve controles nativos: no los reemplaza** | `campo`, `campo__error`, `con-error`, `campo__vacio`, `campo-checkbox` |
| `Dialogo` | contenedor sobre Radix: superficie, fondo, foco retenido, portal, `Escape` | Los cuatro `role="dialog"` propios |
| `DialogoConfirmacion` | usa `Dialogo`; título, mensaje, dos acciones | Se muda desde `modules/usuarios`; los cinco envoltorios siguen funcionando |
| `Estado` | los cuatro juegos de la sección 3 × neutro, éxito, advertencia, error | `estado`, `estado--*` y todos los estados escritos como texto suelto |
| `Aviso` | éxito · advertencia · error · nota | `role="status"` y `role="alert"` sin tratamiento; `formulario__error` |
| `Paginacion` | única | Las cuatro copias |
| `Filtros` | única | `filtros` |
| `EncabezadoDePantalla` | con y sin acción principal; además fija el título de la pestaña | No existe |
| `Listado` | contenedor: encabezado, filtros, tabla, paginación | No existe |
| `Ficha` | contenedor: encabezado con identidad, estado y acciones; secciones | `acciones` al pie |
| `Historial` | única | Las tablas de historial |
| `EstadoVacio` | vacío · sin coincidencias · cargando · error | Los `<p role="status">` sueltos |
| `iconos` | Reexporta de `lucide-react` lo que se usa: decorativos, heredan el color | No existe |

**Regla de crecimiento**: si una pantalla necesita algo que ninguna primitiva da, la respuesta es una
**variante nueva de la primitiva**, no una clase suelta en la pantalla. Es lo único que evita volver
a cuatro paginaciones.

**Regla que sostiene el sistema** (research §2): la variante de `Boton` es un parámetro
**obligatorio y tipado**. Es lo que vuelve imposible repetir el defecto de origen, donde un botón sin
variante heredaba el estilo de acción principal y la celda que abría una ficha se veía como el botón
más importante de la pantalla.

**Regla que protege la suite** (research §3): las primitivas **envuelven** controles nativos —
`<select>`, `<input>`, casillas, `<table>`— y nunca los sustituyen por una implementación propia o de
una biblioteca. De Radix entra sólo el diálogo.

---

## 3. Vocabulario de estados

El sistema tiene cuatro juegos de estados repartidos en cinco módulos. Hoy cada uno se dibuja como
texto suelto. Todos pasan a la misma primitiva `Estado`, que combina **la palabra que ya está en
pantalla** con un color de la paleta y una forma. FR-040: nunca sólo el color.

| Juego | Valores | Tono |
|---|---|---|
| **Documentación** (choferes, flota) | En regla / Vigente · Próxima a vencer · Vencida · Sin documentación | éxito · advertencia · error · neutro |
| **Viaje** | Pendiente · En curso · Rendido · Anulado · Facturado | neutro · acento · éxito · atenuado · éxito |
| **Factura** | Pendiente · Pagada · Vencida · Anulada | neutro · éxito · error · atenuado |
| **Vehículo** | Disponible · En viaje · Fuera de servicio · Parado | éxito · acento · advertencia · advertencia |
| **Alta y baja** (usuarios, clientes, transportistas, tipos) | Activo · Dado de baja | neutro · atenuado |

**Las palabras no se tocan.** Salen de `NombresDeEstado` y de los `TEXTO_ESTADO_*` de cada módulo, y
FR-004 las congela. Lo que esta feature agrega es cómo se ven.

**El tono atenuado** usa `--color-texto-tenue` y se aplica a la fila entera cuando la regla del módulo
lo pide. Es el que estaba escrito en el marcado desde el Módulo 3 y nunca tuvo efecto.

---

## 4. Secciones de navegación

Mapa estático `código de opción` → sección, en `frontend/src/compartido/seccionesDeMenu.ts`. El
servidor sigue decidiendo qué opciones existen; el mapa sólo decide dónde se dibujan (research §6).

| Sección | Códigos |
|---|---|
| **Operación** | `viajes`, `facturas` |
| **Padrones** | `clientes`, `choferes`, `flota`, `transportistas`, `personas` |
| **Seguimiento** | `vencimientos-choferes`, `vencimientos-flota`, `vencimientos-facturas`, `totales`, `totales-facturados` |
| **Configuración** | `tipos-documentacion`, `tipos-vehiculo`, `empresa-emisora` |
| **Administración** | `usuarios` |

**Reglas del mapa**:

1. Sólo se dibujan las opciones que llegaron del servidor
2. Una sección sin ninguna opción autorizada no se dibuja
3. Un código que el mapa no conozca se dibuja igual, en **Administración**, que es la última. Un
   módulo futuro aparece en el menú sin tocar el frontend
4. `vencimientos-choferes` y `vencimientos-flota` **no existen todavía** en el catálogo del servidor:
   los agrega esta feature (research §7)

---

## 5. Lo que este modelo NO define

Por si alguien lo busca acá:

- **Entidades, tablas y migraciones**: no hay. La feature no toca la base
- **Contratos de API**: no cambia ninguno. La única modificación de backend agrega dos elementos a
  una lista que ya se serializa igual
- **Textos**: están congelados por FR-004 y viven donde ya vivían, en los `contracts/README.md` de
  los seis módulos
- **Modo oscuro**: fuera de alcance por decisión de la spec
