# Contrato de UI y HTTP — Gestión de caja (Módulo 11)

## Pantallas

| Ruta | Pantalla | Quién la ve |
|---|---|---|
| `/caja` | Consulta de cajas (FR-026) | `caja.consultar` |
| `/caja/nueva` | Abrir caja | `caja.gestionar` |
| `/caja/:id` | Detalle de caja (`DetalleDeCaja`): resumen en vivo, botón *Registrar movimiento*, botón *Cerrar caja* | `caja.gestionar` para operar, `caja.consultar` para mirar una cerrada |
| `/caja/:id/movimientos/nuevo` | Registrar movimiento | `caja.gestionar` |
| `/caja/:id/cierre` | Resumen de cierre y confirmación | `caja.gestionar` |
| `/movimientos-caja` | Consulta de movimientos por rango de fechas o por caja (FR-023, FR-024) | `caja.consultar` |

Sin permiso del módulo, cualquiera de estas rutas escrita a mano muestra el aviso de FR-033 (no el
formulario ni los datos, sin "volvé a intentar"). Lo decide el `403` de la carga de la pantalla cuando
esa carga exige el mismo permiso que la pantalla —`/caja`, `/caja/:id/cierre`, `/movimientos-caja`—, y
la prop `puedeGestionar` donde no carga nada (`/caja/nueva`) o donde su carga es de consulta y a
*Gerencia* le responde `200` (`/caja/:id/movimientos/nuevo`, que lee `GET /api/caja/{id}`) (convención
[010]).

**Registrar movimiento sin caja operable (CA3, FR-007, FR-035)**: si al cargar la caja responde `404`, o
llega con `puedeOperar` falso —cerrada o de otro empleado—, la pantalla dibuja sólo el aviso *"No hay
una caja abierta. Abrí una caja para poder registrar movimientos."* con enlace a `/caja`, sin
formulario. Si el formulario ya estaba en pantalla y el guardado responde `404`, `409 caja_cerrada` o
`409 caja_ajena`, se muestra ese mismo aviso en `role="alert"`, sin perder lo cargado. Los textos de
`caja_cerrada` y `caja_ajena` son los que ve quien invoca la acción directamente.

## Endpoints

Convención [005]: `400` cuando el problema está en lo que se tipeó, `409` cuando está en el estado de
algo compartido o que cambió (caja ya abierta, caja cerrada, caja ajena, cierre sin confirmar o
desactualizado), y las rutas con
identificador llevan `{id:int}` porque conviven con rutas literales en el mismo prefijo.

| Método | Ruta | Permiso | Qué hace |
|---|---|---|---|
| `GET` | `/api/caja/abierta` | `caja.gestionar` | La caja abierta del usuario en sesión, o `204` si no tiene ninguna. Maneja RN1/CA2 desde la pantalla. |
| `GET` | `/api/caja` | `caja.consultar` | Listado paginado de cajas (FR-026). |
| `GET` | `/api/caja/{id:int}` | `caja.consultar` | Detalle de una caja: responsable, apertura, estado, y si está cerrada, cierre y saldo final. |
| `POST` | `/api/caja` | `caja.gestionar` | Abre una caja. Cuerpo: `{ saldoInicial }`. `409 caja_ya_abierta` si ya tiene una (FR-003). |
| `POST` | `/api/caja/{id:int}/movimientos` | `caja.gestionar` | Registra un movimiento. Cuerpo: `{ tipo, importe, concepto, facturaId?, ordenDePagoId? }`. `409 caja_cerrada` si no está abierta (FR-007). |
| `GET` | `/api/caja/{id:int}/movimientos` | `caja.consultar` | Movimientos de esa caja, paginados. |
| `GET` | `/api/movimientos-caja` | `caja.consultar` | Movimientos por rango de fechas y/o caja (FR-023). Parámetros: `desde`, `hasta`, `cajaId`, `pagina`. |
| `GET` | `/api/caja/{id:int}/cierre` | `caja.gestionar` | Resumen previo al cierre: saldo inicial, movimientos, saldo final calculado (FR-016). |
| `POST` | `/api/caja/{id:int}/cierre` | `caja.gestionar` | Cuerpo: `{ confirmado: true, saldoFinalConfirmado }`. Sin `confirmado`, `409 confirmacion_requerida` con el resumen; sin coincidir con el saldo recién calculado, `409 cierre_desactualizado` con el resumen nuevo (research §3). |
| `GET` | `/api/caja/facturas-pendientes` | `caja.gestionar` | Desplegable del ingreso: facturas `Pendiente` (research §5). |
| `GET` | `/api/caja/ordenes-de-pago` | `caja.gestionar` | Desplegable del egreso: las 50 órdenes de pago más recientes, sin filtro de estado (research §4, FR-011). |

### Cuerpos de error (formato común, convención [005])

