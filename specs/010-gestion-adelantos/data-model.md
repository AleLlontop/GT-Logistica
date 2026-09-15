# Modelo de datos: Gestión de adelantos de sueldo (Módulo 10)

Dos tablas nuevas, **ninguna tabla modificada**, ninguna secuencia, una migración: `Modulo10Adelantos`.

```
   Personas (Módulo 2) ──── Choferes (Módulo 3) ──── Transportistas (Módulo 3)      EmpresaEmisora (Módulo 6)
          │                        │                          │                              │
          │ referencia             │ sólo se lee: ¿tiene      │ sólo se lee su CUIT          │ sólo se lee su CUIT
          │ (sin navegación        │ ficha? ¿está activa?     │                              │
          │  inversa)              ▼                          ▼                              ▼
          ▼                   ──────────── ElegibilidadDeBeneficiario (regla pura) ────────────
  ┌──────────────────────────────────────────────┐
  │                   Adelantos                  │ 1 — *  ┌──────────────────────────┐
  │ persona · tipo · fecha · motivo · importe ·  │───────▶│    CambiosDeAdelanto     │
  │ estado · motivo de rechazo · de anulación    │        │ operación · usuario ·    │
  └──────────────────────────────────────────────┘        │ instante                 │
                                                          └──────────────────────────┘
```

**Ninguna tabla ni entidad de un módulo anterior cambia.** `Personas`, `Choferes`, `Transportistas` y
`EmpresaEmisora` se leen; no ganan columna, índice, navegación ni método (spec §Key Entities, research §1).

---

## Tabla `Adelantos`

Entidad principal del módulo. Pertenece a exactamente una persona del padrón. **No se borra ni se
modifica**: sólo cambia de estado (FR-035).

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK identidad. No se muestra como número propio (spec §Assumptions) |
| `PersonaId` | `int` | no | FK → `Personas`, `Restrict`, **sin navegación inversa** desde `Persona`. Elegible **al registrar** (FR-009); si después se da de baja, el adelanto no cambia |
| `TipoBeneficiario` | `tinyint` | no | `chofer=1`, `empleado=2`. `CK_Adelantos_TipoBeneficiario`: `CHECK ([TipoBeneficiario] IN (1, 2))`. **El elegido al registrar, validado y congelado**: no se recalcula nunca (FR-010, FR-020) |
| `Fecha` | `date` | no | entre el primer día del mes anterior y el día en curso, validado en la aplicación (FR-005). **Sin `CHECK`**: el piso se mueve con el calendario, igual que `Facturas.PeriodoAnio` |
| `Motivo` | `nvarchar(200)` | no | recortado antes de guardar. `CHECK (LEN([Motivo]) > 0)` (FR-006) |
| `Importe` | `decimal(18,2)` | no | `CHECK ([Importe] > 0)` (FR-007). A lo sumo dos decimales, validado en la aplicación |
| `Estado` | `tinyint` | no | `pendiente=0`, `aprobado=1`, `rechazado=2`, `anulado=3`. **Sin `DEFAULT`**: la entidad nace `pendiente` y EF manda siempre el valor (FR-010) |
| `MotivoRechazo` | `nvarchar(500)` | sí | con texto **exactamente** cuando está `rechazado` (FR-024) |
| `MotivoAnulacion` | `nvarchar(500)` | sí | con texto **exactamente** cuando está `anulado` (FR-031) |

### La restricción que ata el estado a los motivos

```sql
-- ⚠ 0, 1, 2 y 3 son EstadoAdelanto escrito a mano: reordenar el enum no falla al compilar.
-- ⚠ Cada LEN va detrás de su IS NOT NULL: LEN(NULL) es NULL, la comparación da UNKNOWN y un CHECK
--   deja pasar UNKNOWN. Sin el IS NOT NULL, un rechazado sin motivo pasaría (research §9.2).
ALTER TABLE Adelantos ADD CONSTRAINT CK_Adelantos_Estado CHECK (
     ([Estado] IN (0, 1) AND [MotivoRechazo] IS NULL AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 2 AND [MotivoRechazo] IS NOT NULL AND LEN([MotivoRechazo]) > 0 AND [MotivoAnulacion] IS NULL)
  OR ([Estado] = 3 AND [MotivoAnulacion] IS NOT NULL AND LEN([MotivoAnulacion]) > 0 AND [MotivoRechazo] IS NULL)
);
```

