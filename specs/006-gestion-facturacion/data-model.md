# Modelo de datos: Gestión de facturación (Módulo 6)

Tres tablas nuevas, una tabla modificada, una migración: `Modulo6Facturacion`.

```
  EmpresaEmisora (1 fila)          Clientes (Módulo 5)         Viajes (Módulo 5)
        │                                 │                          │
        │ se copia al emitir              │ se copia al emitir       │ FacturaId (NUEVA)
        │ (nunca se referencia)           │ + se referencia          │
        ▼                                 ▼                          ▼
  ┌──────────────────────────────────────────────────────────────────────┐
  │                              Facturas                                │
  │  número · fecha · tipo · período · importes · CAE · estado · documento│
  └───────────────┬──────────────────────────────────┬───────────────────┘
                  │ 1 — *                            │ 0..1 — 0..1 (auto-referencia)
                  ▼                                  ▼
        CambiosDeEstadoFactura              FacturaReemplazadaId
```

---

## Tabla `EmpresaEmisora`

Configuración única de todo el sistema (FR-001). **Se edita, nunca se crea una segunda ni se borra**,
y eso lo garantiza la base con un `CHECK`, no la disciplina del código (research §12).

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK. `CHECK ([Id] = 1)` |
| `RazonSocial` | `nvarchar(200)` | **no** | `Trim` al guardar (FR-002) |
| `Cuit` | `nvarchar(11)` | **no** | once dígitos con verificador válido; **normalizado antes de validar y de guardar**, con `NormalizadorDocumentoNumerico` y `ValidadorCuit` del Módulo 3 |
| `Domicilio` | `nvarchar(200)` | **no** | (FR-002) |
| `CondicionIva` | `nvarchar(100)` | **no** | texto libre: la spec no enumera opciones para el emisor |
| `IngresosBrutos` | `nvarchar(50)` | sí | opcional (FR-002) |
| `InicioActividades` | `date` | sí | opcional |
| `PuntoDeVenta` | `nvarchar(4)` | sí | cuatro dígitos; se propone en el alta de factura (FR-027) |
| `Cbu` | `nvarchar(22)` | sí | opcional. Vacío ⇒ la banda de CBU no sale en el documento (FR-031, US2 esc. 28) |
| `Telefono` | `nvarchar(50)` | sí | opcional |
| `Email` | `nvarchar(254)` | sí | opcional, con formato válido si viene |
| `LogoRuta` | `nvarchar(260)` | sí | ruta relativa dentro del volumen; el nombre en disco lo genera el sistema (FR-003) |
| `LogoTipoContenido` | `nvarchar(100)` | sí | deducido de la **firma** del archivo, no de la extensión |
| `LogoNombreOriginal` | `nvarchar(255)` | sí | sólo para mostrar y para "Guardar como" |

**La fila no existe hasta el primer guardado.** El `GET` sin fila responde `configurada: false` con la
lista de los cuatro obligatorios faltantes; el `PUT` la crea la primera vez y la actualiza siempre
después (research §12).

**Sin logo la factura se emite igual** (FR-004) y el documento acomoda el bloque del emisor a su
ausencia, sin hueco ni imagen rota (FR-031g).

---

## Tabla `Facturas`

Entidad principal del módulo. Pertenece a exactamente un cliente y agrupa uno o más viajes.

### Identificación y clasificación

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK identidad |
| `NumeroComprobante` | `nvarchar(13)` | no | formato `0014-00000003`. **Único entre las no anuladas** por índice filtrado (FR-027) |
| `Fecha` | `date` | no | fecha de facturación. Se propone hoy y se puede cambiar (FR-012). Es la fecha de corte de los totales (FR-061) |
| `TipoComprobante` | `tinyint` | no | `facturaA=0`, `facturaB=1`, `facturaC=2` (FR-008) |
| `TipoFacturacion` | `tinyint` | no | `original=0`, `refacturacion=1` (FR-009) |
| `CondicionDeVenta` | `tinyint` | no | `contado=0`, `cuentaCorriente=1`, `tarjeta=2`, `cheque=3` (FR-009a). Es dato de la factura, no del cliente |
| `PeriodoMes` | `tinyint` | no | 1–12. `CHECK` entre 1 y 12 |
| `PeriodoAnio` | `smallint` | no | `2025` o `2026`, validado en la aplicación (FR-010). **Sin `CHECK`**: la lista se amplía con los años y una restricción de base obligaría a una migración cada vez (*Assumptions*) |
| `Detalle` | `nvarchar(500)` | sí | opcional (FR-013). Es el único dato de texto libre que se puede corregir después (FR-035) |

