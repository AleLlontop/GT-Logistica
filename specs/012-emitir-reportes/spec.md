# Feature Specification: Emitir reportes

**Feature Branch**: `012-emitir-reportes`

**Created**: 2026-09-20

**Status**: Draft

**Input**: User description: "Módulo Emitir reportes. Para generar el PDF y el Excel usa librerías conocidas. Con lo ya estructurado crea un botón para imprimir con la opción de reporte en PDF o Excel. Permisos sólo del gerente. Descripción: permite consultar y exportar reportes operativos. Reporte de viajes: en la sección, una vez aplicados los filtros, el gerente presiona el botón *Generar reporte* y aparecen dos opciones, Excel o PDF. Lo mismo para el vencimiento de choferes, flota y facturas. Además de los movimientos de caja."

## Aclaración sobre el nombre

El enunciado lo llama *Módulo Emitir reportes*, pero lo que describe no es una pantalla nueva: son
**botones sobre cinco pantallas que ya existen**. Esta feature no agrega ninguna entrada al menú ni
ninguna dirección nueva a la que se navegue. *Emitir reportes* es el nombre de la capacidad, no de
una sección.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Reporte de viajes con los filtros aplicados (Priority: P1)

Quien tiene el permiso de reportes entra a **Viajes**, acota el listado con los filtros que ya tiene
la pantalla —cliente, transportista, estado, rango de fechas, búsqueda—, mira el resultado y, cuando
es el que quiere llevarse, presiona **Generar reporte**. Se le ofrecen dos formatos, *PDF* y *Excel*;
elige uno y recibe el archivo con **todos** los viajes que cumplen esos filtros, no sólo los de la
página que está viendo.

**Why this priority**: es el caso que el enunciado describe en detalle y el que fija el patrón que
las otras cuatro pantallas repiten. Entregado solo, ya resuelve la necesidad principal —sacar del
sistema la información de viajes para presentarla afuera— y deja construida toda la maquinaria que
los demás reportes reusan.

**Independent Test**: se prueba entero desde la pantalla de Viajes: aplicar un filtro que deje unos
pocos viajes, generar el reporte en cada formato y comprobar que el archivo trae exactamente esos
viajes, con los mismos datos que la pantalla muestra y con los filtros aplicados escritos en el
encabezado.

**Acceptance Scenarios**:

1. **Given** un usuario con el permiso de reportes en la pantalla de Viajes sin filtros aplicados y
   con viajes registrados, **When** presiona *Generar reporte* y elige *PDF*, **Then** recibe un
   documento PDF con todos los viajes registrados, en el mismo orden que la pantalla, y el encabezado
   dice que no hay filtros aplicados
2. **Given** el mismo usuario con el filtro de estado en *En curso* y un rango de fechas aplicado,
   **When** presiona *Generar reporte* y elige *Excel*, **Then** recibe una planilla con únicamente
   los viajes en curso de ese rango y el encabezado nombra los dos filtros con sus valores
3. **Given** un listado filtrado que devuelve más de una página de resultados, **When** genera el
   reporte estando parado en la primera página, **Then** el archivo contiene todos los viajes del
   filtro y no sólo los de esa página
4. **Given** un usuario con el permiso de reportes, **When** presiona *Generar reporte*, **Then** se
   le ofrecen exactamente dos opciones, *PDF* y *Excel*, y puede cerrar sin elegir ninguna y volver
   al listado tal como estaba
5. **Given** un usuario que llega a la pantalla de Viajes **sin** el permiso de reportes, **When**
   mira la pantalla, **Then** no ve el botón *Generar reporte* y el resto de la pantalla funciona
   igual que antes

---

### User Story 2 - Reporte de los tres paneles de vencimientos (Priority: P2)

El mismo botón, con las mismas dos opciones, en los tres paneles de vencimientos: el de
documentación de **choferes**, el de documentación de **flota** y el de **facturas**. Los tres son
pantallas de lectura sin filtros propios: el reporte se lleva lo que el panel muestra, completo.

