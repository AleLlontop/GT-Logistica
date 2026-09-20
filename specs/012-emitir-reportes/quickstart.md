# Quickstart: Emitir reportes (Módulo 12)

Recorrido para validar la feature operando la aplicación, sin leer código ni consultar la base
(Principio IV). Los detalles de columnas están en [data-model.md](./data-model.md) §3 y los textos y
endpoints en [contracts/README.md](./contracts/README.md).

## Prerrequisitos

```bash
podman compose up -d        # SQL Server + backend + frontend
```

**Reiniciar el backend después de desplegar** para que `SembradorInicial` cree el permiso
`reportes.emitir` y se lo dé a Gerencia y al administrador. No hay migración que correr.

Cuentas necesarias:

| Rol | Para qué |
|---|---|
| **Gerencia** | Es quien usa los reportes (FR-014) |
| **Administrador del sistema** | Verificar que también los tiene (FR-014, SC-005) |
| **Tráfico** | Verificar que **no** ve la acción en ninguna de las cinco pantallas (SC-005) |
| **Administración de la empresa** | Lo mismo (SC-005) |

Datos cargados: al menos una decena de viajes de varios clientes, estados y fechas (Módulo 5);
documentación de choferes y de vehículos con algo vencido o por vencer (Módulos 3 y 4); alguna
factura vencida o por vencer (Módulo 6); y movimientos de caja de varios días (Módulo 11).

Hace falta además **una planilla de cálculo** para abrir los `.xlsx` y comprobar SC-004 y SC-007.

---

## Validación por historia

### US1 — Reporte de viajes con los filtros aplicados

1. Con **Gerencia**, entrar a **Viajes** sin ningún filtro. **Verificar**: al lado del título aparece
   *Generar reporte*, y se ve como acción **secundaria** — gris claro, no el pill oscuro relleno
   (FR-005).
2. Presionar *Generar reporte*. **Verificar**: se abre un diálogo con **exactamente dos** opciones,
   *PDF* primero y *Excel* después, más *Cancelar* (FR-002).
3. Presionar `Escape`. **Verificar**: el diálogo se cierra, no se genera nada, el listado queda igual
   y el foco vuelve al botón *Generar reporte* (US1 esc. 4, FR-002).
4. Volver a abrirlo y elegir **PDF**. **Verificar**: mientras se prepara, el botón queda
   deshabilitado y la pantalla dice *"Generando el reporte…"* (FR-004). Al terminar, el PDF se abre
   en una pestaña nueva (o se descarga, si el navegador bloquea emergentes — research §8).
5. En el PDF, leer el encabezado. **Verificar** las cinco piezas de FR-006: dice *Reporte de viajes*,
   dice **`Sin filtros aplicados`**, dice cuántas filas trae, dice el día y la hora de generación y
   dice el usuario que lo generó (SC-006).
6. **Verificar** que trae **todos** los viajes registrados, en el mismo orden que la pantalla, con
   las diez columnas de FR-008: número, fecha, cliente, origen, destino, chofer, vehículo,
   transportista, estado e importe. Los estados están con la **misma palabra** que la pantalla y los
   importes en pesos —`$ 1.240.000,00`— (FR-010).
7. **Verificar** que al pie está el **total de los importes** y que coincide con la suma de las filas
   del propio archivo (FR-009, SC-004).
8. Volver a **Viajes**, poner el filtro de estado en **En curso** y un rango de fechas. Generar en
   **Excel**. **Verificar**: la planilla trae **sólo** esos viajes, y el encabezado nombra los dos
   filtros **con sus valores en palabras** — el estado dice *En curso*, no un código (US1 esc. 2,
   FR-006).
9. En la planilla, **ordenar por la columna Importe** y **sumar el rango con la fórmula de la
   planilla**. **Verificar**: ordena como números y la suma coincide con el total del pie (FR-011,
   SC-004, SC-007). Hacer lo mismo con la columna Fecha: ordena como fechas, no como texto.
10. Con un filtro que deje **más de una página** de resultados, quedarse en la primera página y
    generar. **Verificar**: el archivo trae **todas** las filas del filtro, no las veinte de la
    página (US1 esc. 3, FR-007, SC-002).
11. Contar las filas del archivo y compararlas con el total que la pantalla declara para ese filtro.
    **Verificar**: son el mismo número, con los mismos datos y en el mismo orden (SC-002).
12. Poner un filtro que **no deje ningún viaje**. **Verificar**: *Generar reporte* queda
    **deshabilitado** y al lado se explica por qué (FR-003).
13. Cronometrar el paso 4 sobre un filtro con ~1.000 viajes. **Verificar**: el archivo está
    disponible en **menos de 15 segundos**, en los dos formatos (SC-003).

### US2 — Los tres paneles de vencimientos

14. Con **Gerencia**, entrar a **Vencimientos de choferes**. **Verificar**: aparece *Generar
    reporte*, secundaria, y la pantalla **no tiene ninguna acción primaria** — no se inventó una
    (FR-005, Principio VI).