Con él, FR-024 y FR-029 —rechazo y anulación exigen motivo— y la exclusión entre los dos motivos son
garantías del dato y no del código. Dos columnas y no una `MotivoDeCierre`: un rechazado nunca se anula
(FR-034), así que nunca conviven, pero dos nombres dicen qué motivo es sin mirar el estado (research §3).

### Índices

```sql
CREATE INDEX IX_Adelantos_Fecha     ON Adelantos (Fecha DESC, Id DESC);  -- orden del listado (FR-017) y rango (FR-014)
CREATE INDEX IX_Adelantos_PersonaId ON Adelantos (PersonaId);            -- filtro por persona y personas con adelantos
CREATE INDEX IX_Adelantos_Estado    ON Adelantos (Estado);
```

**El orden del listado es `Fecha DESC, Id DESC`** (FR-017): varias filas comparten fecha, y el `Id` las
desempata para que el orden sea total y ninguna se repita ni se pierda entre páginas (convención [003]).

---

## Tabla `CambiosDeAdelanto`

Historial de FR-036: quién y cuándo registró, aprobó, rechazó o anuló el adelanto.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK |
| `AdelantoId` | `int` | no | FK → `Adelantos`, `Restrict` |
| `Operacion` | `tinyint` | no | `registro=0`, `aprobacion=1`, `rechazo=2`, `anulacion=3` |
| `UsuarioId` | `int` | no | FK → `Usuarios`, `Restrict`. Llega por parámetro desde el endpoint |
| `OcurridoEn` | `datetime2` | no | instante UTC del servidor, con `TimeProvider`. Sale con `Z` (convención [002]) |

```sql
-- Cada operación, a lo sumo una vez por adelanto: las transiciones de FR-034 no repiten ninguna.
-- Sin filtro, así que no lleva ningún valor de enum escrito a mano.
CREATE UNIQUE INDEX IX_CambiosDeAdelanto_Operacion ON CambiosDeAdelanto (AdelantoId, Operacion);
```

- **Todo adelanto tiene al menos una entrada**, la de su registro, escrita en la misma transacción que la
  fila (spec §Relationships). Y **a lo sumo tres**: registro, resolución y anulación.
- **El motivo no se copia acá**: se lee de `Adelantos.MotivoRechazo` o `MotivoAnulacion`. Hay un solo
  rechazo y una sola anulación posibles, y una copia podría discrepar (mismo criterio que el Módulo 9).
  El DTO del historial sí lleva `motivo`, armado al leer.
- **La fecha de registro no es columna del adelanto**: es `OcurridoEn` de la entrada `registro`. Y no es la
  `Fecha` del adelanto, que es el día en que se otorgó (spec §Assumptions).
- No se edita ni se borra por ninguna vía: ningún endpoint la escribe directamente.

---

## Enumeraciones

```csharp
/// ⚠ Los números importan: CK_Adelantos_Estado los lleva escritos a mano.
public enum EstadoAdelanto : byte { Pendiente = 0, Aprobado = 1, Rechazado = 2, Anulado = 3 }

public enum OperacionDeAdelanto : byte { Registro = 0, Aprobacion = 1, Rechazo = 2, Anulacion = 3 }

/// ⚠ CK_Adelantos_TipoBeneficiario lleva el 1 y el 2 escritos a mano.
/// No es TipoIntegrante del Módulo 2: aquél es un dato informativo del padrón, y éste se decide con la
/// ficha de chofer (research §1). Dos ejes distintos, dos enums, como TipoPersona y TipoIntegrante.
public enum TipoBeneficiario : byte { Chofer = 1, Empleado = 2 }
```

