# Research: Rediseño de la aplicación (Módulo 7)

**Fecha**: 2026-08-17 · **Spec**: [spec.md](./spec.md)

Trece decisiones técnicas, cada una con lo que se eligió, por qué, y qué se descartó. Las primeras
salen del relevamiento del frontend existente y son las que definen cuánto trabajo es realmente el
rediseño.

> **Sobre las dependencias.** Una primera versión de este documento decidía construir todo a mano,
> sin agregar ninguna biblioteca. Quien conduce el producto lo revirtió de forma explícita: se
> incorporan las dependencias que hagan falta —estilos, componentes e íconos— para llegar a un mejor
> diseño. Lo que sigue está escrito sobre esa decisión, con una salvedad que **no** es negociable y
> que la sección §3 desarrolla: ninguna dependencia puede reemplazar un control cuyo comportamiento
> la suite de tests existente verifica.

---

## §0. Lo que el relevamiento cambió respecto de la spec

La spec describe el estado actual desde afuera —nueve componentes de confirmación, cuatro
paginaciones—. Mirado por dentro, el punto de partida es mejor de lo que parece, y eso reduce el
trabajo:

- **Los nueve componentes de confirmación no son nueve diálogos.** Cinco —choferes, las dos de flota,
  baja de cliente y rendición de viaje— son envoltorios de treinta líneas que sólo aportan textos y
  delegan en `DialogoConfirmacion` del Módulo 2, que ya maneja foco al abrir, `Escape` y devolución
  del foco al cerrar. **Cuatro sí tienen su propio `role="dialog"`**: las dos de facturación,
  `RegistrarCobro` y la anulación de viaje, porque llevan campos adentro y el diálogo compartido sólo
  acepta título y mensaje. El trabajo real no es unificar nueve: es **separar el contenedor del
  contenido**
- **Las cuatro paginaciones son la misma de 41 a 49 líneas**, con diferencias de comentario y del
  nombre plural que reciben por parámetro
- **Las fichas ya están estructuradas**: usan `<section aria-labelledby>` con su `<h2>` y `<dl>`. La
  `FichaFactura` tiene siete secciones bien delimitadas. El único movimiento de estructura real es
  subir las acciones del pie al encabezado (FR-031)
- **Los 41 archivos de test consultan por rol, etiqueta y texto**: 138 `getByLabelText`, 109
  `getByRole`, 108 `findByText`, 92 `getByText`. **Sólo tres líneas en todo el frontend dependen de
  la estructura del DOM**. Esto es lo que vuelve viable reestructurar 42 pantallas: la red de
  seguridad está atada a los textos y a los roles, no al armado, que es lo que FR-004 congela

---

## §1. Estilos: Tailwind CSS v4

**Decisión**: **Tailwind CSS 4.3.3** con su complemento de Vite (`@tailwindcss/vite`). Los tokens del
sistema se declaran una sola vez en un bloque `@theme` de `index.css`, desde donde Tailwind genera a
la vez las utilidades y las variables CSS correspondientes.

**Compatibilidad verificada**: `@tailwindcss/vite@4.3.3` declara `vite: ^5.2.0 || ^6 || ^7 || ^8`; el
proyecto usa Vite 8.2.0 y Node 24.19, dentro del `>=24 <25` que fija `package.json`.

**Por qué**:

- **Los valores no pueden derivar.** El problema estructural del frontend actual es que cada pantalla
  decide sus valores. Con un `@theme` como única fuente, usar un valor fuera del sistema requiere
  escribir un valor arbitrario entre corchetes, que **salta a la vista en una revisión**. En CSS
  plano, un color inventado es indistinguible de uno del sistema
- **Desaparece por construcción el defecto de las 23 clases muertas.** Hoy `campo__error` se escribe
  en 65 lugares y no existe en ninguna hoja. Con utilidades no hay nombre que definir en otro
  archivo: lo que se escribe es lo que se aplica
- **Tailwind v4 no lleva archivo de configuración en JavaScript.** El sistema de diseño se declara en
  CSS, que es donde corresponde, y queda legible para alguien que no programa

**Alternativas descartadas**:

- **CSS plano con propiedades personalizadas** (lo que decidía la versión anterior de este
  documento): más simple, pero no impide que los valores deriven ni resuelve la separación entre
  dónde se escribe una clase y dónde se define
- **CSS Modules**: da alcance por archivo, que es la propiedad opuesta a la que se busca
- **CSS-in-JS** (emotion, styled-components): costo en tiempo de ejecución y fricción creciente con
  las versiones nuevas de React

---

## §2. Componentes: el patrón de shadcn/ui sobre Radix

