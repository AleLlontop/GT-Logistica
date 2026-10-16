# Feature Specification: Gestión de proveedores y facturas de proveedores

**Feature Branch**: `013-gestion-proveedores`

**Created**: 2026-10-14

**Status**: Draft

**Input**: Padrón administrable de proveedores de G&T Logística y casos GFP_01 Cargar factura de proveedor y GFP_02 Consultar factura de proveedor. Autocomplete, neto obligatorio mayor a cero, IVA del 21%, asociación obligatoria con una liquidación pendiente del transportista externo vinculado para Fleteros y sin liquidación para Proveedores normales, y consulta de comprobantes. Sin incorporar pagos ni conciliaciones.

## Clarifications

### Session 2026-10-14

- Q: ¿La factura de un Fletero puede registrarse sin liquidación? → A: No; debe seleccionarse una liquidación Pendiente correspondiente al transportista externo vinculado.
- Q: ¿Se selecciona liquidación para un Proveedor normal? → A: No; no se selecciona ni se admite asociación con liquidación.
- Q: ¿Qué valores admite el neto de una factura? → A: Es obligatorio y debe ser estrictamente mayor a cero, según GFP_01.
- Q: ¿Debe mantenerse la coincidencia de importes al asociar una liquidación? → A: Sí; el total calculado de la factura debe coincidir exactamente con el importe total de la liquidación.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Administrar proveedores (Priority: P1)

El empleado administrativo registra proveedores normales y fleteros, consulta y corrige sus datos y los da de baja sin perder facturas anteriores.

**Why this priority**: identifica a quién corresponde cada comprobante sin duplicar transportistas existentes.

**Independent Test**: crear un proveedor normal y un Fletero vinculado, buscarlos, editarlos y dar de baja uno conservando su consulta.

**Acceptance Scenarios**:

1. **Given** datos válidos y CUIT libre, **When** se registra un proveedor normal, **Then** queda Activo por defecto y puede consultarse; los campos opcionales pueden quedar vacíos.
2. **Given** razón social o CUIT vacío, CUIT inválido o email informado inválido, **When** se guarda, **Then** se identifica el campo y no se modifica nada.
3. **Given** un CUIT utilizado por un proveedor activo o inactivo, **When** se crea o modifica otro para repetirlo, **Then** se rechaza, incluso con distintos separadores y ante altas simultáneas.
4. **Given** un transportista externo existente, **When** se registra un Fletero vinculado, **Then** se muestran razón social, CUIT, teléfono y email desde Transportistas sin cargarlos nuevamente ni ofrecer otra edición independiente.
5. **Given** alta o edición de Fletero, **When** falta el vínculo, el transportista no existe, es propio o su CUIT corresponde a otro proveedor, **Then** se rechaza con el motivo.
6. **Given** varios proveedores, **When** se combinan búsquedas por razón social, nombre comercial y CUIT con filtros de tipo y estado, **Then** sólo se muestran coincidencias paginadas o un mensaje sin resultados.
7. **Given** un proveedor con facturas, **When** se confirma la baja, **Then** queda Inactivo, conserva sus facturas y no se ofrece para nuevas cargas. Cancelar conserva su estado.
8. **Given** un proveedor Inactivo, **When** se editan sus datos, **Then** sigue Inactivo; al ejecutar la reactivación separada vuelve a ofrecerse.
9. **Given** dos ediciones del mismo proveedor, **When** se guarda la primera y luego la segunda, **Then** la segunda informa el cambio y no sobrescribe la primera.

---

### User Story 2 - Cargar factura recibida, GFP_01 (Priority: P1)

El empleado encuentra un proveedor activo escribiendo parte de sus datos, carga el comprobante y revisa los importes calculados antes de guardar.

**Why this priority**: es el registro de comprobantes recibidos, propósito principal del módulo.

**Independent Test**: seleccionar un Proveedor normal por CUIT y registrar neto $100.000,00 sin liquidación, comprobando IVA $21.000,00, total $121.000,00 y estado Pendiente.

**Acceptance Scenarios**:

1. **Given** proveedores activos e inactivos, **When** se escribe parte de razón social, nombre comercial o CUIT, **Then** las sugerencias se actualizan sólo con activos y pueden seleccionarse con teclado y mouse.
2. **Given** texto sin seleccionar una coincidencia, **When** se guarda, **Then** se exige un proveedor existente; el texto no crea un proveedor.
3. **Given** todos los campos válidos, **When** se cambia el neto, **Then** IVA y total se actualizan sin permitir escribirlos. Neto $0,50 produce IVA $0,11 y total $0,61.
4. **Given** una factura completa de un Proveedor normal sin PDF ni liquidación, **When** se guarda, **Then** queda Pendiente y se abre su detalle con confirmación; el formulario de alta deja de mostrarse.
5. **Given** una factura existente, **When** se repite su número para el mismo proveedor, **Then** se rechaza; para otro proveedor se acepta. Dos cargas simultáneas del mismo par sólo registran una factura.
6. **Given** hoy en Argentina, **When** se registra fecha de hoy y vencimiento de CAE igual a la fecha de factura, **Then** se acepta; mañana o vencimiento anterior se rechazan marcando el campo.
7. **Given** un PDF real de hasta 10 MB, **When** se adjunta y guarda, **Then** puede abrirse desde el detalle con autorización. Otro contenido, aunque se llame PDF, o un tamaño mayor se rechaza sin crear la factura.
8. **Given** un proveedor seleccionado, **When** se da de baja antes del guardado, **Then** se rechaza indicando que está Inactivo.
9. **Given** neto vacío, cero o negativo, **When** se intenta guardar, incluso invocando directamente la acción, **Then** se marca el campo neto como obligatorio o se informa que debe ser mayor a cero, según corresponda, y no se registra nada.

---

### User Story 3 - Asociar liquidación de Fletero (Priority: P1)

El empleado debe seleccionar y asociar una liquidación pendiente del transportista externo vinculado al registrar una factura de Fletero, verificando la igualdad de importes.

**Why this priority**: relaciona el comprobante con el trabajo liquidado sin asociaciones a otros transportistas.

**Independent Test**: seleccionar un Fletero entre dos transportistas y asociar una liquidación de $121.000,00 a una factura de neto $100.000,00.

**Acceptance Scenarios**:

1. **Given** un Fletero seleccionado, **When** se consultan liquidaciones, **Then** sólo aparecen las Pendientes de su transportista no asociadas a otra factura, con número, período y total.
2. **Given** una liquidación pagada, anulada, ya asociada o de otro transportista, **When** se intenta asociarla, incluso directamente, **Then** se rechaza.
3. **Given** total de factura igual al total de liquidación, **When** se guarda, **Then** factura y vínculo se registran juntos, la liquidación conserva su estado y no se ofrece para otra factura.
4. **Given** total de factura distinto por un centavo, **When** se guarda, **Then** se muestran ambos importes y no se registra nada.
5. **Given** una liquidación seleccionada, **When** cambia el proveedor, **Then** se limpia la selección y se ofrecen sólo las del nuevo Fletero si corresponde.
6. **Given** un Fletero sin liquidaciones disponibles, **When** se intenta registrar su factura, **Then** se informa que no hay liquidaciones Pendientes disponibles y no se permite guardar sin asociación.
7. **Given** dos usuarios que eligieron la misma liquidación, **When** guardan a la vez, **Then** sólo una factura logra asociarla y la otra se rechaza sin guardado parcial.
8. **Given** la selección abierta, **When** la liquidación cambia de total, queda pagada o se anula antes de guardar, **Then** se verifican los datos actuales y se rechaza si ya no cumple.
9. **Given** una factura asociada, **When** posteriormente se opera la liquidación en su módulo, **Then** la factura conserva importes y estado; su detalle distingue el importe al asociar de la situación actual de la liquidación.
10. **Given** un Fletero con liquidaciones disponibles pero ninguna seleccionada, **When** se intenta guardar, incluso directamente, **Then** se exige seleccionar una liquidación y no se registra nada.
11. **Given** un Proveedor normal, **When** se carga una factura, **Then** no se ofrece selección de liquidación y se permite guardar sin ella; intentar enviar una asociación directamente se rechaza sin registrar nada.

---

### User Story 4 - Consultar facturas, GFP_02 (Priority: P1)

El empleado combina filtros, abre una factura y consulta el comprobante y su PDF cuando existe.

**Why this priority**: permite recuperar y verificar lo registrado.

**Independent Test**: combinar fechas, proveedor y estado sobre facturas de distintos proveedores y abrir un resultado.

**Acceptance Scenarios**:

