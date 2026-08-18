# Implementation Plan: Rediseño de la aplicación (Módulo 7)

**Branch**: `007-diseno-interfaz` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/007-diseno-interfaz/spec.md`

## Summary

Rediseñar la aplicación entera —las 42 pantallas de los seis módulos— sin cambiar qué hace ninguna.
Se arma un sistema de diseño sobre **Tailwind CSS 4**, con tokens propios, una tipografía servida por
la aplicación, los íconos de **Lucide** y unas catorce primitivas escritas siguiendo el patrón de
**shadcn/ui** —código propio del repositorio, con **Radix** debajo para el diálogo—. Se reemplaza la
navegación plana de catorce entradas por una agrupada en cinco secciones, y se aplica el sistema por
**tipo de pantalla** —listados, formularios, fichas— y no por módulo, para que ninguna decisión de
anatomía se tome mirando un solo caso.

El enfoque se apoya en tres hechos del relevamiento (research §0): las cuatro paginaciones son la
misma; los nueve componentes de confirmación comparten base y sólo cuatro tienen diálogo propio; y
los 41 archivos de test consultan por rol, etiqueta y texto, con **sólo tres líneas en todo el
frontend atadas a la estructura del DOM**. Eso es lo que hace viable reestructurar 42 pantallas con
red: congelando los textos (FR-004) la suite existente prueba que el comportamiento no cambió.

Todo el trabajo es de frontend salvo **dos entradas** que se agregan al catálogo de menú del backend,
para que los paneles de vencimientos de choferes y de flota se alcancen desde la navegación (FR-013).

## Technical Context

**Language/Version**: TypeScript ~6.0 · React 19.2 · Tailwind CSS 4.3

**Primary Dependencies**: se incorporan, por decisión explícita de quien conduce el producto, las
bibliotecas necesarias para llegar a un mejor diseño. Todas verificadas como compatibles con React
19.2, Vite 8.2 y Node 24.19:

| Paquete | Versión | Para qué |
|---|---|---|
| `tailwindcss` + `@tailwindcss/vite` | 4.3.3 | Estilos y tokens en un bloque `@theme` |
| `@radix-ui/react-dialog` | 1.1.23 | Diálogo con retención de foco, portal y `aria` |
| `class-variance-authority` | 0.7.1 | Variantes de componente declaradas y tipadas |
| `clsx` + `tailwind-merge` | 2.1.1 / 3.6.0 | Composición de clases sin conflictos |
| `lucide-react` | 1.31.0 | Juego de íconos |
| `@fontsource-variable/…` | 5.3.0 | Tipografía servida por la propia aplicación |

Las primitivas siguen el patrón de **shadcn/ui**, que no es una dependencia: el código del
componente se copia dentro de `compartido/ui/` y el proyecto lo posee. Es lo que permite tomar
prestado el comportamiento accesible sin heredar una estética reconocible (research §2)

**Storage**: N/A — la feature no toca datos, entidades ni base

**Testing**: vitest 4 + Testing Library sobre los **41 archivos existentes**, que se conservan y se
ejecutan después de cada familia de pantallas; más el recorrido manual de los seis quickstarts y del
quickstart propio de esta feature

**Target Platform**: navegador de escritorio actualizado, **1280 px de ancho o más** (FR-042)

**Project Type**: aplicación web. Esta feature es **frontend**, con una adición de dos líneas en
backend (research §7)

**Performance Goals**: sin objetivos nuevos. Restricción: el rediseño no puede volver la aplicación
perceptiblemente más lenta de cargar. Se sostiene porque ninguna de las dependencias calcula estilos
en tiempo de ejecución —Tailwind produce CSS estático—, los íconos se importan de a uno y la fuente
se sirve desde la propia aplicación

**Constraints**: los textos operativos, las 42 direcciones, las operaciones y los permisos quedan
congelados (FR-001 a FR-005). El piso de accesibilidad sube y no baja. Y la restricción que gobierna
el uso de las dependencias: **ninguna biblioteca puede cambiar el árbol accesible de algo que la
suite consulta**. En concreto, los `<select>`, `<input>`, casillas y tablas **siguen siendo
nativos** —se los estila, no se los reemplaza—, porque 17 pantallas usan selectores y 10 archivos de
test hacen 28 llamadas a `selectOptions` sobre ellos (research §3)

**Scale/Scope**: 42 pantallas · 64 componentes · 6 módulos · 20 pantallas con tabla · ~14 primitivas
a construir · 4 paginaciones y 9 componentes de confirmación a consolidar

## Constitution Check

*GATE: pasa antes de Phase 0 y se re-evalúa después de Phase 1.*

| Principio | Evaluación | Cómo se cumple |
|---|---|---|
| **I. Simplicidad Ante Todo** | ⚠️ Desviación decidida y justificada | Se incorporan seis dependencias. El principio admite desviaciones justificadas por escrito, y ésta lo está en *Complexity Tracking*: quien conduce el producto revirtió de forma explícita la decisión anterior de construir todo a mano. La forma en que la desviación se mantiene acotada es el patrón de shadcn/ui —el código vive en el repositorio, no en una API de terceros— y la regla de §3 de research, que impide que una biblioteca reemplace un control nativo |
| **II. Idioma y Mercado Argentino** | ✅ Pasa | Ningún texto existente se reescribe (FR-004). Los rótulos nuevos —las cinco secciones de navegación— van en español rioplatense. Los formatos de moneda y fecha no se tocan: siguen saliendo de `compartido/moneda` y `compartido/fechas` |
| **III. Cero Alcance Fantasma** | ✅ Pasa | La spec autoriza el rediseño de forma explícita en su sección *Encuadre*, con cuatro puntos de qué se rediseña y siete de qué se conserva. Este plan enumera las etapas contra esos puntos. Nada que no esté en la spec entra: si durante la implementación aparece una mejora, se anota para una spec futura |
| **IV. Verificable por una Persona No Técnica** | ⚠️ Una excepción declarada | Los trece criterios de éxito se comprueban operando la aplicación, salvo **SC-008**, que exige medir contraste con una herramienta. Está declarado en la spec y acotado a verificar la paleta, que es fija y chica (research §11) |
| **V. Datos del Usuario con Respeto** | ✅ Pasa | No se pide ni se guarda ningún dato nuevo. La pantalla de inicio arma sus accesos con lo que la sesión ya trae, sin pedirle nada al servidor (FR-015). La fuente se aloja en el repositorio y no se pide a un tercero, así que ningún navegador de operador consulta un servicio externo al abrir el sistema |

**Cómo se acota la desviación del Principio I.** Incorporar seis dependencias es lo contrario de lo
que el principio pide por defecto, así que lo que sigue no es una excusa sino el conjunto de límites
que hace que la decisión no se derrame:

1. **Ninguna biblioteca de componentes cerrada.** El código de las primitivas se copia al repositorio
   y el proyecto lo posee. No hay una API de terceros a la que 42 pantallas queden atadas, y si
   mañana se quiere sacar Radix del diálogo, se saca de un archivo
2. **Ninguna dependencia toca un control nativo** que la suite verifique (research §3). De Radix
   entra sólo el diálogo. Los `<select>`, `<input>`, casillas y tablas se estilan y no se reemplazan
3. **Ninguna dependencia calcula estilos en tiempo de ejecución.** Tailwind produce CSS estático
4. **Todas se declararon con versión y con compatibilidad verificada**, no con un rango optimista

### Re-evaluación después de Phase 1

Sin cambios en el resultado. El diseño de Phase 1 no creó entidades de datos ni introdujo ninguna
decisión adicional que contradiga un principio. Las dos desviaciones que quedan registradas —las
dependencias y la carpeta `frontend/src/compartido/ui/`— están en *Complexity Tracking* con su
alternativa rechazada y el motivo.

## Project Structure

### Documentation (this feature)

```text
specs/007-diseno-interfaz/
├── plan.md              # Este archivo
├── research.md          # Phase 0 — once decisiones técnicas
├── data-model.md        # Phase 1 — el modelo del sistema de diseño (tokens, primitivas, estados)
├── quickstart.md        # Phase 1 — recorrido de validación
├── contracts/
│   └── README.md        # Phase 1 — contrato de interfaz: API de las primitivas, mapa de secciones,
│                        #           inventario de lo congelado
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec (15 de 16)
└── tasks.md             # Phase 2 — lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

