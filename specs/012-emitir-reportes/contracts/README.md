# Contrato de UI y HTTP — Emitir reportes (Módulo 12)

**Feature**: `012-emitir-reportes` | **Spec**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

## Pantallas

**Esta feature no agrega ninguna pantalla ni ninguna ruta del frontend.** Agrega una acción sobre
cinco pantallas que ya existen, y nada más (spec §Aclaración sobre el nombre, FR-001).

| Ruta existente | Pantalla | Componente que la dibuja hoy | Permiso para mirarla | Filtros que viajan al reporte |
|---|---|---|---|---|
| `/viajes` | Viajes | `modules/viajes/paginas/ListadoViajes` | `viajes.consultar` | cliente, transportista, estado, desde, hasta, búsqueda |
| `/choferes/vencimientos` | Vencimientos de choferes | `modules/choferes/paginas/PanelVencimientos` | `choferes.vencimientos.consultar` | ninguno |
| `/flota/vencimientos` | Vencimientos de flota | `modules/flota/paginas/PanelVencimientosFlota` | `flota.vencimientos.consultar` | ninguno |
| `/facturas/vencimientos` | Vencimientos de facturas | `modules/facturacion/paginas/PanelVencimientos` | `facturacion.consultar` | ninguno |
| `/movimientos-caja` | Movimientos de caja | `modules/caja/paginas/ConsultaDeMovimientos` | `caja.consultar` | desde, hasta, caja |

**Ninguna otra pantalla recibe la acción** (FR-001). Las otras diez pantallas con listado del sistema
—liquidaciones, adelantos, cajas, facturas, choferes, flota, clientes, transportistas y las dos de
totales— siguen exactamente como están (spec §Assumptions).

---

## El componente compartido

Uno solo, `modules/reportes/componentes/GenerarReporte.tsx`, usado por las cinco pantallas. Lo que
las cinco le pasan:

```ts
interface Props {
  /** Cuál de los cinco. Decide la ruta, el título y los filtros que se mandan. */
  reporte: 'viajes' | 'vencimientos-choferes' | 'vencimientos-flota'
         | 'vencimientos-facturas' | 'movimientos-caja'
  /** Los filtros aplicados en la pantalla, ya como pares para la query. Vacío en los tres paneles. */
  filtros?: Record<string, string | number>
  /** Cuántas filas tiene el listado que se está mirando. Decide FR-003. */
  cantidadDeFilas: number
  /** `reportes.emitir`. Sin él no se dibuja nada (FR-013). */
  puedeEmitir: boolean
}
```

**`puedeEmitir` falso → el componente devuelve `null`.** No un botón deshabilitado: la acción no
existe para ese usuario (FR-013, SC-005). Es la regla 2 de `MenuDeFila` aplicada acá: ocultar es
cortesía, la restricción es el `403`.

### Los tres estados de la acción

| Situación | Qué se ve | Requisito |
|---|---|---|
| Hay filas y no se está generando | `Generar reporte`, secundario, habilitado | FR-001, FR-005 |
| `cantidadDeFilas === 0` | `Generar reporte` **deshabilitado**, y al lado *"No hay filas para reportar."*, enlazado con `aria-describedby` | FR-003 |
| Generando | `Generar reporte` **deshabilitado**, y la región viva dice *"Generando el reporte…"* | FR-004 |

### El diálogo de formato

Se abre con `Generar reporte`. Usa el `Dialogo` de `compartido/ui` — foco retenido, `Escape`, foco
devuelto al disparador (research §12).

| Elemento | Texto | Variante |
|---|---|---|
| Título | `Generar reporte` | — |
| Bajada | `Elegí el formato del archivo.` | — |
| Botón 1 | `PDF` | `primario` |
| Botón 2 | `Excel` | `secundario` |
| Botón 3 | `Cancelar` | `secundario` |

**El botón del formato ejecuta la generación**: no hay un *Generar* aparte, para que SC-001 se cumpla
en dos clics. El orden PDF → Excel es el que fija FR-002.

