# Modelo de datos: Gestión de viajes (Módulo 5)

Tablas, campos, reglas y migración. Las decisiones que están detrás de cada forma —y las alternativas
descartadas— están en [research.md](./research.md); acá va la forma concreta.

## Panorama

Tres tablas nuevas, una secuencia y ninguna modificación a lo que ya existe.

```text
Clientes ──1───*── Viajes ──1───*── CambiosDeEstadoViaje ──*───1── Usuarios (M1)
                     │  │  │
                     │  │  └──0..1── Choferes (M3)
                     │  └─────0..1── Vehiculos (M4)
                     └────────0..1── Transportistas (M3)
```

**Qué se agrega**

| Objeto | Tipo | Para qué |
|---|---|---|
| `Clientes` | tabla nueva | Padrón propio del módulo (US1) |
| `Viajes` | tabla nueva | Entidad principal (US2 a US6) |
| `CambiosDeEstadoViaje` | tabla nueva | Historial de FR-035 |
| `dbo.NumeroDeViaje` | secuencia nueva | Número de viaje de FR-011 (research §1) |

**Qué no se toca**: `Choferes`, `Vehiculos`, `Transportistas`, `Documentaciones`,
`DocumentacionesVehiculo`, `DocumentacionTipos`, `Personas`, `Usuarios`, `Roles`. Este módulo los
**consume**: los lee, los referencia con claves foráneas y evalúa sus reglas con los calculadores del
dominio, sin agregarles una columna ni una navegación. Es la primera vez que un módulo se apoya sobre
dos módulos de negocio anteriores sin modificarlos en nada.

Lo único que cambia fuera de estas tablas son **filas**, no estructura: el sembrador agrega los dos
permisos y su reparto por rol.

---

## Cliente (tabla `Clientes`) — NUEVA

Empresa o persona que contrata el servicio de transporte (FR-001 a FR-009).

| Campo | Tipo SQL | Nulo | Regla |
|---|---|---|---|
| `Id` | `int IDENTITY` | no | PK |
| `RazonSocial` | `nvarchar(100)` | no | Obligatoria, con `Trim` al guardar (FR-002) |
| `Cuit` | `nvarchar(11)` | no | Sólo dígitos, once, con dígito verificador válido. **Normalizado antes de validar y de guardar** (FR-004) |
| `Telefono` | `nvarchar(30)` | no | Obligatorio (FR-002) |
| `Email` | `nvarchar(254)` | no | Obligatorio, con formato válido y **sin** unicidad (FR-004) |
| `Direccion` | `nvarchar(200)` | **sí** | Opcional (FR-002) |
| `Activo` | `bit` | no | `false` es la baja lógica. Por defecto `true` (FR-001) |

**Índices**

```sql
CREATE UNIQUE INDEX IX_Clientes_Cuit ON Clientes (Cuit);
```

Sin filtro por `Activo`, a propósito: el CUIT de un cliente dado de baja **sigue ocupado** (FR-003).
Quien intente registrarlo de nuevo recibe `cuit_de_cliente_dado_de_baja`, distinto de
`cuit_duplicado`, para que sepa que tiene que darlo de alta de nuevo en vez de buscarlo sin
encontrarlo (FR-007, research §13).

En la modificación, la comparación previa excluye al propio cliente: conservar su CUIT no genera
conflicto (FR-003, US1 esc. 5).

**Reglas que no viven en la tabla**

- **La baja se rechaza si el cliente tiene al menos un viaje `pendiente` o `en curso`**, e informa
  cuántos, en el mensaje y en el cuerpo del error (FR-006, SC-009). Un cliente cuyos viajes están todos
  `rendido` o `anulado` se da de baja sin problema: es el caso normal del que dejó de operar con la
  empresa. El predicado es `Estado IN (0, 1)`, el mismo criterio de "dependientes vivos" con el que el
  Módulo 3 rechaza la baja de un transportista.
- **La baja pide confirmación explícita** desde la pantalla; cancelarla no modifica nada (FR-005).
- **El cliente inactivo no se ofrece al registrar ni al modificar un viaje**, pero los viajes que ya lo
  tienen lo conservan y lo siguen mostrando, señalado como inactivo con la palabra que lo explica
  (FR-008).
- **El alta de nuevo es un recurso propio, idempotente y sin confirmación aparte** (FR-007).

**Nunca se borra físicamente** (FR-001).