1. **Given** facturas registradas, **When** se abre el listado, **Then** muestra número, fecha, proveedor, total y estado, paginado y ordenado por fecha de factura descendente con desempate estable.
2. **Given** un rango de fechas, **When** se aplica, **Then** incluye los dos días extremos; se admite un solo extremo y se rechaza un rango invertido.
3. **Given** proveedores activos e inactivos, **When** se escribe en el filtro, **Then** el autocomplete encuentra ambos por razón social, nombre comercial o CUIT y filtra por el proveedor seleccionado.
4. **Given** fechas, proveedor y Pendiente aplicados juntos, **When** se consulta, **Then** cada fila cumple todos los filtros; limpiarlos permite consultar todo.
5. **Given** una fila, **When** se abre su detalle, **Then** se ven todos los datos registrados, identificación histórica del proveedor y referencia de origen, PDF cuando existe y la liquidación asociada si la factura se registró para un Fletero; se indica la ausencia de PDF y que no corresponde liquidación en una factura registrada para un Proveedor normal.
6. **Given** un proveedor editado o dado de baja después de cargar, **When** se consulta su factura, **Then** conserva razón social y CUIT al cargarla y sigue encontrándose por el proveedor de origen.
7. **Given** listado vacío o filtros sin coincidencias, **When** se consulta, **Then** el mensaje distingue ambos casos.

---

### User Story 5 - Acceder con permisos (Priority: P1)

El empleado accede con sesión y permisos; otros usuarios no acceden sin autorización.

**Why this priority**: los comprobantes y datos comerciales requieren acceso restringido.

**Independent Test**: intentar consultar, escribir y abrir PDF sin sesión, sin permisos y con Administración de la empresa.

**Acceptance Scenarios**:

1. **Given** Administración de la empresa o Administrador del sistema con permisos del módulo, **When** ingresa, **Then** encuentra Proveedores, Cargar factura de proveedor y Consultar facturas de proveedores y opera lo previsto.
2. **Given** ausencia de sesión o permisos, **When** se accede a pantallas, acciones o PDF, **Then** se exige ingreso o se rechaza sin exponer datos; la pantalla sin permiso indica solicitarlo al administrador del sistema.
3. **Given** permisos de este módulo sin administración de Transportistas, **When** se selecciona un Fletero, **Then** se consulta lo necesario sin obtener permiso para editar Transportistas ni acceder a documentación de choferes.

---

### Edge Cases

- CUIT con guiones, puntos o espacios se normaliza según el padrón existente. Al editar se admite conservar el CUIT propio.
- Una respuesta de búsqueda anterior no reemplaza sugerencias del texto actual; limpiar el texto limpia la selección.
- Cambiar tipo o vínculo del proveedor no reescribe facturas anteriores; el CUIT resultante debe seguir siendo único.
- Transportista dado de baja: conserva vínculos históricos y su actividad no cambia automáticamente la del proveedor. Si deja de ser externo no se admiten nuevas vinculaciones o asociaciones como Fletero.
- Sin empresa emisora configurada se opera con proveedores normales y se explica el impedimento para identificar externos.
- Una liquidación Pendiente con pagos parciales sigue siendo elegible si está disponible; se compara con su total, no con el saldo. No se agregan restricciones por pagos parciales.
- La liquidación editada después de asociar no altera la factura: se conserva el importe comprobado al registro, aunque difiera del total actual.
- Un CAE vencido hoy se acepta para una factura antigua si su vencimiento no es anterior a la fecha de factura.
- Entre las 21:00 y las 23:59 argentinas el día siguiente en otra zona no habilita facturas de mañana.
- Fallar la carga de PDF o la asociación no deja factura parcial ni liquidación reservada por una factura inexistente.
- Un Fletero sin liquidaciones Pendientes disponibles no puede registrar una factura; no se admite omitir la selección para resolver esa ausencia.
- Cambiar de Fletero a Proveedor normal limpia la liquidación y deja de ofrecer su selección; cambiar a Fletero exige una selección válida antes de guardar.
- Neto vacío, cero o negativo se rechaza; el mínimo positivo representable a dos decimales, $0,01, satisface la regla de neto mayor a cero, sin omitir las restantes validaciones.

## Requirements *(mandatory)*

### Functional Requirements

#### Proveedores

