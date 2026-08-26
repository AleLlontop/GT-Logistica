# Data Model — Adopción del sistema de diseño gt-ui (Módulo 8)

**Feature**: `008-diseno-gt-ui` · **Spec**: [spec.md](./spec.md) · **Research**: [research.md](./research.md)

El sistema de diseño no tiene datos. Lo que sí tiene es un **vocabulario cerrado** —tokens, niveles de
acción, formas de estado— y un **inventario** de a qué pantalla se aplica cada cosa. Eso es lo que
este documento fija, para que la revisión pueda contarlo.

**El backend no se toca.** Ninguna entidad, ningún endpoint y ningún DTO cambia.

---

## §1. Tokens

Viven en un único `@theme` dentro de `frontend/src/index.css` (research §2). Los nombres son los de
`references/tokens.md`, no los del Módulo 7.

### 1.1 Neutros

| Token | Valor | Uso | Medición |
|---|---|---|---|
| `canvas` | `#EDEEF1` | Fondo de la app | — (fondo) |
| `surface` | `#FFFFFF` @ 94 % | Tarjetas e islas | — (fondo) |
| `surface-soft` | `#F7F8FA` | Campos en reposo, encabezado y pie de tabla | — (fondo) |
| `surface-mute` | `#F1F2F6` | Botón secundario, chips neutros | — (fondo) |
| `ink` | `#101114` | Texto principal, relleno del botón primario | 16,27:1 sobre `canvas` |
| `ink-soft` | `#3C4048` | Labels, texto de botón secundario | 8,96:1 sobre `canvas` |
| `muted` | `#6E7280` | Texto secundario, íconos | 4,79:1 sobre blanco · 4,51:1 sobre `surface-soft` |
| `faint` | **`#6E7683`** ⚠ | Metadata, ayudas, placeholders, **todo lo atenuado** | **4,58:1** sobre blanco |
| `dim` | **`#BFC4CD`** ⚠ | **Sólo no textual**: separador `·`, relleno de ícono, `#` del token | decorativo |
| `dim-texto` | **`#6B768A`** ⚠ | El valor de `dim` recalibrado, si alguna vez hace falta como texto | **4,58:1** sobre blanco |
| `line` | `rgba(16,17,20,0.06)` | Bordes de tarjeta y divisores | decorativo |
| `line-strong` | `rgba(16,17,20,0.09)` | Bordes de campo y chips | decorativo |

⚠ = valor recalibrado o redefinido respecto de `references/tokens.md`. Los tres cambios vuelven a la
skill (FR-008), con su medición y su fondo de medición al lado.

**Dos reglas que acompañan a la tabla y que no son opcionales** (research §1):

1. **`dim` no pinta texto.** Donde `tokens.md` se lo asignaba —placeholders, *"Sin asignar"*— manda
   `faint`. `faint` es el único tono atenuado de texto del sistema.
2. **Sobre el lienzo y sobre `surface-mute` sólo va texto en `ink` o `ink-soft`.** `muted` cumple
   4,5:1 sobre blanco y sobre `surface-soft`, y no lo cumple sobre esos dos. Como la columna
   principal es transparente (FR-011), el lienzo es un fondo de texto real y hay que tratarlo así.

### 1.2 Estados y feedback

| Token | Texto | Fondo | Medición |
|---|---|---|---|
| `estado-rendido` | `#0B6350` | `#E4F2EE` | 7,32:1 |
| `estado-pendiente` | `#8A5A0C` | `#FBF1E0` | 5,29:1 |
| `estado-facturado` | `#333F84` | `#E9ECF7` | 8,04:1 |
| `estado-anulado` | **`#6A6E7C`** ⚠ | `#F2F3F6` | **4,58:1** (era 4,32:1) |
| `danger` (borde) | `#E5484D` | — | 3,1:1 sobre blanco — no textual |
| `danger-text` | `#C13A3E` | `#FEF4F4` | 5,09:1 |
| `brand` (foco, links de identificador) | `#4453A8` | anillo `rgba(68,83,168,0.16)` spread 3 px | 7,66:1 |

**Encabezado de columna**: `#6B7181` ⚠ sobre `surface-soft` — 4,59:1 (era `#8D93A1`, 2,90:1).

### 1.3 Tipografía, radios, sombras, espaciado y movimiento

