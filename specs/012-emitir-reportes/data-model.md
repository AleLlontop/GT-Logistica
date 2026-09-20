# Data Model: Emitir reportes (Módulo 12)

**Feature**: `012-emitir-reportes` | **Spec**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

## Lo primero: no hay migración

**Esta feature no agrega, no modifica y no borra ninguna tabla, columna, índice, restricción ni
secuencia.** No hay migración de EF Core. `GtDbContext` no cambia. Ninguna entidad de dominio se
toca.

Lo único que persiste es **una fila del catálogo de permisos** y **dos filas de la tabla puente
rol–permiso**, y las tres las escribe `SembradorInicial`, que ya es idempotente y corre en cada
arranque. Una instalación existente las obtiene al reiniciar el backend, sin migración y sin script.

Todo lo demás vive en memoria mientras dura la petición y se entrega. Un reporte no sobrevive a su
entrega (spec §Key Entities).

---

## 1 · Lo que sí se persiste: el permiso

### `Permiso` — una fila nueva

| Columna | Valor |
|---|---|
| `Codigo` | `reportes.emitir` |
| `Modulo` | `Reportes` |
| `Descripcion` | `Emitir los reportes de viajes, vencimientos y movimientos de caja en PDF y Excel` |

Constante: `CodigosPermiso.ReportesEmitir` en `GT.Domain/Usuarios/Rol.cs`, junto a los diecisiete que
ya están.

### `Rol` ↔ `Permiso` — dos filas nuevas

| Rol | ¿Recibe `reportes.emitir`? |
|---|---|
| Gerencia | **Sí** (FR-014) |
| Administrador del sistema | **Sí** (FR-014) |
| Tráfico | No |
| Administración de la empresa | No |

El reparto va en `PermisosPorRol` de `SembradorInicial`. Es el **primer permiso del sistema que
Tráfico y Administración de la empresa no reciben mientras Gerencia sí**: hasta el Módulo 11, Gerencia
siempre recibía un subconjunto de lo de Administración. Acá se invierte, y es deliberado — "sólo del
gerente" acota a los roles operativos, y el administrador lo recibe como recibe todo lo demás
(FR-014).

**No lleva entrada de menú**: no hay pantalla nueva. El catálogo de opciones es una lista de pares
permiso → pantalla y un permiso sin pantalla propia simplemente no figura (research §5).

---

## 2 · El modelo en memoria: `ReporteTabular`

El objeto único que las cinco fuentes producen y los dos armadores consumen (research §2). Vive en
`GT.Application/Reportes/`.

```
ReporteTabular
├── Encabezado : EncabezadoDeReporte
├── Columnas   : IReadOnlyList<ColumnaDeReporte>
├── Filas      : IReadOnlyList<IReadOnlyList<CeldaDeReporte>>
└── Totales    : IReadOnlyList<TotalDeReporte>
```

### `EncabezadoDeReporte` — las cinco piezas de FR-006

| Campo | Tipo | Contenido | Ejemplo |
|---|---|---|---|
| `Titulo` | `string` | El nombre del reporte | `Reporte de viajes` |
| `Filtros` | `string` | Los filtros en palabras, o la indicación de que no hay | `Cliente: Aceitera del Sur · Estado: En curso` / `Sin filtros aplicados` |
| `CantidadDeFilas` | `int` | Filas incluidas | `137` |
| `GeneradoEn` | `DateTime` | Instante de generación, UTC, del `TimeProvider` | — |
| `GeneradoPor` | `string` | Username de la sesión (`ClaimTypes.Name`) | `jlopez` |

`GeneradoEn` se **muestra** en hora de Argentina (`dd/MM/yyyy HH:mm`), con el desplazamiento fijo de
−03:00 que el sistema ya usa. Que el reloj entre al archivo es deliberado y contrario a la regla del
documento de la factura: research §7 explica por qué, y por qué acá **no** va un test de igualdad
byte a byte.

### `ColumnaDeReporte`

| Campo | Tipo | Para qué |
|---|---|---|
| `Nombre` | `string` | El encabezado, **idéntico al de la pantalla** (FR-008) |
| `Tipo` | `TipoDeColumna` | Decide alineación y formato en cada armador |

`TipoDeColumna`: `Texto` · `Fecha` · `Importe` · `Entero`.

### `CeldaDeReporte` — valor y tipo, nunca texto ya formateado

Cuatro constructores estáticos, cada uno anulable:

```
CeldaDeReporte.Texto(string? valor)
CeldaDeReporte.Fecha(DateOnly? valor)
CeldaDeReporte.Importe(decimal? valor)
CeldaDeReporte.Entero(int? valor)
```