---

## Viaje (tabla `Viajes`) — NUEVA

Unidad de trabajo de la empresa. Entidad principal del módulo (FR-010 a FR-047).

| Campo | Tipo SQL | Nulo | Regla |
|---|---|---|---|
| `Id` | `int IDENTITY` | no | PK |
| `Numero` | `int` | no | `DEFAULT NEXT VALUE FOR dbo.NumeroDeViaje`. Único, generado por el sistema, **no editable por nadie en ningún estado** y **nunca reutilizado** (FR-011, FR-017) |
| `ClienteId` | `int` | no | FK → `Clientes`, `Restrict`. Todo viaje pertenece a exactamente un cliente (FR-012) |
| `Fecha` | `date` | no | Obligatoria. Admite pasado —carga retroactiva— y futuro —viaje planificado—, sin límite (FR-016) |
| `Origen` | `nvarchar(100)` | no | Texto libre obligatorio, con `Trim` (FR-012) |
| `Destino` | `nvarchar(100)` | no | Texto libre obligatorio, con `Trim` (FR-012) |
| `NumeroRemito` | `nvarchar(50)` | **sí** | Opcional. Cuando se carga, único entre los viajes no anulados (FR-014) |
| `DetalleCarga` | `nvarchar(500)` | **sí** | Opcional (FR-012) |
| `Importe` | `decimal(18,2)` | no | Pesos argentinos. `CHECK (Importe >= 0)`. Cero es válido (FR-013, research §11) |
| `Estado` | `tinyint` | no | `EstadoViaje`. Todo viaje nace `pendiente` (FR-031, FR-032) |
| `MotivoAnulacion` | `nvarchar(500)` | **sí** | Obligatorio al anular, `null` en cualquier otro estado (FR-036) |
| `ChoferId` | `int` | **sí** | FK → `Choferes`, `Restrict`. La asignación no es obligatoria para el alta (FR-019) |
| `VehiculoId` | `int` | **sí** | FK → `Vehiculos`, `Restrict` (FR-019) |
| `TransportistaId` | `int` | **sí** | FK → `Transportistas`, `Restrict`. Se escribe al asignar el chofer y no se mueve sola (FR-028, research §9) |

**Índices**

```sql
-- FR-011: el número no se repite nunca.
CREATE UNIQUE INDEX IX_Viajes_Numero ON Viajes (Numero);

-- FR-014: único entre los NO anulados; un viaje sin remito no ocupa nada.
CREATE UNIQUE INDEX IX_Viajes_NumeroRemito ON Viajes (NumeroRemito)
    WHERE NumeroRemito IS NOT NULL AND Estado <> 3;

-- FR-026: un chofer, un solo viaje `en curso` a la vez. Ídem vehículo.
CREATE UNIQUE INDEX IX_Viajes_ChoferEnCurso ON Viajes (ChoferId)
    WHERE ChoferId IS NOT NULL AND Estado = 1;

CREATE UNIQUE INDEX IX_Viajes_VehiculoEnCurso ON Viajes (VehiculoId)
    WHERE VehiculoId IS NOT NULL AND Estado = 1;

-- Orden del listado (FR-043).
CREATE INDEX IX_Viajes_Fecha_Numero ON Viajes (Fecha DESC, Numero DESC);

-- Filtros y agrupamientos (FR-041, FR-046).
CREATE INDEX IX_Viajes_ClienteId ON Viajes (ClienteId);
CREATE INDEX IX_Viajes_TransportistaId ON Viajes (TransportistaId);
CREATE INDEX IX_Viajes_Estado ON Viajes (Estado);
```

Los tres primeros son **la garantía real**, no una optimización: la consulta previa da el mensaje
bueno y el índice cierra la carrera entre dos operadores simultáneos (SC-003, SC-005, research §2).

Los dos de exclusividad los ejercitan **dos caminos**, no uno: poner un viaje en curso y **reasignar
la unidad de un viaje que ya está en curso** (FR-026a). Los dos casos de uso consultan la ocupación
antes de guardar; reasignar un viaje `pendiente` no consulta nada, porque un pendiente no ocupa
(FR-027).

Los números literales de los filtros —`1` para `en curso`, `3` para `anulado`— dependen del orden de
`EstadoViaje` y no fallan al compilar si alguien lo cambia. Un test de integración inserta un viaje en
cada estado y verifica que cada índice acepta y rechaza donde corresponde.