**Decisión**: las primitivas se escriben siguiendo **shadcn/ui**, que no es una dependencia sino un
patrón: **el código del componente se copia dentro de `frontend/src/compartido/ui/` y el proyecto lo
posee**. Debajo, para el comportamiento que no conviene escribir a mano, se usan **primitivas de
Radix**. La composición de clases se resuelve con el trío habitual de ese patrón:
`class-variance-authority`, `clsx` y `tailwind-merge`.

**Dependencias que entran**:

| Paquete | Versión | Para qué |
|---|---|---|
| `tailwindcss` + `@tailwindcss/vite` | 4.3.3 | Estilos y tokens |
| `@radix-ui/react-dialog` | 1.1.23 | Diálogo con retención de foco, portal, bloqueo de fondo y cableado de `aria` |
| `class-variance-authority` | 0.7.1 | Variantes de componente declaradas y con tipos |
| `clsx` + `tailwind-merge` | 2.1.1 / 3.6.0 | Composición de clases sin conflictos |
| `lucide-react` | 1.31.0 | Íconos (§5) |
| `@fontsource-variable/…` | 5.3.0 | Tipografía (§4) |

Radix declara `react: ^16.8 || ^17.0 || ^18.0 || ^19.0` y Lucide `^16.5.1 … || ^19.0.0`: los dos
soportan el React 19.2 del proyecto.

**Por qué el patrón de copiar y no una biblioteca de componentes cerrada**: una biblioteca con
estética propia —Material, Chakra, Mantine— resuelve rápido y trae consigo un aspecto reconocible a
primera vista, que es exactamente lo que la spec llama *genérico*. Con el código adentro del
repositorio, lo que se toma prestado es **el comportamiento accesible**, y la identidad visual la
pone el sistema de tokens propio. Además evita tener que adaptar 42 pantallas a la API de un tercero.

**Por qué Radix debajo**: la retención de foco de un diálogo, el portal y el bloqueo del fondo son
código que sale mal escrito a mano y que ya estaba pendiente —el diálogo actual enfoca al abrir pero
no retiene el foco, que es lo que FR-036 exige—.

**La regla que sobrevive**: `class-variance-authority` obliga a **declarar la variante** de cada
botón. Ese fue el defecto de origen —un `button` sin variante heredaba el estilo de acción principal,
y por eso la celda que abre una ficha se veía como el botón más importante de la pantalla—, y con
variantes tipadas no se puede repetir.

---

## §3. Lo que deliberadamente NO se toma de ninguna biblioteca

Es la salvedad que condiciona todo lo anterior, y sale de medir la suite existente.

**Los controles de formulario nativos se conservan.** Se los estila; no se los reemplaza.

| Control | Estado actual | Por qué no se reemplaza |
|---|---|---|
| `<select>` | En **17 pantallas** | **10 archivos de test hacen 28 llamadas a `selectOptions`.** Un selector de Radix o de cualquier biblioteca dibuja una lista propia en lugar de un `<select>`, y las 28 dejan de funcionar |
| `<input>` de texto, número y fecha | En todos los formularios | 138 `getByLabelText` dependen de la asociación etiqueta–control que ya existe |
| `<input type="checkbox">` | Selección de viajes, filtros | Ídem |
| `<table>` | 20 pantallas | 109 `getByRole` y una consulta explícita por `<tr>` |

**Regla general**: ninguna dependencia puede cambiar el árbol accesible de algo que la suite
consulta. Si una biblioteca ofrece un componente que mejora la apariencia pero altera ese árbol, se
toma su **estilo** y no su **implementación**.

**Consecuencia práctica**: de Radix entra sólo el diálogo. Los selectores, campos, casillas y tablas
son nativos con clases de Tailwind encima. Es además lo más barato: un `<select>` nativo ya se abre
con teclado, ya funciona con lector de pantalla y ya se comporta bien en 200 % de zoom.

---

## §4. Tipografía

**Decisión**: una familia variable instalada como paquete —la línea `@fontsource-variable/…` en su
versión 5.3.0—, con la pila del sistema declarada como respaldo. Los archivos quedan servidos por la
propia aplicación: **no se le pide nada a ningún servicio externo**, lo que además respeta el
Principio V y permite que el sistema corra en una red sin salida.

**Criterios para elegir la familia**, a resolver mirando pantallas reales durante la etapa 1:

1. Variable, con licencia libre
2. **Cifras tabulares**, que es lo que sostiene la alineación de importes de FR-020
3. Legible a 13–14 px, que es el tamaño de las 20 tablas
4. Con carácter propio: si se ve como la fuente por defecto del sistema, no cumple el objetivo