**Es la pieza que hace convivir FR-010 y FR-011**: el PDF formatea el `decimal` como `$ 1.240.000,00`
y el Excel lo escribe como número con formato `#,##0.00`; si la celda llegara como texto ya
formateado, la planilla no podría sumarlo (research §2).

**Un valor nulo es una celda vacía, no un texto de relleno** (spec §Edge Cases): un viaje sin chofer
ni vehículo deja las dos celdas en blanco, como la pantalla. No se escribe "Sin asignar" ni un guion.

### `TotalDeReporte`

| Campo | Tipo | Ejemplo |
|---|---|---|
| `Etiqueta` | `string` | `Total de ingresos` |
| `Importe` | `decimal` | `184500.00` |

Se dibujan al pie de la tabla, en los dos formatos. **El Excel los escribe como número**, igual que
las celdas de importe, para que SC-004 se pueda comprobar con la calculadora de la planilla.

---

## 2.5 · Las palabras de los estados: no existen en el backend y hay que escribirlas

FR-010 exige que los estados aparezcan **con la misma palabra que la pantalla usa, nunca con un
código interno**. Hay una trampa acá, y descubrirla tarde significa entregar los cinco reportes con
el código adentro:

**Las clases `NombresDeEstado*` del backend no traducen a palabras.** Devuelven el código del JSON en
camelCase, que es para lo que existen (convención [003]):

```csharp
// GT.Application/Choferes/Dtos.cs — lo que HAY hoy
public static string DelDocumento(DocumentacionEstado estado) => EnCamelCase(estado.ToString());
// DocumentacionEstado.ProximaAVencer  →  "proximaAvencer"   ← un código, no una palabra
```

Las palabras visibles viven **sólo en TypeScript**, en los mapas de cada módulo
(`servicioViajes.ts` → `NOMBRES_DE_ESTADO`, `choferes/servicios/estados.ts` y
`flota/servicios/estados.ts` → `TEXTO_ESTADO_DOCUMENTO`, `caja/servicios/…` → los dos tipos de
movimiento). Un reporte que se arma en C# no puede leerlas.

### La decisión

**Cada módulo gana un método `EnPantalla(...)` en la clase `NombresDeEstado*` que ya tiene**, al lado
de la traducción al JSON que ya vive ahí. Es aditivo: no cambia ninguna firma, ningún contrato JSON,
ninguna pantalla y ninguna suite existente.

| Archivo | Método nuevo | Enum | Palabras |
|---|---|---|---|
| `GT.Application/Viajes/NombresDeEstadoViaje.cs` | `EnPantalla(EstadoViaje)` | `EstadoViaje` | `Pendiente` · `En curso` · `Rendido` · `Anulado` · `Facturado` |
| `GT.Application/Choferes/Dtos.cs` (`NombresDeEstado`) | `EnPantalla(DocumentacionEstado)` | `DocumentacionEstado` | `Al día` · `Próxima a vencer` · `Vencida` |
| `GT.Application/Flota/Dtos.cs` (`NombresDeEstadoFlota`) | `EnPantalla(DocumentacionEstado)` | `DocumentacionEstado` | `Al día` · `Próxima a vencer` · `Vencida` |
| `GT.Application/Caja/NombresDeEstadoCaja.cs` | `EnPantalla(TipoMovimientoCaja)` | `TipoMovimientoCaja` | `Ingreso` · `Egreso` |
| `GT.Application/Facturacion/SituacionDeVencimiento.cs` **(nuevo)** | `Para(int dias)` | — | `Vencida hace N días` · `Vence hoy` · `Vence en N días` |

Las palabras se copian **literalmente** de los mapas de TypeScript nombrados arriba, incluidas la
tilde de `Al día` y la de `Próxima a vencer`, y el singular de `1 día`.

### Por qué así y no llevando las palabras al backend

Lo puro según la convención [009] sería que el backend fuera el dueño único de cada palabra y que las
pantallas la recibieran ya armada en el JSON. Se descarta: **cambia el contrato de los listados de
cinco módulos y toca unas diez pantallas**, un refactor mucho más grande que esta feature, contra el
Principio I y contra el Principio III —la lista de cambios a módulos anteriores es cerrada—.

Así que **la copia se acepta y se declara**, que es lo contrario de que aparezca sin que nadie la
note. Lo que la cierra es un test unitario por módulo que fija las palabras exactas en C#, más un
comentario en cada mapa de TypeScript apuntando a él. Es lo más lejos que llega, entre dos lenguajes,
el precedente de [003]: *cuando una regla se ejecuta en dos lados, va un test que compara las dos
sobre el mismo dato*.

**Lo que esto agrega a la lista de cambios a módulos anteriores del plan**: cinco entradas, las cinco
**aditivas** —cuatro métodos nuevos y un archivo nuevo—, sin una sola modificación de comportamiento
existente.

---

## 3 · Los cinco reportes, columna por columna

