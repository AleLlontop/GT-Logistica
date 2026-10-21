# Especificación de feature: Ampliación de reportes

**Feature Branch**: `015-ampliacion-reportes`  
**Creada**: 2026-10-19  
**Estado**: Especificación funcional lista para revisión. Aclaraciones resueltas; no se ejecuta planificación en esta etapa.

## Fuentes, alcance y decisiones aprobadas

Fuente funcional: alcance de la Entrega 2 y decisiones de producto aportadas por el usuario en esta sesión. El documento completo de Entrega 2 no está incorporado al repositorio; la matriz de cobertura se refiere al alcance expresamente transcripto, sin atribuirle reglas adicionales.

015 amplía [012 Emitir reportes](../012-emitir-reportes/spec.md), conserva sus cinco exportaciones y agrega un dashboard de sólo lectura, análisis de viajes y reportes generales de facturación, liquidaciones y utilización de flota. No reemplaza 012 ni cambia reglas de los módulos fuente. Este entregable es exclusivamente funcional: sin plan, tareas, código, migraciones, commits ni publicación.

Decisiones confirmadas por el usuario durante la especificación:

- **AC-01**: Dashboard con KPI, gráficos, resúmenes/tablas, filtros y acceso a reportes; período global desde/hasta, mes calendario actual de Argentina por defecto.
- **AC-02**: Cobros y pagos se miden por su fecha efectiva dentro del período. No se deben confundir con el cobrado/pagado vigente de documentos emitidos o liquidados en ese período.
- **AC-03**: En liquidaciones, la selección documental se aplica al período liquidado, no a la fecha de generación.
- **AC-04**: Permiso independiente `reportes.consultar` para Gerencia y AdministradorSistema; se conserva `reportes.emitir` y la autorización de lectura de los módulos para exportar. Cada sección exige además su permiso fuente; ver C-04 resuelta.
- **AC-05**: XLSX DEBE preservar exactitud monetaria a centavos. El importe fuente continúa siendo decimal exacto del dominio y se exporta como celda numérica, nunca como texto. Su exportabilidad se verifica mediante round-trip monetario: decimal fuente → valor numérico XLSX → valor recuperado/leído → comparación exacta a escala de dos decimales. El criterio es igualdad monetaria exacta a centavos, no igualdad de representación binaria IEEE-754. Si el recuperado no coincide con el original a dos decimales, se rechaza la generación del XLSX antes de entregarlo, con explicación; consulta y PDF permanecen disponibles. Celdas individuales y totales cumplen el mismo criterio, sin truncado ni redondeo silenciosos y sin convertir importes a texto como workaround.

Referencias: [constitución](../../.specify/memory/constitution.md), [convenciones](../../AGENTS.md), specs de 004/005/006/009/011/012/013/014 y evidencia de inspección al final. No dependencia de Gestión de Empleados.

## User Scenarios & Testing

### US1 — Leer la situación general del período (P1)

La persona autorizada abre Reportes y encuentra primero los KPI principales, luego gráficos y finalmente resúmenes y accesos a los reportes. Cambia el período y compara actividad, facturación, cobros, liquidaciones, pagos y efectivo sin modificar operaciones.

**Prioridad**: brinda la lectura gerencial integrada que hoy no existe.

**Prueba independiente**: preparar datos de dos meses, aplicar uno y cotejar cada KPI y serie contra las filas de su reporte correspondiente, incluso con varias páginas.

**Escenarios de aceptación**:

1. Al abrir el dashboard, desde/hasta corresponden al primer y último día del mes actual argentino, aun cuando UTC ya esté en el día siguiente. El período aparece visible y es editable.
2. Cambiar un filtro y aplicarlo actualiza KPI, gráficos y resúmenes del mismo universo; navegar páginas no cambia los totales. Un resultado anterior no se presenta como si perteneciera al filtro nuevo.
3. Con cero actividad se muestran cantidades/importes cero y explicación de ausencia de datos; una sección sin permiso o que falló no se muestra como cero.
4. Un acceso a reporte conserva el período aplicable. Vencimientos se identifica como situación actual a la fecha de consulta y no simula un corte histórico por el período global.
5. Los gráficos tienen títulos, unidades, leyendas y valores accesibles también en tabla/texto. No se exige interpretar colores para conocer un estado o importe.

### US2 — Analizar viajes y utilización de flota (P1)

La persona consulta viajes por período, cliente, transportista y estado; distingue trabajo realizado, planificación y rendiciones pendientes. En Utilización de flota compara viajes por vehículo y tipo con los datos que realmente existen.

**Prioridad**: completa los reportes operativos y la utilización comprometida en Entrega 2.