```text
frontend/
├── index.html                        # idioma del documento, título base, ícono propio
├── public/
│   └── favicon.svg                   # reemplaza el genérico de la herramienta
└── src/
    ├── index.css                     # @import de Tailwind, bloque @theme con los tokens,
    │                                 # import de la fuente y las pocas reglas base
    ├── compartido/
    │   ├── ui/                       # NUEVO — el sistema de diseño; código propio del repositorio
    │   │   ├── Boton.tsx                     # variantes con cva
    │   │   ├── Campo.tsx                     # envuelve controles NATIVOS
    │   │   ├── Dialogo.tsx                   # sobre @radix-ui/react-dialog
    │   │   ├── DialogoConfirmacion.tsx       # se muda desde modules/usuarios, misma firma
    │   │   ├── Estado.tsx
    │   │   ├── Aviso.tsx
    │   │   ├── Paginacion.tsx                # reemplaza las cuatro
    │   │   ├── Filtros.tsx
    │   │   ├── EncabezadoDePantalla.tsx      # además fija el título de la pestaña
    │   │   ├── Listado.tsx
    │   │   ├── Ficha.tsx
    │   │   ├── Historial.tsx
    │   │   ├── EstadoVacio.tsx
    │   │   ├── iconos.ts                     # reexporta lo que se usa de lucide-react
    │   │   └── cn.ts                         # clsx + tailwind-merge
    │   ├── Layout.tsx                # encabezado del sistema, se rediseña
    │   ├── Menu.tsx                  # pasa a navegación agrupada
    │   ├── seccionesDeMenu.ts        # NUEVO — mapa estático `código` → sección
    │   └── (fechas, moneda, clienteHttp, tipos: sin cambios)
    └── modules/<los seis>/           # se aplican las primitivas; ninguna hoja de estilos propia

backend/src/GT.Application/Autenticacion/
└── CatalogoOpcionesMenu.cs           # + 2 entradas: vencimientos de choferes y de flota
```

