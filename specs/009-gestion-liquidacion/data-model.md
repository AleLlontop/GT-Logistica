# Modelo de datos: Gestión de liquidación a transportistas (Módulo 9)

Cinco tablas nuevas, **ninguna tabla modificada**, dos secuencias, una migración:
`Modulo9Liquidaciones`.

```
  Transportistas (Módulo 3)   EmpresaEmisora (Módulo 6)        Viajes (Módulo 5)
          │                          │                              │
          │ referencia               │ sólo se lee su CUIT          │ referencia
          │                          │ (externo = CUIT distinto)    │ (sin columna nueva)
          ▼                                                         ▼
  ┌──────────────────────────────────────────┐        ┌──────────────────────────┐
  │               Liquidaciones              │ 1 — *  │    LiquidacionViajes     │
  │ número · período · total · pagado ·      │───────▶│ liquidación · viaje ·    │
  │ estado · motivo de anulación             │        │ vigente                  │
  └──────────┬──────────────────┬────────────┘        └──────────────────────────┘
             │ 1 — *            │ 1 — *
             ▼                  ▼
       OrdenesDePago     CambiosDeLiquidacion
```

**Ninguna tabla ni entidad de un módulo anterior cambia.** `Transportistas`, `Viajes` y `EmpresaEmisora`
se leen; no ganan columna, índice, navegación ni método (spec §Assumptions, research §1). Los dos únicos
cambios a módulos anteriores son de presentación en el frontend y no tocan el modelo (research §11b).

---

## Tabla `Liquidaciones`

Entidad principal del módulo. Pertenece a exactamente un transportista externo y agrupa uno o más
viajes de un único período.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK identidad |
| `Numero` | `int` | no | `DEFAULT NEXT VALUE FOR dbo.NumeroDeLiquidacion`. Único, no editable, no reutilizable ni al anular (FR-016). Se muestra como `LQ-{Numero}` (research §4) |
| `TransportistaId` | `int` | no | FK → `Transportistas`, `Restrict`. Externo y activo **al generar** (FR-001); si después se da de baja, la liquidación sigue igual |
| `PeriodoMes` | `tinyint` | no | `CHECK` entre 1 y 12 |
| `PeriodoAnio` | `smallint` | no | `2025` o `2026`, validado en la aplicación (FR-002). **Sin `CHECK`**, por el mismo motivo que `Facturas.PeriodoAnio`: la lista crece con los años |
| `ImporteTotal` | `decimal(18,2)` | no | suma exacta de los importes de los viajes vigentes. `CHECK ([ImporteTotal] > 0)` (FR-007, FR-012a). No llega nunca desde el cliente HTTP |
| `ImportePagado` | `decimal(18,2)` | no | `DEFAULT 0`. Suma de los importes de sus órdenes de pago. `CHECK ([ImportePagado] >= 0 AND [ImportePagado] <= [ImporteTotal])` (FR-043) |
| `Estado` | `tinyint` | no | `pendiente=0`, `pagada=1`, `anulada=2`. `DEFAULT 0` (FR-015) |
| `MotivoAnulacion` | `nvarchar(500)` | sí | obligatorio al anular, nulo en cualquier otro estado (FR-055) |
| `Version` | `int` | no | `DEFAULT 0`. **Sube de a uno con cada edición guardada** y con nada más. La edición sólo se aplica si sigue siendo la versión que se abrió (FR-048, research §2) |

### La restricción que ata el estado a los importes

```sql
-- ⚠ 0, 1 y 2 son EstadoLiquidacion escrito a mano: reordenar el enum no falla al compilar.
ALTER TABLE Liquidaciones ADD CONSTRAINT CK_Liquidaciones_Estado CHECK (
     ([Estado] = 0 AND [ImportePagado] <  [ImporteTotal] AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 1 AND [ImportePagado] =  [ImporteTotal] AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 2 AND [ImportePagado] =  0              AND [MotivoAnulacion] IS NOT NULL)
);
```

Es lo que vuelve seguro guardar `pagada` en vez de derivarla (research §3): la base rechaza una
liquidación `pagada` con saldo, una `pendiente` sin saldo y una `anulada` con pagos o sin motivo. Con
eso FR-030, FR-031 y FR-053 son garantías del dato, no del código.

**Lo que resta pagar no es columna**: `ImporteTotal − ImportePagado`, proyectado en la consulta. En una
`anulada` no se muestra (FR-027).

**La fecha de generación no es columna**: sale del historial (§`CambiosDeLiquidacion`).

### Índices