**Prueba independiente**: usar viajes de distintos estados y dos vehículos, comparar listado, cantidades y clasificación con los registros fuente; exportar PDF y Excel.

**Escenarios de aceptación**:

1. R1 conserva los filtros y las diez columnas exportables de 012; agrega el resumen analítico por estado, cliente, transportista y evolución mensual, sin duplicar el viaje al agrupar.
2. Realizados son viajes ya rendidos, incluidos los que posteriormente pasan a Facturado. Pendientes de rendición son los EnCurso: pueden estar físicamente en marcha o terminados a la espera del remito; el modelo no distingue esas dos situaciones. Pendiente conserva su estado separado de viaje todavía no finalizado ni rendido. Viajes anulados siguen consultables mediante sus filtros y no pasan a contar como trabajo realizado.
3. R6 muestra patente, tipo, transportista/propietario actual, cantidad de viajes del período y estado/disponibilidad actual claramente rotulado. El ranking permite comparar unidades con criterio de desempate estable; incluye unidades sin viajes con cantidad cero y unidades dadas de baja, conservando su identificación y actividad del período. Cuenta Rendido + Facturado, sin duplicar el mismo viaje por haber sido facturado.
4. No aparecen kilómetros, horas, combustible ni porcentajes de ocupación. Un viaje no se cuenta dos veces por chofer, documentos o movimientos asociados.
5. PDF/XLSX de R6 contienen todas las unidades del conjunto filtrado, con sus cantidades y el mismo orden que la tabla, sujetos al límite explícito de exportación.

### US3 — Consultar facturación y cobros (P1)

La persona consulta R3 por fechas, cliente, estado y número de comprobante, distingue facturación y deuda de los cobros efectivamente ocurridos y exporta su análisis.

**Prioridad**: cubre un reporte financiero ausente en 012, sin crear reglas de cobro nuevas.

**Prueba independiente**: factura de septiembre cobrada en octubre, factura de octubre pendiente y factura anulada; comprobar que octubre muestra el cobro de septiembre en su sección temporal y no lo resta de la deuda de octubre.

**Escenarios de aceptación**:

1. La tabla documental presenta número, fecha de factura, cliente histórico, estado visible de 006, neto, IVA, total y fecha de cobro cuando exista. Un cliente dado de baja no hace desaparecer sus documentos.
2. El resumen de documentos del período informa facturado y pendiente vigente; el flujo de cobros usa FechaCobro, incluyendo facturas de períodos anteriores según C-03. No se suman factura anulada y refacturación como dos ventas vigentes.
3. La tabla de cobros permite explicar cada importe del KPI temporal con documento y fecha. El mismo filtro de cliente se aplica a ambas perspectivas; filtros específicos de cada perspectiva se identifican para no mezclar universos.
4. Una factura cobrada aporta su total completo: 006 no implementa cobros parciales. Vencida se deriva según la fecha argentina de consulta, sin alterar el estado guardado.
5. PDF y XLSX identifican ambas perspectivas y sus totales; la exportación de cobros no se limita a la página ni a las facturas emitidas en el mes.

### US4 — Consultar liquidaciones y pagos (P1)

La persona consulta R4 por período liquidado, transportista y estado, junto al flujo de pagos fechado en el rango; conserva la distinción entre OP de 009 y OPP de 014.

**Prioridad**: cubre compromisos y pagos sin doble contabilización entre 009, 013 y 014.

**Prueba independiente**: liquidación de septiembre con pago en octubre, otra con pago parcial manual y otra pagada por OPP luego anulada; comprobar períodos, pendientes y trazabilidad.

**Escenarios de aceptación**:

1. Tabla documental: número LQ, mes/año liquidado, transportista, estado, importe total, pagado vigente y pendiente según 009. Incluye cantidad de liquidaciones del conjunto completo.
2. Tabla temporal de pagos: OP, liquidación, transportista, FechaPago, importe, origen manual/OPP y vigencia/reversión. Una OPP con varias OP aporta cada OP una vez; no se agrega el importe de OPP otra vez.
3. Los pagos revertidos conservan identificación/historia; se excluyen del total vigente de pagos del período y permanecen identificados en el detalle histórico, aun si la reversión ocurrió después del período seleccionado. Un pago manual histórico no desaparece por carecer de OPP.
4. Una liquidación anulada no genera deuda vigente. Pagos parciales de 009 se respetan; no se supone que toda liquidación se paga como en 014.
5. Un filtro sobre meses liquidados no se interpreta como fecha de generación; un rango 15/09–10/10 incluye septiembre y octubre completos, sin prorratear liquidaciones; los pagos se cortan por las fechas exactas.

### US5 — Reutilizar vencimientos y Caja (P2)