Se toman de `references/tokens.md` **sin cambios**. Familias: `Plus Jakarta Sans Variable` para el
texto, `Geist Mono` para identificadores. Radios: pastilla `9999px`, isla `26px`, tarjeta `20px`,
campo `13–14px`, chip `8–12px`, token de identificador `7px`. Las cuatro sombras difusas con spread
negativo. Curva única `cubic-bezier(0.32, 0.72, 0, 1)`, animando sólo `transform` y `opacity`, apagada
bajo `prefers-reduced-motion` (esa regla ya existe en `index.css` y se conserva).

---

## §2. Nivel de acción

Atributo **obligatorio y sin valor por defecto** de todo botón, como ya lo es hoy (FR-021). Cambia la
apariencia de cada nivel; no cambia la garantía.

| Nivel | Forma | Cuándo |
|---|---|---|
| `primario` | Pastilla `ink` rellena, ícono anidado en círculo interno `bg-white/[0.14]` al borde derecho | La acción principal. **Una por pantalla** |
| `secundario` | Pastilla `surface-mute` sin borde, mismo tamaño | Cancelar, y las acciones que acompañan al primario en un encabezado |
| `terciario` | Pastilla blanca con hairline, flecha en círculo, arriba a la izquierda | *Volver* |
| `destructivo` | Idéntico al primario con `bg-danger` | Anular, eliminar, dar de baja. Único uso del rojo |

La variante `texto` que existe hoy —enlace subrayado— **se conserva** como está: la usan los enlaces
contextuales dentro de una celda y de una ficha, y retirarla obligaría a tocar pantallas sin que nadie
lo note. No es un quinto nivel de acción: es un enlace.

---

## §3. Pastilla de estado

`compartido/ui/Estado.tsx` conserva su firma —`valor` en camelCase, `texto` obligatorio, desconocido
cae en neutro— y suma una prop **obligatoria** `forma`.

| `forma` | Cuándo | Aspecto |
|---|---|---|
| `pastilla` | El estado **del que trata la pantalla**: la documentación en el padrón de choferes, el estado del viaje en su ficha | Fondo tonal + palabra + punto |
| `punto` | Un estado de alta y baja que sólo acompaña, para no competir con el anterior | Punto de color + palabra, sin fondo |

### Mapeo de los veinte valores sobre los cuatro tonos

| Valor del API | Módulo | Tono | Forma habitual |
|---|---|---|---|
| `enRegla`, `vigente` | 3, 4 | `rendido` (verde) | pastilla |
| `proximaAvencer` | 3, 4 | `pendiente` (ámbar) | pastilla |
| `vencida` | 3, 4 | `danger` | pastilla |
| `sinDocumentacion` | 3, 4 | neutro | pastilla |
| `pendiente` | 5 | `pendiente` (ámbar) | pastilla |
| `enCurso` | 5 | `facturado` (índigo) | pastilla |
| `rendido` | 5 | `rendido` (verde) | pastilla |
| `facturado` | 5 | `facturado` (índigo) | pastilla |
| `anulado`, `anulada` | 5, 6 | `anulado` (gris) | pastilla |
| `pagada` | 6 | `rendido` (verde) | pastilla |
| `disponible` | 4 | `rendido` (verde) | pastilla |
| `enViaje` | 4 | `facturado` (índigo) | pastilla |
| `fueraDeServicio` | 4 | `pendiente` (ámbar) | pastilla |
| `activo`, `activa` | 1, 2, 4, 5 | neutro | **punto** |
| `inactivo`, `inactiva`, `dadoDeBaja` | 1, 2, 4, 5 | `anulado` (gris) | **punto** |
| `bloqueado` | 1 | `danger` | **punto** |
| *cualquier otro* | — | neutro | pastilla |

**La palabra nunca sale de acá**: llega desde `NombresDeEstado` y los `TEXTO_ESTADO_*` de cada módulo,
que FR-066 congela. Y a la palabra se le suma una **forma** además del color, para que la distinción
sobreviva a una captura en escala de grises (SC-014).

---

## §4. Las 42 pantallas

Cada ruta, su componente y qué patrón de gt-ui le toca. Los patrones son tres: **listado**,
**formulario** y **ficha**; más `panel` (tabla de solo lectura sin ficha propia) y `simple`.