Las columnas y sus nombres salen de FR-008 y **son los mismos que la pantalla muestra**. Donde la
pantalla une dos datos en una celda, el archivo los abre en dos columnas: en un archivo que se va a
ordenar y filtrar cada dato va en su columna.

### 3.1 · Viajes — `Reporte de viajes`

Fuente: `ConsultarViajes` con los filtros de la pantalla, sin paginar (research §3).
Orden: fecha descendente, luego número descendente — **el mismo `OrderBy` del listado**.

| # | Columna | Tipo | De dónde sale |
|---|---|---|---|
| 1 | Número | `Entero` | `ViajeListado.Numero` |
| 2 | Fecha | `Fecha` | `Fecha` |
| 3 | Cliente | `Texto` | `Cliente.Nombre` |
| 4 | Origen | `Texto` | `Origen` |
| 5 | Destino | `Texto` | `Destino` |
| 6 | Chofer | `Texto` | `Chofer?.Nombre` — **vacía si no hay** |
| 7 | Vehículo | `Texto` | `Vehiculo?.Nombre` — **vacía si no hay** |
| 8 | Transportista | `Texto` | `Transportista?.Nombre` — **vacía si no hay** |
| 9 | Estado | `Texto` | `NombresDeEstadoViaje.EnPantalla` — la palabra, **no** el código del JSON (FR-010, §2.5) |
| 10 | Importe | `Importe` | `Importe` |

Totales: **Total de importes**.

*Ruta* (origen + destino) y *Asignación* (chofer + vehículo) se abren en dos columnas cada una
(FR-008). Las señales que la pantalla agrega adentro de una celda —*Demorado*, *Carga retroactiva*,
`(inactivo)`— **no son columnas** y no van: FR-008 enumera diez y sólo diez.

Filtros en palabras: `Cliente` · `Transportista` · `Estado` · `Desde` · `Hasta` · `Búsqueda`, sólo
los aplicados, separados por ` · `. Cliente y transportista se resuelven a su nombre en el backend
(research §9).

### 3.2 · Vencimientos de choferes — `Reporte de vencimientos de choferes`

Fuente: `ConsultarVencimientos` (Choferes), completa. Orden: por urgencia, tal como la devuelve.

| # | Columna | Tipo |
|---|---|---|
| 1 | Chofer | `Texto` — `Apellido, Nombre` |
| 2 | Transportista | `Texto` |
| 3 | Documento | `Texto` — nombre del tipo |
| 4 | Fecha de vencimiento | `Fecha` |
| 5 | Estado | `Texto` — `NombresDeEstado.EnPantalla` sobre `DocumentacionEstado` (§2.5) |

Sin totales. Filtros: `Sin filtros aplicados`.

### 3.3 · Vencimientos de flota — `Reporte de vencimientos de flota`

Fuente: `ConsultarVencimientosFlota`, completa. Mismo orden por urgencia.

| # | Columna | Tipo |
|---|---|---|
| 1 | Patente | `Texto` |
| 2 | Transportista | `Texto` |
| 3 | Documento | `Texto` |
| 4 | Fecha de vencimiento | `Fecha` |
| 5 | Estado | `Texto` — `NombresDeEstadoFlota.EnPantalla` sobre `DocumentacionEstado` (§2.5) |

Sin totales. Filtros: `Sin filtros aplicados`.

Los dos paneles muestran el estado **del documento**, no el del chofer ni el del vehículo, así que
las tres palabras son las mismas en §3.2 y acá: `Al día`, `Próxima a vencer`, `Vencida`.

### 3.4 · Vencimientos de facturas — `Reporte de vencimientos de facturas`

Fuente: `ConsultarVencimientos` (Facturación), completa.

| # | Columna | Tipo | De dónde sale |
|---|---|---|---|
| 1 | Cliente | `Texto` | `FilaDeVencimiento.Cliente` |
| 2 | Número | `Texto` | `NumeroComprobante` — **ya armado por el backend** ([009]) |
| 3 | Importe | `Importe` | `Total` |
| 4 | Vencimiento | `Fecha` | `VencimientoPago` |
| 5 | Situación | `Texto` | `SituacionDeVencimiento.Para(Dias)` (§2.5) |

Totales: **Total de importes** (FR-009).

*Situación* es la única columna **calculada**: la pantalla muestra `Vencida hace N días`, `Vence hoy`
o `Vence en N días` según el signo de `Dias`, y el reporte lleva **el mismo texto**.

La función que la pantalla usa hoy es `situacion(dias)` de
`frontend/src/modules/facturacion/servicios/api.ts` y está escrita en **TypeScript**, así que el
reporte —que se arma en C#— no puede llamarla. Se escribe en C# como
`GT.Application/Facturacion/SituacionDeVencimiento.cs`, con **las tres ramas exactas** de la versión
de TypeScript, incluida la del cero: `Vence en 0 días` sería correcto y no es lo que nadie diría.
Es una segunda copia declarada, con la misma regla de §2.5.