**Cancelar, `Escape` y el clic afuera cierran sin generar nada** y dejan la pantalla como estaba, con
sus filtros y su página (FR-002). El componente no toca ningún estado de la pantalla que lo contiene.

### La región viva

Un `<p role="status">` — el `role` va **en el `<p>` del mensaje**, no en un contenedor que lo envuelva
(convención [008]). Todo resultado que aparece sin que la pantalla cambie se anuncia así (convención
[003]).

| Momento | Texto |
|---|---|
| Al empezar | `Generando el reporte…` |
| PDF, abierto en una pestaña | `Se abrió el reporte en una pestaña nueva.` |
| PDF, con emergentes bloqueados → descarga | `Se descargó el reporte.` |
| Excel | `Se descargó el reporte.` |
| Rechazo | El mensaje del servidor, tal cual llega |

El rechazo va en un `role="alert"` aparte, no en la región de estado.

---

## Endpoints

Cinco `GET`, los cinco declarados juntos en `GT.Api/Reportes/ReportesEndpoints.cs` para que la
revisión pueda contarlos (research §6).

Convención [005]: `400` cuando el problema está en lo que se pidió —el formato— y `409` cuando está en
el estado de los datos —cuántas filas hay ahora— (research §4). Las cinco rutas son **literales** y
conviven con rutas `{id:int}` que ya llevan su restricción de tipo; sin ella quedarían inalcanzables y
no fallaría ni al compilar ni al arrancar (convención [005]).

| Método | Ruta | Política (los **dos** permisos) | Parámetros además de `formato` |
|---|---|---|---|
| `GET` | `/api/viajes/reporte` | `viajes.consultar` **+** `reportes.emitir` | `clienteId`, `transportistaId`, `estado`, `desde`, `hasta`, `busqueda` |
| `GET` | `/api/vencimientos/reporte` | `choferes.vencimientos.consultar` **+** `reportes.emitir` | — |
| `GET` | `/api/flota/vencimientos/reporte` | `flota.vencimientos.consultar` **+** `reportes.emitir` | — |
| `GET` | `/api/facturas/vencimientos/reporte` | `facturacion.consultar` **+** `reportes.emitir` | — |
| `GET` | `/api/movimientos-caja/reporte` | `caja.consultar` **+** `reportes.emitir` | `desde`, `hasta`, `cajaId` |

`formato` es obligatorio y vale `pdf` o `excel`. Los demás parámetros son **los mismos, con los
mismos nombres, que el listado de esa pantalla ya recibe**: el reporte no inventa un enlace de
parámetros propio. `pagina` **no** se manda: el reporte abarca todas las filas del filtro (FR-007).

### Respuesta exitosa

| Cabecera | `formato=pdf` | `formato=excel` |
|---|---|---|
| `Content-Type` | `application/pdf` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| `Content-Disposition` | `inline; filename="viajes-2026-09-20-1432.pdf"` | `attachment; filename="viajes-2026-09-20-1432.xlsx"` |
| `X-Content-Type-Options` | `nosniff` | `nosniff` |

El nombre lo arma el backend una sola vez y viaja armado (FR-012, convención [009]). El frontend lo
lee del `Content-Disposition` —mismo origen, cabecera legible— y **no lo vuelve a componer**.

### Cuerpos de error (formato común, `{ codigo, mensaje }`)

| Estado | Código | Mensaje | Cuándo |
|---|---|---|---|
| `400` | `formato_invalido` | `El formato del reporte tiene que ser PDF o Excel.` | `formato` ausente o distinto de `pdf`/`excel` |
| `403` | `sin_permiso` | `No tenés permiso para emitir reportes. Pedíselo a quien administra el sistema.` | Falta `reportes.emitir` **o** falta el permiso de lectura de la pantalla (FR-015) |
| `409` | `tope_de_filas_superado` | `El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a intentar.` | FR-016 |
| `500` | `reporte_no_generado` | `No pudimos generar el reporte. Volvé a intentar en unos minutos.` | Falla del motor de PDF o de planilla (FR-017) |