| # | Ruta | Componente | Patrón |
|---|---|---|---|
| 1 | `/ingresar` | `PantallaIngreso` | formulario · sin navegación |
| 2 | `/` | `PantallaInicio` | simple |
| 3 | `/usuarios` | `ListadoUsuarios` | listado de índice |
| 4 | `/usuarios/nuevo` | `FormularioUsuario` | formulario |
| 5 | `/usuarios/:id` | `DetalleUsuario` | **ficha** |
| 6 | `/usuarios/:id/editar` | `FormularioUsuario` | formulario |
| 7 | `/usuarios/:id/roles` | `PanelRoles` | formulario |
| 8 | `/personas` | `ListadoPersonas` | listado sin ficha |
| 9 | `/personas/nueva` | `FormularioPersona` | formulario |
| 10 | `/personas/:id/editar` | `FormularioPersona` | formulario |
| 11 | `/choferes` | `ListadoChoferes` | listado de índice |
| 12 | `/choferes/vencimientos` | `PanelVencimientos` | listado de índice |
| 13 | `/choferes/nuevo` | `FormularioChofer` | formulario |
| 14 | `/choferes/:id` | `FichaChofer` | **ficha** |
| 15 | `/choferes/:id/editar` | `FormularioChofer` | formulario |
| 16 | `/transportistas` | `ListadoTransportistas` | listado sin ficha |
| 17 | `/transportistas/nuevo` | `FormularioTransportista` | formulario |
| 18 | `/transportistas/:id/editar` | `FormularioTransportista` | formulario |
| 19 | `/flota` | `ListadoFlota` | listado de índice |
| 20 | `/flota/vencimientos` | `PanelVencimientosFlota` | listado de índice |
| 21 | `/flota/nuevo` | `FormularioVehiculo` | formulario |
| 22 | `/flota/:id` | `FichaVehiculo` | **ficha** |
| 23 | `/flota/:id/editar` | `FormularioVehiculo` | formulario |
| 24 | `/tipos-vehiculo` | `ListadoTiposVehiculo` | listado sin ficha + formulario |
| 25 | `/viajes` | `ListadoViajes` | listado de índice |
| 26 | `/viajes/totales` | `TotalesPeriodo` | panel |
| 27 | `/viajes/nuevo` | `FormularioViaje` | formulario |
| 28 | `/viajes/:id` | `FichaViaje` | **ficha** |
| 29 | `/viajes/:id/asignacion` | `AsignacionViaje` | formulario |
| 30 | `/viajes/:id/editar` | `FormularioViaje` | formulario |
| 31 | `/clientes` | `ListadoClientes` | listado de índice |
| 32 | `/clientes/nuevo` | `FormularioCliente` | formulario |
| 33 | `/clientes/:id` | `FormularioCliente` | formulario (edición) |
| 34 | `/facturas` | `ListadoFacturas` | listado de índice |
| 35 | `/facturas/vencimientos` | `PanelVencimientosFacturas` | listado de índice |
| 36 | `/facturas/totales` | `TotalesFacturados` | panel |
| 37 | `/facturas/nueva` | `AltaFactura` | formulario |
| 38 | `/facturas/:id` | `FichaFactura` | **ficha** |
| 39 | `/facturas/:id/editar` | `CorreccionFactura` | formulario |
| 40 | `/facturacion/empresa` | `EmpresaEmisora` | formulario |
| 41 | `/tipos-documentacion` | `TiposDocumentacion` | listado sin ficha + formulario |
| 42 | `/mi-cuenta/contrasena` | `CambiarPassword` | formulario |

**Las cinco fichas de solo lectura** que FR-050 alcanza son las marcadas en negrita: `DetalleUsuario`,
`FichaChofer`, `FichaVehiculo`, `FichaViaje`, `FichaFactura`.

---

## §5. Las 21 tablas

**20 pantallas con tabla, 21 tablas**: `FichaFactura` tiene dos —el detalle de viajes incluidos y el
historial—. La subsección *Cómo se entra a una fila* de la spec tiene **dos alcances distintos** y
esta tabla los separa, para que la revisión pueda contar cada uno.