- **FR-001**: Permitir alta, listado, detalle, modificación, baja lógica y reactivación de proveedores, sin borrar registros ni facturas anteriores.
- **FR-002**: Exigir razón social, CUIT, tipo Fletero o Proveedor normal y estado Activo o Inactivo; proponer Activo en alta. Nombre comercial, teléfono, email y dirección son opcionales; validar el formato del email informado.
- **FR-003**: Aplicar la validación y normalización de CUIT del padrón existente y garantizar unicidad entre proveedores activos e inactivos al crear o modificar, incluso simultáneamente.
- **FR-004**: Confirmar explícitamente la baja y establecer Inactivo; cancelar no modifica nada. Reactivar es una acción separada, sin confirmación adicional. Editar datos no cambia implícitamente el estado.
- **FR-005**: Exigir al Fletero vínculo con transportista externo existente, definido como CUIT distinto del de la empresa emisora configurada, según Módulo 9. El proveedor normal no tiene vínculo. Informar el impedimento si falta configuración.
- **FR-006**: Leer razón social, CUIT, teléfono y email del Fletero desde Transportistas, sin segunda copia ni edición independiente. Nombre comercial, dirección, tipo y estado pertenecen al proveedor. Indicar dónde se mantienen los datos compartidos sin otorgar permisos adicionales.
- **FR-007**: Buscar parcialmente sin distinguir mayúsculas por razón social, nombre comercial y CUIT; admitir filtros combinables por esos datos, tipo y estado. Mostrar razón social, nombre comercial cuando exista, CUIT, tipo y estado y acceso al detalle.
- **FR-008**: Paginar proveedores de a 20, ordenando por razón social con desempate estable por identificador; distinguir padrón vacío de búsqueda sin resultados.
- **FR-009**: Rechazar ediciones que partan de datos modificados por otra operación, sin sobrescribirlos. Cambiar tipo o vínculo vuelve a validar identidad, unicidad y condición externa, sin cambiar facturas anteriores.

#### Carga, GFP_01

- **FR-010**: Registrar facturas de proveedores activos con proveedor, número, fecha, detalle, neto, IVA, total, CAE y vencimiento del CAE obligatorios. El neto DEBE ser estrictamente mayor a cero; rechazar neto vacío, cero o negativo tanto en pantalla como al invocar directamente la acción. IVA y total se calculan; PDF es opcional. Para Fletero, la liquidación es obligatoria; para Proveedor normal no se selecciona ni se admite.
- **FR-011**: Seleccionar proveedor mediante autocomplete dinámico por razón social, nombre comercial o CUIT entre activos, con teclado y mouse y mensajes de carga, resultados o ausencia. Identificar sugerencias con razón social, nombre comercial cuando exista y CUIT. Exigir selección existente, sin recorrer listas extensas ni crear proveedores desde texto libre.
- **FR-012**: Volver a verificar al guardar que el proveedor existe y sigue Activo, rechazando con motivo si cambió; garantizar esta condición también frente a una baja simultánea.
- **FR-013**: Calcular IVA como neto × 21%, con redondeo comercial a dos decimales, y total como neto más IVA redondeado. Actualizar al cambiar neto y no aceptar importes calculados distintos al invocar directamente la acción.
- **FR-014**: Expresar importes en ARS con dos decimales y formato argentino; formatear el neto mientras se escribe según el control monetario existente. Sin otras alícuotas ni recargos.
- **FR-015**: Exigir número único por proveedor, incluso en cargas simultáneas, permitiendo el mismo número en proveedores distintos. Tratarlo como texto, conservar ceros y separadores y recortar espacios de borde, sin imponer esquema nuevo de numeración.
- **FR-016**: Rechazar fecha de factura posterior al día actual en Argentina y vencimiento del CAE anterior a la fecha de factura. No exigir vigencia actual ni comprobación fiscal externa del CAE.
- **FR-017**: Crear toda factura Pendiente, sin operaciones de cambio de estado en esta entrega.
- **FR-018**: Guardar factura, PDF cuando exista y asociación obligatoria para Fletero como una operación completa o no guardar nada. Para Proveedor normal se guarda sin asociación. Tras éxito abrir el detalle con confirmación y salir del formulario de alta.
- **FR-019**: Conservar razón social y CUIT del proveedor al cargar la factura, además de su referencia de origen; cambios posteriores no reescriben el comprobante histórico.
- **FR-020**: Admitir únicamente PDF real de hasta 10 MB y rechazar otro contenido o tamaño con motivo específico. Abrirlo con autorización, en el navegador y con su nombre original, según la protección existente de adjuntos.

#### Liquidaciones