El `409` lleva además `filas` y `tope` en el cuerpo, para que la pantalla pueda usarlos si alguna vez
los necesita sin volver a parsear el texto. **El mensaje ya viene armado**: el frontend no compone
ninguna de estas cuatro frases (research §4).

**Los dos permisos dan el mismo `403`** y el mismo texto. Distinguirlos le diría a quien invoca la
ruta a mano exactamente qué permiso le falta, que no ayuda a nadie que esté operando la aplicación.

### El `409` vuelve como resultado, no como excepción

En el servicio del frontend, `tope_de_filas_superado` se traduce a un resultado que la pantalla
muestra, igual que los `409` del Módulo 9 y del Módulo 11 (convención [009]). No se deja escapar como
excepción, porque no es un fallo: es una respuesta esperada.

---

## Contenido del archivo

Las columnas, los totales y el encabezado de cada uno de los cinco reportes están en
[../data-model.md](../data-model.md) §3. Lo que el contrato fija acá son las reglas transversales:

1. **El encabezado lleva las cinco piezas de FR-006**, en los dos formatos, en este orden: título ·
   filtros en palabras · filas incluidas · instante de generación · quién lo generó.
2. **Los nombres de las columnas son los de la pantalla** (FR-008). Un renombre en la pantalla es un
   renombre en el reporte.
3. **Los estados van con la palabra de la pantalla**, nunca con el código interno del JSON y nunca
   sólo por color (FR-010, convención [003]).
4. **Los importes del PDF van en pesos argentinos**, `$ 1.240.000,00` (FR-010, Principio II).
5. **Los importes y las fechas del Excel son número y fecha**, no texto (FR-011, SC-007).
6. **Una celda sin dato queda vacía.** Ni guion, ni "Sin asignar", ni "—" (spec §Edge Cases).
7. **Todas las filas del filtro, en el orden de la pantalla** (FR-007, SC-002).

### Textos del encabezado

| Pieza | Forma | Ejemplo |
|---|---|---|
| Título | `Reporte de <pantalla>` | `Reporte de movimientos de caja` |
| Filtros | Pares `Etiqueta: valor` unidos por ` · ` | `Desde: 01/09/2026 · Hasta: 20/09/2026 · Caja: Caja de Marta Ruiz` |
| Filtros, sin ninguno | Literal | `Sin filtros aplicados` |
| Filas | `Filas incluidas: N` | `Filas incluidas: 137` |
| Generación | `Generado el dd/MM/yyyy a las HH:mm` | `Generado el 20/09/2026 a las 14:32` |
| Autor | `Generado por <username>` | `Generado por jlopez` |

La hora es **de Argentina**, con el desplazamiento fijo de −03:00 que el sistema ya usa.

---

## Reglas transversales del frontend que esta feature toca

- **`obtenerPdf` de Facturación se lleva a `compartido/archivos.ts` como `obtenerArchivo`** y la copia
  del módulo se reemplaza en esta misma feature (convención [009], research §8). La prueba de que es
  un refactor y no un cambio de comportamiento es que **la suite de Facturación pasa sin
  modificarse**.
- **`EncabezadoDePantalla` gana una prop opcional `accionSecundaria`**, dibujada a la izquierda de
  `accionPrincipal` en la misma fila. Es aditiva: ninguna de las 42 pantallas deja de compilar ni
  cambia. Va así y no pasando la acción secundaria por `accionPrincipal`, porque un slot llamado
  *acción principal* que recibe una secundaria es exactamente la clase de cosa que la convención
  [007] señala.
- **`sesion.ts` suma `reportesEmitir: 'reportes.emitir'`** y `App.tsx` calcula `puedeEmitirReportes`
  una vez y lo pasa a las cinco pantallas.
