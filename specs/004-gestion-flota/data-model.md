# Modelo de datos: Gestión de flota (Módulo 4)

**Feature**: `004-gestion-flota` | **Fecha**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

Las decisiones que explican por qué el modelo es así están en [research.md](./research.md); acá va el
qué.

## Panorama

Tres tablas nuevas y una columna agregada a una tabla existente.

```
Transportistas (M3) ──1──*── Vehiculos ──*──1── TiposVehiculo
       │                         │
       │                         1
       │                         │
       └──1──*── Choferes        *
                    │      DocumentacionesVehiculo
                    │                 │
                    1                 *
                    │                 │
                    *                 1
              Documentaciones ──*──1── DocumentacionTipos
                                          (+ Ambito, NUEVA columna)
```

| Tabla | Estado | Baja |
|---|---|---|
| `TiposVehiculo` | **NUEVA** | Lógica (`Activo`) |
| `Vehiculos` | **NUEVA** | Lógica (`Activo`) |
| `DocumentacionesVehiculo` | **NUEVA** | **Física** — es la única del módulo que se borra (FR-027, FR-028) |
| `DocumentacionTipos` | **MODIFICADA** — suma `Ambito` | Lógica, ya existente |
| `Transportistas` | Sin cambios de esquema | Cambia sólo la **regla** de baja (FR-008d) |

Ningún estado derivado se almacena. Ni el estado de un documento, ni el estado general de
documentación de un vehículo, ni el estado operativo que el listado muestra: los tres se calculan al
leer (FR-014, FR-019, FR-033 y convención [003] de `CLAUDE.md`).

---

## TipoVehiculo (tabla `TiposVehiculo`) — NUEVA

Categoría de unidad: tractor, semirremolque, chasis, utilitario. El catálogo **arranca vacío** y se
completa desde la pantalla del módulo; no se precarga por migración.

| Columna | Tipo | Nulo | Reglas |
|---|---|---|---|
| `Id` | `int` identity | no | PK |
| `Nombre` | `nvarchar(100)` | no | **Único** en el catálogo (FR-009) |
| `Activo` | `bit` | no | `true` por defecto. `false` es la baja lógica (FR-009) |

**Índices**

- `IX_TiposVehiculo_Nombre` — único sobre `Nombre`. Cierra la carrera entre dos altas simultáneas del
  mismo nombre, que ninguna consulta previa evita.

**Reglas**

- La baja se rechaza si el tipo tiene **cualquier** vehículo asociado, activo o no, informando cuántos
  son (FR-010). Un vehículo dado de baja sigue mostrando su tipo (FR-011), así que el tipo tiene que
  seguir existiendo.
- Los tipos inactivos no se ofrecen al registrar ni al modificar un vehículo; los ya registrados con
  un tipo inactivo lo conservan y lo siguen mostrando (FR-011).
- Nunca se borra físicamente (FR-009, FR-028).

**Navegación**: `ICollection<Vehiculo> Vehiculos` — es lo que se cuenta al intentar la baja.

---

## Vehiculo (tabla `Vehiculos`) — NUEVA

Unidad de la flota. Entidad principal del módulo.

| Columna | Tipo | Nulo | Reglas |
|---|---|---|---|
| `Id` | `int` identity | no | PK |
| `Patente` | `nvarchar(10)` | no | **Única en toda la flota, incluidos los dados de baja** (FR-002). Guardada ya normalizada: mayúsculas, sin espacios ni guiones ni puntos (FR-003) |
| `Marca` | `nvarchar(50)` | no | Obligatoria, `Trim` al guardar (FR-006) |
| `Modelo` | `nvarchar(50)` | no | Obligatorio, `Trim` al guardar (FR-006) |
| `TipoVehiculoId` | `int` | no | FK a `TiposVehiculo`. Debe estar activo al crear o modificar (FR-005) |
| `TransportistaId` | `int` | no | FK a `Transportistas`. Debe estar activo al crear o modificar (FR-008a) |
| `EstadoOperativo` | `tinyint` | no | `VehiculoEstado`: `Disponible = 1`, `FueraDeServicio = 2` (FR-012). **Es lo que eligió el operador**, no lo que el listado muestra |
| `Activo` | `bit` | no | `true` por defecto. `false` es la baja lógica (FR-001) |

**Índices**

- `IX_Vehiculos_Patente` — **único**, sin filtro por `Activo`: la patente de una unidad dada de baja
  sigue ocupada (FR-002, FR-008f).
- `IX_Vehiculos_TransportistaId` — para el filtro por transportista del listado (FR-030) y para contar
  los vehículos activos de un transportista al intentar darlo de baja (FR-008d).