Desde Reportes se accede a R2 (choferes, flota y facturas) y R5 (movimientos de caja) con las capacidades ya disponibles y contexto visible.

**Prioridad**: integra lo existente sin dos versiones de la misma regla.

**Prueba independiente**: comparar filas, orden y exportaciones contra las pantallas originales de 012.

**Escenarios de aceptación**:

1. Los tres paneles de vencimientos mantienen su universo, urgencia, palabras de estado y exportaciones; no se filtran por el mes global ni prometen reconstrucción histórica inexistente.
2. Caja mantiene filtros desde/hasta/caja y corte de días argentinos; ingresos, egresos y neto cierran contra todos los movimientos filtrados.
3. Un egreso OPP y su ingreso compensatorio son dos movimientos históricos distintos; se incluyen según sus fechas sin borrar ni duplicar. OP y OPP se distinguen en referencia.
4. El neto del período se llama movimiento neto: no se presenta como saldo disponible, saldo inicial o cierre de caja. No se suman cobros/pagos de documentos a efectivo como si fueran movimientos adicionales.

### US6 — Exportar y consultar con permisos independientes (P1)

La persona autorizada exporta cualquier reporte detallado en PDF o Excel sin perder filtros; quien sólo puede consultar no obtiene por ello emisión ni acceso operativo adicional.

**Prioridad**: sostiene la compatibilidad y evita filtraciones por los nuevos resúmenes.

**Prueba independiente**: matriz de permisos aislados, exportación con más de una página y valores monetarios límite.

**Escenarios de aceptación**:

1. Las cinco rutas y botones originales de 012 siguen funcionando con sus permisos anteriores; no se exige `reportes.consultar` nuevo para invocarlos.
2. Las nuevas consultas requieren `reportes.consultar`; cada sección requiere además su permiso fuente según C-04 resuelta; sin él no entrega cifras. Ningún endpoint comprueba directamente un rol.
3. Exportar exige `reportes.emitir` y la lectura autorizada del reporte; se rechaza la invocación directa sin permisos, sin entregar cifras parciales ni nombres sensibles.
4. Con 21 filas y página de 20, el archivo incluye 21. Con 5.001 filas, se informa cantidad, tope 5.000 y necesidad de acotar; nunca se entrega un archivo parcial.
5. El PDF presenta nombre, filtros, instante argentino de generación, autor, cantidad, resumen/totales y tabla; XLSX conserva tipos de fecha y valores numéricos exactos dentro de lo representable. Si no puede preservar centavos, se aplica AC-05, sin truncar ni redondear.

## Requirements

### Functional Requirements