- **FR-021**: Exigir en cada factura de Fletero seleccionar exactamente una liquidación Pendiente del transportista externo vinculado, disponible por no estar asociada a otra factura. Mostrar número, período y total. Rechazar el registro si falta la selección, incluso al invocar directamente la acción. Para Proveedor normal no ofrecer selección de liquidación y rechazar cualquier asociación.
- **FR-022**: Verificar al guardar transportista, estado Pendiente y disponibilidad actuales, también ante operaciones simultáneas. Ninguna liquidación puede asociarse a dos facturas.
- **FR-023**: Exigir coincidencia exacta a dos decimales entre total calculado de factura y total actual de liquidación al registrar; si difieren, informar ambos sin guardar. No comparar con saldo ni ajustar neto automáticamente.
- **FR-024**: Limpiar liquidación al cambiar proveedor. Si el nuevo proveedor es Fletero, exigir una nueva selección válida; si es Proveedor normal, no ofrecer selección ni aceptar asociación. La ausencia de liquidaciones Pendientes disponibles para un Fletero DEBE informarse e impedir registrar su factura.
- **FR-025**: Asociar no paga, edita, anula ni cambia el estado de la liquidación. Conservar su importe al asociar; operaciones posteriores de su módulo no alteran la factura. Distinguir en el detalle importe histórico y situación actual.

#### Consulta, GFP_02

- **FR-026**: Listar número, fecha de factura, proveedor, total y estado, con 20 filas por página, fecha de factura descendente y desempate estable por identificador.
- **FR-027**: Combinar filtros por fecha desde y hasta, proveedor exacto y estado; mostrar filtros aplicados y permitir limpiarlos. Incluir días extremos, permitir extremos omitidos y señalar rango invertido como error.
- **FR-028**: Filtrar proveedor con autocomplete por razón social, nombre comercial y CUIT, incluyendo activos e inactivos. La selección filtra por identidad aunque los datos cambiaron desde la carga.
- **FR-029**: Ofrecer filtro Pendiente y sin restricción; no agregar estados ni transiciones futuras.
- **FR-030**: Abrir detalle desde cada fila con todos los datos registrados, identificación histórica y referencia al proveedor de origen y PDF cuando exista. Si la factura se registró para un Fletero, mostrar su liquidación asociada con número, período, importe al asociar y situación actual; si se registró para un Proveedor normal, indicar que no corresponde liquidación. Indicar ausencia de archivo cuando no tenga PDF.
- **FR-031**: Conservar consulta de facturas de proveedores inactivos y distinguir ausencia de registros de ausencia de coincidencias.

#### Acceso, interfaz y alcance

- **FR-032**: Exigir sesión y permisos de módulo. Administración de la empresa y Administrador del sistema reciben acceso a las acciones previstas; no ampliar otros roles por analogía. Ofrecer Proveedores, Cargar factura de proveedor y Consultar facturas de proveedores en el menú según permisos.
- **FR-033**: Proteger consultas, escrituras, búsquedas y PDF incluso mediante acceso directo. Seleccionar transportistas y liquidaciones no otorga administración de otros módulos ni documentación personal.
- **FR-034**: Marcar errores de datos en sus campos; explicar conflictos por cambios de estado sin guardado parcial. Falta de permiso indica a quién solicitarlo. Fallos recuperables permiten reintentar conservando lo ingresado.
- **FR-035**: Seguir el sistema de diseño vigente, teclado, estados de carga, error y vacío, español argentino y a lo sumo una acción primaria por pantalla, sin inventarla en sólo lectura.
- **FR-036**: Excluir órdenes de pago, cheques, transferencias, pagos en efectivo y conciliaciones bancarias. No incorporar edición, anulación o eliminación de estas facturas, otros impuestos ni generación de comprobantes fiscales propios.
- **FR-037**: Acotar cambios de comportamiento de módulos anteriores a: (1) rechazar modificación de CUIT de un transportista vinculado que duplique el CUIT de otro proveedor, sin modificarlo; (2) contemplar la asociación concurrente con operaciones de liquidaciones para comprobar FR-022 y FR-023 sobre un resultado consistente. Reutilizar sus consultas y reglas. No restringir editar, anular o pagar liquidaciones ya asociadas ni modificar viajes, facturas de clientes, choferes o flota.

### Key Entities *(include if feature involves data)*