Viajan en el JSON en **camelCase** —`pendiente`, `aprobado`, `registro`, `chofer`— con su traducción al
español en `NombresDeEstadoAdelanto` (convención [003]). Los cuatro estados son **excluyentes** y el
filtro opera sobre la columna, que es exactamente lo que la fila muestra (FR-033).

---

## Reglas de dominio (funciones puras)

En `GT.Domain/Adelantos/`. **Reciben el día por parámetro y nunca leen el reloj** (convención [005]).

### `ReglasDeAdelanto`

| Regla | Firma conceptual | Requisito |
|---|---|---|
| Piso de la fecha | `PrimeraFechaAdmitida(hoy)` → primer día del mes anterior a `hoy`, cruzando el año | FR-005 |
| Fecha admitida | `FechaAdmitida(fecha, hoy)` → `PrimeraFechaAdmitida(hoy) ≤ fecha ≤ hoy` | FR-005 |
| Importe válido | `ImporteValido(importe)` → `> 0`, a lo sumo dos decimales y dentro de `decimal(18,2)` | FR-007, FR-008 |
| Por qué no se aprueba ni rechaza | `MotivoNoResoluble(estado)` → `null` si `Pendiente`; si no, el estado | FR-021 |
| Por qué no se anula | `MotivoNoAnulable(estado)` → `null` si `Aprobado`; si no, el estado | FR-027, FR-028 |

`PrimeraFechaAdmitida(01/01/2027)` es `01/12/2026` (spec §Edge Cases). **La regla vive dos veces**: acá y
en el frontend, que la usa para la ayuda del campo y el error antes de enviar. El servidor es quien la
garantiza, como `PeriodoAdmitido` (convención [009]).

### `ElegibilidadDeBeneficiario`

La **única** escritura de FR-002 y FR-009 (research §1):

```
Evaluar(tipo, persona, cuitEmpresaEmisora) → null | MotivoNoElegible

  persona          : null | { Activa, Tipo, TieneFicha, FichaActiva, CuitTransportista }
  cuitEmpresaEmisora : null si no está configurada
```

| Orden | Condición | Resultado |
|---|---|---|
| 1 | `persona` es `null` | `inexistente` |
| 2 | `!persona.Activa` | `inactiva` |
| 3 | `tipo = Chofer` y `cuitEmpresaEmisora` es `null` | `empresaEmisoraNoConfigurada` |
| 4 | `tipo = Chofer` y `!persona.TieneFicha` | `tipoDistinto` |
| 5 | `tipo = Chofer` y `!persona.FichaActiva` | `inactiva` |
| 6 | `tipo = Chofer` y `persona.CuitTransportista ≠ cuitEmpresaEmisora` | `choferExterno` |
| 7 | `tipo = Empleado` y (`persona.TieneFicha` o `persona.Tipo ≠ Empleado`) | `tipoDistinto` |
| — | ninguna de las anteriores | `null`: elegible |

- **Chofer lo decide la ficha, no `Persona.Tipo`** (research §1 del Módulo 3): una persona cargada como
  empleado y registrada después como chofer aparece bajo *Chofer* y deja de aparecer bajo *Empleado*.
- **Empleado exige no tener ficha, activa o no**: así nadie aparece bajo los dos tipos, y quien tiene la
  ficha dada de baja no aparece bajo ninguno (spec §Edge Cases).
- Los dos CUIT ya están normalizados a once dígitos, como en el Módulo 9: la comparación es exacta.

---

## Consultas

### Beneficiarios (FR-002, FR-002a, FR-004)