- **FR-001**: El módulo DEBE ofrecer Dashboard, Viajes, Vencimientos, Facturación, Liquidaciones, Caja y Utilización de flota como recorridos accesibles desde Reportes; conservar los accesos originales de 012.
- **FR-002**: El dashboard DEBE ser sólo lectura y mostrar KPI, gráficos y resúmenes/enlaces en ese orden de prioridad visual. No genera operaciones ni datos ficticios para gráficos.
- **FR-003**: Desde/hasta globales DEBEN ser obligatorios en el dashboard, inclusivos, con mes calendario argentino actual por defecto y validación de rango invertido antes de consultar. Los reportes nuevos requieren período explícito; las consultas antiguas conservan sus filtros opcionales.
- **FR-004**: KPI, gráficos, tablas y exportaciones DEBEN indicar sus filtros y universo completo. La paginación afecta sólo filas visibles, nunca agregados.
- **FR-005**: Viajes DEBE informar cantidades totales, realizados, pendientes de rendición, distribución por sus cinco estados, por cliente y transportista y evolución por mes. Realizados = Rendido + Facturado, pendientes de rendición = EnCurso (en marcha o terminado esperando remito) y Pendiente separado; anulados identificados y excluidos de trabajo realizado.
- **FR-006**: R1 DEBE conservar número, fecha, cliente, origen, destino, chofer, vehículo, transportista, estado e importe, filtros originales y orden total. Los agrupamientos no alteran el detalle.
- **FR-007**: Facturación DEBE presentar neto/IVA/total persistidos, estado visible, cliente y número, facturado y pendiente vigente del conjunto documental, junto al cobrado por FechaCobro según AC-02. No recalcula impuestos ni crea cobros parciales.
- **FR-008**: R3 DEBE ofrecer filtros combinables de fechas, cliente, estado y número de factura; las perspectivas documental y de cobro DEBEN tener contexto y detalle reconciliable separados según C-03.
- **FR-009**: Liquidaciones DEBE mostrar cantidad, importe total, pagado vigente y pendiente por transportista, período liquidado y estado; distribución por estado y evolución mensual.
- **FR-010**: R4 DEBE distinguir selección por período liquidado y pagos por FechaPago. Los pagos de OP manuales y de OPP se identifican sin doble suma; no se asume igualdad entre pago del período y pago acumulado documental.
- **FR-011**: Vigencia, anulación y reversión DEBEN seguir 009/014; historia conservada y deuda calculada con pagos vigentes. Los pagos revertidos se excluyen del flujo vigente aunque su reversión sea posterior al rango, conservando detalle histórico. Los meses liquidados se seleccionan completos entre el mes inicial y final, sin prorrateo; los pagos usan días exactos.
- **FR-012**: Caja DEBE presentar ingresos, egresos, neto y evolución mensual de movimientos efectivamente registrados en el rango, con corte de días argentinos; no inventa saldos ni conciliación.
- **FR-013**: R5 DEBE reutilizar el reporte existente y sus referencias tipadas; efectivo compensado no elimina el egreso original. Cobros 006 y pagos 009 no son, por sí mismos, movimientos de Caja.
- **FR-014**: R6 DEBE medir utilización exclusivamente por conteo de viajes vinculados a VehiculoId en el período de Fecha del viaje. Cuenta Rendido + Facturado; incluye unidades sin viajes con cantidad cero y bajas históricas; ningún porcentaje, kilómetro u hora se deriva de ese conteo.
- **FR-015**: R6 DEBE permitir período, vehículo, tipo y transportista/propietario, tabla y ranking/distribución por vehículo/tipo. Atributos actuales no se presentan como históricos; viajes sin vehículo se identifican aparte y no se asignan ficticiamente.
- **FR-016**: R2 DEBE conservar los tres paneles de vencimientos y sus exportaciones sin duplicar reglas. El período global no reemplaza su evaluación a hoy; no se agrega panel de cheques de 014 como reporte nuevo.
- **FR-017**: Cada reporte DEBE tener detalle tabular, paginación de 20 por defecto, orden total estable y filtros combinables; enlaces a detalles sólo con su permiso propio. Una agrupación debe permitir explicar sus cifras mediante el detalle pertinente.
- **FR-018**: Los gráficos DEBEN representar evolución mensual y distribuciones pertinentes sin duplicaciones visuales redundantes: viajes por estado, facturación/cobros por mes y cliente, liquidaciones/pagos por mes y transportista, caja por mes y ranking de flota. Se pueden organizar en secciones sin imponer todos simultáneamente en un único gráfico.
- **FR-019**: Toda serie DEBE mostrar unidad y criterio temporal; importes y cantidades no se confunden. Los valores exactos y categorías completas DEBEN estar disponibles en tabla; si se resume un ranking, debe declararse el recorte y mantenerse acceso al resto sin perder totales.
- **FR-020**: Todos los reportes detallados, incluidas las dos perspectivas financieras, DEBEN ofrecer PDF y XLSX del conjunto completo filtrado, con orden y totales coherentes. No se exige exportar el dashboard ni incrustar gráficos en PDF.
- **FR-021**: Exportaciones DEBEN conservar encabezados, autor, filtros, fecha/hora argentina, cantidad y totales de 012; PDF legible en ARS y XLSX con fechas reales e importes numéricos aptos para operar dentro de sus límites exactos.
- **FR-022**: El límite de 5.000 filas DEBE mantenerse, inclusive 5.000 admitidas; sobre el límite no se generan archivos parciales. El límite no recorta KPI ni agregados. Un conjunto vacío mantiene exportación no disponible en UI como 012.
- **FR-023**: Cada celda monetaria y cada total del XLSX DEBEN superar el round-trip de AC-05: decimal fuente exacto → celda numérica XLSX → valor recuperado/leído → igualdad monetaria exacta con el original a escala de dos decimales. No se exige igualdad binaria IEEE-754. Si cualquier celda o total pierde centavos, se DEBE rechazar la generación completa antes de entregar el archivo e informar el motivo; consulta y PDF siguen disponibles. No truncar ni redondear silenciosamente, ni convertir importes a texto como workaround.
- **FR-024**: Al emitir, se DEBEN releer los datos actuales y declarar el instante de la foto. Si cambiaron desde la consulta, el archivo puede reflejar ese cambio sin alterar operaciones; no se promete una foto congelada a la fecha histórica del filtro.
- **FR-025**: `reportes.consultar` DEBE ser independiente de `reportes.emitir`, otorgado inicialmente a Gerencia y AdministradorSistema. Cada sección exige además permiso fuente; Utilización usa un permiso propio de lectura `reportes.flota.consultar`, otorgado inicialmente a Gerencia y AdministradorSistema, sin conceder flota.gestionar. Consultar utilización requiere ambos permisos; exportarla agrega reportes.emitir. No implica gestión ni descarga de documentos/adjuntos operativos.
- **FR-026**: Se DEBEN preservar los permisos actuales de exportación 012 y sus rutas, parámetros, formatos y errores. No se agrega un permiso nuevo obligatorio a sus consumidores. Las nuevas consultas/exportaciones se autorizan en servidor sin chequeos directos de rol.
- **FR-027**: Consultar/exportar NO DEBE modificar estados, importes, historial económico, movimientos ni documentos fuente. 012 no guarda auditoría de emisión ni archivos; 015 no introduce auditoría transversal ni almacenamiento de reportes.
- **FR-028**: Se DEBEN conservar clientes, transportistas y unidades inactivos necesarios para consultar historia; snapshots documentales existentes se respetan y filtros usan identidades estables. No se inventa una historia de nombres, tipo/propiedad/disponibilidad que no está persistida.
- **FR-029**: Fallos, carga, vacío y acceso denegado DEBEN distinguirse, conservar filtros y permitir reintentar cuando corresponda. Error parcial no se presenta como total cero ni mezcla respuestas de distintos períodos.
- **FR-030**: La UI DEBE mantener español argentino, pesos, componentes/diseño actuales, controles accesibles, navegación por teclado y disposición responsive; consulta no recibe una acción primaria artificial por tener exportación.