**Reglas que no viven en la tabla**

- **`rendido` es inmutable para todos los roles** (FR-018, SC-013): no se editan sus datos, no se
  reasigna, no se anula, no se retrocede. Los cinco caminos consultan el estado antes de tocar nada.
- **La edición aplica las mismas validaciones que el alta** y sólo procede en `pendiente` o `en curso`
  (FR-017).
- **La edición no incluye chofer, vehículo ni estado**: no los ignora, no están en el contrato de
  entrada (FR-019a, FR-034, research §4).
- **Cambiar la fecha revalida la asignación contra la fecha nueva** y rechaza el cambio entero si
  quedara bloqueada (FR-022a). No se guarda nada: ni la fecha ni la asignación cambian.
- **Origen igual a destino se acepta con advertencia** que llega con el resultado (FR-015).

---

## CambioDeEstadoViaje (tabla `CambiosDeEstadoViaje`) — NUEVA

Historial de FR-035. No se edita ni se borra por ninguna vía: no hay endpoint que lo escriba
directamente ni que lo modifique.

| Campo | Tipo SQL | Nulo | Regla |
|---|---|---|---|
| `Id` | `int IDENTITY` | no | PK |
| `ViajeId` | `int` | no | FK → `Viajes`, `Cascade`. Única cascada del módulo |
| `EstadoAnterior` | `tinyint` | **sí** | `null` **sólo** en el registro del alta: antes del alta no había estado |
| `EstadoNuevo` | `tinyint` | no | El estado al que se llegó |
| `UsuarioId` | `int` | no | FK → `Usuarios`, `Restrict`. Quién lo produjo (research §7) |
| `OcurridoEn` | `datetime2` | no | Instante en UTC, puesto por el servidor con `TimeProvider`. Sale del API con la `Z` por la convención [002] |

```sql
CREATE INDEX IX_CambiosDeEstadoViaje_ViajeId_OcurridoEn
    ON CambiosDeEstadoViaje (ViajeId, OcurridoEn);
```

El índice sirve para dos cosas: mostrar el historial ordenado en la ficha y resolver la subconsulta
que deriva `demorado` (FR-039, research §6).

**Todo viaje tiene al menos una fila**: la de su alta, con `EstadoAnterior = null` y
`EstadoNuevo = pendiente`. Se escribe en la misma transacción que el viaje.

---

## Secuencia `dbo.NumeroDeViaje` — NUEVA

```sql
CREATE SEQUENCE dbo.NumeroDeViaje AS int START WITH 1 INCREMENT BY 1 NO CACHE;
```

`NO CACHE` es el punto de la decisión, no un detalle: sin él, un apagado sucio del motor hace saltar la
numeración y el viaje siguiente al 12 pasa a ser el 1012 (research §1). El costo es una escritura de
log por número, invisible a este volumen.

La anulación **no** devuelve el número: la secuencia sólo avanza (FR-011, US2 esc. 5).

---

## Estado del viaje — guardado, con transiciones cerradas

`EstadoViaje` es `tinyint` y toma exactamente cuatro valores (FR-031):

| Valor | Guardado | En el JSON |
|---|---|---|
| Pendiente | `0` | `pendiente` |
| En curso | `1` | `enCurso` |
| Rendido | `2` | `rendido` |
| Anulado | `3` | `anulado` |

Las únicas transiciones permitidas (FR-033):

```text
                 ┌──────────────┐
   (alta) ──────▶│  pendiente   │──────────┐
                 └──────┬───────┘          │
                        │                  ▼
                        │            ┌──────────┐
                        │            │ anulado  │  ← terminal
                        ▼            └──────────┘
                 ┌──────────────┐          ▲
                 │  en curso    │──────────┘
                 └──────┬───────┘
                        │
                        ▼
                 ┌──────────────┐
                 │   rendido    │  ← terminal e inmutable (FR-018)
                 └──────────────┘
```

Cualquier otra transición se rechaza con `transicion_no_permitida`, y la pantalla no la ofrece
(FR-033, US4 esc. 10). Los dos estados terminales no tienen salida: no hay camino de vuelta a
`pendiente` ni a `en curso` desde ninguno de los dos.

**Cada transición es un recurso propio** y nunca un campo del `PUT` (FR-034, research §4). Cada una
escribe su fila de historial en la misma transacción que el cambio.

