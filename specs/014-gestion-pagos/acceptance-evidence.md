# Evidencia de aceptación operativa — 014

Ejecutada el 2026-10-19 en Chrome headless por el agente, con UI real, HTTPS local y SQL Server real en una base efímera del fixture. Reloj de aplicación fijado a hoy argentino 2026-10-18, incluyendo UTC ya en el día siguiente. No es una aprobación documental de gt-ui: R1 sigue pendiente.

| Recorrido del quickstart | Resultado observado |
|---|---|
|1 Bancos|Alta,duplicado rechazado,edición con Id estable,baja/reactivación; banco usado Inactivo visible en cheques y excluido del combo nuevo. Referencias OP/OPP distintas verificadas en UI y SQL de las suites.|
|2 Normal/validación/temporal|Dos facturas121+242;362,99rechazado sin POST exitoso ni orden,363admitido. Cancelar armado sin persistencia,Fecha hoy/ayer y futura rechazada tanto por UI como servidor; Detalle omitido válido.|
|3 Efectivo/mixta|60+50conservados como dos medios y un egreso110;600cheque+250transferencia+150efectivo produce sólo egreso150. CBU conserva ceros.|
|4 Fletero|Tres facturas/liquidaciones pagadas con un cheque diferido363, tres OP distintas y sin Caja; combo sólo Cheque. Negativos estrictos cubiertos por integración SQL.|
|5 Inactivo|Normal y Fletero Inactivos pagados sin reactivar; baja real de Proveedores durante armado no impidió Generar.|
|6 Consultas|GP05 paginado,filtrado inclusivo por Fecha de negocio,detalle completo y vínculos autorizados;sin repetición entre páginas.|
|7 Cheques|Banco histórico Inactivo y seis estados;ayer/hoy/+30incluidos,+31excluido. Filtro Anulado incluye tres casos y rango amplio no expande universo. Detalle+31 accesible desde OPP. Entregado→Depositado→Rechazado agrega historial y conserva factura Pagada;Anulado individual impide anular OPP.|
|8 Anular Fletero/nuevo pago|OPP Anulada, tres OP revertidas con historia y facturas Pendientes. Nueva OPP paga las tres otra vez; replay de Generar original devuelve documento original sin otro pago.|
|9 Compensación|Anular efectivo conserva egreso110 y agrega ingreso110 distinto. Transferencia impide anulación; caja original cerrada devuelve409sin ingreso parcial.|
|10 Carreras/replay|Dos contextos/sesiones sobre misma factura:201/409. Dos facturas distintas con saldo para una:201/409. Respuesta abortada en navegador después del commit real en Generar y Anular:reintento con idéntica clave/payload,un solo efecto.|
|11 Permisos|Gerencia/Tráfico sin menú Pagos y403directo;Banco consultar no crea;Generar solo muestra DTO propio en runtime y refresh403sin recuperar datos desde storage/history.|
|12 UI/teclado|Viewport1280;foco inicial en diálogo,Tab/Shift+Tab retenidos,Escape/cancelar retornan al disparador y no envían. Cheque200caracteres tras trim exterior,espacios internos preservados,completo y sin overflow horizontal. Contraste medido sobre píxeles reales del fondo y color computado de etiquetas.|

## Evidencia local temporal

- Script de operación: `/tmp/gt014-browser/aceptacion.cjs`.
- Host HTTPS con fixture real: `/tmp/gt014-host/Program.cs`; salida compilada sólo en `backend/tests/GT.IntegrationTests/bin/Acceptance014/` ignorado por Git.
- Resultados,claves operativas y DTO sintéticos: `/tmp/gt014-final/browser-state.json`; no credenciales ni cadena de conexión.
- Datos de escenario: `/tmp/gt014-final/acceptance-data.json`.
- Capturas: [orden](/tmp/gt014-final/orden-normal-1280.png), [consulta](/tmp/gt014-final/consultas-1280.png), [historial](/tmp/gt014-final/cheque-historial-1280.png), [cheque200](/tmp/gt014-final/cheque-200-1280.png), [anulación](/tmp/gt014-final/anular-dialogo-1280.png), [compensación](/tmp/gt014-final/efectivo-compensado-1280.png), [403](/tmp/gt014-final/generar-sin-consulta-403.png), [validación](/tmp/gt014-final/generar-validacion-1280.png).

Los archivos temporales no se versionan. Playwright se instaló sólo en `/tmp`, usando Chrome existente; package.json/package-lock del proyecto no cambiaron. Primer runner corrigió una ruta legada de baja (`POST /baja` con Version), sin cambios a Proveedores. La revisión visual corrigió espaciado de listas,ubicación de Anular y enlaces/wrapping de números largos, reutilizando tokens/primitivas; se repitieron las pruebas afectadas y la suite frontend completa.

Contraste mínimo real de etiquetas: 4.54:1. Las carreras devolvieron [201, 409] y [201, 409]. La base efímera y el host se limpiaron al terminar; ver comprobación de metadata en implementation-log.md.