```sql
CREATE UNIQUE INDEX IX_Liquidaciones_Numero ON Liquidaciones (Numero DESC);  -- FR-016 y el orden del listado (FR-023)
CREATE INDEX IX_Liquidaciones_TransportistaId ON Liquidaciones (TransportistaId);
CREATE INDEX IX_Liquidaciones_Periodo ON Liquidaciones (PeriodoAnio, PeriodoMes);
CREATE INDEX IX_Liquidaciones_Estado ON Liquidaciones (Estado);
```

**El orden del listado es `Numero DESC`** (FR-023): la secuencia crece con cada generación, así que el
número más alto es la liquidación más reciente, y como es único el orden es total sin desempate
(convención [003]).

---

## Tabla `LiquidacionViajes`

Qué viajes agrupa cada liquidación, **y qué agrupaba cada anulada** (FR-028). El porqué de una tabla
y no una columna en `Viajes`, en research §1.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `LiquidacionId` | `int` | no | FK → `Liquidaciones`, `Restrict`. PK compuesta |
| `ViajeId` | `int` | no | FK → `Viajes`, `Restrict`, **sin navegación inversa** desde `Viaje`. PK compuesta |
| `Vigente` | `bit` | no | `DEFAULT 1`. Vale `0` **exactamente** cuando la liquidación está `anulada` |

```sql
-- FR-014, SC-002: la garantía real, no una optimización. La consulta previa da el mensaje bueno;
-- el índice cierra la carrera entre dos operadores simultáneos (convención [005]).
CREATE UNIQUE INDEX IX_LiquidacionViajes_ViajeVigente
    ON LiquidacionViajes (ViajeId) WHERE [Vigente] = 1;
```

- **Generar y agregar en la edición** insertan con `Vigente = 1`.
- **Quitar en la edición** borra la fila: la liquidación es `pendiente` y sin pagos, y qué se quitó
  queda en `CambiosDeLiquidacionViajes`.
- **Anular** pone `Vigente = 0` en todas las filas de la liquidación. Los viajes quedan libres y la
  anulada los sigue listando.

Un viaje puede tener **muchas** filas —una por cada liquidación anulada que lo agrupó— y **a lo sumo
una** vigente.

**`Vigente` repite un dato del estado de la liquidación**, y se acepta porque un índice filtrado sólo
mira columnas de su propia tabla. Se escribe en la misma transacción que la anulación y lo verifica
`CoherenciaDeLiquidacionTests` (research §1).

---

## Tabla `OrdenesDePago`

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK identidad |
| `Numero` | `int` | no | `DEFAULT NEXT VALUE FOR dbo.NumeroDeOrdenDePago`. Único (FR-039). Se muestra como `OP-{Numero}` |
| `LiquidacionId` | `int` | no | FK → `Liquidaciones`, `Restrict` |
| `FechaPago` | `date` | no | entre la fecha de generación de la liquidación y el día en curso, los dos incluidos (FR-038) |
| `Importe` | `decimal(18,2)` | no | `CHECK ([Importe] > 0)` (FR-037) |
| `UsuarioId` | `int` | no | FK → `Usuarios`, `Restrict`. Llega por parámetro desde el endpoint (FR-034) |
| `RegistradaEn` | `datetime2` | no | instante UTC del servidor, con `TimeProvider`. Sale con `Z` (convención [002]) |

```sql
CREATE UNIQUE INDEX IX_OrdenesDePago_Numero ON OrdenesDePago (Numero);
CREATE INDEX IX_OrdenesDePago_LiquidacionId ON OrdenesDePago (LiquidacionId);
```

**No se edita ni se borra por ninguna vía** (FR-044): no hay endpoint que lo haga.

**Que la suma no supere el total no lo garantiza esta tabla**, sino `Liquidaciones.ImportePagado` con su
`CHECK` y el `UPDATE` condicional de §Pagar. Un `CHECK` no puede sumar filas de otra tabla.

---

## Tabla `CambiosDeLiquidacion`

Historial de FR-033: quién y cuándo generó, editó o anuló.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK |
| `LiquidacionId` | `int` | no | FK → `Liquidaciones`, `Restrict` |
| `Operacion` | `tinyint` | no | `generacion=0`, `edicion=1`, `anulacion=2` |
| `UsuarioId` | `int` | no | FK → `Usuarios`, `Restrict` |
| `OcurridoEn` | `datetime2` | no | instante UTC, con `TimeProvider` |

