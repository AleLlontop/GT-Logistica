# Quickstart: Gestión de liquidación a transportistas (Módulo 9)

Cómo levantar el sistema y **comprobar a mano** que el módulo hace lo que la spec dice. El recorrido
está pensado para una persona de negocio: se opera la aplicación y se leen pantallas, sin abrir el
código, los logs ni la base (Principio IV).

Al final está **lo que este recorrido no puede verificar y por qué**, con el test que lo cubre.

Los textos exactos de cada pantalla están en [`contracts/README.md`](./contracts/README.md); acá se
citan sólo cuando son lo que se verifica.

---

## Antes de empezar

```bash
cp .env.template .env      # completá GT_SQL_PASSWORD y GT_ADMIN_PASSWORD_INICIAL
podman compose up -d       # SQL Server + backend + frontend
```

La aplicación queda en <http://localhost:5173>. La migración `Modulo9Liquidaciones` y los dos permisos
nuevos se aplican solos al arrancar el backend. **El módulo no agrega dependencias ni variables de
entorno**: si el build falla resolviendo nombres, es el problema de DNS de `AGENTS.md`, no de este
módulo.

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

### Usuarios que hacen falta

Desde *Gestión de usuarios*, con el `admin` inicial:

| Usuario | Rol | Qué tiene que poder hacer |
|---|---|---|
| `admin` | Administrador del sistema | todo |
| `admin.empresa` | Administración de la empresa | todo |
| `gerencia` | Gerencia | mirar — **nada más** |
| `trafico` | Tráfico | **nada** de este módulo |

### Datos que hacen falta

Los CUIT de abajo **pasan la validación**: el dígito verificador está calculado, no inventado.

1. **Empresa emisora** (Módulo 6), con `admin.empresa`: dejala **sin configurar** hasta el paso 5.
2. **Transportistas** (Módulo 3):
   - `G&T Logística S.A.`, CUIT `30-71234567-1` —**el mismo** que vas a cargar en la empresa emisora—.
   - `Transportes Díaz`, persona física, CUIT `20-12345678-6`.
   - `Fletes del Sur S.R.L.`, persona jurídica, CUIT `30-70987654-2`.
3. **Choferes y vehículos** (Módulos 3 y 4): uno de cada transportista, con la documentación en regla a
   las fechas de los viajes.
4. **Viajes** (Módulo 5), de un cliente con domicilio cargado:

| # | Transportista | Fecha | Importe | Llevalo hasta |
|---|---|---|---|---|
| A | Transportes Díaz | 05/07/2026 | `$ 120.000,00` | `rendido` |
| B | Transportes Díaz | 12/07/2026 | `$ 95.000,00` | `rendido` |
| C | Transportes Díaz | 20/07/2026 | `$ 140.000,00` | `rendido` y después **facturado** (Módulo 6) |
| D | Transportes Díaz | 25/07/2026 | `$ 70.000,00` | `en curso` |
| E | Transportes Díaz | 28/07/2026 | `$ 30.000,00` | `pendiente` |
| F | Transportes Díaz | 15/06/2026 | `$ 50.000,00` | `rendido` |
| G | Fletes del Sur | 10/07/2026 | `$ 80.000,00` | `rendido` |
| H | Fletes del Sur | 10/06/2026 | `$ 0,00` | `rendido` (el Módulo 5 pide confirmar) |
| I | G&T Logística | 18/07/2026 | `$ 60.000,00` | `rendido` |

> Los números de viaje los asigna el sistema. Anotá cuál le tocó a cada letra: el recorrido los nombra
> por letra.

---

## Recorrido

### US7 — Acceso

1. **Sin sesión**, abrí `/liquidaciones`, `/liquidaciones/nueva`, `/liquidaciones/1` y
   `/liquidaciones/1/editar`: las cuatro llevan a *Ingresar*, aunque la liquidación 1 todavía no exista.
   ✅ FR-066, US7 esc. 1