### Requisitos no funcionales

- **NFR-001 — Exactitud**: cálculos autoritativos en aritmética decimal exacta; sin float/double ni cálculos financieros autoritativos con JS number. Nuevos contratos monetarios conservan todos los centavos según convención 013, sin convertir contratos legacy silenciosamente. La escala visual del gráfico no sustituye el valor exacto accesible. La representación numérica XLSX se valida mediante round-trip monetario a dos decimales según AC-05, para celdas y totales; esto no exige igualdad de representación binaria IEEE-754 ni cambia el decimal exacto del dominio.
- **NFR-002 — Consulta acotada**: dashboard/reportes nuevos no cargan filas históricas sin período; devolver una página no requiere descargar el conjunto completo al navegador. Los agregados se obtienen sobre el conjunto filtrado completo sin sumar sólo la página ni hacer agregación financiera autoritativa en frontend.
- **NFR-003 — Crecimiento**: no hay consultas adicionales por cada fila/vehículo/documento (N+1). La cantidad de consultas de datos para un mismo reporte y cantidad de secciones permanece acotada al pasar de 20 a 200 filas, verificable mediante instrumentación de prueba; no fija arquitectura.
- **NFR-004 — Tiempo verificable**: mantener el objetivo de 012 de exportar 1.000 filas en menos de 15 segundos para ambos formatos. Como objetivo de aceptación nuevo, dashboard y primera página deben completar en hasta 5 segundos en al menos 19 de 20 consultas con 10.000 viajes, 5.000 facturas, 5.000 liquidaciones y 10.000 movimientos sintéticos, período anual y una sesión activa; medir en entorno local de aceptación documentando recursos, calentamiento y versiones. No se afirma medición ejecutada durante esta spec.
- **NFR-005 — Consistencia**: una respuesta de análisis no mezcla estados leídos de modo que los totales contradigan su propio detalle de referencia. Consultas hechas en momentos distintos pueden cambiar; deben mostrar contexto de actualización. Verificar con cobro, pago y anulación concurrentes.
- **NFR-006 — Compatibilidad**: regresión de las cinco exportaciones de 012, sus permisos, encabezados/columnas y filtros; cambios sobre módulos fuente exclusivamente de lectura, sin alterar sus reglas de negocio. La guardia de precisión XLSX de AC-05 es una extensión explícita de seguridad del dato, no redondeo.

### Entidades funcionales

No nuevas entidades de negocio persistidas. Período de análisis; resumen/KPI; serie mensual; distribución/ranking; fila documental; cobro/pago fechado; reporte tabular y exportación son vistas sobre datos existentes. Los permisos usan el sistema actual.

### Edge Cases

- Rango invertido, ausente en dashboard, sin actividad, mes bisiesto y cambio de día UTC/Argentina.
- Viajes futuros planificados, anulados, rendidos luego facturados y sin asignación; no duplicar realizados por cambio de estado.
- Documentos anulados/refacturados, cobro posterior al mes emitido, pagos parciales y pagos de deuda anterior; nunca definir pendiente como facturado del mes menos cobros del mes.
- OPP anulada después del mes del pago, OP manual sin OPP, varias OP de una OPP, egreso y compensación en diferentes meses; conservar origen e historia.
- Inactivos, renombres y cambios de tipo/propietario/disponibilidad; no inferir snapshots inexistentes ni borrar historia por filtros de activos.
- 0, 20, 21, 5.000 y 5.001 filas; máximos monetarios, sumas mayores que un documento, netos negativos de Caja y XLSX con pérdida de centavos.
- Datos cambiados durante consulta/exportación, filtro cambiado con solicitud anterior pendiente, sección fallida o sin permiso, acceso directo y sesión revocada.
- Gráfico con muchas categorías o todas cero; tabla exacta accesible y sin leyenda incompleta engañosa.