```sql
-- Una sola generación y una sola anulación por liquidación; ediciones, las que hagan falta.
-- ⚠ El 1 es OperacionDeLiquidacion.Edicion escrito a mano.
CREATE UNIQUE INDEX IX_CambiosDeLiquidacion_Unica
    ON CambiosDeLiquidacion (LiquidacionId, Operacion) WHERE [Operacion] <> 1;
```

- **El motivo de la anulación no se copia acá**: se lee de `Liquidaciones.MotivoAnulacion`. Hay una sola
  anulación posible y una copia podría discrepar.
- **La fecha de generación** (FR-017) es `OcurridoEn` de la entrada `generacion`, convertido con
  `FechaHoyArgentina.Desde(instante)`. El índice único garantiza que esa entrada es una sola.
- **Qué viajes quitó y agregó cada edición** no va en esta tabla sino en su tabla hija,
  `CambiosDeLiquidacionViajes` (FR-033, research §3b).
- **Los pagos no entran al historial**: cada orden de pago ya registra quién y cuándo (FR-034).

Toda liquidación tiene **al menos una** entrada, la de su generación, escrita en la misma transacción.

---

## Tabla `CambiosDeLiquidacionViajes`

Qué viajes quitó y agregó cada edición (FR-033, spec §Clarifications). El porqué de una tabla hija, en
research §3b.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `CambioDeLiquidacionId` | `int` | no | FK → `CambiosDeLiquidacion`, `Restrict`. PK compuesta. **Sólo entradas de `edicion`** |
| `ViajeId` | `int` | no | FK → `Viajes`, `Restrict`, sin navegación inversa desde `Viaje`. PK compuesta |
| `Agregado` | `bit` | no | `1` si la edición lo agregó, `0` si lo quitó |

- Se escribe **en la misma transacción que la edición**, con la misma diferencia de viajes que se
  aplica sobre `LiquidacionViajes`.
- La PK compuesta impide que una edición registre dos veces el mismo viaje. Quitar y volver a agregar el
  mismo viaje dentro de una sola edición **no es un cambio** y no se registra: la diferencia se calcula
  entre lo guardado y el conjunto final.
- Una edición sin cambios no escribe nada (§Editar), así que toda entrada de `edicion` tiene al menos
  una fila acá.
- Que la tabla sólo cuelgue de entradas `edicion` lo asegura el caso de uso, que es el único que la
  escribe; un `CHECK` no puede mirar la operación de la fila padre. Lo cubre `EdicionTests`.

---

## Secuencias

```sql
CREATE SEQUENCE dbo.NumeroDeLiquidacion AS int START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE dbo.NumeroDeOrdenDePago AS int START WITH 1 INCREMENT BY 1;
```

Declaradas en `GtDbContext`, igual que `NumeroDeViaje`. **La entidad no asigna el número**: sin
`required`, con `private set` y `ValueGeneratedOnAdd` en la configuración (research §12.2).

---

## Enumeraciones

```csharp
/// ⚠ Los números importan: CK_Liquidaciones_Estado los lleva escritos a mano.
public enum EstadoLiquidacion : byte { Pendiente = 0, Pagada = 1, Anulada = 2 }

/// ⚠ IX_CambiosDeLiquidacion_Unica lleva el 1 escrito a mano.
public enum OperacionDeLiquidacion : byte { Generacion = 0, Edicion = 1, Anulacion = 2 }
```

Viajan en el JSON en **camelCase** —`pendiente`, `pagada`, `anulada`, `generacion`— con su traducción
al español en `NombresDeEstadoLiquidacion` (convención [003]). Los tres valores del estado son
**excluyentes** y el filtro del listado opera sobre la columna, que es exactamente lo que la fila
muestra (FR-029).

---

## Reglas de dominio (funciones puras)

En `GT.Domain/Liquidaciones/ReglasDeLiquidacion.cs`. **Reciben las fechas por parámetro y nunca leen el
reloj** (convención [005]):

| Regla | Firma conceptual | Requisito |
|---|---|---|
| Período admitido | `PeriodoValido(mes, anio)` → mes 1–12, año 2025 o 2026 | FR-002 |
| Rango de la fecha de pago | `FechaDePagoValida(fecha, fechaGeneracion, hoy)` → `fechaGeneracion ≤ fecha ≤ hoy` | FR-038 |
| Resta pagar | `RestaPagar(total, pagado)` → `total − pagado` | FR-027 |
| Estado después de un pago | `EstadoTrasPago(total, pagado, importe)` → `Pagada` si `pagado + importe = total`, si no `Pendiente` | FR-030, FR-041 |
| Por qué no se edita | `MotivoNoEditable(estado, pagado)` → `null`, `pagada`, `anulada` o `conOrdenesDePago` | FR-045 |
| Por qué no se anula | `MotivoNoAnulable(estado, pagado)` → ídem | FR-053, FR-054 |