```jsonc
// 409 caja_ya_abierta
{ "codigo": "caja_ya_abierta", "mensaje": "Ya tenés una caja abierta. Cerrala antes de abrir otra." }

// 409 caja_cerrada
{ "codigo": "caja_cerrada", "mensaje": "Esta caja ya está cerrada y no admite nuevos movimientos." }

// 409 confirmacion_requerida — POST de cierre sin confirmado: true (US3 esc. 6); mismo cuerpo que el
// GET de resumen, como cierre_desactualizado
{
  "codigo": "confirmacion_requerida",
  "mensaje": "Revisá el resumen y confirmá el cierre.",
  "saldoInicial": 10000.00,
  "movimientos": [ /* MovimientoListado[] */ ],
  "saldoFinal": 11500.00
}

// 409 cierre_desactualizado — el mismo cuerpo que devuelve el GET de resumen
{
  "codigo": "cierre_desactualizado",
  "mensaje": "El resumen cambió desde que lo viste. Revisalo antes de confirmar el cierre.",
  "saldoInicial": 10000.00,
  "movimientos": [ /* MovimientoListado[] */ ],
  "saldoFinal": 11500.00
}

// 400 referencia_invalida (RF-011)
{ "codigo": "referencia_invalida", "mensaje": "La factura elegida ya no está pendiente de cobro.", "campo": "facturaId" }

// 409 caja_ajena — registrar o cerrar la caja de otro empleado (FR-035)
{ "codigo": "caja_ajena", "mensaje": "Esta caja es de otro empleado. Sólo quien la abrió puede registrar movimientos y cerrarla." }
```

Los dos `409` del cierre traen además `totalIngresos` y `totalEgresos`, con la misma forma que el `GET`
del resumen.

## Decisiones fijadas en `tasks.md`

1. **La caja es personal también en el servidor** (FR-035): registrar y cerrar exigen que el usuario en
   sesión sea el responsable. El `UPDATE` que toma el lock suma `AND UsuarioResponsableId = @usuario`; con
   cero filas se relee y responde `409 caja_ajena` si la caja existe, está abierta y es de otro. Una caja
   cerrada responde `caja_cerrada` aunque sea de otro.
2. **`CajaDetalle` trae `saldoInicial`, `totalIngresos`, `totalEgresos`, `saldoActual` y `puedeOperar`**,
   calculados al leer. `puedeOperar` es abierta y del usuario en sesión, y lo decide el servidor (SC-007).
3. **Las dos entradas de menú son de consulta**: *Caja* (`consultar-caja`, `/caja`) y *Movimientos de
   caja* (`consultar-movimientos-caja`, `/movimientos-caja`), las dos por `caja.consultar`, en la sección
   *Operación*. *Abrir caja* se llega desde `/caja`, que muestra el botón sólo con `caja.gestionar`.
4. **El rango del filtro son días de Argentina** sobre un instante UTC: *desde* es `desde 00:00 −03:00` y
   *hasta* es `< hasta+1 00:00 −03:00`. Un movimiento del 16/09 a las 23:30 de Argentina entra con
   *hasta* = 16/09.
5. **La pastilla de estado**: `abierta` en el tono de `pendiente` y `cerrada` en el de `rendido`, en
   `TONO_POR_VALOR` de `compartido/ui/Estado.tsx`.

## Textos (español rioplatense, sin voseo forzado en mensajes de sistema)

- Botón abrir: **Abrir caja**. Confirmación: *"Caja abierta con éxito."* (`role="status"`)
- Rechazo de segunda apertura: **"Ya tenés una caja abierta. Cerrala antes de abrir otra."**
- Sin caja abierta, al intentar registrar: **"No hay una caja abierta. Abrí una caja para poder registrar movimientos."**
- Importe inválido: **"Escribí un importe mayor que cero."** / **"Escribí el importe con hasta dos decimales."**
- Concepto vacío: **"Escribí el concepto del movimiento."**
- Referencia no aplicable al tipo: **"Una orden de pago sólo puede asociarse a un egreso."** / **"Una factura sólo puede asociarse a un ingreso."**
- Referencia inválida: **"La factura elegida ya no está pendiente de cobro."**
- Referencia de egreso, debajo del desplegable: *"Se muestran las 50 órdenes de pago más recientes."*
- Cierre — resumen vacío (CL1): tabla vacía con `EstadoVacio`: *"Esta caja no tiene movimientos. El saldo final es igual al saldo inicial."*
- Cierre — botón primario: **Confirmar cierre**; secundaria: **Cancelar**
- Cierre — desactualizado: *"El resumen cambió desde que lo viste. Revisalo antes de confirmar el cierre."*, con el resumen recargado automáticamente.
- Consulta sin movimientos (CL2, FR-025): *"No existen movimientos para los filtros aplicados."* —vale
  para rango, caja o los dos—
- Consulta de cajas — fila cerrada: pastilla `Cerrada` + saldo final; fila abierta: pastilla `Abierta`, sin saldo final.