- **Proveedor**: contraparte emisora de facturas recibidas, con identidad, datos opcionales, tipo y estado; en Fleteros identidad y contacto compartidos proceden del Transportista.
- **Transportista externo**: entidad existente referenciada para identificar al Fletero y sus liquidaciones, sin identidad duplicada.
- **Factura de proveedor**: comprobante identificado por proveedor y número, con fechas, detalle, neto obligatorio estrictamente mayor a cero, IVA y total calculados, CAE, estado e identificación histórica; PDF opcional y exactamente una liquidación si se registró para un Fletero, ninguna si se registró para un Proveedor normal. La consulta conserva esta distinción aunque cambie posteriormente el tipo del proveedor.
- **Asociación de liquidación**: relación obligatoria en facturas registradas para Fleteros y ausente en las de Proveedores normales, exclusiva por liquidación, con importe comprobado al registrar conservado para consulta histórica.
- **Archivo de factura**: PDF opcional con nombre original y apertura restringida.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con 1.000 proveedores, se puede seleccionar uno por cualquiera de los tres datos sin recorrer manualmente el padrón; el 95% de las búsquedas muestra coincidencias o su ausencia en menos de 2 segundos desde que termina de escribir.
- **SC-002**: El 100% de las facturas de prueba muestra IVA y total iguales al cálculo comercial del 21%, incluyendo fracciones de centavo y neto $100.000,00 → IVA $21.000,00 → total $121.000,00; todos los intentos con neto vacío, cero o negativo se rechazan sin registrar facturas.
- **SC-003**: En todas las pruebas de duplicados y simultaneidad se conserva un proveedor por CUIT, una factura por proveedor y número y una factura asociada por liquidación, sin comprobantes parciales.
- **SC-004**: El 100% de las facturas de prueba sigue consultable tras baja o modificación del proveedor, con identificación e importes históricos y PDF disponible sólo para autorizados.
- **SC-005**: Un empleado completa GFP_01 y GFP_02 sin asistencia técnica en todos los recorridos válidos de aceptación: busca, carga, revisa cálculos, guarda, filtra y abre detalle.
- **SC-006**: El 100% de los intentos sin autorización no expone padrón, facturas ni PDF ni completa escrituras.
- **SC-007**: Todos los casos de facturas de Fleteros exigen una liquidación y rechazan selección ausente, transportista distinto, falta de disponibilidad o importe distinto; aceptan una liquidación elegible de importe exactamente igual, sin registrar pagos. Todas las facturas de Proveedores normales se registran sin liquidación y se rechaza cualquier intento de asociarla.

## Assumptions

- Se reutilizan sesión, permisos, protección de archivos y diseño existentes. Arquitectura, persistencia, errores, concurrencia y testing se concretan en el plan siguiendo AGENTS.md; esta spec define comportamiento observable.
- El empleado administrativo corresponde a Administración de la empresa. No se deduce acceso de Gerencia ni Tráfico porque el pedido no lo establece.
- Los datos manuales de factura se asumen obligatorios salvo PDF. Según la aclaración de GFP_01, neto es obligatorio, tiene dos decimales y debe ser estrictamente mayor a cero; no se agregan topes. CAE se registra sin imponer longitud fiscal. Liquidación es obligatoria para Fletero y no corresponde a Proveedor normal.
- La igualdad con liquidación se exige al registrar. Mantenerla después requeriría restringir operaciones del Módulo 9 no solicitadas: se conserva el importe comprobado, perdiendo la igualdad con el total actual si se edita posteriormente, pero conservando la igualdad histórica.
- Según la aclaración de GFP_01, cada factura de Fletero debe asociar exactamente una liquidación Pendiente de su transportista externo vinculado; cada liquidación puede estar asociada a lo sumo a una factura. Las facturas de Proveedores normales no tienen liquidación. No se agregan restricciones por pagos parciales ni por período.
- La baja de proveedor es independiente de Transportistas y no se bloquea por facturas. Su estado es propio; la condición externa se verifica en nuevas vinculaciones y asociaciones, conservando vínculos históricos si cambia.
- Datos compartidos de Fletero se mantienen en Transportistas sin ampliar permisos; nombre comercial y dirección pertenecen a Proveedores porque no existen allí.
- Dependencias: Transportistas del Módulo 3, empresa emisora del Módulo 6 y liquidaciones del Módulo 9. Proveedores normales no requieren empresa emisora configurada. No se precargan proveedores ni se convierten transportistas automáticamente.
- Límite PDF de 10 MB, paginación de 20 filas y redondeo comercial siguen precedentes existentes. SC-001 es un objetivo de usabilidad, no un límite del padrón.