**Candidatas verificadas como disponibles**: `@fontsource-variable/geist`,
`@fontsource-variable/public-sans`, `@fontsource-variable/plus-jakarta-sans` y
`@fontsource-variable/inter`. La última se anota con una reserva: es tan usada que hoy **es** el
aspecto por defecto de una aplicación web, que es justamente lo que se quiere evitar.

**Elegida (T003): `@fontsource-variable/geist` 5.3.0.** Cumple los cuatro criterios y gana en el
tercero, que es el que más pesa acá: está dibujada para interfaces densas y aguanta los 13–14 px de
las quince tablas sin que los dígitos se confundan entre sí. Trae cifras tabulares propias. Se
descartó *Public Sans* por neutra —resuelve la legibilidad pero no el cuarto criterio—, *Plus Jakarta
Sans* porque su carácter geométrico se paga en legibilidad a tamaño chico, que es exactamente donde
vive este sistema, e *Inter* por la reserva del párrafo anterior.

**Independencia**: las cifras tabulares se declaran en la hoja de estilos y funcionan también con la
pila de respaldo. **La alineación de los importes no queda atada a qué familia se elija.**

---

## §5. Iconografía: lucide-react

**Decisión**: `lucide-react` 1.31.0. Se importa sólo lo que se usa —el paquete permite descartar el
resto en el empaquetado—, con una docena larga de íconos entre secciones de navegación, estados y
acciones recurrentes.

**Por qué**: escribir doce SVG a mano era defendible cuando la regla era no agregar dependencias;
levantada esa regla, un juego dibujado por diseñadores, a la misma grilla y con el mismo grosor de
trazo, se ve mejor que doce formas hechas a mano, y cubre sin fricción los íconos que aparezcan
después.

**Reglas que los acompañan, y que no cambian**:

1. **Ningún ícono comunica solo.** Siempre acompaña a una palabra (FR-040)
2. Van marcados como decorativos, para que un lector de pantalla no lea dos veces lo mismo (FR-005)
3. Heredan el color del texto: no se les asigna color propio fuera de la paleta

---

## §6. Cómo se agrupa el menú sin romper la autoridad del servidor

**Decisión**: el frontend tiene un **mapa estático de `código` a sección**; el servidor sigue
decidiendo qué opciones existen. La navegación recorre las opciones recibidas, las ubica en su
sección y dibuja sólo las secciones que quedaron con al menos una opción. Un código desconocido cae
en la última sección.

Cinco secciones, en este orden:

| Sección | Códigos de opción |
|---|---|
| **Operación** | `viajes`, `facturas` |
| **Padrones** | `clientes`, `choferes`, `flota`, `transportistas`, `personas` |
| **Seguimiento** | `vencimientos-choferes`, `vencimientos-flota`, `vencimientos-facturas`, `totales`, `totales-facturados` |
| **Configuración** | `tipos-documentacion`, `tipos-vehiculo`, `empresa-emisora` |
| **Administración** | `usuarios` |

**Por qué no rompe la regla del Módulo 2**: esa regla —*"el frontend dibuja lo que recibe y no tiene
lógica propia de permisos"*— protege que el frontend no decida **si** una opción existe. Decidir
**dónde** se dibuja una que el servidor ya autorizó es presentación. El mapa no menciona permisos y
no puede hacer aparecer nada. Y como un código desconocido cae en la última sección, un módulo futuro
aparece en el menú **sin tocar el frontend**.

**Alternativa descartada**: que el servidor mande la sección en cada opción. Cambia el contrato de la
sesión del Módulo 1 para resolver un problema de presentación.

---

## §7. Las dos pantallas que no están en el menú

**Decisión**: se agregan **dos entradas al catálogo del servidor** —`vencimientos-choferes` y
`vencimientos-flota`—, atadas a `choferes.gestionar` y `flota.gestionar`, que son los permisos que
esas pantallas ya exigen.

**Por qué en el backend y no en el frontend**: derivar en TypeScript que *"si ves Choferes, también
ves sus vencimientos"* duplica el mapeo permiso → pantalla que ya vive en `CatalogoOpcionesMenu`, que
es lo que la investigación del Módulo 1 decidió no hacer.

**Por qué no contradice FR-002**: no cambia quién puede hacer qué. Las dos pantallas ya son
alcanzables por dirección para quien tiene el permiso, y ya la rechazan para quien no. Lo que cambia
es que ahora se **encuentran**. Es el único cambio de backend de la feature.

---

## §8. El diálogo