**Why this priority**: son los reportes que sostienen el control que Gerencia ya hace hoy mirando la
pantalla —qué está por vencer y qué ya venció—, pero para llevarlo a una reunión o mandarlo por
correo. Dependen del patrón que fija la Historia 1, pero cada panel se entrega y se prueba por
separado.

**Independent Test**: se prueba panel por panel: abrir el panel, generar el reporte en cada formato y
comparar fila por fila contra lo que la pantalla muestra, incluido el orden por urgencia.

**Acceptance Scenarios**:

1. **Given** el panel de vencimientos de choferes con alertas, **When** genera el reporte en PDF,
   **Then** el documento trae las mismas filas que la pantalla —chofer, transportista, documento con
   su fecha de vencimiento y estado—, en el mismo orden por urgencia
2. **Given** el panel de vencimientos de flota con alertas, **When** genera el reporte en Excel,
   **Then** la planilla trae patente, transportista, documento, fecha de vencimiento y estado, una
   fila por alerta
3. **Given** el panel de vencimientos de facturas, **When** genera el reporte en cualquiera de los
   dos formatos, **Then** el archivo trae cliente, número, importe, vencimiento y situación, y el
   total de los importes
4. **Given** cualquiera de los tres paneles **sin ninguna alerta**, **When** el usuario mira la
   pantalla, **Then** el botón *Generar reporte* no está disponible, porque no hay nada que reportar

---

### User Story 3 - Reporte de movimientos de caja (Priority: P3)

En **Movimientos de caja**, el mismo botón sobre el listado ya filtrado por rango de fechas y por
caja. El archivo trae los movimientos del filtro con sus totales: cuánto entró, cuánto salió y el
neto.

**Why this priority**: es el reporte de dinero, el que más se revisa, pero es también el que menos
urgencia tiene para salir del sistema: el detalle de una caja ya se consulta en pantalla. Llega
último sin que eso lo haga menos completo.

**Independent Test**: se prueba desde *Movimientos de caja*: acotar por rango de fechas y por caja,
generar el reporte en cada formato y comprobar que trae los mismos movimientos que la pantalla y que
los tres totales cierran contra las filas.

**Acceptance Scenarios**:

1. **Given** la consulta de movimientos con un rango de fechas y una caja elegidos, **When** genera
   el reporte en PDF, **Then** el documento trae fecha, tipo, importe, concepto, responsable y
   referencia de cada movimiento del filtro, y al pie el total de ingresos, el de egresos y el neto
2. **Given** la misma consulta, **When** genera el reporte en Excel, **Then** la planilla trae las
   mismas columnas y los mismos tres totales, y los importes quedan como números con los que se puede
   seguir calculando
3. **Given** un rango de fechas que no deja ningún movimiento, **When** el usuario mira la pantalla,
   **Then** el botón *Generar reporte* no está disponible

---

### Edge Cases

- **El filtro no deja ninguna fila.** No se genera reporte: el botón no está disponible cuando el
  listado está vacío. Un archivo con cero filas no informa nada que la pantalla no diga mejor.
- **El filtro deja más filas de las que un reporte puede cargar.** Por encima del tope (FR-016) la
  generación se rechaza con un mensaje que dice cuántas filas quedaron seleccionadas, cuál es el tope
  y que hay que acotar los filtros. No se genera un archivo parcial: un reporte recortado en silencio
  es peor que ninguno.
- **Los datos cambian entre que se mira la pantalla y se genera el reporte.** El reporte se arma con
  lo que hay al momento de generarlo, no con lo que la pantalla tenía cargado. Por eso el archivo
  lleva el instante de generación: es la única manera de saber a qué momento corresponde.
- **La generación demora.** Mientras el archivo se prepara, el botón queda deshabilitado y la
  pantalla informa que se está generando, para que nadie presione dos veces y se lleve dos copias.
