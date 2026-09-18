# Data Model: Gestión de caja (Módulo 11)

## Enumeraciones

### `EstadoCaja` (`GT.Domain.Caja.EstadoCaja`, `byte`)

| Valor | Número | Notas |
|---|---|---|
| `Abierta` | 0 | Nace acá (FR-001). Admite movimientos. |
| `Cerrada` | 1 | Terminal: no se reabre (fuera de alcance). |

⚠ Los números sostienen el `CHECK` de `Cajas` (§Restricciones). Reordenar el enum no falla al compilar.

### `TipoMovimientoCaja` (`GT.Domain.Caja.TipoMovimientoCaja`, `byte`)

| Valor | Número | Referencia que admite |
|---|---|---|
| `Ingreso` | 0 | `FacturaId` opcional; `OrdenDePagoId` siempre `NULL` |
| `Egreso` | 1 | `OrdenDePagoId` opcional; `FacturaId` siempre `NULL` |

⚠ Los números sostienen el `CHECK` de `MovimientosDeCaja` (§Restricciones, research §6).

## Entidades

### `Caja`

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | `int` PK identity | — |
| `SaldoInicial` | `decimal(18,2)` NOT NULL | ≥ 0 (RN4, FR-002) |
| `FechaApertura` | `datetime2` NOT NULL | Instante del servidor (`TimeProvider`), nunca tipeado (FR-005) |
| `UsuarioResponsableId` | `int` NOT NULL, FK → `Usuarios.Id` | Quien la abre (FR-001) |
| `Estado` | `tinyint` NOT NULL | `EstadoCaja`, default `Abierta` |
| `FechaCierre` | `datetime2` NULL | Obligatoria si `Cerrada`, prohibida si `Abierta` |
| `SaldoFinal` | `decimal(18,2)` NULL | Ídem |