| # | Tabla | Enlace de fila + fila navegable + chevron (FR-040–043) | Menú `···` (FR-044–046) |
|---|---|---|---|
| 1 | `ListadoViajes` | **Sí** → `/viajes/:id` | no (hoy no tiene acciones) |
| 2 | `ListadoChoferes` | **Sí** → `/choferes/:id` | no ⁽¹⁾ |
| 3 | `ListadoFlota` | **Sí** → `/flota/:id` | no ⁽¹⁾ |
| 4 | `ListadoFacturas` | **Sí** → `/facturas/:id` | no (hoy no tiene acciones) |
| 5 | `ListadoUsuarios` | **Sí** → `/usuarios/:id` | **Sí**: Editar · Roles · Dar de baja |
| 6 | `ListadoClientes` | **Sí** → `/clientes/:id` | **Sí**: Editar · Dar de baja / Dar de alta |
| 7 | `PanelVencimientos` (choferes) | **Sí** → `/choferes/:id` | no |
| 8 | `PanelVencimientosFlota` | **Sí** → `/flota/:id` | no |
| 9 | `PanelVencimientos` (facturación) | **Sí** → `/facturas/:id` | no |
| 10 | `ListadoPersonas` | no ⁽²⁾ | **Sí**: Editar · Dar de baja |
| 11 | `ListadoTransportistas` | no ⁽²⁾ | **Sí**: Editar · Dar de baja |
| 12 | `ListadoTiposVehiculo` | no ⁽²⁾ | **Sí**: Editar · Dar de baja |
| 13 | `TiposDocumentacion` | no ⁽²⁾ | **Sí**: Editar · Dar de baja |
| 14 | `TotalesPeriodo` | no — panel de totales | no |
| 15 | `TotalesFacturados` | no — panel de totales | no |
| 16 | `SelectorDeViajes` | no — filas con casilla | no |
| 17 | `FichaFactura` · viajes incluidos | no — detalle dentro de una ficha | no |
| 18 | `FichaFactura` · historial | no — pasa a línea de tiempo (FR-054) | no |
| 19 | `FichaViaje` · historial | no — pasa a línea de tiempo (FR-054) | no |
| 20 | `FichaChofer` · documentación | no — detalle dentro de una ficha | **Sí**: Corregir · Eliminar |
| 21 | `FichaVehiculo` · documentación | no — detalle dentro de una ficha | **Sí**: Corregir · Eliminar |

**Totales**: 9 tablas con enlace de fila · **8 tablas con menú `···`** · **10 columnas `Acciones` que
desaparecen** (SC-007) · las 21 reciben tokens, divisores hairline, encabezados en eyebrow, importes
alineados y celdas que dicen qué falta.

Los dos números de acciones **no coinciden a propósito**: desaparecen 10 columnas `Acciones` y quedan
8 menús `···`. La diferencia son `ListadoChoferes` y `ListadoFlota` ⁽¹⁾, cuya columna contenía sólo
*Ver ficha* y por eso no deja nada que alojar.

⁽¹⁾ Su columna `Acciones` contiene **únicamente** *Ver ficha*, que es exactamente lo que pasa a ser el
enlace de fila. La columna desaparece y no queda ningún `···`: no hay acción secundaria que alojar
(research §8).

⁽²⁾ No tiene ficha de destino: su único destino es la edición, y editar es una acción secundaria que va
al `···`. Inventarle un destino de solo lectura sería alcance fantasma. `ListadoTiposVehiculo` y
`TiposDocumentacion` editan además en la misma pantalla, sin navegar.

### Columnas que se fusionan (FR-047)

| # | Tabla | Antes | Después |
|---|---|---|---|
| 1 | `ListadoViajes` | `Origen` + `Destino` | **Ruta** — `Rosario → Córdoba` |
| 2 | `ListadoViajes` | `Chofer` + `Vehículo` | **Asignación** — nombre arriba, patente en mono debajo |
| 3 | `SelectorDeViajes` | `Origen` + `Destino` | **Ruta** |
| 4 | `FichaFactura` · viajes | `Origen` + `Destino` | **Ruta** |
| 5 | `ListadoChoferes` | `Apellido y nombre` + `DNI` | **Chofer** — apellido y nombre subrayado, DNI en mono debajo |
| 6 | `ListadoPersonas` | `Nombre` + `Apellido` + `DNI` | **Persona** — apellido y nombre, DNI en mono debajo |
| 7 | `ListadoFlota` | `Marca` + `Modelo` | **Vehículo** |
| 8 | `ListadoTransportistas` | `Nombre` + `CUIT` | **Transportista** — nombre, CUIT en mono debajo |
| 9 | `ListadoTransportistas` | `Teléfono` + `Email` | **Contacto** |
| 10 | `ListadoClientes` | `Razón social` + `CUIT` | **Cliente** — razón social subrayada, CUIT en mono debajo |
| 11 | `ListadoClientes` | `Teléfono` + `Email` | **Contacto** |
| 12 | `FichaViaje` · historial | `Estado anterior` + `Estado nuevo` | la transición — y deja de ser tabla (FR-054) |
| 13 | `FichaFactura` · historial | `Estado anterior` + `Estado nuevo` | la transición — y deja de ser tabla (FR-054) |
| 14 | `PanelVencimientos` (choferes) | `Documento` + `Vencimiento` | **Documento** — nombre, fecha debajo |
| 15 | `PanelVencimientosFlota` | `Documento` + `Vencimiento` | **Documento** — nombre, fecha debajo |