- **Alguien sin el permiso invoca la generación directamente**, sin pasar por la pantalla. El
  servidor responde que no tiene permiso y no genera nada. Ocultar el botón es una cortesía; la
  restricción es del servidor.
- **Alguien con el permiso de reportes pero sin el permiso de lectura de la pantalla** invoca la
  generación de ese reporte. Se rechaza: el reporte no es una puerta lateral a datos que la pantalla
  no le muestra.
- **Un viaje sin chofer ni vehículo asignados, o una factura sin ciertos datos opcionales.** La celda
  queda vacía en el archivo, como en la pantalla; no se escribe ningún texto de relleno.
- **El rango de fechas está invertido** —*desde* posterior a *hasta*—. La pantalla ya rechaza ese
  filtro hoy y no llega a listar nada, así que tampoco hay reporte que generar.

## Requirements *(mandatory)*

### Functional Requirements

#### El botón y la elección de formato

- **FR-001**: El sistema DEBE ofrecer una acción **Generar reporte** en las cinco pantallas
  alcanzadas: el listado de Viajes, el panel de vencimientos de choferes, el panel de vencimientos de
  flota, el panel de vencimientos de facturas y la consulta de movimientos de caja. Ninguna otra
  pantalla la ofrece en esta feature.
- **FR-002**: Al activar *Generar reporte*, el sistema DEBE ofrecer exactamente **dos** formatos, en
  este orden: **PDF** y **Excel**. Elegir uno genera el reporte; cerrar sin elegir devuelve la
  pantalla exactamente como estaba, sin perder los filtros aplicados ni la página en la que se estaba.
- **FR-003**: La acción DEBE quedar **no disponible** cuando el listado que se está mirando no tiene
  ninguna fila, y la pantalla DEBE decir por qué no se puede generar.
- **FR-004**: Mientras un reporte se está generando, la acción DEBE quedar deshabilitada y la
  pantalla DEBE anunciar que la generación está en curso. Al terminar, la pantalla DEBE anunciar el
  resultado —entregado o rechazado— sin que la pantalla cambie.
- **FR-005**: La acción *Generar reporte* DEBE ser **secundaria** en las cinco pantallas: no
  reemplaza ni compite con la acción primaria de cada una, y en las pantallas de sólo lectura no
  convierte a la pantalla en una pantalla con acción primaria.

#### Qué contiene cada reporte

- **FR-006**: Todo reporte DEBE contener, en los dos formatos, un **encabezado** con: el nombre del
  reporte, los filtros aplicados con sus valores en palabras (o la indicación explícita de que no hay
  filtros aplicados), la cantidad de filas incluidas, el instante de generación y el usuario que lo
  generó.
- **FR-007**: El reporte DEBE contener **todas las filas que cumplen los filtros aplicados**, no sólo
  las de la página visible, y DEBE respetar el mismo orden que la pantalla.
- **FR-008**: Las columnas de cada reporte DEBEN ser las mismas que la pantalla muestra, con los
  mismos nombres:
  - **Viajes**: número, fecha, cliente, origen, destino, chofer, vehículo, transportista, estado e
    importe. *Ruta* y *Asignación* se abren en sus dos columnas cada una, porque en un archivo que se
    va a ordenar y filtrar cada dato va en su columna.
  - **Vencimientos de choferes**: chofer, transportista, documento, fecha de vencimiento y estado.
  - **Vencimientos de flota**: patente, transportista, documento, fecha de vencimiento y estado.
  - **Vencimientos de facturas**: cliente, número, importe, vencimiento y situación.
  - **Movimientos de caja**: fecha, tipo, importe, concepto, responsable y referencia.
- **FR-009**: Los reportes que llevan importes DEBEN cerrar con sus totales: el de viajes y el de
  vencimientos de facturas, con el **total de los importes** de las filas incluidas; el de movimientos
  de caja, con **total de ingresos**, **total de egresos** y **neto**.