- `IX_Vehiculos_TipoVehiculoId` — para el filtro por tipo (FR-030) y para contar los vehículos de un
  tipo al intentar darlo de baja (FR-010).

**Claves foráneas**: las dos con `DeleteBehavior.Restrict`. Nada se borra físicamente en este módulo
salvo los documentos, así que el borrado en cascada no tiene a quién servir.

**Formato de la patente** (FR-004), validado sobre el valor ya normalizado:

| Formato | Expresión | Ejemplo |
|---|---|---|
| Viejo | `^[A-Z]{3}[0-9]{3}$` | `ABC123` |
| Mercosur | `^[A-Z]{2}[0-9]{3}[A-Z]{2}$` | `AB123CD` |

**Reglas de estado operativo**

- Al guardar, `Disponible` se rechaza si el estado general de documentación es `vencida` o
  `sinDocumentacion`, informando qué documento lo impide (FR-013, FR-014a).
- **Un vehículo recién registrado no tiene documentos**, así que su alta sólo admite
  `fuera de servicio` (US2 esc. 8). El formulario lo dice de entrada.
- El valor guardado nunca se sobrescribe. Lo que el listado muestra y filtra se deriva al consultar
  (FR-014, ver más abajo).

**Navegación**: `ICollection<DocumentacionVehiculo> Documentacion`, `TipoVehiculo? Tipo`,
`Transportista? Transportista`.

---

## DocumentacionVehiculo (tabla `DocumentacionesVehiculo`) — NUEVA

Documento obligatorio de una unidad: VTV, seguro, RUTA, cédula verde. Tabla propia, separada de
`Documentaciones` (research §1).

| Columna | Tipo | Nulo | Reglas |
|---|---|---|---|
| `Id` | `int` identity | no | PK |
| `VehiculoId` | `int` | no | FK a `Vehiculos` |
| `DocumentacionTipoId` | `int` | no | FK a `DocumentacionTipos`. El tipo debe estar **activo y de ámbito `vehiculo`** (FR-017a) |
| `Numero` | `nvarchar(50)` | no | Obligatorio, **sin unicidad**: una póliza conserva su número al renovarse (FR-016) |
| `FechaEmision` | `date` | no | |
| `FechaVencimiento` | `date` | no | **Posterior** a la emisión, no igual (FR-018) |
| `ArchivoRuta` | `nvarchar(400)` | **sí** | Ruta relativa dentro del volumen. `null` es un documento sin respaldo, que es válido (FR-016a) |
| `ArchivoNombre` | `nvarchar(255)` | sí | Nombre original, sólo para mostrar y descargar |
| `ArchivoTipoContenido` | `nvarchar(100)` | sí | `application/pdf`, `image/jpeg` o `image/png`, **determinado por la firma del archivo**, nunca por la extensión ni por el `Content-Type` declarado (FR-025) |

**No tiene columna de estado**: se calcula al leer (FR-019).

**No tiene `Activo`**: es la única entidad del módulo que se borra físicamente (FR-027, FR-028). Un
documento cargado por error no es historia que convenga conservar, y además taparía el estado real del
vehículo, porque el vigente de cada tipo es el de vencimiento más lejano.

**Propiedad calculada**: `TieneArchivo => ArchivoRuta is not null` — mapeada con `Ignore`, es lo que la
ficha usa para distinguir, documento por documento, cuál tiene respaldo (FR-016a).

**Índices**

- `IX_DocumentacionesVehiculo_VehiculoId_TipoId_Vencimiento` — sobre
  `(VehiculoId, DocumentacionTipoId, FechaVencimiento DESC)`. Cubre la ficha y, sobre todo, la
  elección del documento vigente de cada tipo sin ordenar en memoria (FR-024).
- `IX_DocumentacionesVehiculo_DocumentacionTipoId` — para contar los documentos de un tipo al intentar
  darlo de baja o cambiarle el ámbito (FR-017b, FR-017d).
- `IX_DocumentacionesVehiculo_FechaVencimiento` — para el panel de vencimientos y el filtro por estado
  calculado (FR-033, FR-035).

**Claves foráneas**: las dos con `DeleteBehavior.Restrict`. Borrar un documento es una operación
explícita del operador con confirmación previa, nunca un efecto colateral de borrar otra cosa.

---

## DocumentacionTipo (tabla `DocumentacionTipos`) — MODIFICADA

Una columna nueva. El resto queda tal como lo dejó el Módulo 3.