No hay `Numero` ni ningún identificador visible propio (spec §Assumptions: "la caja no se identifica con
un número visible propio"): se referencia por su responsable, fecha de apertura y estado.

### `MovimientoDeCaja`

| Columna | Tipo | Regla |
|---|---|---|
| `Id` | `int` PK identity | — |
| `CajaId` | `int` NOT NULL, FK → `Cajas.Id` | (RF3) |
| `Tipo` | `tinyint` NOT NULL | `TipoMovimientoCaja` |
| `Importe` | `decimal(18,2)` NOT NULL | > 0, ≤ 2 decimales (RN3, FR-008) |
| `Concepto` | `nvarchar(200)` NOT NULL | No vacío tras `Trim()` (RN5, FR-009) |
| `UsuarioId` | `int` NOT NULL, FK → `Usuarios.Id` | Quien lo carga (RF5, RN7) |
| `Fecha` | `datetime2` NOT NULL | Instante del servidor, nunca tipeado (FR-013) |
| `FacturaId` | `int` NULL, FK → `Facturas.Id` | Sólo con `Tipo = Ingreso`; debe estar `Pendiente` al guardar |
| `OrdenDePagoId` | `int` NULL, FK → `OrdenesDePago.Id` | Sólo con `Tipo = Egreso`; sin filtro de estado (research §4) |

No se edita, no se anula, no se borra (FR-014): no hay `Version`, no hay endpoint que lo permita.

## Restricciones (`CHECK`, índices)

```sql
-- Cajas
ALTER TABLE Cajas ADD CONSTRAINT CK_Cajas_SaldoInicial CHECK (SaldoInicial >= 0);

ALTER TABLE Cajas ADD CONSTRAINT CK_Cajas_CierreConsistente CHECK (
    (Estado = 0 AND FechaCierre IS NULL AND SaldoFinal IS NULL) OR
    (Estado = 1 AND FechaCierre IS NOT NULL AND SaldoFinal IS NOT NULL)
);

-- RN1: una caja abierta por empleado. Índice único FILTRADO, no una tabla aparte (research §1).
CREATE UNIQUE INDEX IX_Cajas_UsuarioResponsable_Abierta
    ON Cajas (UsuarioResponsableId) WHERE Estado = 0;

-- MovimientosDeCaja
ALTER TABLE MovimientosDeCaja ADD CONSTRAINT CK_MovimientosDeCaja_Importe CHECK (Importe > 0);

ALTER TABLE MovimientosDeCaja ADD CONSTRAINT CK_MovimientosDeCaja_Concepto
    CHECK (LEN(LTRIM(RTRIM(Concepto))) > 0);

-- RN8: la referencia sigue el tipo. CHECK en la base, no sólo validación de aplicación (research §6).
ALTER TABLE MovimientosDeCaja ADD CONSTRAINT CK_MovimientosDeCaja_Referencia CHECK (
    (Tipo = 0 AND OrdenDePagoId IS NULL) OR
    (Tipo = 1 AND FacturaId IS NULL)
);

CREATE INDEX IX_MovimientosDeCaja_Caja_Fecha ON MovimientosDeCaja (CajaId, Fecha);
CREATE INDEX IX_MovimientosDeCaja_Fecha ON MovimientosDeCaja (Fecha);
```

`FacturaId` y `OrdenDePagoId` **no** llevan `ON DELETE`/actualización en cascada: ninguna de las dos
tablas de origen borra filas (FR-014 de este módulo y FR-044 del Módulo 9 son simétricos: nada se
borra en ningún lado). Tampoco llevan índice único: la spec permite que la misma factura o la misma
orden de pago se referencie desde más de un movimiento (RN8, FR-012).

## Transacciones

### Abrir (`AbrirAsync`)

```text
1. Verificación previa (fuera de transacción, da el mensaje de CA2):
   ¿existe una fila en Cajas con UsuarioResponsableId = X y Estado = Abierta?
   → si sí, rechazar con "cerrá la actual primero" (FR-003).
2. INSERT en una transacción normal.
3. Si el INSERT viola IX_Cajas_UsuarioResponsable_Abierta (dos pedidos simultáneos, CL3):
   el repositorio traduce la violación a CajaYaAbiertaException (convención [003], patrón de
   GuardarTraduciendoIndiceAsync del Módulo 9) y el caso de uso la traduce al mismo mensaje de FR-003.
```

### Registrar movimiento (`RegistrarMovimientoAsync`)

```text
1. BEGIN TRAN
2. "Touch" UPDATE que toma el lock de la fila de la caja, sin cambiar nada (research §2):
     UPDATE Cajas SET Estado = Estado WHERE Id = @cajaId AND Estado = 0 (Abierta)
   Si afecta 0 filas → ROLLBACK, devolver "no hay caja abierta" (RN2, FR-007).
3. Si la referencia (FacturaId) viene informada: revalidar que exista y siga Pendiente
   (research §4; OrdenDePagoId no se revalida porque no tiene estado).
4. INSERT en MovimientosDeCaja.
5. COMMIT
```

### Consultar resumen de cierre (`ConsultarResumenAsync`, sin escribir nada)

```text
SELECT SaldoInicial de la caja + SUM(Importe) WHERE Tipo = Ingreso - SUM(Importe) WHERE Tipo = Egreso,
sobre los movimientos de esa caja. Sin transacción: es una foto, no un candado (RF6, RN6).
```

### Cerrar (`CerrarAsync`)

```text
1. BEGIN TRAN
2. Mismo "touch" UPDATE de §Registrar movimiento, mismo WHERE Estado = Abierta.
   Si afecta 0 → ROLLBACK, releer y devolver "ya está cerrada" (FR-021).
3. Con el lock tomado, releer los movimientos y calcular SaldoFinal = SaldoInicial + Σingresos − Σegresos.
4. Si no llegó confirmado:true → ROLLBACK, 409 confirmacion_requerida con el resumen (US3 esc. 6).
   Si SaldoFinalConfirmado ≠ SaldoFinal recién calculado → ROLLBACK, 409 cierre_desactualizado con el
   resumen actualizado (research §3, FR-018, US3 esc. 7).
5. UPDATE Cajas SET Estado = Cerrada, FechaCierre = @ahora, SaldoFinal = @saldoFinal
   WHERE Id = @id AND Estado = Abierta (misma transacción, mismo lock ya tomado: no puede fallar).
6. COMMIT
```

## Consultas de sólo lectura

- **Listado de cajas** (FR-026): pagina `Cajas` ordenado por `FechaApertura DESC, Id DESC` (orden total,
  convención [003]), proyectando responsable, fecha de apertura, estado, y si está cerrada, fecha de
  cierre y saldo final.
- **Listado de movimientos** (FR-023 a FR-025): filtra por `CajaId` y/o rango `[Desde, Hasta]` sobre
  `Fecha` (los dos extremos incluidos, cualquiera de los dos opcional), pagina, y proyecta fecha, tipo,
  importe, concepto, responsable y la referencia armada (`Factura #…` o el número de orden de pago) si
  la tiene.
- **Facturas pendientes** (desplegable del ingreso): `Facturas.Where(f => f.Estado == Pendiente)`
  (research §5), sin paginar.
- **Órdenes de pago** (desplegable del egreso): sin filtro de estado, `OrderByDescending(Numero)`
  `.Take(50)` en SQL (research §4, FR-011). El guardado sólo verifica que exista.