- **FR-010**: Los estados DEBEN aparecer con la **misma palabra** que la pantalla usa —nunca con un
  código interno y nunca sólo por color—, y los importes DEBEN llevar el formato de pesos argentinos
  en el PDF.
- **FR-011**: En el formato Excel, los importes y las fechas DEBEN quedar como **valores numéricos y
  de fecha**, no como texto, de modo que se puedan ordenar, filtrar y sumar en la planilla.
- **FR-012**: El archivo entregado DEBE tener un **nombre descriptivo** que identifique el reporte y
  la fecha de generación, para que varios reportes guardados en una misma carpeta se distingan.

#### Permisos

- **FR-013**: El sistema DEBE exigir un **permiso propio de emisión de reportes**, distinto de los
  permisos de gestión y de consulta de los cinco módulos alcanzados. Quien no lo tiene no ve la acción
  *Generar reporte* en ninguna de las cinco pantallas.
- **FR-014**: Ese permiso lo DEBEN otorgar **Gerencia** y **Administrador del sistema**, y ningún otro
  rol: *Tráfico* y *Administración de la empresa* siguen viendo las cinco pantallas exactamente como
  hoy, sin el botón. El administrador lo recibe como recibe todos los demás —es quien tiene que poder
  verificar y sostener lo que Gerencia usa—; "sólo del gerente" acota a los **roles operativos**, no
  al administrador.
- **FR-015**: La generación de un reporte DEBE exigir, además del permiso de reportes, el **permiso
  de lectura de la pantalla** de la que sale: quien no puede mirar el listado tampoco puede exportarlo.
  Invocar la generación sin alguno de los dos permisos DEBE rechazarse y no generar ningún archivo.

#### Volumen y errores

- **FR-016**: El sistema DEBE rechazar la generación cuando los filtros aplicados dejan **más de
  5.000 filas**, con un mensaje que diga cuántas quedaron seleccionadas, cuál es el tope y que hay que
  acotar los filtros. No se genera un archivo recortado.
- **FR-017**: Si la generación falla por cualquier otro motivo, la pantalla DEBE informarlo sin
  perder los filtros aplicados ni la página en la que se estaba, y DEBE permitir volver a intentarlo.

### Key Entities

Esta feature **no agrega ninguna entidad persistida**: no guarda los reportes generados, no lleva
historial de quién generó qué y no crea ninguna tabla. Cada reporte se arma al momento con datos que
ya existen y se entrega; lo único que queda del lado del sistema es el permiso nuevo, que es una fila
del catálogo de permisos que ya existe.

- **Reporte**: un pedido de exportación. Lo definen la pantalla de origen, los filtros aplicados en
  ella y el formato elegido. No sobrevive a su entrega.
- **Permiso de emisión de reportes**: una entrada más del catálogo de permisos del sistema, otorgada
  a los roles que corresponda y evaluada como cualquier otro permiso.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Desde cualquiera de las cinco pantallas, con los filtros ya aplicados, un usuario
  autorizado obtiene el archivo en **dos clics**: *Generar reporte* y el formato.
- **SC-002**: Las filas del archivo coinciden **una a una** con las que el listado devuelve para esos
  mismos filtros: misma cantidad, mismos datos y mismo orden, verificable comparando la pantalla
  contra el archivo abierto.
- **SC-003**: Un reporte de 1.000 filas queda disponible en **menos de 15 segundos** en cualquiera de
  los dos formatos.
- **SC-004**: En los **tres** reportes que llevan importes —viajes, vencimientos de facturas y
  movimientos de caja (FR-009)—, el total que el archivo muestra al pie coincide con la suma de las
  filas del propio archivo, comprobable con la calculadora de la planilla. Los dos reportes de
  vencimientos de documentación no llevan importes y por lo tanto no llevan totales.
- **SC-005**: Un usuario de *Gerencia* y uno de *Administrador del sistema* encuentran la acción
  *Generar reporte* en las cinco pantallas; un usuario de *Tráfico* y uno de *Administración de la empresa* recorren las cinco
  pantallas y **no encuentran** la acción *Generar reporte* en ninguna, y todo lo demás de esas
  pantallas funciona igual que antes de esta feature.