| Columna | Tipo | Nulo | Cambio |
|---|---|---|---|
| `Id` | `int` identity | no | — |
| `Nombre` | `nvarchar(100)` | no | — (sigue único en **todo** el catálogo, no por ámbito) |
| `DiasAvisoVencimiento` | `int` | no | — |
| `Activo` | `bit` | no | — |
| `Ambito` | `tinyint` | no | **NUEVA.** `DocumentacionAmbito`: `Chofer = 1`, `Vehiculo = 2` (FR-017) |

**Reglas nuevas**

- El ámbito es **obligatorio** al crear y al modificar un tipo (FR-017).
- Cada módulo ofrece únicamente los tipos activos de su ámbito: el formulario de documento de vehículo
  no muestra los de chofer, ni al revés (FR-017a).
- La baja de un tipo se rechaza si tiene documentos asociados, **contando las dos tablas** —choferes y
  vehículos— e informando cuántos son en total (FR-017b).
- El ámbito se puede corregir mientras el tipo no tenga ningún documento, de ninguno de los dos lados.
  Con documentos asociados se rechaza informando cuántos son (FR-017d).

**Impacto en el Módulo 3**: el único método cuyo resultado cambia es el conteo de documentos por tipo,
que ahora suma las dos tablas. Cambia hacia el lado seguro: bloquea más bajas, nunca menos.

---

## Estado de un documento — calculado, nunca almacenado

Tres valores (`DocumentacionEstado`, reutilizado del Módulo 3 sin cambios), con la misma regla de
FR-019 y los mismos bordes:

| Estado | Condición |
|---|---|
| `vencida` | `fechaVencimiento < hoy` |
| `proximaAvencer` | `hoy <= fechaVencimiento <= hoy + diasAvisoVencimiento` |
| `vigente` | `fechaVencimiento > hoy + diasAvisoVencimiento` |

- **Los días de aviso son días corridos**, no hábiles: sábados, domingos y feriados cuentan igual
  (FR-019a). No hay ningún calendario de feriados que mantener. Es lo que `CalculadorEstadoDocumento`
  ya hace con `AddDays`, así que el requisito documenta la conducta existente en vez de cambiarla.
- **"Hoy" es el día en curso en Argentina (UTC−3)**, no el del servidor ni el del navegador (FR-020).
  Lo resuelve `FechaHoyArgentina`, ya existente.
- **Vence exactamente hoy → `proximaAvencer`**, no `vencida`. Pasa a `vencida` mañana.
- **Días de aviso en cero → sin ventana intermedia**: vigente hasta el día del vencimiento inclusive,
  vencida al día siguiente.

La regla vive en `CalculadorEstadoDocumento` (Módulo 3, se reutiliza tal cual) y se traduce a SQL en
la consulta del listado para poder filtrar sin traer filas. **Las dos escrituras van cubiertas por un
test que las compara sobre el mismo dato**, según la convención [003] de `CLAUDE.md`.

---

## Documento vigente de cada tipo — el que manda

De cada tipo de documentación, un solo documento del vehículo cuenta: **el de fecha de vencimiento más
lejana** y, con empate, el de `Id` mayor (FR-024, research §12). Los demás son historial: se ven en la
ficha, no definen el estado del vehículo y no alertan.

Es lo que hace que cargar una renovación saque la alerta sin que nadie borre el papel viejo (SC-010), y
que eliminar el vigente devuelva el mando al más reciente de los que quedan, con el estado
recalculándose solo.

En SQL viaja como subconsulta correlacionada —"no existe otro del mismo tipo que le gane"—, que el
índice `VehiculoId, TipoId, FechaVencimiento DESC` resuelve directo. **El predicado va escrito en el
árbol de expresión, no extraído a un método propio**: extraerlo rompe la traducción de EF Core y la
consulta pasaría a evaluarse en memoria (convención [003] de `CLAUDE.md`).

---

## Estado general de documentación del vehículo — calculado, cuatro valores

`EstadoDocumentacionVehiculo`, derivado y nunca almacenado (FR-033):

| Valor | Condición sobre los documentos **vigentes de cada tipo** |
|---|---|
| `sinDocumentacion` | El vehículo no tiene ningún documento cargado |
| `vencida` | Al menos uno está `vencida` |
| `proximaAvencer` | Ninguno vencido y al menos uno `proximaAvencer` |
| `enRegla` | Todos `vigente` |

Precedencia: `vencida` > `proximaAvencer` > `enRegla`.

Dos cosas que **no** hace, y son deliberadas:

- No compara contra ninguna lista de documentación obligatoria. Ningún tipo lo es en este módulo, y el
  sistema no infiere que falte un documento que nunca se cargó (FR-034).
- **No mira el archivo adjunto.** Que un documento no tenga escaneo es un dato del documento, no del
  vehículo, y no altera este estado: los cuatro valores se conservan exactamente (FR-016a).