### Cliente: referencia **y** copia congelada

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `ClienteId` | `int` | no | FK → `Clientes`, `Restrict`. Es la que usan el vínculo, el filtro (FR-058) y los totales (FR-061) |
| `ClienteRazonSocial` | `nvarchar(100)` | no | copia al emitir (FR-034a) |
| `ClienteCuit` | `nvarchar(11)` | no | copia al emitir |
| `ClienteDomicilio` | `nvarchar(200)` | no | copia al emitir. **Es lo que vuelve obligatorio el domicilio para facturar** (FR-011a): la columna no admite nulo, así que un cliente sin domicilio no puede llegar acá |

Los **dos** hacen falta y ninguno reemplaza al otro: la copia es lo que muestran la ficha, el listado y
el documento; la referencia es lo que permite filtrar y totalizar. Una corrección posterior en el
padrón no altera nada de una factura ya emitida (FR-034a, US3 esc. 12, SC-007).

### Emisor: sólo copia congelada

Diez columnas, todas escritas al emitir y nunca releídas de la configuración (FR-034):

`EmisorRazonSocial` · `EmisorCuit` · `EmisorDomicilio` · `EmisorCondicionIva` · `EmisorIngresosBrutos`
· `EmisorInicioActividades` · `EmisorPuntoDeVenta` · `EmisorCbu` · `EmisorTelefono` · `EmisorEmail`.

Los cuatro primeros no admiten nulo —son los obligatorios de FR-002—; los otros seis sí, porque son
opcionales en la configuración. **No hay `EmpresaEmisoraId`**: la factura no referencia la
configuración, la copia. Y **no hay copia del logo**, que es la única excepción declarada (research §5).

### Importes

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Neto` | `decimal(18,2)` | no | suma exacta de los importes de los viajes incluidos (FR-022) |
| `Iva` | `decimal(18,2)` | no | neto × alícuota del tipo, redondeo comercial a dos decimales (FR-023) |
| `Total` | `decimal(18,2)` | no | neto + IVA. `CHECK ([Total] = [Neto] + [Iva])` |

`decimal`, nunca punto flotante (convención [005]). **Los tres son inmutables después de emitir**
(FR-025, FR-036) y **no llegan nunca desde el cliente HTTP**: los calcula el servidor a partir de los
viajes que encontró en la base (FR-024, research §9).

**La alícuota no es columna**: se deriva del tipo de comprobante con `AlicuotasIva.De(tipo)`, que está
congelado. La alternativa y por qué se descartó, en research §5.

### CAE y vencimientos

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Cae` | `nvarchar(20)` | no | obligatorio para dar por emitida la factura (FR-028). **Corregible** (FR-035), nunca vaciable (US4 esc. 6) |
| `CaeVencimiento` | `date` | no | no anterior a `Fecha` (FR-029) |
| `VencimientoPago` | `date` | no | no anterior a `Fecha` (FR-030). Se propone `Fecha + 30 días` (*Assumptions*) |

**El vencimiento del CAE no influye en el estado de cobro** (FR-041, US5 esc. 10): son dos plazos
distintos y sólo el de pago mueve la factura a `vencida`.