Las **12 y 13 son fusiones que no terminan en columna**: el par `Estado anterior`+`Estado nuevo`
desaparece, pero lo reemplaza una entrada de línea de tiempo y no un encabezado nuevo (FR-047 ítem 7,
FR-054). Se cuentan igual, porque el par de columnas es lo que deja de existir.

**Quince fusiones repartidas en doce tablas** —`ListadoViajes`, `ListadoTransportistas` y
`ListadoClientes` llevan dos cada una—; las **nueve restantes** —`ListadoUsuarios`, `ListadoFacturas`,
`ListadoTiposVehiculo`, `TiposDocumentacion`, `TotalesPeriodo`, `TotalesFacturados`,
`PanelVencimientos` de facturación, y las tablas de documentación de `FichaChofer` y `FichaVehiculo`—
se revisaron y **no tienen ningún par de columnas que nombre un solo concepto**. Se las declara
explícitamente, con nombre, para que la revisión sepa que se las miró y no que se las salteó. Doce
más nueve son las 21 tablas.

**Los rótulos nuevos** —*Ruta*, *Asignación*, *Chofer*, *Persona*, *Vehículo*, *Transportista*,
*Cliente*, *Contacto*, *Documento*— son textos que introduce esta feature y los escribe ella (FR-065).
Sustantivo corto, mayúscula sólo en la primera palabra.

---

## §6. Los 16 formularios y sus secciones (FR-029)

Se numera **cuando hay dos o más secciones**. Un formulario que es un solo grupo no lleva chip ni
título: sus campos van directo dentro de la isla, con la misma barra de acciones al pie.

### De una sola sección — sin chip ni título (7)

| # | Formulario | Campos | Por qué es uno solo |
|---|---|---|---|
| 1 | `PantallaIngreso` | Nombre de usuario, Contraseña | Son las credenciales, nada más |
| 2 | `CambiarPassword` | Contraseña actual, Contraseña nueva, Repetir contraseña nueva | Un solo acto |
| 3 | `FormularioPersona` | Nombre, Apellido, DNI, Tipo, Teléfono, Email, Fecha de nacimiento | Identidad y contacto de una persona, sin nada más que agrupar |
| 4 | `PanelRoles` | Roles (casillas) | Un único conjunto |
| 5 | `FormularioTipoVehiculo` | Nombre | Un campo |
| 6 | `AsignacionViaje` | Chofer, Vehículo | Los dos son la asignación |
| 7 | `RegistrarCobro` (diálogo) | Fecha de cobro | Un campo |

### Agrupados en secciones numeradas (9)

| # | Formulario | Secciones |
|---|---|---|
| 8 | `FormularioUsuario` | **1. Acceso** — Nombre de usuario, Email, Contraseña inicial · **2. Estado y permisos** — Estado, Roles |
| 9 | `FormularioChofer` | **1. Identidad** — DNI, Nombre, Apellido, Fecha de nacimiento, CUIL · **2. Contacto** — Teléfono, Email · **3. Dependencia** — Transportista |
| 10 | `FormularioTransportista` | **1. Identidad fiscal** — Razón social o nombre completo, CUIT, Tipo de persona · **2. Contacto** — Teléfono, Email |
| 11 | `FormularioVehiculo` | **1. Identificación** — Patente, Marca, Modelo, Tipo de vehículo · **2. Dependencia y estado** — Transportista, Estado operativo |
| 12 | `FormularioViaje` | **1. Cliente y fecha** — Cliente, Fecha del viaje · **2. Recorrido** — Origen, Destino, Número de remito, Detalle de la carga · **3. Importe** — Importe en pesos |
| 13 | `FormularioCliente` | **1. Identidad fiscal** — Razón social, CUIT · **2. Contacto** — Teléfono, Email, Dirección |
| 14 | `AltaFactura` | **1. Cliente y comprobante** — Cliente, Tipo de comprobante, Tipo de facturación, Factura que reemplaza, Condición de venta · **2. Período** — Mes, Año · **3. Viajes a facturar** — el selector · **4. Datos del comprobante** — Fecha de facturación, Número de comprobante, CAE, Vencimiento del CAE, Vencimiento de pago, Detalle |
| 15 | `CorreccionFactura` | **1. Datos que no se modifican** — solo lectura · **2. Datos corregibles** — Detalle, CAE, Vencimiento del CAE, Vencimiento de pago |
| 16 | `EmpresaEmisora` | **1. Identidad fiscal** — Razón social, CUIT, Domicilio, Condición de IVA · **2. Datos de facturación** — Número de ingresos brutos, Inicio de actividades, Punto de venta · **3. Contacto y cobro** — CBU, Teléfono, Email · **4. Logo** — la carga de archivo |