- **SC-006**: El archivo generado permite reconstruir su contexto sin preguntarle a nadie: leyéndolo
  se sabe qué reporte es, con qué filtros salió, cuántas filas trae, cuándo se generó y quién lo
  generó.
- **SC-007**: Los reportes en Excel se abren en una planilla y sus columnas de importe y de fecha se
  ordenan y se suman sin conversión previa.
- **SC-008**: Un filtro que deja más de 5.000 filas devuelve un mensaje que dice cuántas quedaron y
  cuál es el tope, y no entrega ningún archivo.

## Assumptions

Decisiones tomadas al escribir la spec donde el enunciado no se pronunciaba. Cada una se puede
revertir, pero está elegida.

- **No hay pantalla nueva ni entrada de menú nueva.** El enunciado dice "con lo ya estructurado" y
  describe el botón dentro de cada sección. *Emitir reportes* es una capacidad que se agrega a cinco
  pantallas existentes, no un módulo con dirección propia.
- **Son exactamente cinco reportes**, los cinco que el enunciado nombra. Los demás listados del
  sistema —liquidaciones, adelantos, cajas, facturas, choferes, flota, clientes, las dos pantallas de
  totales— **no** reciben el botón en esta feature. Sumarlos después es agregar una pantalla a la
  lista, no rehacer nada.
- **Los tres paneles de vencimientos no tienen filtros hoy y esta feature no se los agrega.** En esos
  tres el reporte se lleva el panel completo, y su encabezado dice que no hay filtros aplicados. La
  frase del enunciado "una vez aplicados los filtros" sólo tiene efecto real en Viajes y en
  Movimientos de caja, que son las dos pantallas que sí filtran.
- **El reporte abarca todas las filas del filtro, no la página visible.** Un archivo con los veinte
  renglones que se ven en pantalla no sirve para lo que un reporte se usa. Es también lo que obliga a
  poner un tope (FR-016).
- **El tope es 5.000 filas.** Es un número elegido, no medido: alcanza con holgura para el volumen de
  la empresa y evita que un filtro vacío arme un documento de miles de páginas. Se puede mover sin
  cambiar nada más de la spec.
- **El PDF se abre a la vista y el Excel se descarga.** El PDF es para mirar e imprimir —que es lo que
  el enunciado pide— y el sistema ya sirve sus PDF en línea; una planilla, en cambio, ningún navegador
  la muestra, así que se entrega como archivo.
- **El reporte lleva el instante de generación y por eso no es byte a byte reproducible.** Es una
  diferencia deliberada con el documento de la factura del Módulo 6, que sí lo es: una factura es un
  comprobante que no cambia, y un reporte es una foto de un listado que cambia. Saber a qué momento
  corresponde la foto es parte del dato, así que acá el reloj **sí** entra en el archivo, y la prueba
  de igualdad que protege a la factura no aplica a los reportes.
- **No se guarda nada.** Ni el archivo generado ni el registro de quién lo generó. El enunciado no lo
  pide y guardarlo abriría preguntas —dónde vive, cuánto se conserva, quién lo puede volver a
  descargar— que ninguna necesidad actual justifica.
- **Los dos formatos se arman en el servidor**, que es donde están todas las filas del filtro y donde
  ya se genera el documento de la factura. La pantalla pide y recibe; no arma el archivo.
- **Para el PDF se reusa la herramienta con la que ya se genera el documento de la factura**; para el
  Excel se incorpora una biblioteca conocida y de uso extendido, que es lo que el enunciado pide. Cuál
  exactamente es una decisión de la fase de planificación, no de la spec.
- **Los cinco listados ya devuelven sus datos hoy.** Esta feature no agrega consultas de negocio
  nuevas ni cambia lo que los cinco módulos muestran: agrega una manera de sacar afuera lo que ya se
  ve.