`sinDocumentacion` **no es lo mismo que `enRegla`**: un vehículo sin papeles no está al día por
ausencia de papeles, y no puede quedar disponible.

En la consulta del listado se resuelve con tres conteos sobre los vigentes —cuántos hay, cuántos
vencidos y cuántos por vencer—, que alcanzan para los cuatro valores sin traer un solo documento.

---

## Estado operativo mostrado — derivado del guardado y de la documentación

```
estadoOperativoDerivado =
    (estadoDocumentacion ∈ { vencida, sinDocumentacion })
        ? fueraDeServicio
        : estadoOperativoGuardado
```

Es lo que el listado muestra, lo que la ficha muestra y por lo que el filtro discrimina (FR-014). La
columna guardada no se toca nunca: al renovar el documento, la unidad vuelve a estar disponible sola,
sin proceso nocturno y sin que nadie edite nada.

**Filtro `estado` del listado**, valor único con tres opciones excluyentes (FR-030a):

| Valor | Predicado |
|---|---|
| *omitido* | `Activo` (FR-031) |
| `disponible` | `Activo && EstadoOperativo == Disponible && vigentes > 0 && vencidos == 0` |
| `fueraDeServicio` | `Activo && (EstadoOperativo == FueraDeServicio \|\| vigentes == 0 \|\| vencidos > 0)` |
| `dadoDeBaja` | `!Activo` |

Los dos valores del medio son complementarios dentro de `Activo`: todo vehículo activo cae en
exactamente uno. Por eso `disponible` no puede devolver una unidad con documentación vencida o ausente
(FR-015, SC-006).

---

## Enumeraciones

| Enum | Valores | Dónde vive | Se almacena |
|---|---|---|---|
| `VehiculoEstado` | `Disponible = 1`, `FueraDeServicio = 2` | `GT.Domain/Flota/` | **Sí**, columna `EstadoOperativo` |
| `EstadoDocumentacionVehiculo` | `SinDocumentacion = 1`, `EnRegla = 2`, `ProximaAvencer = 3`, `Vencida = 4` | `GT.Domain/Flota/` | No, derivado |
| `DocumentacionAmbito` | `Chofer = 1`, `Vehiculo = 2` | `GT.Domain/Choferes/`, junto a `DocumentacionTipo` | **Sí**, columna `Ambito` |
| `DocumentacionEstado` | `Vigente`, `ProximaAvencer`, `Vencida` | `GT.Domain/Choferes/` — **reutilizado sin cambios** | No, derivado |

Todos viajan en el JSON en **camelCase** (`enRegla`, `proximaAvencer`, `fueraDeServicio`,
`dadoDeBaja`), nunca en PascalCase; la traducción vive en `NombresDeEstado` (convención [003] de
`CLAUDE.md`).

---

## Orden y paginación del listado

- 20 filas por página, con el total de coincidencias (FR-032).
- Los cuatro filtros se aplican **sobre toda la flota antes de paginar**.
- Orden: `Patente, Id`. La patente es única, así que ya es un orden total; el `Id` va igual porque la
  convención [003] lo pide sin excepciones y cuesta nada.
- Respuesta: `{ items, total, pagina, tamanioPagina }`, con `PaginaDe<T>` reutilizado del Módulo 3.

---

## Migración

Una sola migración, `Modulo4Flota`, con estos pasos en este orden:

1. **`ALTER TABLE DocumentacionTipos ADD Ambito tinyint NOT NULL DEFAULT 1`.** El valor por defecto es
   `Chofer`, así que **todos los tipos ya cargados quedan con ámbito chofer** y ningún documento
   existente cambia de comportamiento (FR-017c). No hace falta ninguna corrección manual ni ningún
   tratamiento de excepciones: la spec confirmó que los tipos cargados son pocos y se migran sin
   excepciones.
2. `CREATE TABLE TiposVehiculo` con su índice único sobre `Nombre`.
3. `CREATE TABLE Vehiculos` con sus tres índices y sus dos claves foráneas.
4. `CREATE TABLE DocumentacionesVehiculo` con sus tres índices y sus dos claves foráneas.
5. **Siembra de los dos permisos nuevos** (`flota.gestionar`, `flota.tipos.gestionar`) y su asignación
   a los roles, vía `SembradorInicial`, que es idempotente y ya corre en cada arranque.

Ninguna tabla arranca con datos: el catálogo de tipos de vehículo y el padrón de flota se completan
desde las pantallas del módulo (US1, US2).

**Reversibilidad**: la migración se puede revertir. El `Down` borra las tres tablas nuevas y quita la
columna `Ambito`; los datos del Módulo 3 quedan exactamente como estaban.