2. Entrá como `trafico`: el menú **no** tiene *Consultar liquidación* ni *Generar liquidación*.
   Escribí `/liquidaciones` en la barra: no entra. ✅ FR-065, US7 esc. 4
3. Entrá como `gerencia`: el menú tiene **sólo** *Consultar liquidación*, en la sección *Operación*.
   ✅ FR-063, US7 esc. 3
4. Entrá como `admin.empresa`: el menú tiene **las dos**, en *Operación*. ✅ FR-064, US7 esc. 2

### US1 — Generar

5. Como `admin.empresa`, abrí *Generar liquidación*. **No hay formulario**: el aviso dice que falta
   configurar la empresa emisora y dónde. Seguí el enlace y configurala con el CUIT
   `30-71234567-1`. ✅ FR-001a, US1 esc. 6
6. Volvé a *Generar liquidación* y desplegá *Transportista*: aparecen `Fletes del Sur S.R.L.` y
   `Transportes Díaz`, con su CUIT. **G&T Logística no aparece.** ✅ FR-001, US1 esc. 5, SC-014
7. Sin elegir nada, apretá *Buscar viajes*: los tres campos quedan marcados y no se busca.
   *Guardar liquidación* sigue deshabilitado. ✅ FR-003, US1 esc. 7
8. Elegí `Transportes Díaz`, `08`, `2026` y buscá: el mensaje dice que no tiene viajes rendidos para
   liquidar en `08/2026`, y *Guardar liquidación* sigue deshabilitado. ✅ FR-009, US1 esc. 8
9. Cambiá a `07` / `2026` y buscá. Aparecen **A, B y C** —C con la pastilla `Facturado`—; **no** D ni E,
   ni F, que es de junio, ni G, que es de otro transportista. ✅ FR-004, US1 esc. 1, 2, 3, 10, 11
10. El pie dice `3 viajes` e `Importe total $ 355.000,00`. **No hay ningún campo donde escribir el
    total.** ✅ FR-007, US1 esc. 4
11. Cambiá el mes a `06` **sin buscar**: la lista y el total se vacían y *Guardar liquidación* se
    deshabilita. Volvé a `07` y buscá. ✅ FR-010, US1 esc. 15
12. Apretá *Guardar liquidación*. Te lleva al **detalle** —no queda el formulario— con el aviso
    `Se generó la liquidación LQ-… por $ 355.000,00 con 3 viajes.` Anotá el número: es la **LQ-a**.
    ✅ FR-015, FR-019, US1 esc. 12
13. Volvé a *Generar liquidación*, `Transportes Díaz`, `07/2026`, buscá: **no aparece ninguno**.
    ✅ FR-014, US1 esc. 9, 13
14. Elegí `Fletes del Sur S.R.L.`, `06/2026`, y buscá: aparece H con `$ 0,00`, el aviso de que no hay
    importe a liquidar, y *Guardar liquidación* deshabilitado. ✅ FR-012a, US1 esc. 16
15. **Carrera, en dos navegadores.** Con `admin` en uno y `admin.empresa` en otro, los dos abren
    *Generar liquidación* con `Fletes del Sur S.R.L.`, `07/2026` y buscan: los dos ven G. Guardá en el
    primero; guardá en el segundo. El segundo **no crea nada** y dice qué viaje ya está liquidado y en
    qué liquidación. Anotá la del primero: es la **LQ-b**. ✅ FR-011, FR-014, US1 esc. 14

### US3 — Detalle

16. Abrí **LQ-a**. Arriba: `Liquidación`, el token, la pastilla `Pendiente`, y
    `Transportes Díaz · 07/2026 · Generada el {hoy}`. ✅ FR-025, US3 esc. 1
17. *Viajes liquidados* lista A, B y C con fecha, ruta e importe, y el pie suma `$ 355.000,00`.
    ✅ US3 esc. 2