**Structure Decision**: el trabajo vive en `frontend/src/compartido/ui/`, una carpeta nueva dentro de
`compartido/`, que ya existe y ya está fuera del esquema por módulo de negocio —hoy contiene
`Layout`, `Menu`, `clienteHttp`, `fechas`, `moneda` y `tipos`—. Se agrupa en `ui/` porque son unas
catorce primitivas, y dejarlas sueltas volvería `compartido/` inservible para encontrar nada. La
regla de la constitución que fija la estructura habla de los **módulos de negocio**, que siguen
exactamente donde estaban: ninguna pantalla se muda, ninguna carpeta de módulo cambia. Queda anotado
como paso de mantenimiento para `AGENTS.md`.

**Sobre las hojas de estilos**: desaparecen. Con Tailwind, el único archivo CSS del proyecto es
`index.css` —el `@theme` con los tokens y unas pocas reglas base—, y los estilos de cada primitiva
viven en su propio `.tsx`. Esto elimina de raíz el defecto de origen: hoy hay 23 clases escritas en
el marcado que no existen en ninguna hoja, y con utilidades no hay nombre que definir en otro
archivo.

## Fases de implementación

Las siete etapas de research §12 —que son las siete historias de la spec en orden— más una de cierre.
Cada una deja el sistema en un estado coherente y verificable.

### Etapa 1 — Dependencias y sistema de diseño (US1)