## GAP ANALYSIS — Contrato aprobado vs implementación actual vs 015

| Contrato transcripto | Implementación actual comprobada | Cobertura 015 |
|---|---|---|
| Viajes realizados | 005 listado con cinco estados, totales por cliente/transportista; 012 exporta listado filtrado | US2, FR-005/006: KPI, evolución/distribución; C-01 resuelta: Rendido + Facturado |
| Pendientes de rendición | 005 estado EnCurso y señal de demorado; filtro de estado y exportación 012 | R1 analiza EnCurso, incluido finalizado esperando remito, sin crear un nuevo estado; C-01 resuelta |
| Vencimientos | 012 exporta tres paneles de choferes/flota/facturas | R2 integra/enlaza los tres; FR-016, sin duplicar reglas ni fingir corte histórico |
| Facturación | 006 listado, cobro total, FechaCobro, totales por cliente; 012 sólo exporta vencimientos de facturas | R3 nuevo general con detalle documental/cobros, US3 y FR-007/008 |
| Liquidaciones | 009 listado por mes/año y pagos; 014 agrega origen/vigencia/reversión; 012 no exporta liquidaciones | R4 nuevo, período liquidado y pagos por fecha, US4 y FR-009–011 |
| Movimientos de caja | 011 consulta, resumen y referencias; 012 PDF/XLSX con ingresos/egresos/neto | R5 reutilizado + KPI/evolución de efectivo; US5 y FR-012/013 |
| Utilización de flota | 004 vehículo/tipo/propietario/estado actual; 005 VehiculoId y Fecha; sin métricas de km/horas | R6 nuevo conteo de viajes, ranking y detalle; FR-014/015, C-02 |
| Reportes operativos/de viajes | Cinco exportaciones aisladas 012; sin módulo analítico central | Dashboard, R1/R2/R6 y accesos existentes; US1/2/5 |
| Reportes administrativos | Paneles documentales y listados existentes | Integración R2, R3/R4 y lectura transversal sin ABM nuevo |
| Reportes financieros | Exportación de caja y alertas de facturas, totales 006 sin reporte general | R3/R4/R5 y KPI diferenciados de documentos/flujos, AC-02/03 |
| Estadísticas e historia | Totales parciales de Viajes/Facturación; exportación de la foto actual | Series mensuales, distribuciones y rankings sobre registros históricos; sin reconstrucción arbitraria de estados pasados |

La cobertura de las categorías comprometidas queda trazada; C-01–C-04 quedan resueltas con las respuestas del usuario y su correspondencia con el modelo existente. Stock/almacenamiento/posiciones/estibas/inventario no pertenecen a esta matriz ni a 015.

## Dependencias y extensiones acotadas

1. **004 Flota / 003 documentación**: datos actuales de vehículo, tipo, propietario, estado derivado y paneles existentes; sin nuevas mediciones ni historia inventada. No escaneos ni ABM.
2. **005 Viajes**: Fecha de servicio, estados, cliente, transportista asignado y VehiculoId; consultas y agregados de lectura. No cambia asignación/rendición/facturación.
3. **006 Facturación**: FacturaCliente, snapshots, Neto/Iva/Total, Fecha, FechaCobro y estado derivado; no nuevas reglas fiscales, cobros ni comprobantes.
4. **009 Liquidaciones**: mes/año liquidado, total/pagado/estado, OP FechaPago y vigencia; no pagos ni edición desde Reportes.
5. **011 Caja**: movimientos reales con sus instantes y referencias, neto del rango y corte argentino. No cierre, compensación ni reglas de propiedad nuevas.
6. **012 Reportes**: preservar consultas/fuentes, capacidades PDF/XLSX, formatos, tope y permisos existentes; ampliar lectura y nuevos reportes sin reescribir el módulo.
7. **013 Proveedores**: fuente contextual de facturas de Fletero vinculadas a liquidaciones; no sustituye FacturaCliente en R3 ni agrega reporte general de compras.
8. **014 Pagos**: origen/reversión de OP y referencias OPP de caja para evitar doble cómputo; no exportación general de cheques, reglas nuevas de pagos ni acceso a CBU innecesario.
9. **001/002**: sesión, permisos, menú y revocación existentes; sin rol hardcodeado en endpoints. Gestión de Empleados no es dependencia.