18. *Órdenes de pago* dice que todavía no hay ninguna, y el aside muestra `Resta pagar $ 355.000,00`.
    ✅ FR-015, US3 esc. 3
19. *Historial* tiene una entrada: `Generada por …` con fecha y hora. ✅ FR-033

### US5 — Editar

20. En el Módulo 5, cargá un viaje **J** de `Transportes Díaz`, `29/07/2026`, `$ 80.000,00`, y llevalo a
    `rendido`.
21. En **LQ-a**, apretá *Editar liquidación*. Transportista y período se ven **sin poder cambiarse**.
    Incluidos: A, B, C. Disponibles: **J**. ✅ FR-046, US5 esc. 1
22. Quitá **B**: pasa a disponibles y el total se anuncia en `$ 260.000,00` con 2 viajes. Agregá **J**:
    `$ 340.000,00` con 3 viajes. ✅ FR-047, US5 esc. 2, 3
23. Quitá los tres incluidos: *Guardar cambios* se deshabilita y se ve el mensaje de liquidación sin
    viajes. Volvé a agregar **A, C y J**. ✅ FR-049, US5 esc. 5
24. Guardá. Vuelve al detalle con el aviso `…ahora suma $ 340.000,00 con 3 viajes.` El historial suma
    `Editada por …` con `Quitó #B · Agregó #J`. ✅ FR-033, FR-051, US5 esc. 8
25. En *Generar liquidación*, `Transportes Díaz`, `07/2026`: aparece **B**, el que se quitó.
    ✅ FR-050, US5 esc. 4
    **Dos ediciones a la vez**: abrí *Editar liquidación* de **LQ-a** en dos navegadores. En el primero
    quitá J y guardá; en el segundo quitá A y guardá. El segundo **se rechaza** avisando que otro usuario
    guardó cambios, y la liquidación queda como la dejó el primero, con A, C y `$ 260.000,00`. Volvé a
    abrir la edición, agregá J otra vez y guardá: `$ 340.000,00`. ✅ FR-048, US5 esc. 9

### US6 — Anular

26. Abrí **LQ-b** (Fletes del Sur, G) y apretá *Anular liquidación*. Apretá el botón **sin motivo**: no
    anula y marca el motivo. Apretá *Volver*: la liquidación sigue `Pendiente`.
    ✅ FR-055, FR-056, US6 esc. 1, 2
27. Volvé a abrir el diálogo, escribí `El viaje era de otro transportista.` y anulá. La pastilla pasa a
    `Anulada`, el callout muestra el motivo, el aside dice que no hay saldo por pagar y **no hay
    ninguna acción**. ✅ FR-057, US3 esc. 6, US6 esc. 3
28. La tarjeta se llama *Viajes que agrupaba* y **sigue listando G**. El historial suma
    `Anulada por …` con el motivo. ✅ FR-028, FR-033, US3 esc. 5
29. En *Generar liquidación*, `Fletes del Sur S.R.L.`, `07/2026`: **G vuelve a aparecer**. Generá: la
    liquidación nueva tiene **otro número**. ✅ FR-016, US6 esc. 4

### US4 — Órdenes de pago

30. En **LQ-a** (`$ 340.000,00`) apretá *Registrar orden de pago*. La fecha viene con hoy y el importe
    con `340000,00`. ✅ FR-036
31. Poné una fecha **de mañana**: se rechaza con el rango permitido. Poné una fecha **de ayer** (anterior a
    la generación): también. Volvé a hoy. ✅ FR-038, US4 esc. 4
32. Poné `0`: se rechaza. Poné `400000`: se rechaza diciendo que resta pagar `$ 340.000,00`.
    ✅ FR-037, US4 esc. 3, 4
33. Poné `200000` y registrá. **Todavía no se registra**: aparece la confirmación con
    `$ 200.000,00` y `resta pagar $ 140.000,00`. Apretá *Volver*: los datos siguen ahí y no se registró
    nada. ✅ FR-040, US4 esc. 5