### 3.5 · Movimientos de caja — `Reporte de movimientos de caja`

Fuente: `ConsultarMovimientos` con los filtros de la pantalla, sin paginar. Orden: el del listado.

| # | Columna | Tipo |
|---|---|---|
| 1 | Fecha | `Fecha` |
| 2 | Tipo | `Texto` — `NombresDeEstadoCaja.EnPantalla`: `Ingreso` / `Egreso`, no `ingreso` / `egreso` (§2.5) |
| 3 | Importe | `Importe` |
| 4 | Concepto | `Texto` |
| 5 | Responsable | `Texto` |
| 6 | Referencia | `Texto` — **vacía si no hay** |

Totales, los tres de FR-009: **Total de ingresos** · **Total de egresos** · **Neto**.

`Neto = ingresos − egresos`. Se calcula sobre **las filas del reporte**, que son las del filtro: es
lo que hace que SC-004 se compruebe sumando las propias filas del archivo.

Filtros en palabras: `Desde` · `Hasta` · `Caja`. La caja se resuelve a su nombre visible en el
backend.

---

## 4 · Las reglas que gobiernan un reporte

`GT.Application/Reportes/ReglasDeReporte.cs`:

| Regla | Valor | Requisito |
|---|---|---|
| `TopeDeFilas` | `5000` | FR-016 |

**Un solo valor, en el backend.** El frontend no lo conoce y no lo pre-verifica: el mensaje que lo
nombra lo arma el servidor y viaja en el `409` (research §4).

### Su forma exacta

Un `const` no se puede bajar para un test, y research §13 pide justamente eso —probar el `409` **sin
sembrar 5.001 filas**—. Así que:

```csharp
public sealed record OpcionesDeReporte(int TopeDeFilas)
{
    public const int TopePorDefecto = 5000;          // el valor de FR-016, escrito una sola vez
    public static OpcionesDeReporte PorDefecto => new(TopePorDefecto);
}
```

Se registra en el contenedor con `OpcionesDeReporte.PorDefecto` y las fuentes lo reciben por
inyección. La aplicación de prueba lo reemplaza por `new OpcionesDeReporte(3)` y el test del tope
siembra cuatro filas.

**El `5000` sigue estando escrito una sola vez**, que es lo que la regla perseguía; lo que se agrega
es poder inyectarlo, no un segundo lugar donde vive.

**El cero no es una regla del backend.** FR-003 es sobre la disponibilidad del botón y la decide la
pantalla, que ya sabe cuántas filas tiene. El servidor no rechaza un reporte de cero filas.

---

## 5 · Los formatos

`FormatoDeReporte`: `Pdf` · `Excel`. Dos valores, y son los dos que FR-002 fija.

De este enum salen **tres** cosas, todas en un solo lugar:

| Derivado | `Pdf` | `Excel` |
|---|---|---|
| Tipo de contenido | `application/pdf` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` |
| Extensión del archivo | `.pdf` | `.xlsx` |
| `Content-Disposition` | `inline` | `attachment` |

Que las tres salgan del mismo enum es lo que impide que el backend sirva un `.xlsx` diciendo que es
un PDF.

---

## 6 · El nombre del archivo (FR-012)

`NombreDeArchivoDeReporte.Para(reporte, formato, generadoEn)` — función pura, del backend, con test
propio.

```
viajes-2026-09-20-1432.pdf
vencimientos-choferes-2026-09-20-1432.xlsx
vencimientos-flota-2026-09-20-1432.pdf
vencimientos-facturas-2026-09-20-1432.xlsx
movimientos-caja-2026-09-20-1432.pdf
```

Fecha **y hora** de Argentina: sin la hora, dos reportes del mismo listado el mismo día colisionan en
la carpeta, que es justo lo que FR-012 quiere evitar. Sólo ASCII, minúsculas y guiones — así viaja
en el `filename` simple del `Content-Disposition` y el frontend lo extrae con una expresión regular
de una línea (research §8).

---

## 7 · Lo que esta feature **no** agrega

Para que la revisión lo pueda contar:

- **Ninguna tabla, columna, índice, `CHECK` ni migración.**
- **Ningún historial de reportes generados.** No se guarda quién generó qué ni cuándo (spec
  §Key Entities).
- **Ningún archivo en disco.** El reporte se arma en memoria y se entrega; a diferencia del documento
  de la factura, no se escribe en el volumen de archivos.
- **Ninguna consulta de negocio nueva.** Las cinco fuentes usan las consultas que ya alimentan las
  cinco pantallas.
- **Ninguna variable de entorno.**