```csharp
contexto.Personas
    .Where(persona => persona.Activa)
    .Select(persona => new {
        persona.Id, persona.Apellido, persona.Nombre, persona.Dni, persona.Tipo,
        TieneFicha = persona.Chofer != null,
        FichaActiva = persona.Chofer != null && persona.Chofer.Activo,
        CuitTransportista = persona.Chofer != null ? persona.Chofer.Transportista!.Cuit : null })
    .OrderBy(fila => fila.Apellido).ThenBy(fila => fila.Nombre).ThenBy(fila => fila.Id)
```

y **después, en memoria**, `ElegibilidadDeBeneficiario.Evaluar(tipo, fila, cuitEmisora) is null`. Sin
paginar: es el desplegable de un padrón de decenas de personas (research §1). La respuesta dice además
`empresaEmisoraConfigurada`.

### Validación al registrar (FR-009)

La **misma** proyección filtrada por `Id` —sin la condición de `Activa`, para poder decir `inactiva` y no
`inexistente`— y la **misma** regla. Lo que el desplegable ofrece y lo que el guardado acepta no pueden
separarse, porque es una sola función (convención [006]).

### Personas con adelantos (FR-014)

```csharp
contexto.Personas
    .Where(persona => contexto.Adelantos.Any(adelanto => adelanto.PersonaId == persona.Id))
    .OrderBy(p => p.Apellido).ThenBy(p => p.Nombre).ThenBy(p => p.Id)
```

Activas o dadas de baja, de cualquier tipo, sin mirar el estado de sus adelantos (spec §Clarifications).

### Listado (FR-013 a FR-018)

Filtros opcionales y combinables, **todos aplicados antes de contar, sumar y paginar**:

| Filtro | Predicado |
|---|---|
| `personaId` | `PersonaId == personaId` |
| `desde` | `Fecha >= desde` |
| `hasta` | `Fecha <= hasta` |
| `estado` | `Estado == estado`, sobre la columna |

`desde > hasta` ⇒ `400 rango_invalido` sin consultar (FR-015).

Sobre la consulta filtrada:

- `total` = `CountAsync`.
- `totalAdelantado` = `Where(Estado == Aprobado).SumAsync(Importe)`: **toda** la selección y no la página
  (FR-016). Con un filtro de estado que no sea `aprobado`, da `0` y es correcto (research §5).
- las 20 filas, `OrderByDescending(Fecha).ThenByDescending(Id)`.

Proyección: `id`, `fecha`, `persona` (apellido, nombre y DNI **del padrón vigente**, FR-020), `tipo`
guardado, `motivo`, `importe`, `estado`.

Respuesta: `{ items, total, pagina, tamanioPagina, totalAdelantado }`. Es la forma de la convención [003]
con **un campo más**, en un DTO propio del módulo (`PaginaDeAdelantos`); `PaginaDe<T>` no se toca.

### Detalle (FR-019, FR-020, FR-036)

El adelanto con su persona y su historial con el usuario, ordenado por `OcurridoEn` y `Id`. Los datos de
la persona se leen del padrón. Cada entrada de `rechazo` y `anulacion` lleva el motivo **leído de la
columna** del adelanto. `puedeResolverse` y `puedeAnularse` salen del estado; que el usuario tenga el
permiso lo decide la sesión (como en el Módulo 9).

---

## Transacciones

Las cuatro son **todo o nada** y **releen el detalle al terminar** para armar la respuesta (convención
[006]).

### Registrar (FR-001 a FR-012)

```
1. Validar, en este orden.                                   ──▶ 400 sin tocar nada
   tipo presente y conocido · persona presente · fecha presente    datos_invalidos con campo
   fecha admitida                                                  fecha_fuera_de_rango { desde, hasta }
   motivo con texto, hasta 200                                     datos_invalidos con campo
   importe válido                                                  datos_invalidos con campo
   elegibilidad (proyección por Id + ElegibilidadDeBeneficiario)   empresa_emisora_no_configurada
                                                                   beneficiario_no_elegible { motivo }
2. BEGIN
     INSERT Adelantos (Estado = pendiente)
     INSERT CambiosDeAdelanto (registro)
   COMMIT
3. Releer el detalle.                                        ──▶ 201
```