### Estado, cobro y anulación

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Estado` | `tinyint` | no | **`pendiente=0`, `pagada=1`, `anulada=2`**. `vencida` **no es un valor de esta columna** |
| `FechaCobro` | `date` | sí | obligatoria al registrar el cobro, no anterior a `Fecha` (FR-042). Corregir el CAE no la toca (US4 esc. 8) |
| `MotivoAnulacion` | `nvarchar(500)` | sí | obligatorio al anular (FR-046). Visible en la ficha, en el listado filtrado por `anulada` y **impreso en el documento regenerado** (FR-031d) |

### Refacturación

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `FacturaReemplazadaId` | `int` | sí | FK → `Facturas`, `Restrict`, auto-referencia. Obligatoria si `TipoFacturacion = refacturacion`, prohibida si `original` (FR-049) |

La referencia se muestra en **las dos** fichas: la nueva dice a cuál reemplaza y la anulada dice cuál
la reemplazó (FR-050). La segunda dirección se resuelve con una consulta por
`FacturaReemplazadaId == id`, no con una columna espejo que habría que mantener sincronizada.

### Documento generado

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `DocumentoRuta` | `nvarchar(260)` | no | ruta relativa dentro del volumen. **No admite nulo**: toda factura emitida tiene su documento, porque se genera en la misma operación (FR-031, SC-007a) |

La factura guarda la **referencia**, nunca el contenido (FR-031a). Se sirve por endpoint autorizado,
en línea, con nombre que identifica la factura.

---

## Índices de `Facturas`

```sql
-- FR-027. El `2` es EstadoFactura.Anulada, escrito a mano: reordenar el enum
-- no falla al compilar y deja el índice protegiendo el estado equivocado.
CREATE UNIQUE INDEX IX_Facturas_Numero
    ON Facturas (NumeroComprobante) WHERE Estado <> 2;

-- FR-049a. Una anulada la reemplaza a lo sumo una Refacturación.
CREATE UNIQUE INDEX IX_Facturas_FacturaReemplazada
    ON Facturas (FacturaReemplazadaId) WHERE FacturaReemplazadaId IS NOT NULL;

-- FR-059. El orden exacto del listado.
CREATE INDEX IX_Facturas_Fecha_Numero ON Facturas (Fecha DESC, NumeroComprobante DESC);

CREATE INDEX IX_Facturas_ClienteId ON Facturas (ClienteId);

-- FR-041, FR-063. El panel de vencimientos y el filtro por estado derivado.
CREATE INDEX IX_Facturas_Estado_VencimientoPago ON Facturas (Estado, VencimientoPago);
```

Los dos únicos son **la garantía real** de FR-027 y FR-049a, no una optimización: la consulta previa da
el mensaje bueno y el índice cierra la carrera entre dos operadores simultáneos (SC-004, research §4).
Los dos llevan un valor de enum en el filtro, y eso lo cubre `IndicesDeFacturaTests`.

---

## Tabla `CambiosDeEstadoFactura`

Historial de FR-045 **y** registro de correcciones de FR-037, en la misma tabla.

| Columna | Tipo | Nulo | Regla |
|---|---|---|---|
| `Id` | `int` | no | PK |
| `FacturaId` | `int` | no | FK → `Facturas`, `Cascade` desde la factura sólo a efectos del modelo; nada borra facturas |
| `EstadoAnterior` | `tinyint` | sí | `null` en el registro de la emisión —antes no había estado— **y** en el de una corrección |
| `EstadoNuevo` | `tinyint` | sí | `null` **sólo** en una corrección (FR-037) |
| `UsuarioId` | `int` | no | FK → `Usuarios`. Llega por parámetro desde el endpoint, igual que en el Módulo 5 |
| `OcurridoEn` | `datetime2` | no | instante UTC del servidor, con `TimeProvider`. Sale con `Z` por la convención [002] |

**Una entrada es una corrección cuando `EstadoNuevo` es `null`.** No hay columna `EsCorreccion`: la
ausencia de estado nuevo **es** la marca, y una columna que repite un dato que ya está puede
discrepar de él. En la pantalla se lee `Corrección de datos` en la columna de estado.

**No guarda qué campos cambiaron ni sus valores anteriores** (FR-037): es lo que CL7 del enunciado pide
literalmente, y una auditoría de valores sería una entidad que ningún otro módulo del sistema tiene.
Queda anotada como candidata para una spec futura.

**No se edita ni se borra por ninguna vía**: ningún endpoint la escribe directamente. La escriben los
casos de uso, en la misma transacción que el cambio que registran.

Toda factura tiene **al menos una** entrada: la de su emisión, con `EstadoAnterior = null` y
`EstadoNuevo = pendiente`.

---

## Tabla `Viajes` (modificada)

Los **seis** cambios de FR-051 a FR-055a y ninguno más (FR-056). El detalle de por qué cada uno, en
research §8.

| Cambio | Qué es |
|---|---|
| `FacturaId` `int NULL` | FK → `Facturas`, `Restrict`. Nula mientras el viaje no esté facturado |
| `IX_Viajes_FacturaId` | índice de consulta, no único: una factura tiene muchos viajes |
| `EstadoViaje.Facturado = 4` | **agregado al final** del enum. Los tres índices filtrados existentes llevan `1` y `3` escritos a mano y no se tocan |

**El índice de remito pasa a cubrir los facturados sin cambiarlo**: su filtro es
`WHERE [NumeroRemito] IS NOT NULL AND [Estado] <> 3`, y `3` sigue siendo `anulado`. Un viaje facturado
no libera su remito, que es lo correcto.

**Migración de datos: ninguna.** `FacturaId` nace nula para todas las filas existentes y el estado
nuevo no cambia el de ninguna. Los viajes que ya estaban `rendido` sin remito quedan como están, sin
poder facturarse: es la limitación conocida y aceptada de FR-019a.

---

## Enumeraciones

```csharp
public enum TipoComprobante : byte { FacturaA = 0, FacturaB = 1, FacturaC = 2 }
public enum TipoFacturacion : byte { Original = 0, Refacturacion = 1 }
public enum CondicionDeVenta : byte { Contado = 0, CuentaCorriente = 1, Tarjeta = 2, Cheque = 3 }

