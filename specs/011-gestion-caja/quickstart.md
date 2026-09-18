# Quickstart: Gestión de caja (Módulo 11)

## Prerrequisitos

```bash
podman compose up -d
```

Dos usuarios con `caja.gestionar` (p.ej. dos cuentas con rol *Administración de la empresa*), uno con
sólo `caja.consultar` (rol *Gerencia*), y al menos: una factura de cliente `Pendiente` (Módulo 6) y una
orden de pago registrada (Módulo 9), para poder elegirlas como referencia.

## Validación por historia

### US1 — Abrir la caja

1. Con el usuario A, sin caja abierta, entrar a **Abrir caja**, cargar saldo inicial `$10.000` y
   confirmar. **Verificar**: la caja queda `Abierta`, con la fecha de hoy y el usuario A como responsable
   (CA1).
2. Con el usuario A, intentar abrir otra. **Verificar**: mensaje "Ya tenés una caja abierta..." y no se
   crea nada (CA2).
3. Intentar abrir con saldo inicial `-1000`. **Verificar**: el campo se marca, nada se crea (RN4). Con
   `$0`, se acepta.
4. Con el usuario B (otra cuenta con `caja.gestionar`), abrir su propia caja. **Verificar**: se acepta en
   paralelo (RN1 es por empleado).

### US2 — Registrar movimientos

5. Con la caja del usuario A abierta, registrar un ingreso de `$3.500`, concepto "cobro flete", con la
   factura pendiente como referencia. **Verificar**: queda con fecha, responsable y referencia (CA5).
6. Registrar un egreso de `$2.000`, concepto "pago a proveedor", con la orden de pago como referencia.
7. Intentar guardar un movimiento con importe `$0` o negativo. **Verificar**: se marca el campo, no se
   guarda (CA4).
8. Intentar asociar una orden de pago a un ingreso. **Verificar**: se rechaza (RN8).
9. Cerrar la caja del usuario B (sin movimientos) y, sobre ella ya cerrada, intentar registrar un
   movimiento. **Verificar**: se rechaza informando que está cerrada (CA8).

### US3 — Cerrar la caja

10. Sobre la caja del usuario A (con los movimientos de los pasos 5 y 6), entrar a cerrarla. **Verificar**:
    el resumen muestra saldo inicial `$10.000`, los dos movimientos, y saldo final `$11.500` (CA6, RN6).
11. Cancelar el cierre. **Verificar**: la caja sigue `Abierta` (CA7).
12. **Con dos pestañas** con la misma sesión: en la pestaña 1, abrir el resumen de cierre; en la pestaña 2,
    registrar un movimiento nuevo sobre la misma caja y confirmarlo; volver a la pestaña 1 y confirmar el
    cierre con el saldo que vio. **Verificar**: el sistema no cierra con el saldo viejo — muestra el
    resumen actualizado y pide confirmar de nuevo (US3 esc. 7). Confirmar de nuevo. **Verificar**: cierra
    con el saldo correcto.
13. Abrir una caja nueva sin movimientos y cerrarla. **Verificar**: resumen vacío, saldo final = saldo
    inicial, cierre válido (CL1).

### US4 — Consultar

14. Con el usuario Gerencia, entrar a la consulta de movimientos y filtrar por rango de fechas que incluya
    los del paso 5-6. **Verificar**: fecha, tipo, importe, concepto, responsable y referencia de cada uno
    (CA5).
15. Filtrar un período sin movimientos. **Verificar**: "No existen movimientos" (CA9, CL2).
16. Entrar a la consulta de cajas. **Verificar**: se ven las cajas con su responsable, apertura, estado, y
    en las cerradas, cierre y saldo final; sin ningún botón de abrir, registrar ni cerrar (US4 esc. 7).

### US5 — Acceso

17. Sin sesión, intentar entrar a `/caja`. **Verificar**: redirige al ingreso.
18. Con un usuario de *Tráfico* (sin permisos de este módulo), entrar. **Verificar**: no ve las opciones
    del módulo en el menú, y escribiendo `/caja` a mano ve el aviso de falta de permiso, sin datos ni
    formulario (FR-033).
19. Con Gerencia, intentar invocar directamente `POST /api/caja` (por ejemplo con las herramientas de
    desarrollo del navegador). **Verificar**: `403`.

## Lo que no se puede verificar a mano

- La carrera de doble apertura (CL3) y la de cierre simultáneo (FR-021): tests de integración que
  lanzan las dos peticiones en paralelo contra el SQL Server real.
- Los `CHECK` de la base: tests de integración que insertan filas violando cada restricción por
  separado (research §6, §7).
- La invocación directa de `POST /api/caja/{id}/movimientos` sobre una caja cerrada, sin pasar por la
  pantalla: test de integración.