## Inspección comprobada y límites técnicos reales

- 012: revisados spec/plan/tasks/contratos; `GT.Api/Reportes/ReportesEndpoints.cs`, cinco fuentes en `GT.Application/Reportes/Fuentes/`, `ReporteTabular`, `ArmadoDeReporte`, armadores de Infrastructure y componente/servicio/tests frontend. Tasks 012 registran sus entregas completas; esta etapa no reejecuta suites ni afirma nueva evidencia de runtime.
- Endpoints originales a preservar: `/api/viajes/reporte`, `/api/vencimientos/reporte`, `/api/flota/vencimientos/reporte`, `/api/facturas/vencimientos/reporte`, `/api/movimientos-caja/reporte`. Exportan con permiso fuente + `reportes.emitir`. Sus pantallas son `/viajes`, `/choferes/vencimientos`, `/flota/vencimientos`, `/facturas/vencimientos`, `/movimientos-caja`.
- Tests existentes de Reportes cubren consultas/filtros/orden, tipos de celdas y totales, encabezados, estados visibles, permisos aislados y de roles, tope, errores, PDF real y reapertura XLSX; pruebas frontend cubren diálogo, descargas, permisos y errores. Son la regresión obligatoria futura, no pruebas ejecutadas en especificación.
- PDF usa QuestPDF y XLSX ClosedXML con modelo tabular común; no hay auditoría persistida de emisión. No se elige nueva arquitectura ni librería en la spec.
- `frontend/package.json` no contiene biblioteca de gráficos; existen React, controles compartidos y diálogos Radix. La planificación decidirá representación técnica, sin instalación en esta etapa.
- `RepositorioFacturas.ConsultarTotalesAsync` hoy selecciona por Fecha de factura y suma cobrado según estado vigente. **No satisface por sí solo AC-02**: 015 requiere lectura por FechaCobro para flujo temporal, sin cambiar ese contrato previo.
- `Liquidacion` guarda mes/año y agregados vigentes; fecha de generación sale de historial. `OrdenDePago` guarda FechaPago, Importe, Vigente y origen OPP. No se puede deducir el mes liquidado desde la fecha de pago.
- `Viaje` guarda una asignación actual por vehículo; no una bitácora completa de cada reasignación. `Vehiculo` no registra kilómetros/horas ni historial completo de tipo/propiedad. R6 no promete medir todas las asignaciones históricas.
- La consulta histórica selecciona registros pasados con los estados vigentes al consultar. No equivale a una contabilidad ni a reconstrucción de saldos como estaban en cualquier fecha pasada.
- Excel numérico tiene precisión finita: asignar un decimal a ClosedXML no garantiza conservar todo decimal(18,2). El importe fuente sigue siendo decimal exacto del dominio; la celda XLSX debe ser numérica. La verificación es un round-trip monetario: decimal fuente → valor numérico XLSX → valor recuperado/leído → comparación exacta a escala de dos decimales. Se exige igualdad monetaria a centavos, no identidad de representación binaria IEEE-754. El plan deberá demostrar esta verificación tanto para celdas individuales como para totales. Si cualquiera no coincide con el original a dos decimales, se rechaza la generación completa del XLSX antes de entregarlo, sin truncar ni redondear silenciosamente y sin convertir a texto como workaround. Consulta y PDF permanecen disponibles; no se alteran en silencio las exportaciones legacy.

## Success Criteria

- **SC-001**: Al abrir el dashboard, el período coincide con el mes argentino en los bordes horarios y todos los KPI visibles identifican su universo/fecha de referencia.
- **SC-002**: Con datos de dos períodos y varias páginas, cada KPI/serie coincide con su detalle completo; suma de grupos reconcilia sin filas duplicadas ni truncado oculto.
- **SC-003**: Factura emitida antes y cobrada dentro del rango se incluye en flujo de cobros; liquidación anterior pagada en rango se incluye en flujo de pagos, sin alterar los pendientes documentales por restas entre universos.
- **SC-004**: Cada uno de R1–R6 ofrece detalle y los dos formatos, conservando las tres variantes R2. Archivo de 21 filas contiene 21; 5.001 se rechaza con mensaje de tope.
- **SC-005**: El conteo por vehículo reconcilia con viajes por VehiculoId, sin métricas de uso inexistentes y con reglas de C-02 verificadas.
- **SC-006**: Los cinco reportes originales mantienen rutas, permisos, columnas y comportamiento; consultar/exportar no produce ninguna mutación de negocio.
- **SC-007**: Siete recorridos del módulo funcionan por teclado y a 1280px y 375px sin perder acciones ni valores; tabla/descripción hace accesibles todos los datos de gráficos.
- **SC-008**: Se cumplen NFR-003/004 con mediciones reproducibles; sin histórico completo descargado al navegador ni N+1 por fila.
- **SC-009**: Permisos aislados, sesión revocada y acceso directo respetan C-04; exportación no habilita acceso lateral a módulos, adjuntos o gestión.
- **SC-010**: Pruebas de importes ordinarios, máximos monetarios, netos negativos y totales conservan todos los centavos en consulta/PDF. En XLSX, cada celda monetaria y total es numérico y cumple el round-trip decimal fuente → valor numérico XLSX → valor recuperado/leído, coincidiendo exactamente con el original a escala de dos decimales, sin exigir igualdad binaria IEEE-754. Cualquier pérdida monetaria provoca rechazo completo antes de entregar el XLSX; nunca éxito con truncado o redondeo silenciosos ni sustitución por texto. Consulta y PDF siguen disponibles.