34. Registrá y confirmá. El detalle anuncia `Se registró la orden de pago OP-…`, la tabla la lista con
    tu usuario, y el aside dice `Resta pagar $ 140.000,00` · `Pagado $ 200.000,00`. Sigue `Pendiente`.
    ✅ FR-027, FR-034, US3 esc. 4, US4 esc. 1
35. **Ya no están** *Editar liquidación* ni *Anular liquidación*, y el callout dice que tiene pagos
    registrados. Escribí `/liquidaciones/{id}/editar`: la pantalla muestra el bloqueo y no el formulario.
    ✅ FR-045, FR-053, US5 esc. 7, US6 esc. 5
36. Registrá otra orden por `140000` y confirmá: la confirmación ya decía que la liquidación **queda
    pagada**. Pasa a `Pagada`, resta `$ 0,00`, no queda ninguna acción y el historial suma
    `Pagada por …`; los dos pagos parciales anteriores no agregaron entradas. ✅ FR-030, FR-033, FR-041,
    US4 esc. 2, 6
37. En las órdenes de pago **no hay** acción para modificar ni eliminar. ✅ FR-044, US4 esc. 8

### US2 — Listado y filtros

38. Abrí *Consultar liquidación*. Cada fila: número, período, transportista con su CUIT, importe total,
    resta pagar y estado. La anulada, **atenuada y con la palabra** `Anulada` y su motivo; su resta
    pagar dice `No corresponde`. La pagada dice `$ 0,00`. ✅ FR-021, FR-027, FR-032, US2 esc. 1, 6, 10
39. Sin filtro de estado, el control dice `Mostrando todas las liquidaciones, incluidas las anuladas.`
    ✅ FR-024
40. Filtrá por `Pendiente`: **no** aparecen ni la pagada ni la anulada, y el control lo dice. Probá
    `Pagada` y `Anulada`: cada liquidación aparece bajo un solo estado. ✅ FR-029, US2 esc. 2
41. Filtrá por `Transportes Díaz`, después por `07` / `2026`, después combiná los tres con `Pagada`:
    en cada caso sólo quedan las que cumplen. ✅ FR-022, US2 esc. 3, 4, 5
42. Combiná filtros que no den nada: `Ninguna liquidación coincide con los filtros aplicados.`
    ✅ US2 esc. 7
43. Como `gerencia`: el listado y el detalle se ven iguales, **sin** *Generar liquidación* ni ninguna
    acción en el detalle. ✅ FR-065, US2 esc. 9, SC-012

---

## Lo que este recorrido no puede verificar

| Qué | Por qué no a mano | Test |
|---|---|---|
| Dos órdenes de pago simultáneas que juntas superan el total; una edición o anulación que se cruza con un pago | hace falta que las dos transacciones se crucen en el mismo instante; el paso 15 prueba la carrera de la generación porque ahí la ventana es de minutos, no de milisegundos | `PagoConcurrenteTests`, `EdicionConcurrenteTests`, `AnulacionConcurrenteTests`, `GeneracionConcurrenteTests` |
| `ImporteTotal` e `ImportePagado` coinciden con las sumas; `Vigente` coincide con el estado | la pantalla muestra el total, no la fila contra la suma | `CoherenciaDeLiquidacionTests` |
| El `CHECK` del estado rechaza filas inválidas | ninguna pantalla permite intentarlo | `RestriccionesDeLiquidacionTests` |
| Una invocación directa sin `confirmado` no anula ni paga | la pantalla siempre manda la confirmación que corresponde | `AnulacionTests`, `OrdenDePagoTests` |
| Las rutas literales no las captura `{id:int}` | si fallara, los pasos 6 y 9 fallarían —pero sin decir por qué— | `RutasLiquidacionesTests` |