`EstadoTrasPago` vive **dos veces**: acá y como `CASE` dentro del `UPDATE` condicional de §Pagar. Las dos
las compara `CoherenciaDeLiquidacionTests` sobre el mismo dato (convención [003]).

**Formato visible** en `GT.Domain/Liquidaciones/NumerosVisibles.cs`: `Liquidacion(n)` → `LQ-{n}`,
`OrdenDePago(n)` → `OP-{n}`. Un solo lugar, usado por los DTO y por los mensajes (research §4).

---

## Consultas

### Transportistas liquidables (research §5)

```csharp
contexto.Transportistas.Where(t => (incluirInactivos || t.Activo) && t.Cuit != cuitEmisora)
    .OrderBy(t => t.Nombre).ThenBy(t => t.Id)
```

Sin fila de `EmpresaEmisora`: lista vacía y `empresaEmisoraConfigurada: false`.

### Viajes disponibles (FR-004, research §6)

```csharp
contexto.Viajes.Where(viaje =>
    viaje.TransportistaId == transportistaId &&
    (viaje.Estado == EstadoViaje.Rendido || viaje.Estado == EstadoViaje.Facturado) &&
    viaje.Fecha.Month == mes &&
    viaje.Fecha.Year == anio &&
    !contexto.LiquidacionViajes.Any(v => v.ViajeId == viaje.Id && v.Vigente))
    .OrderBy(viaje => viaje.Fecha).ThenBy(viaje => viaje.Numero)
```

Escrita en el árbol, no extraída a un método: extraerla rompe la traducción (convención [003]).

### Listado (FR-021 a FR-024)

Filtros opcionales y combinables, **todos aplicados antes de paginar**: `TransportistaId`, `PeriodoMes`,
`PeriodoAnio` y `Estado` sobre la columna. Proyección: número, período, transportista (nombre, CUIT,
`Activo` del padrón), `ImporteTotal`, `ImporteTotal − ImportePagado` y estado. Orden `Numero DESC`.
Página de 20 con `{ items, total, pagina, tamanioPagina }`.

Mes y año filtran **por separado**, igual que el listado de facturas: elegir sólo `2026` trae todas las
de ese año.

### Detalle (FR-025)

La liquidación con su transportista, **todas** sus filas de `LiquidacionViajes` —vigentes o no— con sus
viajes, sus órdenes de pago con el usuario y su historial con el usuario. Los datos del transportista se
leen del padrón (FR-026).

---

## Transacciones

Las cuatro son **todo o nada** (FR-018, FR-051, FR-060) y **releen el detalle al terminar** para armar
la respuesta: la entidad con la que se escribió no refleja lo que hicieron los `UPDATE` condicionales
(convención [006], research §12.4).

### Generar (FR-011, FR-014, FR-018)

```
1. Validar.                                                  ──▶ 400 sin tocar nada
   empresa emisora configurada · transportista externo y activo · período admitido ·
   viajeIds no vacío · cada viaje del transportista, del período, rendido o facturado ·
   ninguno con vínculo vigente (consulta previa: da el mensaje bueno) · total > 0
2. BEGIN
     INSERT Liquidaciones (ImporteTotal = suma, ImportePagado = 0, Estado = pendiente)
     INSERT LiquidacionViajes (una por viaje, Vigente = 1)
       ── violación de IX_LiquidacionViajes_ViajeVigente ⇒ ROLLBACK + ChangeTracker.Clear()
          y 409 viaje_ya_liquidado nombrando el viaje y la liquidación que lo tiene
     INSERT CambiosDeLiquidacion (generacion)
   COMMIT
3. Releer el detalle.                                        ──▶ 201
```

### Editar (FR-045 a FR-052)