**La elegibilidad no la cierra la base**, y se declara: una baja de la persona entre la validación y el
`INSERT` deja registrado un adelanto de alguien que se dio de baja un instante después. Es exactamente el
estado que la spec ya admite —un adelanto cuya persona se dio de baja después— y cerrarlo exigiría
bloquear filas del Módulo 2 desde este módulo (research §2).

### Aprobar (FR-021 a FR-023, FR-026)

```
1. existente                                                 ──▶ 404
   pendiente (consulta previa: da el estado)                 ──▶ 409 adelanto_no_resoluble { estado }
2. BEGIN
     UPDATE Adelantos SET Estado = aprobado
      WHERE Id = @id AND Estado = pendiente
       ── 0 filas ⇒ ROLLBACK, releer y 409 adelanto_no_resoluble con el estado actual
     INSERT CambiosDeAdelanto (aprobacion)
   COMMIT
3. Releer.                                                   ──▶ 200
```

### Rechazar (FR-021, FR-024 a FR-026)

```
1. existente                                                 ──▶ 404
   pendiente                                                 ──▶ 409 adelanto_no_resoluble { estado }
   motivo con texto, hasta 500                               ──▶ 400 motivo_requerido / datos_invalidos
   confirmado = true                                         ──▶ 409 rechazo_requiere_confirmacion
2. BEGIN
     UPDATE Adelantos SET Estado = rechazado, MotivoRechazo = @motivo
      WHERE Id = @id AND Estado = pendiente
       ── 0 filas ⇒ ROLLBACK, releer y 409 adelanto_no_resoluble con el estado actual
     INSERT CambiosDeAdelanto (rechazo)
   COMMIT
```

Aprobar contra rechazar, dos aprobaciones o dos rechazos: la segunda transacción se bloquea sobre la fila
y, al desbloquearse, reevalúa `Estado = pendiente` contra el dato confirmado (convención [009]).

### Anular (FR-027 a FR-032)

```
1. existente                                                 ──▶ 404
   aprobado                                                  ──▶ 409 adelanto_no_anulable { estado }
   motivo con texto, hasta 500                               ──▶ 400 motivo_requerido / datos_invalidos
   confirmado = true                                         ──▶ 409 anulacion_requiere_confirmacion
2. BEGIN
     UPDATE Adelantos SET Estado = anulado, MotivoAnulacion = @motivo
      WHERE Id = @id AND Estado = aprobado
       ── 0 filas ⇒ ROLLBACK, releer y 409 adelanto_no_anulable con el estado actual
     INSERT CambiosDeAdelanto (anulacion)
   COMMIT
```

En el rechazo y en la anulación, la confirmación se pide **después** de verificar que la operación
procede, como en el Módulo 9. Los dos estados son finales, y por eso los dos se confirman en el backend
(research §4).

`IX_CambiosDeAdelanto_Operacion` es una segunda red y no la primera: si el `UPDATE` condicional fallara en
su trabajo, la segunda entrada de la misma operación no entraría.

---

## Lo que este modelo deliberadamente no tiene

- **Ninguna columna en `Personas`, `Choferes`, `Transportistas` ni `EmpresaEmisora`**, ni navegación desde
  `Persona` a sus adelantos.
- **Ninguna copia de apellido, nombre ni DNI**: se leen del padrón vigente (FR-020). Lo único congelado es
  el tipo.
- **Ningún número visible ni secuencia** (spec §Assumptions).
- **Ninguna columna `FechaDeRegistro`**: sale del historial.
- **Ningún motivo copiado al historial.**
- **Ninguna `Version`**: no hay edición, así que las cuatro carreras las distingue el estado (research §2).
- **Ninguna marca de entregado, de aplicado a una liquidación de haberes ni movimiento de caja**
  (FR-023, FR-028, FR-037).
- **Ningún tope ni acumulado guardado** (FR-012): el total adelantado se suma al leer.