**Requisitos de cada transición**

| Transición | Exige |
|---|---|
| `pendiente → en curso` | Chofer **y** vehículo asignados y **activos en sus padrones** (FR-025); ninguno de los dos ocupado por otro viaje `en curso` (FR-026). La documentación **no** se revalida acá: se controló al asignar |
| `en curso → rendido` | Confirmación previa si el importe es cero (FR-038, research §5) |
| `pendiente → anulado` | Motivo escrito obligatorio y confirmación explícita (FR-036) |
| `en curso → anulado` | Ídem |

Al pasar a `rendido` o a `anulado`, el chofer y el vehículo **dejan de estar ocupados** —los índices
filtrados dejan de alcanzarlos— pero la asignación se conserva y se sigue viendo (FR-037): liberar es
dejar de ocupar, nunca borrar el dato.

---

## Habilitación de una asignación — calculada, nunca almacenada

Se evalúa contra **la fecha del viaje**, no contra el día en curso (FR-024, research §3). Reutiliza sin
cambios `CalculadorEstadoDocumento` y la regla de "documento vigente por tipo" de los Módulos 3 y 4.

```text
Para el chofer y para el vehículo, por separado:

  1. De cada tipo de documento, quedarse con el vigente: el de vencimiento más lejano,
     con desempate por Id mayor.                                    ← M3/M4, sin cambios
  2. Calcular el estado de cada uno contra la FECHA DEL VIAJE.      ← M3/M4, sin cambios
  3. Traducir a veredicto:                                          ← lo único nuevo

        alguno vencido            → bloqueado      (no se guarda; FR-022)
        ninguno vencido,
          alguno próximo a vencer → conAdvertencia (se guarda; FR-023)
        todos vigentes            → habilitado
        ninguno cargado           → habilitado     (FR-024)
```

Los tres valores del veredicto —`habilitado`, `conAdvertencia`, `bloqueado`— **no se guardan en
ninguna columna**: se calculan al asignar y se devuelven con el resultado de esa operación.

**Que un chofer sin documentos no bloquee es deliberado** y no contradice al Módulo 4, que sí impide
dejar una unidad sin documentación como `disponible`: son dos preguntas distintas, y la lista de
asignables ya filtró por el estado operativo guardado antes de llegar acá (research §3).

**El bloqueo nombra el documento** —tipo y número— y la advertencia nombra el afectado. Sin eso, quien
opera sabe que no puede pero no qué resolver.

---

## Lista de asignables — resuelta en la base

La pantalla de asignación ofrece (FR-021):

```csharp
// Choferes: activos.
contexto.Choferes.Where(chofer => chofer.Activo)

// Vehículos: activos y con estado operativo GUARDADO `disponible`.
// No el derivado contra el día en curso: eso rompería la carga retroactiva (FR-021, SC-014).
contexto.Vehiculos.Where(vehiculo =>
    vehiculo.Activo && vehiculo.EstadoOperativo == VehiculoEstado.Disponible)
```

Ningún dado de baja aparece, aunque tenga viajes históricos (US3 esc. 2 y 3). No pagina: son dos
desplegables sobre padrones de decenas de filas.

La habilitación por documentación **no** filtra esta lista: se resuelve al asignar, contra la fecha del
viaje.

**Los dos se asignan juntos** (FR-019b): no hay asignación parcial, así que un viaje tiene chofer y
vehículo o no tiene ninguno de los dos. Eso simplifica todo lo que viene después —FR-025 pregunta una
sola cosa, y FR-022a no tiene que contemplar el caso de una sola unidad asignada—.

---

## Demorado — derivado al leer, nunca un quinto estado

`demorado` es una señal booleana del listado (FR-039). **No es un valor de `EstadoViaje` ni una
columna**: un viaje demorado sigue estando `en curso`.

```text
demorado  ⟺  Estado = enCurso
              ∧  (ahora − instante en que pasó a `en curso`) > 5 días corridos
```

El instante sale del historial con una subconsulta correlacionada; existe a lo sumo uno, porque
`pendiente → en curso` es la única transición que llega a ese estado y no hay camino de vuelta
(research §6). El umbral vive en una constante única, `Viaje.DiasParaDemora = 5`.

El destaque lleva la palabra que lo explica y no se comunica sólo por color (FR-039, FR-049), y **el
sistema no le cambia el estado a ningún viaje por sí solo**.

---