/// ⚠ Los números importan: IX_Facturas_Numero lleva `<> 2` escrito a mano.
public enum EstadoFactura : byte { Pendiente = 0, Pagada = 1, Anulada = 2 }

/// Lo que se ve y lo que se filtra. No es columna de ninguna tabla.
public enum EstadoFacturaVisible : byte { Pendiente = 0, Vencida = 1, Pagada = 2, Anulada = 3 }
```

Todas viajan en el JSON en **camelCase** —`facturaA`, `cuentaCorriente`, `proximaAvencer`— y su
traducción al español vive en `NombresDeEstadoFactura`, siguiendo la convención [003].

`EstadoViaje` suma `Facturado = 4` **al final**, sin reordenar los cuatro existentes.

---

## Reglas derivadas (nunca en columna)

### `vencida`

```csharp
public static EstadoFacturaVisible Derivar(EstadoFactura guardado, DateOnly vencimientoPago, DateOnly hoy) =>
    guardado switch
    {
        EstadoFactura.Pagada  => EstadoFacturaVisible.Pagada,
        EstadoFactura.Anulada => EstadoFacturaVisible.Anulada,
        _ => vencimientoPago < hoy ? EstadoFacturaVisible.Vencida : EstadoFacturaVisible.Pendiente,
    };