**Decisión**: un contenedor `Dialogo` construido sobre `@radix-ui/react-dialog`, y un
`DialogoConfirmacion` que lo usa para el caso título + mensaje + dos acciones. Los cuatro diálogos con
campos adentro pasan a usar el contenedor. `DialogoConfirmacion` se muda de `modules/usuarios` a
`compartido/ui/` **conservando su firma**, incluida `etiquetaConfirmar`, así que los cinco
envoltorios que ya delegan en él sólo cambian de dónde lo importan.

**Qué aporta Radix sobre lo que hay**: retención de foco —la tabulación cicla dentro del diálogo—,
portal, bloqueo del fondo y cableado de `aria`. Lo que ya funciona hoy —recibir el foco al abrir,
cerrar con `Escape`, devolver el foco al origen— Radix lo cubre con el mismo comportamiento.

**Riesgo anotado**: varios de los 41 archivos de test abren diálogos. Radix Dialog se usa
habitualmente con este entorno de test, pero **la migración del diálogo es el único punto de la
feature donde una dependencia toca algo que la suite verifica**, así que se hace temprano en la etapa
6 y se corre la suite inmediatamente. Si apareciera un problema de entorno, el repliegue es
conservar el diálogo actual y agregarle la retención de foco a mano: son unas veinte líneas y no
afecta a ninguna otra decisión.

---

## §9. El título de la pestaña

**Decisión**: lo fija el componente de encabezado de pantalla. Como FR-016 obliga a que **toda**
pantalla lleve ese encabezado, poner ahí el título del documento garantiza que las 42 lo tengan y que
nadie se olvide en la pantalla 43. Formato: `{título de la pantalla} · {nombre del sistema}`.

**Alternativa descartada**: declararlo en la tabla de rutas; se desincroniza a la primera pantalla
que cambie de nombre.

El idioma del documento y el ícono se declaran una vez en el HTML de arranque.

---

## §10. Cómo se prueba que el rediseño no cambió el comportamiento

**Decisión**: no se agrega infraestructura de test. La verificación se apoya en tres cosas que ya
existen:

1. **Los 41 archivos de test**, que se ejecutan después de cada familia de pantallas rediseñada. La
   regla operativa es: **nunca se quita ni se renombra un nombre accesible, una etiqueta, un rol ni
   un texto visible**; todo lo demás se reordena con libertad
2. **Los seis quickstarts**, que se recorren enteros al final. Es lo que mide SC-001, y es lo que en
   los Módulos 2, 3, 4 y 6 encontró lo que los tests no veían
3. **Un recorrido con teclado** del alta de factura, el flujo más largo del sistema (SC-009)

**Alternativas descartadas**: test de regresión visual con capturas —dependencia pesada y capturas
versionadas, para 42 pantallas que se rediseñan una sola vez— y test automático de accesibilidad,
que aportaría poco sobre un marcado que ya está resuelto y no mide contraste ni jerarquía, que es lo
que esta feature agrega.

---

## §11. Cómo se verifica el contraste

**Decisión**: se mide con el verificador de contraste de las herramientas de desarrollo del navegador
sobre las pantallas reales, recorriendo la lista de pares color/fondo del bloque `@theme` —que son
pocos y fijos— en lugar de pantalla por pantalla.

**Por qué**: como ningún elemento puede usar un color fuera de la paleta (FR-008), verificar la
paleta verifica el sistema. Es la única parte de la feature que no se comprueba mirando, y queda
declarada como excepción al Principio IV en la spec.

---

## §12. En qué orden se rediseñan 42 pantallas

**Decisión**: por tipo de pantalla, no por módulo.

1. **Dependencias, tokens y primitivas** — nada visible todavía
2. **Estructura**: navegación agrupada, encabezado de pantalla, pantalla de inicio, ingreso. Al
   terminar, **el sistema entero ya cambió de aspecto**, porque el marco lo comparten las 42
3. **Listados** (20 pantallas), que es donde se pasa el día
4. **Formularios**
5. **Fichas**
6. **Diálogos, avisos y estados** — incluida la migración a Radix, con la suite corrida enseguida
7. **Densidad, foco, anchos y recorrido final**

**Por qué no por módulo**: rediseñar facturación entera antes de tocar viajes deja el sistema partido
al medio durante la mayor parte del trabajo y obliga a decidir la anatomía de un listado mirando un
solo caso. Por tipo, cada decisión se toma una vez con los veinte casos a la vista, y después de la
etapa 2 no hay ningún momento en que el producto se vea a mitad de camino entre dos diseños.

**Consecuencia**: las siete historias de la spec son estas siete etapas, en orden.