## Carga retroactiva — derivada al leer

Un viaje con `Fecha` anterior al día en curso en Argentina se señala explícitamente como carga
retroactiva (FR-016). Se calcula al mostrar, contra `FechaHoyArgentina.Hoy()`, y no se guarda: un viaje
cargado ayer para ayer deja de ser "retroactivo" mañana en el sentido en que a nadie le importa, y
guardarlo obligaría a mantenerlo.

---

## Totales por cliente y por transportista — calculados en la consulta

Dos agregaciones sobre el mismo predicado, con el rango de fechas obligatorio (FR-046, FR-046a):

```csharp
var enElPeriodo = contexto.Viajes.Where(viaje =>
    viaje.Fecha >= desde &&
    viaje.Fecha <= hasta &&
    // FR-047: la exclusión es un predicado de la consulta, no un filtrado posterior.
    viaje.Estado != EstadoViaje.Anulado);

var porCliente = enElPeriodo
    .GroupBy(viaje => new { viaje.ClienteId, viaje.Cliente!.RazonSocial })
    .Select(grupo => new TotalDto(
        grupo.Key.ClienteId, grupo.Key.RazonSocial, grupo.Count(), grupo.Sum(v => v.Importe)));
```

El de transportista es el mismo con `TransportistaId`. **Los viajes sin transportista no aparecen en
ese cuadro**: un viaje sin chofer asignado todavía no tiene transportista, y es el comportamiento
esperado.

La fecha de corte es **la fecha del viaje**, no la de carga ni la de rendición (FR-046a). Sin rango
elegido no se calcula nada y la pantalla dice que falta elegirlo.

**SC-008 se verifica comparando las dos pantallas**: la suma de los importes de las filas del listado
filtrado por cliente y rango tiene que coincidir con el total de ese cliente. Coincide porque las dos
consultas excluyen los anulados con el mismo predicado.

---

## Enumeraciones

| Enum | Valores en el JSON | Dónde vive | Se guarda |
|---|---|---|---|
| `EstadoViaje` | `pendiente`, `enCurso`, `rendido`, `anulado` | `GT.Domain/Viajes/` | **sí**, `tinyint` |
| `HabilitacionAsignacion` | `habilitado`, `conAdvertencia`, `bloqueado` | `GT.Domain/Viajes/` | no, derivado |

Los dos viajan en camelCase, no en PascalCase, con la traducción al español en `NombresDeEstadoViaje`
(convención [003]). `demorado` y `esRetroactivo` viajan como booleanos del listado, no como enums.

---

## Orden y paginación del listado

- **20 filas por página**, del lado del servidor, con `{ items, total, pagina, tamanioPagina }` de
  `PaginaDe<T>` (convención [003], FR-043).
- **Orden: `Fecha` descendente, `Numero` descendente.** Lo más reciente primero, con criterio total: es
  la primera vez que el orden no termina en `Id`, y no hace falta que termine —`Numero` tiene índice
  único propio y es además el que ve el usuario— (research §12).
- **Los filtros y la búsqueda se aplican antes de paginar**, sobre todos los viajes, y `total` cuenta
  las coincidencias completas (FR-043).
- **Sin filtro de estado no se muestran los anulados**, escrito como predicado único (FR-044).

---

## Migración

Una sola: `Modulo5Viajes`.

1. `CREATE SEQUENCE dbo.NumeroDeViaje AS int START WITH 1 INCREMENT BY 1 NO CACHE`.
2. `CREATE TABLE Clientes` con su índice único de CUIT.
3. `CREATE TABLE Viajes` con el `DEFAULT` de `Numero`, el `CHECK (Importe >= 0)`, las cuatro claves
   foráneas en `Restrict` y los siete índices.
4. `CREATE TABLE CambiosDeEstadoViaje` con su FK en cascada al viaje, su FK en `Restrict` al usuario y
   su índice.

**No modifica ninguna tabla existente y no siembra filas.** Los dos permisos y su reparto por rol los
agrega `SembradorInicial`, que ya corre en cada arranque y es idempotente: los módulos anteriores lo
hicieron igual.

**Los dos padrones arrancan vacíos.** El de clientes no se siembra —FR-001 y el escenario US1 esc. 1
esperan la pantalla vacía con su mensaje— y la numeración de viajes arranca en 1: los números 1041 y
1042 del enunciado son ilustrativos y no un punto de partida a precargar.