```
1. Validar.                                                  ──▶ 400 / 409 sin tocar nada
   liquidación existente · editable (consulta previa: da el motivo) ·
   versión igual a la que se abrió (consulta previa: 409 liquidacion_modificada) ·
   conjunto final no vacío · agregados disponibles del mismo transportista y período ·
   quitados que efectivamente le pertenecen · total nuevo > 0
   Conjunto igual al actual ⇒ no se escribe nada y se devuelve el detalle (no hay edición que registrar).
2. BEGIN
     UPDATE Liquidaciones SET ImporteTotal = @nuevo, Version = Version + 1
      WHERE Id = @id AND Estado = pendiente AND ImportePagado = 0 AND Version = @versionAbierta
       ── 0 filas ⇒ ROLLBACK y releer:
            ya no está pendiente o tiene pagos ⇒ 409 liquidacion_no_editable con el motivo actual
            sigue editable pero con otra versión ⇒ 409 liquidacion_modificada
     DELETE LiquidacionViajes WHERE LiquidacionId = @id AND ViajeId IN (@quitados)
     INSERT LiquidacionViajes (agregados, Vigente = 1)
       ── violación del índice ⇒ ROLLBACK + ChangeTracker.Clear() y 409 viaje_ya_liquidado
     INSERT CambiosDeLiquidacion (edicion)
     INSERT CambiosDeLiquidacionViajes (una por viaje quitado con Agregado = 0, una por agregado con 1)
   COMMIT
```

El `UPDATE` va **primero** a propósito: toma el bloqueo de la fila antes de tocar los vínculos, y es lo
que hace que una orden de pago simultánea espere o haga fallar a la edición (research §2).

**La condición de `Version` es lo que vuelve segura la diferencia de viajes**: quitados y agregados se
calculan antes de la transacción, contra la composición leída. Si la versión sigue siendo la abierta
cuando el `UPDATE` toma el bloqueo, nadie cambió la composición en el medio y la diferencia es correcta;
si no, la edición se rechaza antes de tocar un solo vínculo.

### Anular (FR-053 a FR-060)

```
1. Validar, en este orden.
   liquidación existente                                     ──▶ 404
   anulable (consulta previa)                                ──▶ 409 liquidacion_no_anulable
   motivo con texto, hasta 500                               ──▶ 400 motivo_requerido
   confirmado = true                                         ──▶ 409 anulacion_requiere_confirmacion
2. BEGIN
     UPDATE Liquidaciones SET Estado = anulada, MotivoAnulacion = @motivo
      WHERE Id = @id AND Estado = pendiente AND ImportePagado = 0
       ── 0 filas ⇒ ROLLBACK, releer y 409 liquidacion_no_anulable con el motivo actual
     UPDATE LiquidacionViajes SET Vigente = 0 WHERE LiquidacionId = @id
     INSERT CambiosDeLiquidacion (anulacion)
   COMMIT
```

La confirmación se pide **después** de verificar que la anulación procede: pedir confirmación para algo
que igual se va a rechazar es pedir una decisión que no existe.

### Pagar (FR-036 a FR-044)

```
1. Validar, en este orden.
   liquidación existente                                     ──▶ 404
   estado pendiente                                          ──▶ 409 liquidacion_no_pagable
   fecha presente y en [fecha de generación, hoy]            ──▶ 400 fecha_de_pago_fuera_de_rango
   importe > 0                                               ──▶ 400 datos_invalidos
   importe ≤ resta pagar                                     ──▶ 400 importe_supera_saldo
   confirmado = true                                         ──▶ 409 pago_requiere_confirmacion
                                                                 { importe, restaPagarAntes,
                                                                   restaPagarDespues, quedaPagada }
2. BEGIN
     UPDATE Liquidaciones
        SET ImportePagado = ImportePagado + @importe,
            Estado = CASE WHEN ImportePagado + @importe = ImporteTotal THEN pagada ELSE pendiente END
      WHERE Id = @id AND Estado = pendiente AND ImportePagado + @importe <= ImporteTotal
       ── 0 filas ⇒ ROLLBACK y releer:
            ya no está pendiente   ⇒ 409 liquidacion_no_pagable
            el importe ya no entra ⇒ 400 importe_supera_saldo con la resta actual
     INSERT OrdenesDePago
   COMMIT
```

`hoy` y `RegistradaEn` salen del mismo `TimeProvider`, en el mismo caso de uso.

---

## Lo que este modelo deliberadamente no tiene

- **Ninguna columna nueva en `Viajes`**, ni navegación desde `Viaje` a sus liquidaciones (research §1).
- **Ninguna tabla aparte para los viajes de las anuladas**: `Vigente = 0` los conserva (research §1).
- **Ninguna columna `FechaGeneracion`**: sale del historial (research §3).
- **Ninguna columna `RestaPagar`**: es una resta sobre dos columnas de la misma fila.
- **Ningún motivo copiado al historial.**
- **Ningún importe ni composición completa copiada en el historial**: cada edición guarda sólo qué
  viajes quitó y agregó, y la composición de cualquier momento se reconstruye (research §3b).
- **Ningún camino para modificar o anular una orden de pago** (FR-044).
- **Ningún documento ni archivo** de la liquidación.