**Los títulos de sección son textos nuevos** y los escribe esta feature (FR-065). Los **nombres de
campo no se tocan**: son los que los Módulos 1 a 6 fijaron y los que consultan los 138
`getByLabelText` de la suite (FR-066, FR-068).

### Los tres formularios que no entran en los 16

`FormularioDocumento`, `FormularioDocumentoVehiculo` y el alta en línea de `TiposDocumentacion` son
formularios **dentro de otra pantalla**, no pantallas propias: no aparecen en la lista de 42 ni en el
recuento de 16. Reciben el mismo tratamiento de campos, la misma barra de acciones al pie de su
contenedor y ninguna sección numerada — cada uno es un solo grupo.

---

## §7. Piezas nuevas del vocabulario

Lo que esta feature agrega a `compartido/ui/`. Todo lo demás son las primitivas del Módulo 7,
revestidas.

| Pieza | Qué es | Dónde se usa |
|---|---|---|
| `Lienzo` | Los dos orbes radiales en `fixed inset-0 -z-10 pointer-events-none` | Una vez, en la raíz. Nunca dentro de un contenedor con desplazamiento |
| `Isla` | Superficie de contenido: blanco 94 %, hairline, sombra difusa, radio de tarjeta | Toda tarjeta, listado y sección |
| `MenuDeFila` | El `···` de 28 px con su comportamiento de teclado (research §5) | Las 8 tablas que quedan con acciones secundarias de fila (§5) |
| `TokenDeIdentificador` | El recuadro con `#` atenuado y el número en mono | Enlaces de fila que son identificadores, encabezados de ficha |
| `EnlaceDeFila` | El dato que se busca con la vista, subrayado, con su identificador en mono debajo | Los 9 listados de índice |
| `FilaNavegable` | El `<tr>` con el `onClick` que replica el destino del `<a>` de la celda, más el chevron `›` al pasar el mouse. **Sin `tabIndex`** (research §6) | Los 9 listados de índice, junto a `EnlaceDeFila` |
| `SeccionNumerada` | Chip + título + explicación de un grupo de campos | Los 9 formularios agrupados |
| `BarraDeAcciones` | Leyenda de obligatorios a la izquierda, acciones a la derecha, primario último | Los 16 formularios. Fija al viewport en los de página completa; al pie de su contenedor en diálogo y sin sesión |
| `LineaDeTiempo` | El historial cronológico con el paso actual marcado | `FichaViaje`, `FichaFactura` — reemplaza a `Historial` |
| `AsideDeFicha` | La columna fija de 330 px con el dato de más valor | Las 5 fichas |
| `Callout` | El aviso de estado bloqueado con su salida | `FichaViaje`, `FichaFactura` |

**`Historial` se retira**: `LineaDeTiempo` es su reemplazo y ya se leía como línea de tiempo; lo que
cambia es que las dos tablas de historial de cuatro columnas pasan a usarla (FR-054).

---

## §8. El dato de más valor del aside (FR-053)

El aside **nunca se dibuja vacío**, y **no se trae ningún dato que la ficha no reciba hoy**.

| Ficha | Dato destacado | Forma |
|---|---|---|
| `FichaViaje` | Importe del viaje | Cifra destacada 32 px ExtraBold |
| `FichaFactura` | Importe total | Cifra destacada 32 px ExtraBold |
| `FichaChofer` | Semáforo de documentación | Pastilla de estado grande + el documento más próximo a vencer |
| `FichaVehiculo` | Estado operativo con su semáforo | Pastilla de estado grande + la patente en mono |
| `DetalleUsuario` | Roles | La lista de roles, cada uno como chip |

Debajo del dato destacado va lo que la ficha ya mostraba y que más se consulta —asignación y
facturación en el viaje, cliente y cobro en la factura, transportista en chofer y vehículo, estado y
último acceso en usuario—, en la jerarquía que le corresponde.
