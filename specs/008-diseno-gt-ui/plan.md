# Implementation Plan: Adopción del sistema de diseño gt-ui (Módulo 8)

**Branch**: `008-diseno-gt-ui` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008-diseno-gt-ui/spec.md`

## Summary

Vestir las 42 pantallas con la identidad de `.claude/skills/gt-ui/` sin cambiar qué hace ninguna. El
Módulo 7 dejó el trabajo estructural hecho —un vocabulario de primitivas, un menú agrupado, un
encabezado común— pero eligió sus colores y su tipografía sobre la marcha; esta feature los reemplaza
por los del sistema de diseño que el proyecto adoptó después y que la constitución volvió obligatorio
en su Principio VI.

El enfoque sale de research §9 y es lo que hace barata a la feature: **las 42 pantallas casi no
declaran estilo**. Lo declaran los tokens de `index.css`, las diez primitivas de `compartido/ui/`,
`clases.ts`, `Layout` y `Menu`. Cambiar esa capa cambia el aspecto de las 42 de una vez. Recién
después queda el trabajo que sí es pantalla por pantalla, y que **no es estilo sino estructura**: las
15 fusiones de columna repartidas en 12 tablas, el enlace de fila en 9 listados, el menú `···` en 8
tablas —las 10 columnas `Acciones` desaparecen, pero dos de ellas no dejan ningún `···` detrás—, las
secciones numeradas en 9 formularios y el aside de las 5 fichas.

Tres decisiones cargan el resto del plan:

1. **Los cuatro valores de contraste se recalibran midiendo cada uno sobre el fondo donde aparece**, y
   la medición mostró que la jerarquía de cinco tonos neutros de gt-ui **no sobrevive a 4,5:1**: por
   debajo de `ink-soft` hay lugar para dos tonos de texto, no para cuatro. `faint` queda como el único
   tono atenuado de texto y `dim` se degrada a token no textual (research §1).
2. **El menú `···` se escribe, no se instala.** Radix Dropdown convertiría cada ítem en un
   `div[role="menuitem"]` y obligaría a reescribir aserciones que FR-069 no autoriza a tocar
   (research §5).
3. **La fila navega con un `onClick` y el enlace real sigue siendo el `<a>` de la celda.** El `<tr>`
   no recibe `tabIndex`: duplicaría cada fila en el recorrido del tabulador (research §6).

## Technical Context

**Language/Version**: TypeScript 6.0 · React 19.2 · Node ≥ 24 < 25

**Primary Dependencies**: Tailwind CSS 4.3 (`@theme`, CSS-first — **no** `tailwind.config.js`) ·
`class-variance-authority` · `tailwind-merge` · `@radix-ui/react-dialog` (se conserva; **no** se suma
`react-dropdown-menu`) · `lucide-react` · `react-router-dom` 7.18 · `date-fns` ·
**nuevas**: `@fontsource-variable/plus-jakarta-sans`, `@fontsource/geist-mono` ·
**se retira**: `@fontsource-variable/geist`

**Storage**: N/A — **el backend no se toca**. Ninguna entidad, endpoint ni DTO cambia

**Testing**: Vitest 4.1 + Testing Library · 285 pruebas en 43 archivos, todas tienen que seguir
pasando (FR-068)

**Target Platform**: navegador de escritorio, **mínimo 1280 px de ancho** (FR-072)

**Project Type**: aplicación web — sólo se toca `frontend/`

**Performance Goals**: N/A — no hay objetivo de rendimiento nuevo. La única restricción de ejecución
es que las animaciones se limiten a `transform` y `opacity` y usen `IntersectionObserver`, nunca un
listener de scroll (FR-006)

**Constraints**:
- Contraste **4,5:1 para texto y 3:1 para lo no textual que comunica**, medido con herramienta, no a
  ojo (SC-009)
- **Los textos de los Módulos 1 a 6 no se reescriben** (FR-066): son el contrato de las 285 pruebas
- **Ninguna aserción de la suite cambia**; el único cambio autorizado son pasos de interacción en
  **5 archivos** (FR-069)
- Las **42 direcciones** siguen siendo las mismas (FR-067)
- Los **controles nativos se estilan, no se sustituyen** (FR-031) — convención [007]
- **Sin emoji** en ninguna parte de la interfaz (FR-063)

**Scale/Scope**: 42 pantallas · 21 tablas en 20 pantallas · 16 formularios (+3 en línea) · 5 fichas ·
8 superficies de filtro · 12 primitivas existentes revestidas —8 con la firma intacta, 4 con firma
nueva: `Boton`, `Estado`, `Filtros` y `Ficha` (contracts §2)— + 11 nuevas · 4 valores de color
recalibrados que vuelven a la skill

## Constitution Check

*GATE: se evalúa antes de Phase 0 y se re-evalúa después de Phase 1.*

| Principio | Evaluación | Resultado |
|---|---|---|
| **I. Simplicidad Ante Todo** | Se toma el sistema de diseño **como está**, salvo los cuatro valores que FR-007 autoriza. El menú `···` se escribe a mano en vez de sumar una dependencia, y es más simple: 10 reglas de teclado ya especificadas contra adaptar 20 líneas de suite a la API de un tercero (research §5). No se crea `tailwind.config.js` porque `@theme` ya cumple esa función (research §2) | ✅ |
| **II. Idioma y Mercado Argentino** | Los textos existentes se congelan. Los nuevos —9 rótulos de columna, 24 títulos de sección, el nombre accesible del `···`, la leyenda de obligatorios— se escriben en español rioplatense con voseo. Importes con `compartido/moneda` y fechas con `compartido/fechas`, sin cambios (FR-064) | ✅ |
| **III. Cero Alcance Fantasma** | El alcance está enumerado y es contable: 42 pantallas, 21 tablas, 15 fusiones, 16 formularios, 5 fichas, 4 valores. **`muted` falla el contraste sobre dos fondos y no se lo recalibra**, porque FR-007 nombra cuatro y no cinco: en su lugar rige la regla verificable de FR-007b, que se mide con SC-009a (research §1). Las tablas sin ficha **no reciben un destino inventado**. El aside **no trae ningún dato que la ficha no reciba hoy** | ✅ |
| **IV. Verificable por una Persona No Técnica** | Los 6 escenarios de `quickstart.md` se recorren operando la aplicación. Dos puntos exigen herramienta —el contraste y la familia tipográfica efectiva— y están señalados como tales, porque medirlos a ojo es exactamente lo que produjo los cuatro valores que hay que recalibrar | ✅ |
| **V. Datos del Usuario con Respeto** | No se piden datos nuevos, no se agregan campos, no hay secretos. Las dos tipografías se empaquetan con la aplicación: ninguna petición sale a un servicio externo | ✅ |
| **VI. Interfaz Gobernada por el Sistema de Diseño** | Es la feature. Las dos condiciones que el principio exige —acción primaria por pantalla y estados de campo— están declaradas abajo, para las 42 pantallas y para los campos de los 19 formularios | ✅ |

**Re-evaluación post-Phase 1**: sin violaciones nuevas. Las dos tensiones que la medición de contraste
destapó —la jerarquía de tonos que no sobrevive al piso y `muted` fallando sobre dos fondos— se
resolvieron del lado de no ampliar el alcance y **volvieron a la spec** como FR-007a, FR-007b y
SC-009a. Quedan anotadas en *Complexity Tracking* como registro de por qué se decidió así, no como
excepciones que vivan sólo en el plan.

## UI Design Check

*GATE (Principio VI). Re-evaluado después de Phase 1.*

Se leyeron `references/tokens.md`, `references/componentes.md`, `references/contenido.md` y las cuatro
imágenes de referencia.

### El contrato de campo, que rige para todos

Los cuatro estados son iguales en todo el sistema y salen de `componentes.md`. Se declaran una vez
acá y la tabla de abajo sólo dice lo que es **propio de cada campo** —su ancho y su mensaje—:

- **Reposo**: `bg-surface-soft`, borde `line-strong`, radio de campo, texto `ink` 13,5 px. El **ancho
  lo fija el dato** (FR-026), y es la columna que la tabla detalla.
- **Foco**: `focus:shadow-focus` — anillo `rgba(68,83,168,0.16)` de 3 px — más borde `brand` y fondo
  blanco. **El anillo es visible por sí solo**, no depende del cambio de color de borde (FR-025). Rige
  igual en los controles nativos, por la regla `:focus-visible` de `index.css`, que se conserva.
- **Error**: borde `danger`, fondo `danger-bg`, `aria-invalid`, y el mensaje debajo en `danger-text`
  con su ícono. **Aparece al enviar o al volver del servidor, nunca en el primer dibujo** (FR-028,
  SC-010). Esta feature **no cambia cuándo se valida**.
- **Deshabilitado**: `opacity-50` y cursor bloqueado. Se ve deshabilitado y no se confunde con uno
  disponible.
- **Vacío**: el placeholder es **un ejemplo real del formato**, nunca una instrucción, en `faint`, y
  desaparece al escribir. Un `<select>` sin elegir muestra *Seleccioná …*.
- **Obligatorio**: marca visible `*` **y** en el nombre accesible, con la leyenda al pie (FR-027).

### Acción primaria por pantalla

Una sola por pantalla. Donde no hay ninguna accionable dice `ninguna`, y no se inventa un primario
para llenar la celda.

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| `/ingresar` | Ingresar | — | — |
| `/` | `ninguna` | — | — |
| `/usuarios` | Nuevo usuario | — | — |
| `/usuarios/nuevo` | Guardar | Cancelar, Volver | — |
| `/usuarios/:id` | Editar | Roles, Restablecer contraseña, Volver | — |
| `/usuarios/:id/editar` | Guardar | Cancelar, Volver | — |
| `/usuarios/:id/roles` | Guardar | Cancelar, Volver | — |
| `/personas` | Nueva persona | — | — |
| `/personas/nueva` | Guardar | Cancelar, Volver | — |
| `/personas/:id/editar` | Guardar | Cancelar, Volver | — |
| `/choferes` | Nuevo chofer | Ver vencimientos | — |
| `/choferes/vencimientos` | `ninguna` | Volver a choferes | — |
| `/choferes/nuevo` | Guardar chofer | Cancelar, Volver | — |
| `/choferes/:id` | Cargar documento | Editar chofer, Reactivar ⁽ᶜ⁾, Volver ⁽ᵈ⁾ | Dar de baja |
| `/choferes/:id/editar` | Guardar cambios | Cancelar, Volver | — |
| `/transportistas` | Nuevo transportista | — | — |
| `/transportistas/nuevo` | Guardar transportista | Cancelar, Volver | — |
| `/transportistas/:id/editar` | Guardar cambios | Cancelar, Volver | — |
| `/flota` | Registrar unidad | Ver vencimientos | — |
| `/flota/vencimientos` | `ninguna` | Volver a flota | — |
| `/flota/nuevo` | Registrar unidad | Cancelar, Volver | — |
| `/flota/:id` | Agregar documento | Editar, Reactivar ⁽ᶜ⁾, Volver ⁽ᵈ⁾ | Dar de baja |
| `/flota/:id/editar` | Guardar cambios | Cancelar, Volver | — |
| `/tipos-vehiculo` | Cargar tipo | Cancelar (en edición) | — |
| `/viajes` | Nuevo viaje | — | — |
| `/viajes/totales` | `ninguna` ⁽ᵃ⁾ | Volver a viajes | — |
| `/viajes/nuevo` | Guardar viaje | Cancelar, Volver | — |
| `/viajes/:id` | Asignar chofer y vehículo ⁽ᵉ⁾ | Editar, Poner en curso, Rendir, Cargar el remito, Volver al listado | Anular |
| `/viajes/:id/asignacion` | Asignar | Volver al viaje | — |
| `/viajes/:id/editar` | Guardar cambios | Cancelar, Volver | — |
| `/clientes` | Nuevo cliente | — | — |
| `/clientes/nuevo` | Guardar cliente | Cancelar, Volver | — |
| `/clientes/:id` | Guardar cambios | Cancelar, Volver | — |
| `/facturas` | Nueva factura | — | — |
| `/facturas/vencimientos` | `ninguna` | Volver a facturas | — |
| `/facturas/totales` | Ver totales ⁽ᵇ⁾ | Volver a facturas | — |
| `/facturas/nueva` | Emitir factura | Cancelar, Volver | — |
| `/facturas/:id` | Registrar cobro | Corregir datos, Volver al listado | Anular |
| `/facturas/:id/editar` | Guardar cambios | Cancelar, Volver | — |
| `/facturacion/empresa` | Guardar | Cancelar | — |
| `/tipos-documentacion` | Cargar tipo | Cancelar (en edición) | — |
| `/mi-cuenta/contrasena` | Cambiar contraseña | Cancelar | — |

**Notas que la tabla no puede llevar adentro:**

> **Las tres columnas llevan el texto literal que está hoy en pantalla, no el nombre conceptual de la
> acción.** La tabla se verificó contra el código (research §10, §11) y **18 filas cambiaron**: decía
> *Guardar usuario* donde el botón dice `Guardar`, *Corregir* donde dice `Corregir datos`, *Cargar
> documento* donde dice `Agregar documento`. Escritos así, los verbos de esta tabla se pueden pegar en
> un `getByRole` y encontrar el control (FR-066).

- ⁽ᵃ⁾ **`/viajes/totales` no tiene primario y está bien**: su `<form>` hace `preventDefault` y no
  lleva botón — la consulta se dispara sola al completar el rango. ⁽ᵇ⁾ **`/facturas/totales` sí lo
  tiene**: un `<button type="submit">Ver totales</button>` que ejecuta la consulta. **Los dos paneles
  de totales no son simétricos**, y esta tabla los trataba igual hasta que se los miró.
- **Los cuatro paneles con `ninguna`** —`/choferes/vencimientos`, `/flota/vencimientos`,
  `/viajes/totales`, `/facturas/vencimientos`— son de solo lectura. No tienen exportación ni impresión
  —el sistema no las ofrece hoy y agregarlas sería alcance fantasma—, así que no llevan primario: su
  único control es el *volver* terciario. De los cuatro, **`/facturas/vencimientos` es el único que
  hoy no tiene ninguna acción de encabezado**, ni volver: hay que crearlo. La quinta fila con
  `ninguna` es `/`, que no es un panel sino la pantalla de inicio.
- **Las secundarias de `/viajes` y `/facturas` se retiraron de esta tabla porque no existen.**
  `/viajes/totales`, `/facturas/totales` y `/facturas/vencimientos` se alcanzan **únicamente desde el
  menú**. Inventarles un enlace desde el listado sería alcance fantasma: se corrige la tabla, no la
  pantalla. Las dos secundarias que **sí** existen son las dos *Ver vencimientos* de `/choferes` y
  `/flota`.
- **Las primarias de las cinco fichas hay que designarlas, no revestirlas.** Hoy sus botones son
  todos `type="button"` y el encabezado los estila por descendiente, así que *Editar*, *Dar de baja* y
  *Registrar cobro* se dibujan idénticas: la ficha **no tiene** acción principal (research §10). Es el
  problema que [007] resolvió en los formularios, sobreviviendo en los encabezados.
- **El *volver* de seis pantallas viaja hoy dentro de `accionPrincipal`**, no en la prop `volverA`
  que la primitiva ya tiene: `/choferes/vencimientos`, `/flota/vencimientos`, `/tipos-vehiculo`,
  `/viajes/:id`, `/facturas/:id` y `/usuarios/:id`. Mudarlo de prop es lo que FR-022 pide, y no toca
  texto ni rol ni etiqueta accesible. **Son seis y no cinco**: research §10 contó cinco y §11 encontró
  el sexto, `/viajes/:id`, al verificar los verbos contra el código (T035c).
- **`/facturas/:id` de una factura anulada no tiene primario**: *Registrar cobro*, *Corregir* y
  *Anular* desaparecen y queda el callout explicando por qué (FR-058).
- **`/viajes/:id` de un viaje rendido no tiene primario**: es el caso que muestra `viaje-detalle.png`,
  con el callout que ofrece revertir la rendición desde Totales.
- ⁽ᶜ⁾ **`Reactivar` aparece en lugar de `Dar de baja`** cuando el registro ya está dado de baja: no
  conviven. ⁽ᵈ⁾ **`/choferes/:id` y `/flota/:id` no tienen *volver* hoy**: hay que crearlo (T035d).
  ⁽ᵉ⁾ El botón dice `Asignar chofer y vehículo` o `Reasignar chofer y vehículo` según el viaje ya
  tenga asignación.
- **`Guardar` a secas es el verbo real de cinco formularios** —usuario, persona, roles, corrección de
  factura y empresa emisora— y **once aserciones de la suite lo consultan así**, en tres archivos que
  FR-069 no autoriza a tocar. FR-065 pide que un botón nombre su objeto, pero **alcanza sólo a los
  textos nuevos**: éstos los fijaron los Módulos 1 a 6 y FR-066 los congela. Renombrarlos a *Guardar
  usuario* sería el cambio más chico capaz de romper la feature entera.
- **Las acciones secundarias del encabezado** —*Ver vencimientos* junto a *Nuevo chofer*— van como
  pastilla gris claro, nunca como un segundo relleno pleno (FR-019).

### Estados de campo

Reposo = ancho, sobre el contrato de arriba. Foco = el anillo, igual en todos. La tabla dice el ancho
que le toca a cada dato, cuándo aparece el error y qué se ve vacío.

**Los mensajes de error de la columna *Error* son los que ya existen** y no se reescriben; lo que la
columna declara es **cuándo** aparecen.

| Pantalla | Campo | Reposo (ancho) | Foco | Error | Vacío |
|---|---|---|---|---|---|
| `/ingresar` | Nombre de usuario | completo (tarjeta 380 px) | anillo | al enviar, texto del Módulo 1 | placeholder vacío |
| `/ingresar` | Contraseña | completo | anillo | al enviar | `type=password` siempre |
| `/mi-cuenta/contrasena` | Contraseña actual | medio | anillo | al enviar / al volver del servidor | — |
| `/mi-cuenta/contrasena` | Contraseña nueva | medio | anillo | al enviar | ayuda con la regla debajo |
| `/mi-cuenta/contrasena` | Repetir contraseña nueva | medio | anillo | al enviar, si no coinciden | — |
| `/usuarios/nuevo`, `/usuarios/:id/editar` | Nombre de usuario | medio | anillo | al enviar / duplicado del servidor | — |
| ídem | Email | medio | anillo | al enviar / al volver | `nombre@empresa.com.ar` |
| ídem | Contraseña inicial | medio | anillo | al enviar | ayuda con la regla |
| ídem | Estado | corto (`select`) | anillo | — | valor actual |
| ídem | Roles | casillas | anillo por casilla | al enviar, si ninguno | ninguna tildada |
| `/usuarios/:id/roles` | Roles | casillas | anillo por casilla | al volver del servidor | ninguna tildada |
| `/personas/*` | Nombre | medio | anillo | al enviar | — |
| `/personas/*` | Apellido | medio | anillo | al enviar | — |
| `/personas/*` | DNI | **corto** — 8 dígitos | anillo | al enviar / duplicado | `30.123.456` |
| `/personas/*` | Tipo | corto (`select`) | anillo | — | *Seleccioná un tipo* |
| `/personas/*` | Teléfono | medio | anillo | al enviar | `+54 9 221 555-0148` |
| `/personas/*` | Email | medio | anillo | al enviar | `nombre@empresa.com.ar` |
| `/personas/*` | Fecha de nacimiento | **corto** — fecha | anillo | al enviar | `dd/mm/aaaa` nativo |
| `/choferes/nuevo`, `/choferes/:id/editar` | DNI | **corto** | anillo | al enviar / duplicado | `30.123.456` |
| ídem | Nombre | medio | anillo | al enviar | — |
| ídem | Apellido | medio | anillo | al enviar | — |
| ídem | Fecha de nacimiento | **corto** | anillo | al enviar | nativo |
| ídem | CUIL | **corto** — 11 dígitos | anillo | al enviar, si no coincide con el DNI | `20-30123456-4` |
| ídem | Teléfono | medio | anillo | al enviar | `+54 9 221 555-0148` |
| ídem | Email | medio | anillo | al enviar | `nombre@empresa.com.ar` |
| ídem | Transportista | **largo** (`select`) | anillo | al enviar | *Seleccioná un transportista* |
| `/transportistas/*` | Razón social o nombre completo | **largo** | anillo | al enviar | — |
| ídem | CUIT | **corto** | anillo | al enviar / duplicado | `30-71234567-8` |
| ídem | Tipo de persona | corto (`select`) | anillo | — | valor actual |
| ídem | Teléfono | medio | anillo | al enviar | `+54 9 221 555-0148` |
| ídem | Email | medio | anillo | al enviar | `nombre@empresa.com.ar` |
| `/flota/nuevo`, `/flota/:id/editar` | Patente | **corto** — 7 caracteres | anillo | al enviar / duplicado | `EF456GH` |
| ídem | Marca | medio | anillo | al enviar | — |
| ídem | Modelo | medio | anillo | al enviar | — |
| ídem | Tipo de vehículo | medio (`select`) | anillo | al enviar | *Seleccioná un tipo* |
| ídem | Transportista | **largo** (`select`) | anillo | al enviar | *Seleccioná un transportista* |
| ídem | Estado operativo | medio (`select`) | anillo | — | valor actual |
| `/tipos-vehiculo` | Nombre | medio | anillo | al enviar / duplicado | — |
| `/tipos-documentacion` | Nombre | medio | anillo | al enviar / duplicado | — |
| `/tipos-documentacion` | Días de aviso de vencimiento | **corto** — número | anillo | al enviar | `30` |
| `/tipos-documentacion` | Ámbito | corto (`select`) | anillo | — | valor actual |
| `/viajes/nuevo`, `/viajes/:id/editar` | Cliente | **largo** (`select`) | anillo | al enviar | *Seleccioná un cliente* |
| ídem | Fecha del viaje | **corto** | anillo | al enviar | nativo |
| ídem | Origen | **largo** | anillo | al enviar | `Rosario` |
| ídem | Destino | **largo** | anillo | al enviar | `Córdoba` |
| ídem | Número de remito (opcional) | medio, en **mono** | anillo | — | chip *Opcional* |
| ídem | Detalle de la carga (opcional) | completo (`textarea`) | anillo | — | chip *Opcional* |
| ídem | Importe en pesos | **corto**, alineado a la derecha | anillo | al enviar | `22644,63` |
| `/viajes/:id/asignacion` | Chofer | **largo** (`select`) | anillo | al enviar / `409` de unidad ocupada | *Seleccioná un chofer* |
| ídem | Vehículo | **largo** (`select`) | anillo | al enviar / `409` | *Seleccioná un vehículo* |
| `/clientes/nuevo`, `/clientes/:id` | Razón social | **largo** | anillo | al enviar | `Distribuidora del Litoral` |
| ídem | CUIT | **corto** | anillo | al enviar / duplicado | `30-71234567-8` |
| ídem | Teléfono | medio | anillo | al enviar | `+54 9 221 555-0148` |
| ídem | Email | medio | anillo | al enviar | `nombre@empresa.com.ar` |
| ídem | Dirección (opcional) | **largo** | anillo | — | chip *Opcional* |
| `/facturas/nueva` | Cliente | **largo** (`select`) | anillo | al enviar | *Seleccioná un cliente* |
| ídem | Tipo de comprobante | corto (`select`) | anillo | al enviar | valor actual |
| ídem | Tipo de facturación | medio (`select`) | anillo | al enviar | valor actual |
| ídem | Factura que reemplaza | **largo** (`select`) | anillo | al enviar | sólo visible si aplica |
| ídem | Condición de venta | medio (`select`) | anillo | al enviar | valor actual |
| ídem | Mes / Año | **corto** cada uno | anillo | al enviar | período actual |
| ídem | Fecha de facturación | **corto** | anillo | al enviar | nativo |
| ídem | Número de comprobante | medio, en **mono** | anillo | al enviar / duplicado | `0000-00000111` |
| ídem | CAE | medio, en **mono** | anillo | al enviar | `71234567890123` |
| ídem | Vencimiento del CAE | **corto** | anillo | al enviar | nativo |
| ídem | Vencimiento de pago | **corto** | anillo | al enviar | nativo |
| ídem | Detalle (opcional) | completo (`textarea`) | anillo | — | chip *Opcional* |
| ídem | Viajes a facturar (casillas) | tabla | anillo por casilla | al enviar, si ninguno | estado vacío con su texto |
| `/facturas/:id/editar` | Detalle | completo (`textarea`) | anillo | al enviar | — |
| ídem | CAE | medio, en **mono** | anillo | al enviar | `71234567890123` |
| ídem | Vencimiento del CAE | **corto** | anillo | al enviar | nativo |
| ídem | Vencimiento de pago | **corto** | anillo | al enviar | nativo |
| `/facturacion/empresa` | Razón social | **largo** | anillo | al enviar | — |
| ídem | CUIT | **corto** | anillo | al enviar | `30-71234567-8` |
| ídem | Domicilio | **largo** | anillo | al enviar | — |
| ídem | Condición de IVA | medio (`select`) | anillo | al enviar | valor actual |
| ídem | Número de ingresos brutos (opcional) | medio, en **mono** | anillo | — | chip *Opcional* |
| ídem | Inicio de actividades (opcional) | **corto** | anillo | — | chip *Opcional* |
| ídem | Punto de venta (opcional, 4 dígitos) | **corto**, en **mono** | anillo | al enviar, si no son 4 dígitos | `0001` |
| ídem | CBU (opcional) | medio, en **mono** | anillo | — | chip *Opcional* |
| ídem | Teléfono (opcional) | medio | anillo | — | chip *Opcional* |
| ídem | Email (opcional) | medio | anillo | — | chip *Opcional* |
| ídem | Logo | campo de archivo | anillo | al enviar, tipo o tamaño | vista previa o *Sin logo* |
| `/facturas/:id` · Registrar cobro | Fecha de cobro | **corto** | anillo | al enviar | nativo |
| `/choferes/:id`, `/flota/:id` · documento | Tipo de documentación | medio (`select`) | anillo | al enviar | *Seleccioná un tipo* |
| ídem | Número | medio, en **mono** | anillo | al enviar | — |
| ídem | Fecha de emisión | **corto** | anillo | al enviar | nativo |
| ídem | Fecha de vencimiento | **corto** | anillo | al enviar, si es anterior a la emisión | nativo |
| ídem | Archivo adjunto (opcional) | campo de archivo | anillo | al enviar, tipo o tamaño | chip *Opcional* |

**Los controles de filtro no llevan estado de error** —no se envían, se aplican— y por eso no figuran
en esta tabla. Sus estados son reposo, foco y *filtrando*: el desplegable que está filtrando lleva
borde más fuerte para distinguirse de los que no (FR-034), y el listado sigue declarando por escrito
qué se está mostrando.

## Project Structure

### Documentation (this feature)

```text
specs/008-diseno-gt-ui/
├── plan.md              # Este archivo
├── research.md          # Phase 0 — nueve decisiones, con las mediciones de contraste
├── data-model.md        # Phase 1 — tokens, 42 pantallas, 21 tablas, 16 formularios
├── quickstart.md        # Phase 1 — cómo se comprueba, operando la aplicación
├── contracts/
│   └── README.md        # Phase 1 — las firmas de las primitivas
├── checklists/
│   └── requirements.md  # ya existente
└── tasks.md             # Phase 2 — lo genera /speckit-tasks, NO este comando
```

### Source Code (repository root)

Sólo se toca `frontend/`. **El backend no aparece en este árbol porque no se toca.**

```text
frontend/src/
├── index.css                        # ← los tokens de gt-ui en @theme (reemplaza los del Módulo 7)
├── App.tsx                          # ← el Lienzo en la raíz; las 42 rutas no cambian
├── compartido/
│   ├── Layout.tsx                   # ← se retira la barra superior; isla flotante + pie de cuenta
│   ├── Menu.tsx                     # ← isla de 266 px, eyebrow por sección, activo por fondo y peso
│   └── ui/
│       ├── clases.ts                # ← las cuatro variantes con la forma de gt-ui
│       ├── Boton.tsx                # revestido — firma intacta
│       ├── Campo.tsx                # revestido — firma intacta
│       ├── Estado.tsx               # ← suma `forma`, obligatoria
│       ├── Aviso.tsx  Dialogo.tsx  DialogoConfirmacion.tsx
│       ├── EstadoVacio.tsx  Filtros.tsx  Listado.tsx  Paginacion.tsx
│       ├── Ficha.tsx  EncabezadoDePantalla.tsx
│       ├── Historial.tsx            # ← se retira, lo reemplaza LineaDeTiempo
│       ├── Lienzo.tsx               # nuevo — los dos orbes
│       ├── Isla.tsx                 # nuevo
│       ├── MenuDeFila.tsx           # nuevo — el ···
│       ├── EnlaceDeFila.tsx         # nuevo
│       ├── TokenDeIdentificador.tsx # nuevo
│       ├── FilaNavegable.tsx        # nuevo
│       ├── SeccionNumerada.tsx      # nuevo
│       ├── BarraDeAcciones.tsx      # nuevo
│       ├── LineaDeTiempo.tsx        # nuevo
│       ├── AsideDeFicha.tsx         # nuevo
│       └── Callout.tsx              # nuevo
└── modules/
    ├── autenticacion/  choferes/  facturacion/
    ├── flota/  usuarios/  viajes/
    └── …                            # 42 pantallas: estructura, no estilo (columnas, filas,
                                     #   secciones, aside). Ningún texto cambia

.claude/skills/gt-ui/                # ← también es SALIDA de esta feature (FR-008)
├── SKILL.md                         # ← registrar `choferes- padron.png` en las referencias
└── references/tokens.md             # ← los cuatro valores recalibrados, con su medición al lado
```

**Structure Decision**: se conserva la estructura del Módulo 7 sin ninguna carpeta nueva. Las
primitivas nuevas van en `compartido/ui/` junto a las que ya están, y las 42 pantallas siguen
organizadas por módulo de negocio, como fija la constitución. Lo único que se agrega fuera de
`frontend/` es la escritura de vuelta sobre `.claude/skills/gt-ui/`, que FR-008 exige y sin la cual el
sistema de diseño quedaría diciendo una cosa y la aplicación haciendo otra.

## Complexity Tracking

> Se llena sólo si el Constitution Check tiene violaciones que justificar.

| Violación | Por qué hace falta | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **`dim` deja de ser color de texto** — *ya no es desviación: la spec lo incorporó en **FR-007a*** | Recalibrar `dim` y `faint` a 4,5:1 los deja a **0,0001 de luminancia uno del otro** (research §1): son el mismo gris. La redacción original de FR-007 pedía dos cosas que no pueden cumplirse juntas, y la que se conserva es el contraste, porque es la que SC-009 mide. Se enmendó la spec en vez de dejar la excepción viviendo sólo acá | *Recalibrar los cuatro y aceptar que se vean iguales* — cumpliría la letra y dejaría un sistema de diseño con dos tokens indistinguibles, que es peor que tener uno solo bien definido. *Bajar el piso a 3:1 para metadata* — contradice a SC-009 y al Módulo 7 |
| **`muted` falla el contraste sobre `surface-mute` y sobre el lienzo y no se lo recalibra** — *ya no es desviación: la regla es **FR-007b** y se mide con **SC-009a*** | FR-007 acota el alcance a cuatro valores y nombrarlos fue una decisión de la spec. Tocar un quinto sería alcance fantasma (Principio III) | *Recalibrarlo igual* — resolvería el caso pero abriría el alcance sin autorización. En su lugar se escribe una **regla verificable** —sobre el lienzo y sobre `surface-mute` sólo va texto en `ink` o `ink-soft`—, que cuesta menos, no cambia ningún valor y se comprueba operando |
| **Se escribe una primitiva de menú en vez de usar una biblioteca** | Radix Dropdown convierte cada ítem en `div[role="menuitem"]` y obligaría a reescribir aserciones en los 5 archivos que FR-069 sólo autoriza a tocar con pasos de interacción | *Adoptar `@radix-ui/react-dropdown-menu`* — traería foco y flechas gratis, pero rompería el contrato de FR-069 y contradice la convención [007]: se toma lo que agrega comportamiento, no lo que reemplaza un control que los tests operan |
| **FR-069 decía 21 líneas y el relevamiento encontró 20** — *ya no es desviación: la spec se corrigió con el número medido* | La lista de **archivos** —que es lo que de verdad acota el permiso— se verificó completa y correcta (research §8) | Ninguna: no hay nada que simplificar. Se enumeran las 20 con nombre y línea; si al implementar aparece una 21ª dentro de esos cinco archivos, se agrega. Tocar un archivo **fuera** de los cinco sigue siendo señal de que algo se rompió |

## Mantenimiento

Como último paso de la fase final, **actualizar `AGENTS.md`** con las decisiones transversales de esta
feature, una línea por decisión y con referencia `[008]`. Candidatas, sujetas a que sobrevivan a la
implementación —sólo entra lo que de verdad sirva a una feature futura, no una entrada por entrada—:

- Una **rampa de grises no puede tener más niveles que los que el piso de contraste permite**: por
  debajo del texto secundario y por encima de 4,5:1 entran dos tonos, no cuatro. Un sistema de diseño
  escrito mirando describe cinco; medirlos es lo que revela que tres son el mismo. Y la medición se
  hace **sobre el fondo donde cada token aparece**, no contra un fondo único de referencia
- Cuando una **superficie deja de tener fondo propio** —una columna transparente sobre el lienzo—,
  **pasa a ser un fondo de texto** y hay que medirla como tal. Es el caso que hace fallar a un token
  que venía cumpliendo, sin que cambie ni su valor ni su uso
- Una **fila que navega entera** conserva el `<a>` de la celda como destino real y **no recibe
  `tabIndex`**: el `onClick` del `<tr>` es comodidad de mouse, y agregarlo al recorrido del tabulador
  duplicaría cada fila
- Un **menú de acciones de fila no es un diálogo**: no retiene el foco ni bloquea el fondo. Y su
  disparador va **siempre en el DOM**, porque uno que aparece al pasar el mouse queda inalcanzable con
  teclado y en pantalla táctil
- Mover un control **dentro de un menú lo saca del DOM**, y ése es el único cambio de esta clase que
  rompe una suite que consulta por rol y por texto. Reordenar, envolver y reestilar no rompen nada;
  **esconder sí**
- Cuando una **skill es la fuente de un sistema de diseño, también es su destino**: una corrección que
  vive sólo en el código deja a la skill diciendo una cosa y a la aplicación haciendo otra, y la
  próxima feature vuelve a introducir el valor viejo