Instalar las seis dependencias y enchufar el complemento de Tailwind a Vite. Declarar en `index.css`
el bloque `@theme` con los tokens: paleta, escala tipográfica, escala de espaciado, radios,
profundidad y foco. Elegir la familia tipográfica mirando pantallas reales contra los cuatro
criterios de research §4, e instalarla. Escribir las catorce primitivas y el reexport de íconos.

**Nada visible cambia todavía**: al terminar la etapa el sistema se ve igual, porque ninguna pantalla
usa aún las piezas.

Cierra borrando lo que causó el defecto de origen: se **elimina** `index.css` entero —sus 136 líneas
y, con ellas, la regla global que pinta todos los `button`— y se verifica que la suite sigue en verde
antes de empezar a aplicar nada.

### Etapa 2 — Estructura y navegación (US2)

`Layout` rediseñado, con la jerarquía que FR-016 pide. `Menu` pasa a agrupar por las cinco secciones
del mapa estático, mostrando sólo las que quedaron con opciones. Las dos entradas nuevas en el
catálogo del backend. `EncabezadoDePantalla` aplicado a las 42 pantallas, que es lo que además fija
el título de la pestaña. La pantalla de inicio deja de ser un saludo. El idioma del documento y el
ícono propio.

**Al terminar esta etapa el sistema entero ya cambió de aspecto**, porque el marco lo comparten las
42 pantallas.

### Etapa 3 — Listados (US3)

Las 20 pantallas con tabla adoptan la misma anatomía: encabezado con la acción principal, filtros
como bloque resuelto, tabla legible, estados de vacío/sin coincidencias/cargando diferenciados, y la
paginación única en reemplazo de las cuatro. Es la etapa más grande.

### Etapa 4 — Formularios (US4)

Agrupación de campos, obligatorios señalados, errores donde se los busca, acciones siempre en el
mismo lugar con la primaria distinguida, y el ancho de cada campo acorde a su dato.

### Etapa 5 — Fichas (US5)

Encabezado que identifica, muestra estado y reúne las acciones —el único movimiento real de
estructura de toda la feature, porque hoy las acciones están al pie—, secciones navegables e
historial como secuencia en el tiempo.

### Etapa 6 — Diálogos, avisos y estados (US6)

El contenedor de diálogo sobre Radix; los cuatro diálogos con campos adentro reenganchados a él; los
cinco envoltorios de texto que ya delegan, apuntados a la primitiva mudada. El indicador de estado
único para documentación, viajes, facturas y vehículos. Avisos y rechazos diferenciados sin depender
del color.

**Es el único punto de la feature donde una dependencia toca algo que la suite verifica**, porque
varios de los 41 archivos abren diálogos. Se hace al principio de la etapa y se corre la suite
inmediatamente. Si el entorno de test diera problemas con Radix, el repliegue está escrito en
research §8: conservar el diálogo actual y agregarle la retención de foco a mano, unas veinte líneas
que no afectan ninguna otra decisión.

### Etapa 7 — Densidad, foco y anchos (US7)

Revisión de densidad, foco visible en todo elemento interactivo, comportamiento a 1280 px y al 200 %
de zoom, desplazamiento contenido en las tablas que no entren, y respeto por la preferencia de
movimiento reducido.

### Etapa 8 — Validación y cierre

Ejecutar la suite completa de frontend y backend. Recorrer los seis quickstarts para SC-001. Recorrer
el quickstart de esta feature. Medir contraste sobre la paleta. Completar el alta de factura con
teclado. Y el paso de mantenimiento de siempre:

> **Actualizar `AGENTS.md`** con las decisiones de diseño y convenciones nuevas de esta feature, una
> línea por decisión, con referencia `[007]`. Confirmar contra lo realmente implementado las
> candidatas que este plan anota abajo, y **descartar las que no resulten transversales**: no se
> agregan entradas por completar la lista.

**Candidatas a decisión transversal** (a confirmar al cierre, no antes):

