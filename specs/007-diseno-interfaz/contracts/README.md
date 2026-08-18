# Contrato de interfaz — Módulo 7

**Fecha**: 2026-08-17 · **Spec**: [spec.md](../spec.md) · **Modelo**: [data-model.md](../data-model.md)

Los módulos anteriores usan este archivo para fijar **los textos exactos de cada pantalla**. Este
módulo no escribe textos nuevos salvo unos pocos, así que acá se fija otra cosa: **la API de las
primitivas** que las 42 pantallas van a usar, **los textos nuevos** que sí se introducen, y **el
inventario de lo que queda congelado** — que es lo que hace que una revisión pueda contar los
cambios.

No hay contrato de API HTTP en esta feature: ningún endpoint cambia de forma. La única modificación
de backend agrega dos elementos a una lista que ya se serializa igual (research §7).

---

## 1. API de las primitivas

Sólo la superficie pública: qué recibe cada pieza. El detalle de variantes está en
[data-model.md §2](../data-model.md).

### `Boton`

```
variante: 'primario' | 'secundario' | 'texto' | 'destructivo'   (obligatorio, sin valor por defecto)
tipo: 'button' | 'submit'
deshabilitado, onClick, children
```

**Obligar a declarar la variante es el punto.** El defecto que esta feature corrige nació de que
`button` sin variante heredaba el estilo de acción principal, y por eso la celda que abre una ficha
se veía como el botón más importante de la pantalla.

### `Campo`

```
id, etiqueta, obligatorio, error, ayuda, ancho: 'corto' | 'medio' | 'largo' | 'completo', children
```

`ancho` es lo que cumple FR-030: un CUIT es `corto`, una razón social es `largo`. El `id` se asocia a
la etiqueta y al mensaje de error, como ya se hace hoy: **eso no cambia**, porque es lo que los 138
`getByLabelText` de la suite consultan.

### `Dialogo`

```
titulo, onCerrar, children
```

Resuelve superficie, fondo, foco al abrir, retención del foco, `Escape` y devolución del foco al
origen. Lo usan tanto `DialogoConfirmacion` como los cuatro diálogos con campos adentro.

### `DialogoConfirmacion`

```
titulo, mensaje, etiquetaConfirmar (por defecto 'Confirmar'), onConfirmar, onCancelar
```

**Misma firma que hoy**, incluida `etiquetaConfirmar`, que trajo el Módulo 5. Los cinco envoltorios
de texto que ya delegan en él siguen funcionando sin cambios: sólo cambia de dónde se lo importa.

### `Estado`

```
juego: 'documentacion' | 'viaje' | 'factura' | 'vehiculo' | 'alta'
valor: el valor del juego
texto: la palabra que ya está en pantalla   (obligatorio)
```

`texto` es obligatorio a propósito: la primitiva **no puede** dibujarse sin palabra, que es la forma
de que FR-040 no dependa de que alguien se acuerde.

### `Aviso`

```
tono: 'exito' | 'advertencia' | 'error' | 'nota'
rol: 'status' | 'alert'     (obligatorio: reemplaza los role= escritos a mano, no los elimina)
children
```

### `EncabezadoDePantalla`

```
titulo, accionPrincipal, volverA
```

Además fija el título de la pestaña como `{titulo} · Sistema Integral de Gestión`. Como toda pantalla
lleva encabezado (FR-016), toda pantalla tiene título de pestaña sin que nadie se acuerde.

### `Paginacion`

```
pagina, total, tamanioPagina, nombrePlural, onCambiarPagina
```

**Misma firma que las cuatro que reemplaza**, incluido `nombrePlural`, que es lo que permite decir
"20 de 73 choferes" y no "20 de 73 elementos". Las cuatro copias se borran.

### `Listado`, `Ficha`, `Historial`, `EstadoVacio`, `Filtros`, `iconos`

Contenedores y utilidades sin lógica propia. `EstadoVacio` recibe el texto: los mensajes de listado
vacío y de sin coincidencias los fijan las specs de cada módulo y no se tocan.

---

## 2. Textos nuevos

Los únicos textos que esta feature escribe. Todo lo demás está congelado (sección 3).

**Secciones de navegación** — cinco rótulos:

| Sección | Rótulo |
|---|---|
| Operación | `Operación` |
| Padrones | `Padrones` |
| Seguimiento | `Seguimiento` |
| Configuración | `Configuración` |
| Administración | `Administración` |

**Entradas de menú nuevas** — las dos que agrega el backend (research §7). Se nombran así para no
repetir el error que el propio catálogo del servidor comenta, donde *Totales* y *Totales facturados*
quedaron indistinguibles:

| Código | Etiqueta | Ruta | Permiso |
|---|---|---|---|
| `vencimientos-choferes` | `Vencimientos de choferes` | `/choferes/vencimientos` | `choferes.gestionar` |
| `vencimientos-flota` | `Vencimientos de flota` | `/flota/vencimientos` | `flota.gestionar` |

**Título del documento**: `{título de la pantalla} · Sistema Integral de Gestión`

**Pantalla de inicio**: conserva el saludo y la lista de roles que ya tiene, y suma los accesos que
los permisos habilitan, con el **mismo rótulo** que cada opción trae del servidor. No inventa nombres.

---

## 3. Inventario de lo congelado

Lo que una revisión tiene que poder contar. Si algo de esta lista cambió, el rediseño se pasó de
alcance.

| Qué | Dónde está fijado | Cómo se verifica |
|---|---|---|
| **Los textos operativos** — errores, confirmaciones, estados vacíos, etiquetas de campo, verbos de botón | `contracts/README.md` de los Módulos 1 a 6 | 92 `getByText` + 108 `findByText` de la suite |
| **Los nombres accesibles y las etiquetas** | El marcado actual | 138 `getByLabelText` + 109 `getByRole` |
| **Las 42 direcciones** | `App.tsx` y el catálogo del servidor | El menú deja de funcionar si cambian |
| **Las operaciones de cada pantalla** | Las specs de los seis módulos | Los seis quickstarts |
| **Los permisos** | `CatalogoOpcionesMenu` y los endpoints | Recorrido con las tres cuentas |
| **Los formatos** de moneda y fecha | `compartido/moneda`, `compartido/fechas` | Sus tests propios |
| **El PDF de la factura** | Módulo 6 | Su test de igualdad byte a byte |

**Lo único que cambia de estructura visible**: las acciones de una ficha suben del pie al encabezado
(FR-031). Es el único movimiento de marcado que la revisión debería encontrar más allá de estilos y
del reemplazo de elementos por primitivas.

---

## 4. Lo que rompe un test, y lo que no

Regla operativa de la implementación, derivada de research §0.

**No rompe nada**: reordenar bloques, envolver elementos, cambiar clases, reemplazar un `<button>`
por `<Boton>`, mover las acciones de una ficha, agrupar campos, cambiar la disposición de un listado.

**Rompe**: quitar o renombrar una etiqueta, un nombre accesible, un rol o un texto visible. Convertir
una tabla en algo que no sea tabla. Sacar un `role="status"`.

**Las tres únicas líneas de la suite atadas a la estructura**, que hay que respetar:

1. `SelectorDeViajes.test.tsx:140` — un anuncio tiene que seguir estando dentro de un `[role="status"]`
2. `ListadoFacturas.test.tsx:122` — una fila de listado tiene que seguir siendo un `<tr>`
3. `ListadoViajes.test.tsx:193` — el listado de viajes no puede tener `<tfoot>`