15. Generar en **PDF**. **Verificar**: trae las mismas filas que la pantalla —chofer, transportista,
    documento, fecha de vencimiento y estado—, **en el mismo orden por urgencia**, y el encabezado
    dice `Sin filtros aplicados` (US2 esc. 1).
16. Entrar a **Vencimientos de flota** y generar en **Excel**. **Verificar**: patente,
    transportista, documento, fecha de vencimiento y estado, una fila por alerta (US2 esc. 2).
17. Entrar a **Vencimientos de facturas** y generar en los **dos** formatos. **Verificar**: cliente,
    número, importe, vencimiento y situación, **y el total de los importes** al pie (US2 esc. 3,
    FR-009).
18. Buscar uno de los tres paneles **sin ninguna alerta** (o dejarlo sin alertas renovando la
    documentación). **Verificar**: *Generar reporte* no está disponible (US2 esc. 4, FR-003).

### US3 — Movimientos de caja

19. Con **Gerencia**, entrar a **Movimientos de caja**, elegir un rango de fechas **y** una caja.
    Generar en **PDF**. **Verificar**: trae fecha, tipo, importe, concepto, responsable y referencia
    de cada movimiento del filtro, y al pie **total de ingresos, total de egresos y neto** (US3
    esc. 1, FR-009).
20. **Verificar** que el encabezado nombra los dos filtros con sus valores: el rango en fechas
    legibles y **la caja por su nombre**, no por su número interno (FR-006).
21. Generar la misma consulta en **Excel** y, en la planilla, sumar por separado los ingresos y los
    egresos. **Verificar**: coinciden con los dos totales del pie, y el neto es la resta (US3
    esc. 2, SC-004).
22. Poner un rango de fechas **sin ningún movimiento**. **Verificar**: *Generar reporte* queda
    deshabilitado (US3 esc. 3, FR-003).
23. Poner un rango **invertido** —*desde* posterior a *hasta*—. **Verificar**: la pantalla lo rechaza
    como ya lo hacía y no hay nada que generar (spec §Edge Cases).
24. Tomar un viaje sin chofer ni vehículo asignados y generarlo en el reporte de viajes.
    **Verificar**: esas dos celdas quedan **vacías**, sin guion ni texto de relleno (spec §Edge
    Cases).

### Permisos (FR-013, FR-014, FR-015, SC-005)

25. Con **Administrador del sistema**, recorrer las **cinco** pantallas. **Verificar**: la acción
    está en las cinco.
26. Con **Tráfico**, recorrer las cinco pantallas a las que llega. **Verificar**: **no** aparece
    *Generar reporte* en ninguna, y todo lo demás de esas pantallas funciona igual que antes de esta
    feature (SC-005).
27. Lo mismo con **Administración de la empresa** (SC-005).
28. Con **Tráfico**, escribir a mano en el navegador
    `/api/viajes/reporte?formato=pdf`. **Verificar**: responde `403` y no se genera ningún archivo
    (spec §Edge Cases).
29. Con **Gerencia**, escribir a mano `/api/facturas/vencimientos/reporte?formato=pdf`.
    **Verificar**: funciona — Gerencia tiene `facturacion.consultar`. Comprobar que **ninguna otra
    pantalla** del sistema ganó la acción (FR-001): recorrer Liquidaciones, Adelantos, Cajas,
    Facturas, Choferes, Flota, Clientes y las dos de totales; en las nueve no hay *Generar reporte*.

### Fallos y reintentos

30. Con la generación en curso, **verificar** que el botón no se puede presionar dos veces (FR-004,
    spec §Edge Cases).
31. Detener el backend (`podman compose stop backend`), presionar *Generar reporte* y elegir un
    formato. **Verificar**: la pantalla informa el fallo **sin perder los filtros aplicados ni la
    página**, y permite volver a intentar (FR-017). Levantar el backend y reintentar: funciona.
32. Mirar el nombre de los archivos descargados en la carpeta. **Verificar**: cada uno dice qué
    reporte es y de qué día y hora, y dos reportes del mismo listado en el mismo día **no colisionan**
    (FR-012).

---

## Lo que no se puede verificar a mano

- **El rechazo por el tope de 5.000 filas** (FR-016, SC-008): haría falta un filtro que deje más de
  5.000 filas, que el volumen de la empresa no alcanza. Lo cubre un test de integración que baja el
  tope por configuración de prueba y comprueba el `409`, el mensaje con los dos números y que **no se
  entregue ningún archivo**.
- **La caída de `window.open` a descarga** cuando el navegador bloquea emergentes (research §8):
  depende de la configuración del navegador de quien prueba. Lo cubre un test de frontend que falsea
  `window.open` devolviendo `null`.
- **La paridad fila a fila con el listado sobre volúmenes grandes** (SC-002): a mano se comprueba con
  los pasos 10 y 11 sobre unas decenas de filas; el test de integración compara la fuente del reporte
  contra la consulta del listado sobre los mismos datos sembrados.
- **Que el Excel salga de verdad con celdas numéricas y de fecha** más allá de lo que el paso 9
  muestra: un test de integración abre el `.xlsx` generado y verifica el tipo de cada celda,
  **resolviendo el armador del contenedor** y no instanciándolo a mano (convención [006]).