```

Regla pura: **recibe la fecha por parámetro y no lee el reloj** (convención [005]). Eso es lo que
permite probar en un test lo que a mano exigiría esperar a que venza una factura.

La misma regla va escrita como **predicado dentro de la consulta** para el filtro del listado
(FR-058a), y un test compara las dos sobre el mismo dato (convención [003], research §3).

Los cuatro valores del filtro son **excluyentes**: `pendiente` devuelve sólo las impagas en plazo y
`vencida` sólo las pasadas de fecha. Ninguna factura sale bajo los dos (US3 esc. 11).

### Días de atraso o de plazo (panel de vencimientos)

`VencimientoPago − hoy`, en días corridos. Negativo es atraso. El panel muestra las `vencida` y las que
vencen dentro de los **7 días corridos** siguientes; las `pagada` y `anulada` no figuran (FR-063).

### Estado de la factura de un viaje

El listado y la ficha de viajes del Módulo 5 muestran, para un viaje `facturado`, el **número y la
fecha** de su factura (FR-055). Sale de la navegación por `FacturaId`, no de columnas copiadas al
viaje.

---

## Cálculo de importes

```csharp
public static decimal De(TipoComprobante tipo) => tipo switch
{
    TipoComprobante.FacturaA => 0.21m,
    TipoComprobante.FacturaB => 0.21m,   // misma alícuota que la A (spec §Clarifications)
    TipoComprobante.FacturaC => 0.00m,   // IVA cero: el total es igual al neto (FR-023)
    _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
};
```

- **Neto** = suma exacta de los importes de los viajes seleccionados.
- **IVA** = `Math.Round(neto * alícuota, 2, MidpointRounding.AwayFromZero)`.
- **Total** = neto + IVA.

Verificado con el ejemplo de la propia spec: `82.644,63` → IVA `17.355,37` → total `100.000,00`
(US2 esc. 8). Los subtotales por fila del documento son **informativos** y pueden diferir de la suma
por centavos; manda el pie (FR-031f, research §9).

---

## Transacciones: las tres operaciones que tienen que ser atómicas

### Emitir (FR-054, SC-005)

```
1. Validar todo.  ──▶ 400 / 409 sin tocar nada
2. Confirmaciones de FR-032.  ──▶ 409 sin tocar nada
3. Armar el PDF y escribirlo en disco.        ← antes de la transacción (convención [003])
4. BEGIN
     INSERT Facturas (con DocumentoRuta ya puesta)
     INSERT CambiosDeEstadoFactura (emisión)
     UPDATE Viajes SET Estado = facturado, FacturaId = @id
        WHERE Id IN (...) AND Estado = rendido AND FacturaId IS NULL
     ── si las filas afectadas ≠ las seleccionadas ⇒ ROLLBACK y rechazo nombrando el viaje
     INSERT CambiosDeEstadoViaje (una por viaje)
   COMMIT
5. Si algo falló: borrar el PDF escrito en el paso 3.
```

O se facturan todos los viajes y la factura se crea, o no se factura ninguno y la factura no se crea.
El `UPDATE` condicional es lo que cierra la carrera entre dos operadores simultáneos (research §4).

### Anular (FR-048, FR-031b)

```
BEGIN
  UPDATE Facturas SET Estado = anulada, MotivoAnulacion = @motivo
  INSERT CambiosDeEstadoFactura
  UPDATE Viajes SET Estado = rendido, FacturaId = NULL WHERE FacturaId = @id
  INSERT CambiosDeEstadoViaje (una por viaje)
  Regenerar el PDF con la leyenda de anulada y el motivo, y escribirlo   ← archivo nuevo
  UPDATE Facturas SET DocumentoRuta = @nueva
COMMIT
Borrar el PDF anterior.   ← recién después de confirmar
```

**La regeneración va adentro**: si el documento no se puede armar, la anulación no queda aplicada a
medias (FR-031b). O vuelven todos los viajes o no vuelve ninguno.

### Corregir (FR-035, FR-031b)

Mismo esquema, más chico: se actualizan los cuatro campos corregibles, se agrega la entrada de
corrección al historial, se escribe el PDF nuevo, se confirma, y recién ahí se borra el anterior.
**No toca el estado ni la fecha de cobro** (FR-035, FR-044).

---

## Lo que este modelo deliberadamente no tiene

- **Ninguna tabla intermedia `FacturaViajes`.** La relación la sostiene `Viajes.FacturaId` (research §4).
- **Ninguna columna `Vencida`** ni proceso que la escriba (FR-041).
- **Ninguna columna `AlicuotaIva`** (research §5).
- **Ninguna columna espejo `ReemplazadaPorId`** (FR-050).
- **Ninguna auditoría de valores anteriores** (FR-037).
- **Ninguna copia del logo por factura** (FR-034).
- **Ningún historial de documentos generados**: la regeneración reemplaza y no versiona (FR-031b).