- Una regla de estilo global **nunca selecciona un elemento interactivo a secas**: la variante se
  declara, no se hereda. Es el origen del defecto que hacía que la celda que abre una ficha se viera
  como el botón principal de la pantalla, y lo que lo vuelve imposible de repetir es que la variante
  sea un parámetro obligatorio y tipado del componente
- Cuando se incorpora una biblioteca de interfaz, el límite es el **árbol accesible**: se toma lo que
  agrega comportamiento —foco retenido, portal— y **no** lo que reemplaza un control nativo que los
  tests operan. Un `<select>` cambiado por una lista dibujada rompe veintiocho pruebas y no mejora
  nada que el usuario note
- Cuando el servidor decide **qué** se muestra, el frontend puede decidir **dónde**: agrupar no es
  autorizar. Lo que lo mantiene sano es que un código desconocido siga apareciendo, para que un
  módulo nuevo no necesite tocar la pantalla
- Un rediseño que congela los textos convierte a la **suite de tests existente en la prueba de que no
  cambió el comportamiento**, porque las consultas van por rol, etiqueta y texto. Lo que rompe un
  test no es mover el marcado: es renombrar
- Un componente compartido que **sólo acepta texto** obliga a que el primer caso con un campo adentro
  se escriba aparte, y a partir de ahí se multiplican las copias. Separar el **contenedor** del
  **contenido** desde el principio es lo que evita llegar a cuatro variantes del mismo diálogo

## Complexity Tracking

> Se completa sólo ante violaciones de la constitución que haya que justificar.

| Violación | Por qué hace falta | Alternativa más simple, y por qué se rechazó |
|---|---|---|
| **Seis dependencias nuevas** (Principio I) | **Decisión explícita de quien conduce el producto**, tomada después de que este plan propusiera lo contrario: se incorporan las bibliotecas necesarias —estilos, componentes e íconos— para llegar a un mejor diseño. El objetivo declarado de la feature es dejar de verse genérico, y construir a mano un sistema de diseño, un juego de íconos y una retención de foco es más código propio, peor resuelto y más lento | Construir todo a mano con CSS plano, doce SVG dibujados y el foco resuelto a mano, que es lo que este documento proponía en su versión anterior. Se rechaza por decisión de producto, y quedan en su lugar cuatro límites que acotan la desviación (arriba, tras el Constitution Check) |
| **Una biblioteca de componentes cerrada** — *no* se adopta | — | MUI, Chakra o Mantine resolverían más rápido y traerían una estética reconocible a primera vista, que es exactamente lo que la spec llama genérico. Además obligarían a adaptar 42 pantallas a su API y chocarían con el marcado accesible ya resuelto. Por eso se toma el patrón de shadcn/ui, que deja el código adentro del repositorio |
| **Reemplazar controles nativos** — *no* se hace | — | Un selector de biblioteca se ve mejor, pero dibuja una lista propia en lugar de un `<select>`, y **10 archivos de test hacen 28 llamadas a `selectOptions`** sobre los selectores de 17 pantallas. Se estilan los nativos: además ya funcionan con teclado, con lector de pantalla y al 200 % de zoom |
| Carpeta nueva `compartido/ui/` (Estructura del Repositorio) | Catorce primitivas con su hoja al lado dejarían `compartido/` inservible para encontrar nada | Dejarlas sueltas en `compartido/`, que hoy tiene seis archivos y pasaría a treinta. La regla de la constitución rige los módulos de negocio, que no se tocan; queda anotado en `AGENTS.md` |
| Dos entradas nuevas en el catálogo de menú del backend (Principio III) | FR-013 exige que los dos paneles de vencimientos se alcancen desde la navegación, y el mapeo permiso → pantalla vive en el backend por decisión del Módulo 1 | Derivarlas en el frontend a partir de las opciones de choferes y flota. Se rechaza porque duplica en TypeScript el mapeo de permisos que la investigación del Módulo 1 decidió centralizar en el servidor |