## Aclaraciones resueltas — 2026-10-19

- **C-01 — Rendición y remito**: el usuario define pendientes de rendición como viajes en curso o físicamente terminados cuyo remito todavía no llegó para completar datos. Realizado es el viaje rendido con el remito correspondiente, aunque aún no esté facturado. Pendiente es un viaje que todavía no terminó ni se rindió. Correspondencia con 005/006: EnCurso agrupa ambas situaciones sin rendir; Rendido es realizado sin factura vigente y Facturado conserva la condición de realizado después de facturarlo. Por eso realizados suma Rendido + Facturado una sola vez. No se agrega un estado «terminado sin remito», no se deduce fin físico desde la fecha ni se exige cargar un adjunto nuevo. La disponibilidad de remito se interpreta a través de la rendición ya registrada; 015 no cambia sus validaciones.
- **C-02 — Flota**: aceptadas unidades con cero viajes y bajas con viajes en el período. R6 mide viajes realizados (Rendido + Facturado) por vehículo, no reasignaciones históricas. El universo incluye unidades con cero viajes y unidades dadas de baja; su condición se identifica y puede acotarse mediante filtros, sin descartar silenciosamente unidades por estar inactivas. Tipo, propietario y disponibilidad se muestran como actuales, nunca como una reconstrucción histórica. Los filtros permiten acotar el universo sin cambiar la definición del conteo.
- **C-03 — Documentos y flujos separados**: aceptada la separación entre documentos del período con deuda vigente y cobros/pagos fechados en el rango, aunque correspondan a documentos anteriores. Pagos revertidos excluidos del total vigente y visibles como historia identificada. Aceptados meses liquidados completos: desde 15/09 hasta 10/10 incluye liquidaciones de septiembre y octubre completas; pagos y cobros usan las fechas exactas, sin prorratear. La evolución documental mensual utiliza Fecha de factura o mes liquidado; la evolución de cobros/pagos utiliza FechaCobro/FechaPago. «Vigente» refiere al momento de consulta, no a reconstruir el saldo como estaba al cierre histórico.
- **C-04 — Permisos de lectura**: aceptado reportes.consultar como entrada y permiso fuente adicional por sección. Viajes: viajes.consultar; Facturación y vencimientos de facturas: facturacion.consultar; Liquidaciones: liquidaciones.consultar; Caja: caja.consultar; vencimientos de choferes/flota: sus permisos actuales de vencimientos. Utilización de flota agrega lectura propia reportes.flota.consultar para evitar conceder gestión; se otorga inicialmente a Gerencia y AdministradorSistema. Sin permiso de sección no se entregan cifras ni detalle y se informa acceso no disponible. Enlaces a fichas operativas conservan su permiso propio. Las nuevas exportaciones requieren además reportes.emitir; las cinco exportaciones legacy no incorporan requisitos nuevos. Los permisos se comprueban en servidor, no mediante roles directos.

No quedan preguntas funcionales abiertas con las respuestas recibidas. Las limitaciones del modelo y de precisión XLSX permanecen declaradas; su solución técnica y verificación corresponden a la planificación. No se ejecuta `/speckit.plan` como parte de esta especificación.

## Fuera de alcance

Empleados/ABM, stock, almacenes, pallets, posiciones/estibas e inventario; mantenimiento predictivo, kilometraje no registrado, combustible, costos por kilómetro, contabilidad, reglas impositivas nuevas, conciliación bancaria, cambios de pagos/facturación/liquidaciones/Caja; almacenamiento de reportes, auditoría transversal nueva y exportación de gráficos del dashboard. No rediseño global ni implementación en esta etapa.
