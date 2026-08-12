# Feature Specification: Gestión de facturación (Módulo 6)

**Feature Branch**: `006-gestion-facturacion`

**Created**: 2026-08-12

**Status**: Draft

**Input**: User description: "Gestionar facturación (Módulo 6) v0. Una vez que los viajes se rindieron, G&T Logística S.A. tiene que cobrarlos: hoy la factura se arma a mano en una planilla, se transcriben los viajes uno por uno y recién ahí se emite el comprobante, lo que produce facturas cuyos importes no cierran con los viajes, viajes facturados dos veces y viajes que nunca se facturan. Este módulo permite emitir una factura a un cliente agrupando uno o varios viajes rendidos de un período: el sistema propone los viajes pendientes de facturar de ese cliente, calcula el neto, el IVA y el total a partir de los importes de los viajes, genera y guarda el documento de la factura con su CAE, y marca los viajes incluidos como `facturado` para que no vuelvan a facturarse. El valor central es la trazabilidad viaje ↔ factura. Incluye la configuración de la empresa emisora con su logo, la selección de viajes por cliente y período, el cálculo automático de neto/IVA/total, la vista previa antes de confirmar, el número de comprobante único, el CAE y su vencimiento, la generación del documento de la factura en PDF, los estados pendiente / vencida / pagada / anulada con registro del cobro, la anulación con motivo que devuelve los viajes a `rendido`, la refacturación que referencia a la factura anulada, el listado con filtros, el panel de vencimientos y los totales facturado/cobrado/pendiente por cliente en un período. La emisión fiscal propiamente dicha (obtención del CAE ante AFIP/ARCA) se hace por fuera del sistema. Fuera de alcance: emisión electrónica ante AFIP/ARCA y obtención del CAE por web service, notas de crédito y débito, facturas de varios períodos o varios clientes, percepciones/retenciones/IIBB/descuentos/recargos, facturación de conceptos que no sean viajes, cuenta corriente del cliente, liquidación al transportista, envío automático por email y portal de autoconsulta, registro contable y libro IVA ventas, moneda extranjera y facturación recurrente, y el ABM de clientes y de viajes, que se consumen del Módulo 5."

## Clarifications

### Session 2026-08-12

- Q: El enunciado sólo fija la alícuota de IVA de `Factura A` en 21%, verificable con su propio
  ejemplo (neto $82.644,63 → IVA $17.355,37). ¿Qué alícuota corresponde a `Factura B` y a
  `Factura C`? → A: `Factura A` 21%, `Factura B` 21% y `Factura C` 0%. Es la práctica estándar
  argentina: G&T Logística S.A. como Responsable Inscripto emite A y B —las dos con el mismo 21%
  adentro—, mientras que la C la emite un Monotributista o un Exento y no lleva IVA. La consecuencia
  visible es que una Factura C tiene total igual al neto (FR-023).
- Q: La regla RN9 del enunciado rechaza anular una factura `pagada` diciendo que "primero hay que
  revertir el cobro", pero RF31 no admite ninguna transición que salga de `pagada`. ¿Cuál de las dos
  cede? → A: `pagada` es **terminal** y no se agrega ningún camino de retroceso. El rechazo informa
  que la factura está cobrada —con la fecha del cobro— y no promete una reversión que no existe. Es
  la misma lección que dejó CL11 del Módulo 5: no habilitar un camino de escape que ningún criterio
  de aceptación describe. Revertir un cobro queda anotado como candidato para una spec futura
  (FR-043).
- Q: El enunciado deja fuera de alcance "la generación del PDF de la factura" y pide adjuntar el
  archivo del comprobante emitido, guardando "únicamente su URL". ¿El sistema produce el documento de
  la factura o lo sube el usuario? → A: **Lo genera el sistema**, con una biblioteca, en el servidor,
  al confirmar la emisión. El atributo de archivo del modelo del enunciado es el **lugar donde se
  guarda esa factura generada**, con el mismo mecanismo de almacenamiento de los Módulos 3 y 4, y no
  un adjunto que alguien sube: la carga manual del comprobante **se elimina** del alcance. Lo que
  sigue fuera de alcance es la emisión fiscal —el CAE se obtiene en AFIP/ARCA por fuera y se carga a
  mano—, así que el documento generado es la representación impresa de la factura y no el comprobante
  fiscal (FR-031, FR-031c).
- Q: Con el formato de comprobante de referencia a la vista, ¿cómo se lista el detalle: una fila por
  viaje o una única fila consolidada como en el ejemplo? → A: **Una fila por viaje**, con el número
  del viaje en `Código` y su origen, destino y remito en `Producto / Servicio`. Una fila consolidada
  dejaría al cliente sin saber qué se le cobra, y es justamente lo que el módulo existe para
  resolver: toda factura se explica por los viajes que la componen (FR-031e).
- Q: El formato pide condición de IVA y condición de venta del cliente, y el padrón del Módulo 5 no
  tiene ninguno de los dos. ¿De dónde salen? → A: Ninguno se agrega al padrón. La **condición de IVA
  sale impresa como texto fijo `Responsable Inscripto`**, porque todos los clientes de la empresa son
  empresas. La **condición de venta se elige al emitir**, en un desplegable con las formas de pago
  que maneja la empresa —`Contado`, `Cuenta Corriente`, `Tarjeta de Débito / Crédito`, `Cheque`—, y
  queda congelada en la factura: el mismo cliente puede pagar de una forma esta factura y de otra la
  siguiente. Con esto el Módulo 5 no se toca más allá de lo que ya estaba previsto (FR-009a, FR-031h).
- Q: US4 permite corregir el CAE de una factura ya emitida, y el documento guardado lleva el CAE
  adentro. ¿Qué pasa con el archivo? → A: **Se regenera y reemplaza al anterior**. El archivo
  guardado y la ficha dicen siempre lo mismo, y el documento viejo no se conserva. Si el CAE estaba
  mal, el documento que se le mandó al cliente también estaba mal: dejarlo congelado obligaría a
  rehacerlo a mano justo en el caso que la corrección existe para resolver (FR-031b).
- Q: CL7 del enunciado pide que corregir el CAE de una factura emitida deje "registro de quién y
  cuándo lo modificó". ¿Ese registro guarda además qué campos cambiaron y sus valores anterior y
  nuevo? → A: Sólo **quién y cuándo**, como una entrada más del mismo historial que ya lleva los
  cambios de estado, marcada como corrección. Es literalmente lo que CL7 pide y no agrega ninguna
  entidad de auditoría que ningún otro módulo del sistema tiene. Guardar los valores anteriores queda
  anotado como candidato para una spec futura (FR-037).
- Q: La práctica argentina imprime el IVA discriminado en una `Factura A` y embebido en el total en
  una `Factura B`. ¿El documento generado tiene entonces dos pies distintos según el tipo? → A:
  **Un único pie para los tres tipos**: neto, IVA y total siempre visibles, y la columna `% IVA`
  siempre presente en la tabla de detalle. Un pie condicional serían dos documentos que mantener y
  probar por separado, y el documento de este módulo no es el comprobante fiscal (FR-031c) sino la
  representación impresa de lo que la ficha ya muestra: si la ficha discrimina el IVA de una B, el
  documento también (FR-031, FR-031j).
- Q: Si después de emitir una factura le corrigen la razón social o el domicilio al cliente en el
  padrón del Módulo 5, ¿la factura emitida muestra el dato corregido o el que tenía al emitirse? →
  A: **El que tenía al emitirse**: los datos del cliente se congelan en la factura igual que los del
  emisor (FR-034). Es el mismo criterio para las dos partes del comprobante y evita que la ficha y el
  documento digan cosas distintas: el PDF se generó una sola vez y sólo se regenera al corregir el
  detalle, el CAE o los vencimientos (FR-031b). Un comprobante dice a quién se le facturó ese día, no
  quién es hoy (FR-034a, SC-007).
- Q: FR-038 cierra la edición sólo de las facturas `anulada`, pero *Assumptions* decía que corregir
  una factura `pagada` quedaba fuera de esta versión. ¿Se puede corregir el CAE de una factura ya
  cobrada? → A: **Sí**. La corrección se permite en `pendiente`, `vencida` y `pagada`, y sólo
  `anulada` queda cerrada. Un dígito mal tipeado del CAE es un error fiscal que no depende de si el
  cliente ya pagó, y bloquearlo dejaría ese error sin ninguna salida: `pagada` es terminal y la
  factura tampoco se puede anular (FR-043a). La frase de *Assumptions* que decía lo contrario se
  elimina (FR-035, FR-038).
- Q: *Relationships* dice que una factura anulada puede ser reemplazada por a lo sumo una
  Refacturación, pero FR-049 pide ofrecer todas las anuladas del cliente. ¿El desplegable esconde las
  ya reemplazadas? → A: **Sí**: ofrece únicamente las anuladas **sin reemplazo**, y una restricción
  de unicidad en la base garantiza que ninguna quede referenciada por dos Refacturaciones. Es el
  mismo patrón con el que el módulo ya resuelve el viaje que no puede estar en dos facturas vigentes
  (FR-053): la consulta previa da el mensaje bueno y el índice cierra la carrera entre dos operadores
  simultáneos (FR-049, FR-049a).
- Q: FR-034 congela nueve datos del emisor y el CBU no está entre ellos, pero el CBU sale impreso en
  el documento. ¿Se congela también? → A: **Sí, se congela con el resto**. Es el mismo criterio de
  FR-034 y el mismo problema que resuelve: el documento ya se generó con el CBU viejo impreso, y si
  la ficha mostrara el nuevo las dos discreparían. Una factura vieja tiene que seguir diciendo a qué
  cuenta se pedía pagarla. El logo sigue siendo la única excepción, porque no se congela ninguna
  copia del archivo (FR-034).
- Q: El bloque del cliente del comprobante lleva el domicilio, pero en el padrón del Módulo 5 el
  domicilio es un campo **opcional**. ¿Qué pasa al facturarle a un cliente que no lo tiene? → A: **La
  emisión se rechaza** nombrando el dato que falta y dónde cargarlo, con el mismo patrón que ya rige
  para la empresa emisora sin configurar (FR-006). El padrón **no se toca**: el domicilio sigue
  siendo opcional en el Módulo 5 —un cliente que no se factura no lo necesita— y se vuelve obligatorio
  recién al facturar. Hacerlo obligatorio en el padrón sería un sexto cambio al Módulo 5, que FR-056
  acota a cinco, y con migración de los clientes que hoy no lo tienen (FR-011a).
- Q: FR-031e imprime el número de remito en cada fila del detalle, pero en el Módulo 5 el remito es
  **opcional** y un viaje `rendido` es **inmutable** (FR-018 del Módulo 5), así que un rendido sin
  remito no podría facturarse nunca. ¿El remito deja de ser obligatorio para facturar o se abre una
  excepción? → A: **El remito pasa a ser obligatorio para rendir un viaje** en el Módulo 5: se exige
  en el paso a `rendido`, que es el último momento en que el viaje todavía se puede editar. Así todo
  viaje que llega a facturarse lo trae, y la inmutabilidad del viaje rendido no se toca. Es un
  **sexto cambio** al Módulo 5 y FR-056 se amplía en consecuencia. **Limitación conocida y aceptada**:
  los viajes que ya estaban `rendido` sin remito antes de esta regla no se pueden facturar, porque no
  admiten edición; se documenta como tal y no se agrega un camino de corrección (FR-019a, FR-055a).
- Q: `pendiente` está guardado y `vencida` se deriva al leer (FR-041), así que una misma factura es
  las dos cosas según desde dónde se la mire. Al filtrar el listado por `pendiente`, ¿aparecen las
  vencidas? → A: **No**. El filtro trabaja sobre el **estado derivado**, el que el usuario ve en la
  fila, y sus cuatro valores son **excluyentes**: `pendiente` devuelve sólo las impagas todavía en
  plazo y `vencida` sólo las pasadas de fecha. El predicado va escrito en la consulta y no como
  filtrado posterior, y la derivación en SQL DEBE coincidir con la de C#, con un test que compare las
  dos sobre el mismo dato ([003] de `AGENTS.md`). Filtrar por la columna devolvería facturas que el
  propio listado muestra como `vencida` en la fila de al lado (FR-058a).
- Q: FR-031d exige que el documento de una factura anulada lo diga con su motivo, pero FR-031b sólo
  manda regenerar al corregir el detalle, el CAE o los vencimientos: la anulación no toca el archivo.
  ¿Cómo llega la leyenda al PDF? → A: **Anular regenera el documento**, que sale con la leyenda de
  anulada y el motivo impresos. Es una línea más en la regla de regeneración que ya existe, mantiene
  la promesa de que el archivo y la ficha nunca discrepan (SC-007a) y no agrega ninguna pieza nueva.
  Estampar la leyenda en cada descarga sería un segundo camino de armado del documento que habría que
  mantener igual al primero (FR-031b, FR-031d).
- Q: FR-033 pide que la vista previa tenga la **misma disposición** que el documento final pero sin
  generar ningún archivo. ¿Cómo se evita que las dos maquetas se separen con el tiempo? → A: **Un
  único armador de documento en el servidor**, invocado por las dos. La vista previa lo llama y
  devuelve el documento para mirarlo, sin guardarlo ni registrar nada; la emisión llama al mismo y
  guarda el resultado. FR-033 pide no **persistir**, no dejar de **producir**. Dos maquetas paralelas
  —una dibujada en la pantalla y otra armada en el servidor— se separan sin que nadie lo note, y
  entonces revisar la vista previa deja de servir para algo (FR-033).

### Session 2026-08-12 — recorrido del checklist `documento.md`

- Q: ¿Qué ofrecen los desplegables del período? → A: el **mes**, los doce valores `01` a `12`; el
  **año**, exactamente `2025` y `2026`. Son los años con operación cargada. El servidor rechaza un
  período fuera de esas opciones, y no hay restricción en la base de datos porque la lista se amplía
  con los años y obligaría a una migración cada vez (FR-010).
- Q: FR-031 enumera nueve bloques del documento y ninguno imprime el período, pero FR-033 lo da por
  presente en la vista previa. ¿Dónde sale? → A: en el **bloque de identificación**, en formato
  `MM/AAAA`, junto a la fecha de emisión. Es donde lo lleva el comprobante argentino y no agrega un
  bloque nuevo (FR-031).
- Q: FR-031b manda regenerar el documento cuando cambia el detalle, o sea que sale impreso, pero
  ningún bloque lo ubica. ¿Dónde va? → A: en el **pie de importes**, a su izquierda, bajo el rótulo
  `Observaciones`, omitido entero —rótulo incluido— cuando el detalle está vacío, con el mismo
  criterio que la banda de CBU (FR-031).
- Q: El logo no se congela y el documento se regenera. ¿Qué logo lleva el documento regenerado de una
  factura vieja? → A: el **vigente**, y se declara como consecuencia conocida. El logo no es un dato
  de la ficha, así que la ficha y el documento no llegan a discrepar; guardar una copia del archivo
  por cada factura agregaría complejidad sin un caso de uso que la pida (FR-034).
- Q: FR-031b resuelve la falla de generación al anular y no dice nada de la emisión. ¿Qué pasa si el
  documento no se puede generar al emitir? → A: **no se crea nada**: no hay factura, los viajes siguen
  `rendido` y el número de comprobante queda libre. Es el criterio de todo o nada de FR-054 y es lo que
  sostiene SC-007a sin excepciones. La misma regla se extendió a la **corrección**, que tampoco la
  tenía (FR-031, FR-031b).
- Q: ¿Registrar el cobro regenera el documento? → A: **no**, y la fecha de cobro no sale impresa. Es
  información interna de cobranzas: el comprobante que se le mandó al cliente no cambia porque después
  haya pagado. Las operaciones que regeneran son exactamente tres —emitir, corregir y anular—, y
  imprimir el cobro convertiría el documento en un recibo, que está fuera de alcance (FR-031b).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Configurar la empresa emisora (Priority: P1)

Administración carga una sola vez los datos con los que sale toda factura de la empresa —razón
social, CUIT, domicilio, condición de IVA, ingresos brutos, inicio de actividades, punto de venta,
teléfono, email— y sube el logo. A partir de ahí toda factura nueva los trae puestos, sin
retipearlos.

**Why this priority**: Sin la empresa emisora configurada no se puede emitir la primera factura
(FR-006): es la precondición del módulo entero.

**Independent Test**: Se puede verificar de forma completa e independiente entrando con la
configuración vacía, comprobando que el alta de factura informa qué datos faltan y no deja
continuar; cargando después los datos y el logo, y comprobando que la vista previa de una factura
nueva los muestra sin haberlos escrito ahí.

**Acceptance Scenarios**:

1. **Given** la empresa emisora sin configurar, **When** Administración abre la pantalla de
   configuración, **Then** ve el formulario vacío con un mensaje explícito de que todavía no está
   configurada, en vez de una pantalla en blanco.
2. **Given** el formulario de la empresa emisora, **When** Administración completa razón social,
   CUIT, domicilio y condición de IVA y guarda, **Then** los datos quedan guardados y el sistema lo
   confirma sin cambiar de pantalla.
3. **Given** el formulario con el CUIT mal formado —menos de once dígitos o dígito verificador
   incorrecto—, **When** se intenta guardar, **Then** el sistema marca ese campo con el motivo
   puntual y no guarda nada.
4. **Given** la empresa emisora sin razón social, sin CUIT, sin domicilio o sin condición de IVA,
   **When** Administración abre el alta de factura, **Then** el sistema le informa exactamente qué
   datos faltan, con el nombre de cada uno, y no le permite continuar.
5. **Given** la empresa emisora ya configurada, **When** Administración abre el alta de factura,
   **Then** la vista previa muestra la razón social, el CUIT, el domicilio y la condición de IVA sin
   que se los haya ingresado ahí.
6. **Given** la empresa emisora configurada sin logo, **When** Administración sube un archivo de
   imagen, **Then** el logo queda guardado y aparece en la vista previa de las facturas.
7. **Given** un logo ya cargado, **When** Administración sube otro, **Then** el nuevo reemplaza al
   anterior; **When** en cambio pide quitarlo, **Then** el logo se elimina y la configuración queda
   sin logo.
8. **Given** la empresa emisora configurada sin logo, **When** se emite una factura, **Then** la
   emisión procede normalmente y la vista previa muestra los datos de texto: el logo es opcional.
9. **Given** un archivo que no es una imagen admitida, **When** se intenta subirlo como logo,
   **Then** el sistema lo rechaza indicando qué formatos acepta y la configuración queda sin cambios.
10. **Given** una factura ya emitida, **When** después se corrige el domicilio de la empresa emisora,
    **Then** la factura emitida sigue mostrando el domicilio que tenía al emitirse, y sólo las
    facturas nuevas usan el corregido.

---

### User Story 2 - Emitir una factura agrupando viajes rendidos (Priority: P1)

Administración elige el cliente, el mes y el año, y el sistema le ofrece exactamente los viajes de
ese cliente que están rendidos y todavía sin facturar en ese período. Marca los que van, ve la
cantidad y el importe acumulado actualizarse solos, elige el tipo de comprobante, revisa el neto, el
IVA y el total calculados, carga el número de comprobante, el CAE y su vencimiento, mira la vista
previa y confirma. Los viajes incluidos quedan `facturado` y no se ofrecen nunca más.

**Why this priority**: Es el objetivo central del módulo y lo que elimina los tres problemas del
armado a mano: importes que no cierran, viajes facturados dos veces y viajes que nunca se facturan.

**Independent Test**: Se puede verificar de forma independiente con la empresa emisora configurada
(User Story 1), un cliente con tres viajes rendidos del mismo mes y uno `en curso`, comprobando que
se ofrecen sólo los tres rendidos, que el neto es la suma exacta de sus importes, que después de
confirmar los tres figuran `facturado` y que al volver a armar una factura del mismo cliente y
período ya no aparecen.

**Acceptance Scenarios**:

1. **Given** la empresa emisora configurada y clientes activos en el padrón, **When** Administración
   abre el alta de factura, **Then** elige el cliente de una lista desplegable que muestra razón
   social y CUIT, el mes y el año en dos listas desplegables separadas —el mes con los doce valores
   `01` a `12`, el año con exactamente `2025` y `2026`—, y el tipo de comprobante y el tipo de
   facturación en listas desplegables con sus opciones fijas.
2. **Given** el alta de factura, **When** Administración despliega la lista de clientes, **Then** no
   aparece ningún cliente dado de baja en el padrón del Módulo 5.
3. **Given** un cliente con viajes `rendido` sin facturar en septiembre de 2026, **When**
   Administración elige ese cliente y ese período, **Then** la lista muestra únicamente esos viajes,
   con número, fecha, remito, origen, destino e importe de cada uno.
4. **Given** un cliente con un viaje `pendiente`, uno `en curso` y uno `anulado` en el período,
   **When** se arma la factura, **Then** ninguno de los tres se ofrece: sólo se facturan viajes
   `rendido`.
5. **Given** un viaje ya incluido en una factura vigente, **When** se vuelve a armar una factura del
   mismo cliente y período, **Then** ese viaje no aparece en la lista.
6. **Given** viajes de otro cliente en el mismo período, **When** se elige un cliente, **Then**
   ninguno de esos viajes se ofrece: la factura pertenece a un único cliente.
7. **Given** la lista de viajes ofrecidos, **When** Administración marca y desmarca viajes, **Then**
   la cantidad seleccionada y el importe acumulado de la selección se actualizan en pantalla en cada
   cambio.
8. **Given** tres viajes seleccionados de $30.000,00, $30.000,00 y $22.644,63 con `Factura A`,
   **When** Administración mira los totales, **Then** el neto es $82.644,63, el IVA $17.355,37 y el
   total $100.000,00, expresados en pesos argentinos con separador de miles y coma decimal.
9. **Given** los importes calculados, **When** Administración intenta escribir en el neto, en el IVA
   o en el total, **Then** no puede: los tres son de sólo lectura.
10. **Given** esa misma selección con `Factura A`, **When** Administración cambia el tipo de
    comprobante, **Then** el IVA y el total se recalculan solos con la alícuota del tipo nuevo: con
    `Factura B` quedan iguales, porque las dos llevan el 21%, y con `Factura C` el IVA pasa a $0,00 y
    el total a $82.644,63, igual al neto.
11. **Given** un cliente y un período sin ningún viaje facturable, **When** se los elige, **Then** el
    sistema informa explícitamente que no hay viajes facturables para esa combinación, en vez de
    mostrar una lista vacía sin explicación, y no permite confirmar.
12. **Given** el formulario sin cliente, sin tipo de comprobante, sin tipo de facturación, sin
    período, sin fecha de facturación o sin ningún viaje seleccionado, **When** se intenta confirmar,
    **Then** el sistema marca el campo faltante con el motivo puntual y no crea nada.
13. **Given** el formulario sin CAE o sin vencimiento del CAE, **When** se intenta confirmar,
    **Then** el sistema marca el campo faltante y no crea la factura.
14. **Given** el formulario recién abierto, **When** Administración mira la fecha de facturación,
    **Then** trae propuesta la fecha de hoy y puede cambiarla por otra.
15. **Given** el número de comprobante 0014-00000003 ya usado por una factura vigente, **When** se
    intenta confirmar otra con ese número, **Then** el sistema informa el duplicado identificando la
    factura que lo usa, y no guarda nada.
16. **Given** todos los datos completos, **When** Administración pide la vista previa, **Then** ve la
    factura como se lee en el comprobante: datos de la empresa con su logo, datos del cliente,
    período, detalle de los viajes incluidos y los tres importes; **When** después confirma y abre el
    documento guardado, **Then** encuentra exactamente lo mismo, bloque por bloque.
17. **Given** la vista previa revisada, **When** Administración confirma, **Then** la factura queda
    registrada en estado `pendiente`, la pantalla pasa a la ficha de la factura recién creada, y el
    formulario de alta no queda abierto detrás con el botón de guardar habilitado.
18. **Given** la factura confirmada, **When** Administración abre el listado de viajes del Módulo 5,
    **Then** los viajes incluidos figuran en estado `facturado` con el número y la fecha de la
    factura que los incluye.
19. **Given** un viaje seleccionado con importe en cero, **When** Administración confirma, **Then**
    el sistema no emite la factura al primer intento: advierte que ese viaje no aporta al neto,
    nombrándolo, y la emite recién después de una confirmación explícita.
20. **Given** una fecha de facturación anterior a la fecha de algún viaje incluido, **When** se
    confirma, **Then** el sistema no emite al primer intento: advierte que indica un error de carga
    de fechas y emite recién después de una confirmación explícita.
21. **Given** un vencimiento de pago anterior a la fecha de facturación, **When** se intenta
    confirmar, **Then** el sistema lo rechaza marcando ese campo, y lo acepta cuando el vencimiento
    es igual o posterior.
22. **Given** un vencimiento del CAE anterior a la fecha de facturación, **When** se intenta
    confirmar, **Then** el sistema lo rechaza marcando ese campo como dato mal cargado.
23. **Given** dos administrativos armando al mismo tiempo una factura que incluye el viaje 1041,
    **When** ambos confirman, **Then** el primero emite su factura y el segundo recibe un rechazo que
    indica qué viaje ya fue facturado y en qué comprobante; no se crea una segunda factura ni queda
    ningún viaje a medio facturar.
24. **Given** la factura recién confirmada, **When** Administración abre su ficha, **Then** encuentra
    ahí el documento de la factura ya generado por el sistema —con el logo, los datos de la empresa,
    los del cliente, el detalle de los viajes, los importes y el CAE— sin haber subido ningún
    archivo.
25. **Given** una factura de tres viajes, **When** Administración abre el documento, **Then** la
    tabla de detalle tiene **tres filas**, una por viaje, cada una con el número del viaje en
    `Código` y su origen, destino y remito en `Producto / Servicio`; no hay una única fila
    consolidada.
26. **Given** el alta de factura, **When** Administración despliega la condición de venta, **Then**
    ve exactamente `Contado`, `Cuenta Corriente`, `Tarjeta de Débito / Crédito` y `Cheque`, y sin
    elegir una no puede confirmar.
27. **Given** una factura emitida, **When** se mira el bloque del cliente en el documento, **Then**
    la condición de IVA dice `Responsable Inscripto` y la condición de venta muestra la que se eligió
    al emitir.
28. **Given** la empresa emisora sin CBU configurado, **When** se genera el documento, **Then** la
    banda del CBU no aparece y el resto del comprobante se arma normalmente.
29. **Given** un viaje que ya estaba `rendido` sin número de remito, **When** Administración lo
    selecciona e intenta confirmar la factura, **Then** el sistema la rechaza nombrando ese viaje
    como sin remito y no crea nada; el viaje aparece igual en la lista de facturables, señalado con la
    palabra que lo explica y no sólo con un color.
30. **Given** un viaje `en curso` sin número de remito, **When** se lo intenta pasar a `rendido` en el
    Módulo 5, **Then** el sistema marca ese campo y no completa la transición; **When** se carga el
    remito y se reintenta, **Then** el viaje pasa a `rendido` y queda facturable.
31. **Given** un cliente sin domicilio cargado en el padrón, **When** Administración intenta emitirle
    una factura, **Then** el sistema la rechaza diciendo que a ese cliente le falta el domicilio y
    dónde cargarlo, y no crea nada; **When** se lo cargan en el Módulo 5 y se reintenta, **Then** la
    emisión procede normalmente.
32. **Given** una `Factura B` emitida, **When** Administración abre su documento, **Then** el pie
    muestra el neto, el IVA y el total en renglones separados y la tabla de detalle muestra su
    columna `% IVA`, exactamente igual que en una `Factura A`: la disposición no cambia con el tipo.
33. **Given** una vista previa pedida y después abandonada sin confirmar, **When** se busca esa
    factura en el listado, **Then** no existe: la vista previa no crea nada ni guarda ningún archivo.

---

### User Story 3 - Consultar, buscar y filtrar facturas (Priority: P1)

Administración responde una consulta de cobranzas sin levantarse: filtra las facturas por cliente,
rango de fechas, período, estado y tipo de comprobante, y abre la ficha completa de cualquiera, con
los datos de la empresa y del cliente, el detalle de los viajes incluidos, los importes, el CAE, el
documento de la factura y el historial de estados.

**Why this priority**: Es la operación que más se repite. Una factura que no se puede encontrar no
resuelve el problema de saber qué está cobrado y qué no.

**Independent Test**: Se puede verificar de forma independiente emitiendo facturas de dos clientes,
de dos períodos, de dos tipos de comprobante y en distintos estados, aplicando combinaciones de
filtros y comprobando que el listado muestra exactamente lo esperado y la ficha lo detalla.

**Acceptance Scenarios**:

1. **Given** facturas emitidas, **When** Administración abre el listado, **Then** ve de cada una el
   número, la fecha, el cliente, el tipo de comprobante, el período, el importe total, el estado y el
   vencimiento de pago.
2. **Given** el listado de facturas, **When** se aplican filtros combinados por cliente, rango de
   fechas, período, estado y tipo de comprobante, **Then** el listado muestra únicamente las facturas
   que cumplen todas las condiciones a la vez.
3. **Given** más de 20 facturas que cumplen los filtros aplicados, **When** Administración consulta
   el listado, **Then** ve la primera página con 20 filas, el total de coincidencias y la forma de
   avanzar a las páginas siguientes.
4. **Given** un filtro que no coincide con ninguna factura, **When** se aplica, **Then** el sistema
   muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.
5. **Given** una factura del listado, **When** Administración la selecciona, **Then** ve su ficha
   completa: datos de la empresa emisora tal como quedaron al emitirla, datos del cliente, tipo de
   comprobante, tipo de facturación, período, fecha, detalle, la lista de los viajes incluidos con su
   importe, el neto, el IVA, el total, el CAE con su vencimiento, el vencimiento de pago, el acceso
   al documento generado y el historial de estados.
6. **Given** la ficha de una factura, **When** Administración abre el documento de la factura,
   **Then** se ve, sin tener que bajarlo y abrirlo a mano, y lleva los mismos datos que muestra la
   ficha.
7. **Given** la ficha de una factura, **When** Administración mira el historial, **Then** ve cada
   cambio de estado con el estado anterior, el nuevo, el usuario que lo produjo y la fecha y hora,
   empezando por la emisión.
8. **Given** el listado sin filtro de estado aplicado, **When** Administración lo mira, **Then** el
   control de filtro dice explícitamente qué estados está mostrando, de modo que ninguna fila quede
   oculta en silencio.
9. **Given** una factura de un cliente que después se dio de baja en el padrón, **When** se la
   consulta, **Then** conserva y muestra sus datos de cliente, señalado como inactivo con la palabra
   que lo explica.
10. **Given** una factura y sus viajes, **When** Administración abre la ficha de cualquiera de esos
    viajes en el Módulo 5, **Then** ve la factura que lo incluye con su número y su fecha.
11. **Given** una factura impaga cuyo vencimiento de pago ya pasó, **When** Administración filtra el
    listado por estado `pendiente`, **Then** esa factura no aparece; **When** filtra por `vencida`,
    **Then** aparece: los filtros de estado muestran lo mismo que dice la columna de estado de la
    fila, y ninguna factura sale bajo los dos.
12. **Given** una factura ya emitida, **When** después le corrigen al cliente la razón social o el
    domicilio en el padrón del Módulo 5, **Then** la ficha, el listado y el documento de esa factura
    siguen mostrando los datos que el cliente tenía al emitirse, y sólo las facturas nuevas usan los
    corregidos; el filtro por ese cliente sigue encontrándola.

---

### User Story 4 - Corregir los datos de una factura emitida (Priority: P2)

A Administración se le pasó un dígito del CAE. Abre la factura, lo corrige, y de paso ajusta el
vencimiento de pago. El sistema regenera el documento de la factura para que diga lo mismo que la
ficha. El cliente, los viajes y los importes no se tocan: eso, si está mal, se anula y se refactura.

**Why this priority**: Un error de tipeo en un dato fiscal no puede obligar a anular una factura
válida, pero el módulo ya sirve sin esto: la corrección se puede diferir.

**Independent Test**: Se puede verificar de forma independiente emitiendo una factura con un CAE mal
cargado, corrigiéndolo desde su ficha, abriendo el documento y comprobando que ya trae el CAE bueno,
y que el cliente, los viajes y los tres importes no ofrecen ninguna forma de editarse.

**Acceptance Scenarios**:

1. **Given** una factura emitida, **When** Administración abre su edición, **Then** puede modificar
   el detalle, el CAE, el vencimiento del CAE y el vencimiento de pago.
2. **Given** esa misma edición, **When** Administración busca cambiar el cliente, los viajes
   incluidos, el neto, el IVA o el total, **Then** no encuentra dónde: ninguno de esos datos es
   editable, ni en la pantalla ni invocando la acción directamente.
3. **Given** una factura emitida, **When** Administración corrige el CAE y guarda, **Then** el
   cambio queda registrado con el usuario que lo hizo y el instante en que ocurrió, y la factura
   sigue en el mismo estado.
4. **Given** esa misma factura corregida, **When** Administración abre el documento de la factura,
   **Then** el documento ya trae el CAE corregido: el archivo se regeneró y el anterior no quedó
   guardado.
5. **Given** una factura emitida, **When** se intenta guardar un vencimiento de pago anterior a la
   fecha de facturación, **Then** el sistema lo rechaza con el mismo criterio que rige el alta, y el
   documento no se regenera.
6. **Given** una factura emitida, **When** se intenta borrar el CAE o su vencimiento dejándolos
   vacíos, **Then** el sistema lo rechaza: una factura emitida no puede quedarse sin CAE.
7. **Given** una factura `anulada`, **When** se la consulta, **Then** no existe ninguna acción para
   editar sus datos.
8. **Given** una factura `pagada`, **When** Administración corrige su CAE y guarda, **Then** el
   sistema lo acepta, el documento se regenera con el CAE bueno, y la factura sigue `pagada` con la
   misma fecha de cobro: corregir un dato fiscal no depende de si la factura se cobró.

---

### User Story 5 - Registrar el cobro y seguir los vencimientos (Priority: P2)

Administración marca la factura como cobrada el día que entra la plata, y mira en un panel qué
facturas están vencidas y cuáles vencen en los próximos días, con el cliente, el importe y los días
de atraso o de plazo, para llamar antes de que se atrase más.

**Why this priority**: Es lo que convierte el registro de facturas en control de cobranzas, pero
depende de que ya haya facturas emitidas.

**Independent Test**: Se puede verificar de forma independiente emitiendo una factura con vencimiento
pasado y otra con vencimiento próximo, comprobando que la primera figura `vencida` sin que nadie haya
tocado nada, registrando el cobro de la segunda y comprobando que pasa a `pagada` y deja el panel.

**Acceptance Scenarios**:

1. **Given** una factura recién emitida, **When** se la consulta, **Then** su estado es `pendiente`.
2. **Given** una factura `pendiente` cuyo vencimiento de pago ya pasó y que no está cobrada, **When**
   Administración abre el listado, **Then** figura como `vencida` sin que nadie haya ejecutado
   ninguna acción.
3. **Given** una factura `pendiente` o `vencida`, **When** Administración registra el cobro con su
   fecha, **Then** la factura queda en estado `pagada`, con esa fecha visible en la ficha, y el
   historial registra quién lo hizo y cuándo.
4. **Given** una factura recién pagada, **When** Administración abre el panel de vencimientos,
   **Then** esa factura ya no figura.
5. **Given** una fecha de cobro anterior a la fecha de facturación, **When** se intenta registrar el
   cobro, **Then** el sistema lo rechaza marcando ese campo.
6. **Given** una factura `pagada`, **When** se la consulta, **Then** no existe ninguna acción para
   volverla a `pendiente`, a `vencida` ni a `anulada`.
7. **Given** una factura `anulada`, **When** se la consulta, **Then** no existe ninguna acción para
   registrarle un cobro.
8. **Given** facturas vencidas y facturas por vencer, **When** Administración abre el panel de
   vencimientos, **Then** ve de cada una el cliente, el número, el importe y los días de atraso o de
   plazo, con la palabra que lo explica y no sólo con un color.
9. **Given** un panel de vencimientos sin ninguna factura vencida ni por vencer, **When** se lo abre,
   **Then** el sistema lo dice explícitamente en vez de mostrar una tabla vacía.
10. **Given** una factura impaga cuyo CAE ya venció, **When** se la consulta, **Then** su estado de
    cobro no cambia por eso: el vencimiento del CAE es un dato fiscal del comprobante y no afecta al
    seguimiento de la cobranza.

---

### User Story 6 - Anular una factura y refacturar (Priority: P2)

Al administrador del sistema le avisan que a la factura le faltó un viaje. La anula escribiendo por
qué, sus tres viajes vuelven a `rendido` y se ofrecen otra vez, y Administración emite una
Refacturación con los cuatro viajes que referencia a la anulada. Las dos facturas se muestran una a
la otra.

**Why this priority**: Sin anulación no hay forma de corregir una factura mal emitida, pero el
módulo entrega valor emitiendo y cobrando antes de tener este camino.

**Independent Test**: Se puede verificar de forma independiente anulando una factura de tres viajes,
comprobando que sin motivo escrito la confirmación no se habilita, que al cancelar nada cambia, que
al confirmar los tres viajes vuelven a ofrecerse, y emitiendo después una Refacturación que la
referencia.

**Acceptance Scenarios**:

1. **Given** una factura `pendiente` o `vencida` y un usuario con rol *Administrador del sistema*,
   **When** pide anularla, **Then** el sistema le pide un motivo escrito y una confirmación
   explícita.
2. **Given** el formulario de anulación sin motivo escrito, **When** se intenta confirmar, **Then**
   el sistema no habilita la confirmación y no anula nada.
3. **Given** el pedido de confirmación de anulación, **When** el administrador cancela, **Then** la
   factura queda exactamente igual que antes, con su estado y sus viajes sin cambios.
4. **Given** el motivo escrito y la confirmación aceptada, **When** se ejecuta la anulación, **Then**
   la factura queda en estado `anulada` con su motivo visible, el historial registra quién la anuló y
   cuándo, y todos sus viajes vuelven a estado `rendido`.
5. **Given** una factura anulada, **When** se arma una factura nueva para el mismo cliente y período,
   **Then** sus viajes vuelven a aparecer en la lista de viajes facturables.
6. **Given** una factura `pagada`, **When** se intenta anularla, **Then** el sistema lo rechaza
   informando que está cobrada y desde qué fecha, sin ofrecer ni sugerir revertir el cobro: no
   existe ninguna acción que lo haga.
7. **Given** un usuario con el permiso de gestión pero sin el de anulación, **When** abre la ficha de
   una factura, **Then** no ve la acción de anular, y el sistema la rechaza igual si se la invoca
   directamente.
8. **Given** el alta de factura, **When** Administración elige el tipo de facturación
   `Refacturación`, **Then** el sistema pide la factura anulada que se reemplaza y ofrece únicamente
   facturas anuladas de ese mismo cliente.
9. **Given** el tipo de facturación `Refacturación` sin factura reemplazada elegida, **When** se
   intenta confirmar, **Then** el sistema no lo permite y marca ese campo.
10. **Given** una Refacturación emitida, **When** se consulta cualquiera de las dos facturas,
    **Then** cada una muestra la referencia a la otra: la nueva indica a cuál reemplaza y la anulada
    indica cuál la reemplazó.
11. **Given** el tipo de facturación `Original`, **When** se arma la factura, **Then** el sistema no
    pide ninguna factura reemplazada.
12. **Given** una factura anulada, **When** se la consulta, **Then** no existe ninguna acción para
    devolverla a `pendiente`, para cobrarla ni para editarla.
13. **Given** una factura recién anulada, **When** se abre su documento, **Then** el documento trae
    impresas la leyenda de anulada y el motivo escrito: se regeneró al anular y reemplazó al anterior,
    que no quedó guardado.
14. **Given** una factura anulada que ya fue reemplazada por una Refacturación, **When** se arma otra
    Refacturación para el mismo cliente, **Then** esa anulada no aparece entre las que se pueden
    elegir; y si se la elige invocando la acción directamente, el sistema rechaza la emisión
    nombrando la Refacturación que ya la reemplaza.

---

### User Story 7 - Ver lo facturado y lo cobrado por cliente en un período (Priority: P3)

Gerencia mira, entre dos fechas, cuánto se le facturó a cada cliente, cuánto de eso se cobró y cuánto
queda pendiente, para seguir la evolución del negocio con datos y no de memoria.

**Why this priority**: Es el uso que le da valor a los datos cargados, pero depende de que el resto
del módulo ya esté operando y no bloquea la operación diaria.

**Independent Test**: Se puede verificar de forma independiente emitiendo facturas de dos clientes
dentro y fuera de un rango de fechas, cobrando algunas y anulando una, y comprobando que los totales
cuentan sólo las del rango y ninguna anulada.

**Acceptance Scenarios**:

1. **Given** facturas emitidas en distintas fechas, **When** Gerencia elige un rango de fechas,
   **Then** ve por cada cliente el importe facturado, el importe cobrado y el pendiente de cobro
   dentro de ese rango.
2. **Given** la pantalla de totales recién abierta, **When** todavía no se eligió un rango de fechas,
   **Then** el sistema no calcula ni muestra ningún total y dice explícitamente que falta elegirlo.
3. **Given** un cliente con 5 facturas en el período, de las cuales 1 está anulada, **When** se mira
   su total, **Then** figura con 4 facturas y con la suma de los importes de esas 4.
4. **Given** el listado filtrado por cliente y rango de fechas, **When** se compara con el total de
   ese cliente, **Then** la suma de los importes totales de las filas mostradas coincide con el
   importe facturado del cuadro, y ninguna anulada suma.
5. **Given** un rango de fechas sin ninguna factura, **When** se consulta, **Then** el sistema
   muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.
6. **Given** un usuario con rol *Gerencia*, **When** abre el listado, las fichas, el panel de
   vencimientos y los totales, **Then** puede consultarlos, pero no ve las acciones de emitir,
   modificar, cobrar ni anular.
7. **Given** ese mismo usuario, **When** se invoca directamente una acción de emisión, modificación,
   cobro o anulación, **Then** el sistema la rechaza: la restricción no vive sólo en la pantalla.

---

### Edge Cases

- La empresa emisora no está configurada o le falta alguno de los cuatro datos obligatorios: el alta
  de factura informa cuáles faltan, con su nombre, y no deja continuar (cubierto en User Story 1).
- La empresa emisora no tiene logo: la factura se emite igual y la vista previa muestra los datos de
  texto. El logo es opcional (cubierto en User Story 1).
- El cliente y el período elegidos no tienen ningún viaje facturable: la lista lo informa
  explícitamente y no deja confirmar, en vez de mostrarse vacía sin explicación (cubierto en User
  Story 2).
- Hay que facturar viajes de meses distintos en un mismo comprobante: en esta versión no se puede.
  Una factura corresponde a un único período; se emiten dos facturas (FR-018).
- Un viaje incluido tiene importe cero: se puede incluir, pero la emisión pide una confirmación
  explícita previa porque ese viaje no aporta al neto y la factura, una vez emitida, no cambia de
  importes (FR-020, FR-032).
- La fecha de facturación es anterior a la fecha de algún viaje incluido: la emisión pide una
  confirmación explícita previa, porque indica un error de carga de fechas (FR-032).
- El viaje se rindió después de cerrado el período: aparece igual, porque el filtro es por fecha del
  viaje y no por fecha de rendición (FR-016).
- Dos administrativos arman al mismo tiempo una factura que incluye el mismo viaje: el primero que
  confirma gana y el segundo recibe el rechazo indicando qué viaje ya fue facturado y en qué
  comprobante. La garantía está en el guardado y en la base de datos, no sólo en la pantalla
  (FR-053, SC-005).
- Dos administrativos cargan al mismo tiempo el mismo número de comprobante: igual que arriba, quien
  llega segundo recibe el rechazo de duplicado identificando la factura que lo usa (FR-027).
- El CAE se cargó mal: se corrige sobre la factura emitida, dejando registro de quién y cuándo lo
  modificó; no hace falta anularla (cubierto en User Story 4).
- El cliente se dio de baja después de facturado: la factura conserva sus datos y sigue visible, con
  el cliente señalado como inactivo; el cliente no se ofrece para facturas nuevas (FR-011).
- Al cliente le corrigen la razón social o el domicilio después de facturado: la factura emitida
  sigue mostrando los datos que tenía al emitirse, en la ficha, en el listado y en el documento. No
  hay regeneración del documento por este motivo, porque no hay nada que cambie (FR-034a).
- Un cliente dado de baja tiene viajes `rendido` todavía sin facturar: esos viajes no se pueden
  facturar mientras el cliente esté inactivo, porque el desplegable sólo ofrece clientes activos. Se
  lo da de alta de nuevo en el Módulo 5, se factura, y se lo vuelve a dar de baja si corresponde
  (FR-011).
- Se quiere anular una factura cuyos viajes ya se facturaron de nuevo: no puede pasar. Los viajes
  vuelven a `rendido` sólo al anular, y un viaje no puede estar en dos facturas vigentes a la vez
  (FR-053).
- Se quiere refacturar una anulada que ya tiene su Refacturación: no se ofrece en el desplegable, y
  si se la elige invocando la acción directamente el sistema la rechaza nombrando la Refacturación
  que ya la reemplaza. La garantía está en el guardado y en la base, no sólo en la pantalla
  (FR-049a).
- El vencimiento del CAE llegó y la factura sigue impaga: no afecta al estado de la factura. El
  vencimiento del CAE es un dato fiscal del comprobante; el estado de cobro va por el vencimiento de
  pago (FR-041, cubierto en User Story 5).
- Cambia la alícuota de IVA después de emitida una factura: las facturas ya emitidas conservan el IVA
  con el que se calcularon, porque sus importes son inmutables. El cambio sólo afecta a las nuevas
  (FR-025, FR-033).
- Se emite una `Factura C`: el IVA es $0,00 y el total es igual al neto. No es un caso de error ni
  una factura incompleta (FR-023).
- Se emite una `Factura B`: el documento lleva el mismo pie que una `Factura A` —neto, IVA y total
  discriminados— y la misma columna `% IVA`. La disposición del documento no cambia con el tipo de
  comprobante; sólo cambian la letra, el código, el título y la alícuota (FR-031i, FR-031j).
- Se corrige el CAE de una factura ya emitida: el documento se regenera con el dato bueno y reemplaza
  al anterior, que no se conserva. La ficha y el archivo nunca discrepan (FR-031b, cubierto en User
  Story 4).
- Se descarga el documento de una factura `anulada`: se puede, y el propio documento indica que está
  anulada y por qué, porque se regeneró en la misma operación que la anuló y reemplazó al anterior.
  Así no circula un PDF idéntico al del día de la emisión (FR-031b, FR-031d).
- La empresa emisora no tiene logo al emitir: el documento se genera igual y el bloque del emisor se
  acomoda a su ausencia, sin hueco ni imagen rota (FR-004, FR-031g).
- La empresa emisora no tiene CBU configurado: la banda del CBU no aparece en el documento y el resto
  del comprobante se arma normalmente (FR-031).
- Cambia el CBU de la empresa emisora después de emitida una factura: la factura emitida sigue
  mostrando el CBU con el que salió, igual que el resto de los datos del emisor. Sólo las facturas
  nuevas usan el nuevo (FR-034).
- Se registra el cobro de una factura: el documento **no** se regenera y no dice nada del cobro. La
  fecha de cobro vive en la ficha, que es donde se sigue la cobranza; el comprobante que se le mandó
  al cliente no cambia porque después haya pagado (FR-031b, FR-042).
- El documento no se puede generar al confirmar la emisión: no se crea nada. La factura no existe,
  los viajes siguen `rendido` y el número de comprobante queda libre para reintentar. Lo mismo al
  corregir y al anular: la operación no queda aplicada a medias (FR-031, FR-031b).
- Cambia el **logo** y después se regenera el documento de una factura vieja —al corregirle el CAE o
  al anularla—: el documento regenerado sale con el logo vigente, porque el logo es la única excepción
  al congelamiento. Es la contrapartida declarada de no guardar una copia del archivo por factura
  (FR-034, FR-031b).
- Una factura agrupa muchos viajes: la tabla de detalle lleva una fila por cada uno y el documento
  sigue de largo las páginas que haga falta; no se consolida en una sola fila para que entre
  (FR-031e).
- Se intenta anular una factura ya cobrada: el sistema lo rechaza informando desde qué fecha está
  cobrada, y no ofrece revertir el cobro porque `pagada` es terminal y no existe esa acción
  (FR-043, FR-043a, cubierto en User Story 6).
- Se quiere facturar un viaje anulado por error: no se ofrece nunca. Si el viaje se anuló mal, la
  corrección se hace en el módulo de viajes (FR-017).
- Un viaje pasa a `facturado`: deja de poder editarse y de poder anularse, igual que ya ocurría
  estando `rendido`. El Módulo 5 no ofrece ninguna acción de escritura sobre él (FR-052).
- No hay clientes activos en el padrón: el alta de factura lo informa explícitamente y no deja
  continuar, del mismo modo que el alta de viaje del Módulo 5 (FR-011).
- Un viaje ya estaba `rendido` sin número de remito, de antes de FR-055a: **no se puede facturar**.
  El remito sale impreso en su fila del detalle y un viaje rendido no admite edición en ninguna
  versión de este sistema. Es una **limitación conocida y aceptada**: se ofrece en la lista señalado
  con la palabra que lo explica y la emisión se rechaza nombrándolo, en vez de esconderlo. No se
  agrega un camino de corrección del viaje rendido: sería revertir la decisión que el Módulo 5 tomó a
  propósito (FR-019a, FR-055a).
- El cliente elegido no tiene domicilio cargado: la emisión se rechaza nombrando el dato faltante e
  indicando dónde cargarlo. El domicilio sale impreso en el comprobante y sigue siendo opcional en el
  padrón, porque un cliente que no se factura no lo necesita (FR-011a).

## Requirements *(mandatory)*

### Functional Requirements

#### Empresa emisora

- **FR-001**: El sistema DEBE permitir configurar y guardar los datos de la empresa emisora: razón
  social, CUIT, domicilio, condición de IVA, número de ingresos brutos, fecha de inicio de
  actividades, punto de venta, **CBU**, teléfono y email de contacto. La configuración DEBE ser
  **única para todo el sistema**: se edita, nunca se crea una segunda ni se borra.
- **FR-002**: El sistema DEBE exigir razón social, CUIT, domicilio y condición de IVA; el número de
  ingresos brutos, la fecha de inicio de actividades, el punto de venta, el CBU, el teléfono y el
  email DEBEN ser opcionales. El CUIT DEBE validarse con la misma regla del Módulo 3 —once dígitos con dígito
  verificador válido, normalizado a sólo dígitos antes de validar y de guardar— y el email DEBE tener
  formato válido.
- **FR-003**: El sistema DEBE permitir subir, reemplazar y quitar el logo de la empresa. El archivo
  DEBE guardarse fuera del repositorio, con nombre generado por el sistema, y servirse por endpoint
  autorizado, con el tipo validado por la **firma del archivo** y no por su extensión ni por el
  `Content-Type` declarado, admitiendo únicamente JPG y PNG.
- **FR-004**: El logo DEBE ser opcional: el sistema DEBE permitir emitir facturas sin logo cargado.
- **FR-005**: El sistema DEBE reutilizar automáticamente los datos configurados de la empresa en toda
  factura nueva, sin que el usuario tenga que ingresarlos ni pueda editarlos desde el alta de la
  factura.
- **FR-006**: El sistema DEBE exigir que la empresa emisora tenga cargados razón social, CUIT,
  domicilio y condición de IVA antes de permitir emitir la primera factura, y DEBE indicar cuáles de
  los cuatro faltan, nombrándolos. El rechazo DEBE producirse en el guardado y no sólo ocultar el
  formulario.

#### Alta de la factura

- **FR-007**: El sistema DEBE permitir registrar facturas a cliente con número de comprobante, fecha
  de facturación, tipo de comprobante, tipo de facturación, período (mes y año), cliente, detalle,
  neto, IVA, importe total, CAE, vencimiento del CAE, fecha de vencimiento de pago, estado, la
  referencia al documento generado por el sistema (FR-031) y los viajes incluidos.
- **FR-008**: El tipo de comprobante DEBE ofrecerse en una lista desplegable con exactamente las
  opciones `Factura A`, `Factura B` y `Factura C`.
- **FR-009**: El tipo de facturación DEBE ofrecerse en una lista desplegable con exactamente las
  opciones `Original` y `Refacturación`.
- **FR-009a**: La **condición de venta** DEBE ofrecerse en una lista desplegable con exactamente las
  opciones `Contado`, `Cuenta Corriente`, `Tarjeta de Débito / Crédito` y `Cheque`. Es un dato de la
  factura, no del cliente: se elige al emitir y queda congelado en el comprobante.
- **FR-010**: El mes y el año del período DEBEN ofrecerse en **dos listas desplegables separadas**. El
  mes DEBE ofrecer los doce valores `01` a `12`, escritos con dos dígitos. El año DEBE ofrecer
  exactamente `2025` y `2026`. El sistema DEBE rechazar un período fuera de esas opciones aunque se
  invoque la acción directamente: el desplegable es la comodidad, la restricción es la del servidor.
  Las mismas dos listas rigen el filtro de período del listado (FR-058).
- **FR-011**: El cliente DEBE ofrecerse en una lista desplegable con los clientes **activos** del
  padrón del Módulo 5, mostrando razón social y CUIT. Cuando no haya ningún cliente activo, el
  sistema DEBE informarlo explícitamente y NO DEBE permitir completar el alta. Las facturas ya
  emitidas de un cliente dado de baja después DEBEN conservarlo y seguir mostrándolo, señalado como
  inactivo con la palabra que lo explica y no sólo con un color.
- **FR-011a**: El **domicilio del cliente** DEBE estar cargado en el padrón para poder facturarle,
  porque sale impreso en el bloque del cliente del documento (FR-031). Cuando falte, el sistema DEBE
  rechazar la emisión nombrando el dato faltante e indicando dónde cargarlo, con el mismo criterio con
  el que rechaza emitir sin la empresa emisora configurada (FR-006). El rechazo DEBE producirse en el
  guardado y no sólo ocultar el formulario. El padrón del Módulo 5 NO DEBE modificarse por esto: el
  domicilio sigue siendo opcional allá y es obligatorio únicamente para facturar (FR-056).
- **FR-012**: El sistema DEBE permitir ingresar la fecha de facturación, proponiendo la fecha actual
  por defecto y permitiendo cambiarla por cualquier otra.
- **FR-013**: El sistema DEBE exigir cliente, tipo de comprobante, tipo de facturación, condición de
  venta, período, fecha de facturación, número de comprobante, CAE, vencimiento del CAE, fecha de
  vencimiento de pago y **al menos un viaje seleccionado** para dar de alta una factura. El detalle
  DEBE ser opcional, de hasta 500 caracteres.
- **FR-014**: Un alta exitosa NO DEBE dejar el formulario en pantalla: el sistema DEBE llevar al
  usuario a la ficha de la factura recién creada, y cualquier advertencia reversible DEBE viajar con
  la confirmación a esa pantalla.

#### Selección de viajes

- **FR-015**: El sistema DEBE ofrecer para seleccionar únicamente los viajes que cumplan **todas**
  estas condiciones a la vez: pertenecen al cliente elegido, están en estado `rendido`, y su fecha
  cae dentro del mes y año elegidos.
- **FR-016**: El período DEBE evaluarse contra la **fecha del viaje**, nunca contra la fecha de la
  factura ni contra la fecha en que el viaje se rindió.
- **FR-017**: El sistema NO DEBE ofrecer viajes ya incluidos en una factura vigente —una factura no
  anulada—, ni viajes en estado `pendiente`, `en curso`, `anulado` o `facturado`.
- **FR-018**: Una factura DEBE corresponder a **un único cliente y un único período**. El sistema NO
  DEBE permitir incluir viajes de más de un cliente ni de más de un período en el mismo comprobante.
- **FR-019**: El sistema DEBE permitir seleccionar varios viajes a la vez y quitarlos de la selección
  antes de confirmar, mostrando de cada viaje ofrecido su número, fecha, remito, origen, destino e
  importe.
- **FR-019a**: Todos los viajes incluidos en una factura DEBEN tener número de remito, porque sale
  impreso en su fila de la tabla de detalle (FR-031e). Los viajes que se rindan a partir de FR-055a lo
  traen siempre. Para los que ya estaban `rendido` sin remito, el sistema DEBE rechazar la emisión
  **nombrando cuáles** son, en vez de emitir un documento con filas incompletas. Esos viajes NO DEBEN
  ocultarse de la lista de facturables: DEBEN ofrecerse señalados con la palabra que lo explica, para
  que quien opera vea por qué no puede facturarlos.
- **FR-020**: El sistema DEBE mostrar en todo momento la cantidad de viajes seleccionados y el
  importe acumulado de la selección, actualizados en cada cambio.
- **FR-021**: Cuando el cliente y el período elegidos no tengan viajes facturables, el sistema DEBE
  informarlo explícitamente, nombrando la combinación, en vez de mostrar una lista vacía sin
  explicación, y NO DEBE permitir confirmar.

#### Cálculo de importes

- **FR-022**: El neto DEBE calcularse como la **suma exacta** de los importes de los viajes
  seleccionados.
- **FR-023**: El IVA DEBE calcularse aplicando al neto la alícuota que corresponde al tipo de
  comprobante, y el importe total DEBE ser neto más IVA. Las alícuotas DEBEN ser exactamente:
  `Factura A` **21%**, `Factura B` **21%** y `Factura C` **0%**. En una `Factura C` el IVA DEBE ser
  cero y el total DEBE ser igual al neto. La alícuota NO DEBE ser ingresable ni editable por ningún
  usuario.
- **FR-024**: El sistema NO DEBE permitir editar manualmente el neto, el IVA ni el importe total, ni
  desde la pantalla ni invocando la acción directamente.
- **FR-025**: El sistema DEBE recalcular los tres importes cada vez que cambia la selección de viajes
  o el tipo de comprobante, antes de confirmar. Después de emitida, los tres importes DEBEN quedar
  fijos y NO DEBEN recalcularse nunca más.
- **FR-026**: Los importes DEBEN expresarse en pesos argentinos, redondearse a dos decimales y
  mostrarse con el formato de moneda del resto del sistema —punto como separador de miles, coma para
  decimales, símbolo `$`—. NO DEBEN representarse con punto flotante.

#### Emisión, número de comprobante, CAE y documento de la factura

- **FR-027**: El sistema DEBE permitir ingresar el número de comprobante en formato **punto de venta
  + número** —cuatro dígitos, guion, ocho dígitos, por ejemplo `0014-00000003`—, proponiendo el punto
  de venta configurado en la empresa emisora. El número completo DEBE ser único entre las facturas
  **no anuladas**, garantizado con una restricción de unicidad en la base de datos y no sólo con la
  validación previa; el rechazo DEBE identificar la factura que ya lo usa.
- **FR-028**: El sistema DEBE exigir el CAE y su fecha de vencimiento para dar por emitida la
  factura, y NO DEBE crear la factura sin ellos.
- **FR-029**: El vencimiento del CAE NO DEBE ser anterior a la fecha de facturación.
- **FR-030**: La fecha de vencimiento de pago DEBE ser obligatoria y NO DEBE ser anterior a la fecha
  de facturación.
- **FR-031**: Al confirmar la emisión, el sistema DEBE **generar el documento de la factura en
  formato PDF** y guardarlo. El sistema NO DEBE pedirle al usuario que suba ningún archivo: el
  documento lo produce el sistema con los datos que ya tiene. Su disposición DEBE seguir el formato
  de comprobante argentino con estos bloques, en este orden:

  1. **Banda de ejemplar**: la palabra que identifica al tipo de facturación —`ORIGINAL` o
     `REFACTURACIÓN`— centrada arriba de todo.
  2. **Bloque del emisor** (izquierda): el logo cuando esté cargado, la razón social, la condición de
     IVA y el domicilio.
  3. **Recuadro de letra** (centro): la letra del comprobante —`A`, `B` o `C`— con su código
     numérico debajo.
  4. **Bloque de identificación** (derecha): el título del comprobante, la fecha de emisión, el
     **período facturado** en formato `MM/AAAA`, el punto de venta y número, el CUIT, la inscripción
     en ingresos brutos y la fecha de inicio de actividades.
  5. **Banda de vencimiento de pago**, en un renglón propio a todo el ancho.
  6. **Banda de CBU del emisor**, en un renglón propio a todo el ancho, tomada del CBU congelado en
     la factura (FR-034), que DEBE omitirse cuando ese CBU esté vacío.
  7. **Bloque del cliente**: nombre, CUIT, domicilio, condición de IVA, condición de venta y remito.
  8. **Tabla de detalle** según FR-031e.
  9. **Pie de importes**: el neto, el IVA y el importe total, más el CAE y su fecha de vencimiento. A
     su izquierda, en el mismo renglón, el **detalle** de la factura (FR-013) bajo el rótulo
     `Observaciones`, que DEBE omitirse entero —rótulo incluido— cuando el detalle esté vacío, con el
     mismo criterio que la banda de CBU.

  Todos los importes DEBEN salir con el formato de moneda del resto del sistema, y todas las fechas
  con el formato de fecha del resto del sistema.

  Si el documento **no se puede generar**, la emisión DEBE rechazarse entera: NO DEBE crearse la
  factura, los viajes DEBEN quedar en `rendido` y el número de comprobante DEBE quedar libre. Es el
  mismo criterio de todo o nada de FR-054, y es lo que sostiene SC-007a sin excepciones: no existe una
  factura emitida sin su documento.
- **FR-031e**: La tabla de detalle DEBE llevar las columnas `Código`, `Producto / Servicio`,
  `Cantidad`, `U. Medida`, `Precio unit.`, `% Bonif.`, `Importe`, `% IVA` y `Subtotal`, y DEBE
  contener **una fila por cada viaje incluido**, nunca una única fila consolidada: es lo que hace que
  la factura se explique por los viajes que la componen. Cada fila DEBE llevar el **número del viaje**
  en `Código`; su origen, su destino y su número de remito en `Producto / Servicio`; `1` en
  `Cantidad`; `UNIDAD` en `U. Medida`; el importe del viaje en `Precio unit.` y en `Importe`; `0,00`
  en `% Bonif.`; la alícuota del tipo de comprobante en `% IVA`; y el importe del viaje más su IVA en
  `Subtotal`.
- **FR-031f**: Los subtotales por fila son **informativos**. Los importes de la factura son los de
  FR-022 y FR-023 —el neto es la suma de los importes de los viajes y el IVA se calcula sobre ese
  neto—, y son los que DEBEN figurar en el pie del documento. Si por redondeo la suma de los
  subtotales por fila difiere del importe total, DEBEN mandar los tres importes del pie.
- **FR-031g**: El documento DEBE generarse igual **sin logo cargado** (FR-004): el bloque del emisor
  DEBE acomodarse a su ausencia mostrando sólo los datos de texto, sin dejar un hueco ni una imagen
  rota.
- **FR-031h**: La **condición de IVA del cliente** DEBE salir en el documento como el texto fijo
  `Responsable Inscripto`. NO DEBE ser un campo del padrón de clientes ni un dato que se elija al
  emitir: todos los clientes de la empresa son empresas. El campo `Remito` del bloque del cliente
  DEBE quedar vacío, porque cada viaje lleva su propio remito en su fila de la tabla de detalle
  (FR-031e).
- **FR-031i**: El recuadro de letra DEBE mostrar la letra y el código de comprobante que corresponden
  al tipo elegido: `A` con código `001`, `B` con código `006` y `C` con código `011`. El título del
  bloque de identificación DEBE decir `FACTURA A`, `FACTURA B` o `FACTURA C` según el mismo tipo.
- **FR-031j**: La disposición del documento DEBE ser **la misma para los tres tipos de comprobante**.
  El pie de importes DEBE mostrar siempre el neto, el IVA y el importe total, y la tabla de detalle
  DEBE llevar siempre la columna `% IVA`, también en una `Factura B`. El sistema NO DEBE armar un pie
  distinto según el tipo: lo único que cambia entre tipos es la letra, el código, el título
  (FR-031i) y el valor de la alícuota (FR-023), que en una `Factura C` es `0,00 %` con IVA en $0,00.
- **FR-031a**: El documento generado DEBE guardarse fuera del repositorio, con nombre generado por el
  sistema, y servirse por endpoint autorizado con el permiso de consulta. La factura DEBE guardar
  únicamente la **referencia** al archivo, nunca su contenido. El documento DEBE abrirse **en
  línea**, con un nombre que identifique la factura.
- **FR-031b**: Cuando se corrija un dato de la factura que aparece en el documento —el detalle, el
  CAE, el vencimiento del CAE o la fecha de vencimiento de pago (FR-035)— **y también al anular la
  factura**, el sistema DEBE **regenerar el documento y reemplazar al anterior**, de modo que el
  archivo guardado y la ficha digan siempre lo mismo. NO DEBE conservarse el documento viejo. La
  regeneración por anulación DEBE ocurrir en la misma operación que cambia el estado (FR-048): si el
  documento no se puede regenerar, la anulación NO DEBE quedar aplicada a medias. El mismo criterio
  rige la corrección (FR-035): si el documento no se puede regenerar, la corrección NO DEBE quedar
  guardada. Ninguna operación de este módulo deja la ficha diciendo una cosa y el archivo otra.
  **Registrar el cobro NO DEBE regenerar el documento** (FR-042): la fecha de cobro no sale impresa,
  porque es información interna de cobranzas y el comprobante que se le mandó al cliente no cambia
  porque después haya pagado. Las operaciones que regeneran son exactamente tres: emitir, corregir y
  anular.
- **FR-031c**: El documento generado por el sistema **NO es el comprobante fiscal**: es su
  representación impresa, armada con los datos cargados. La validez fiscal la da el CAE obtenido en
  AFIP/ARCA por fuera del sistema (FR-028). El documento NO DEBE presentarse ni describirse como
  comprobante emitido ante el organismo.
- **FR-031d**: El documento DEBE poder descargarse desde la ficha de la factura en cualquiera de sus
  estados. Cuando la factura esté `anulada`, el documento DEBE indicarlo de forma visible junto con
  el motivo de la anulación, para que no circule como si estuviera vigente. Esa leyenda DEBE quedar
  **impresa en el documento regenerado al anular** (FR-031b), y no estamparse al servir el archivo:
  el documento se arma en un solo lugar.
- **FR-032**: El sistema DEBE pedir una **confirmación explícita previa** antes de emitir cuando la
  selección incluya al menos un viaje con importe en cero, o cuando la fecha de facturación sea
  anterior a la fecha de algún viaje incluido. En ambos casos el primer intento NO DEBE crear nada:
  DEBE informar el motivo puntual —qué viaje tiene importe cero, qué viaje es posterior a la fecha de
  la factura— y emitir únicamente después de la confirmación. El criterio es que la emisión no se
  deshace: una vez emitida, la factura no cambia de importes (FR-033) y sólo se corrige anulando.
- **FR-033**: El sistema DEBE mostrar una vista previa de la factura **antes** de confirmar la
  emisión, con los datos de la empresa emisora y su logo, los datos del cliente, el período, el
  detalle de los viajes incluidos y los tres importes. La vista previa DEBE mostrar los mismos datos
  y con la **misma disposición** que va a llevar el documento generado (FR-031), para que revisarla
  sirva de algo. Esa igualdad DEBE garantizarse usando **un único armador de documento**, el mismo
  que produce el archivo al emitir: el sistema NO DEBE mantener una segunda maqueta del comprobante
  dibujada aparte. La vista previa NO DEBE guardar ningún archivo, crear la factura ni registrar
  nada; produce el documento sólo para mirarlo. El archivo se persiste recién al confirmar.
- **FR-034**: Los datos de la empresa emisora DEBEN quedar **congelados en la factura** al momento de
  emitirla: razón social, CUIT, domicilio, condición de IVA, ingresos brutos, inicio de actividades,
  punto de venta, **CBU**, teléfono y email. Un cambio posterior en la configuración NO DEBE alterar
  ninguna factura ya emitida. El **logo es la única excepción**: se lee siempre de la configuración
  vigente y no se copia a la factura. **Consecuencia declarada**: si el logo cambia y después se
  regenera el documento de una factura vieja (FR-031b), el documento regenerado sale con el logo
  vigente. Se acepta, porque el logo no es un dato de la ficha y entonces la ficha y el documento no
  llegan a discrepar; guardar una copia del archivo por cada factura agregaría complejidad sin un caso
  de uso que la pida.
- **FR-034a**: Los datos del cliente DEBEN quedar **congelados en la factura** al momento de
  emitirla, con el mismo criterio que FR-034: razón social, CUIT y domicilio. La factura DEBE además
  conservar la referencia al cliente del padrón, que es la que usan el vínculo, el filtro por cliente
  (FR-058) y los totales por cliente (FR-061). Una corrección posterior en el padrón NO DEBE alterar
  lo que muestran la ficha, el listado ni el documento de una factura ya emitida.

#### Corrección de una factura emitida

- **FR-035**: El sistema DEBE permitir modificar de una factura emitida únicamente el detalle, el
  CAE, el vencimiento del CAE y la fecha de vencimiento de pago, aplicando las mismas validaciones
  que rigen el alta. Guardar la corrección DEBE regenerar el documento de la factura (FR-031b). La
  corrección DEBE estar disponible en los estados `pendiente`, `vencida` y `pagada`: un error de
  tipeo en un dato fiscal no depende de si la factura se cobró, y una factura `pagada` tampoco se
  puede anular (FR-043a). Corregir NO DEBE cambiar el estado de la factura ni su fecha de cobro.
- **FR-036**: El sistema NO DEBE permitir modificar el cliente, el tipo de comprobante, el tipo de
  facturación, el período, la fecha de facturación, el número de comprobante, los viajes incluidos ni
  los importes de una factura emitida. La restricción NO DEBE vivir sólo en la pantalla: el sistema
  DEBE rechazar la acción si se la invoca directamente.
- **FR-037**: Toda corrección DEBE registrar **quién la hizo y cuándo**, como una entrada más del
  mismo historial que lleva los cambios de estado (FR-045), marcada como corrección y sin estado
  anterior ni estado nuevo. El sistema NO DEBE guardar qué campos cambiaron ni sus valores anterior y
  nuevo: no hay auditoría de valores en esta versión.
- **FR-038**: El sistema NO DEBE permitir editar los datos de una factura `anulada`. Es el **único**
  estado que cierra la corrección: una factura anulada ya no representa nada que corregir.

#### Estados de la factura y cobro

- **FR-039**: El estado de una factura DEBE tomar exactamente uno de estos cuatro valores:
  `pendiente`, `vencida`, `pagada` y `anulada`.
- **FR-040**: Toda factura nueva DEBE crearse en estado `pendiente`.
- **FR-041**: Una factura `pendiente` cuya fecha de vencimiento de pago ya pasó y que no está cobrada
  DEBE mostrarse como `vencida` **sin intervención de nadie y sin proceso programado**: `vencida`
  DEBE derivarse al leer, comparando la fecha de vencimiento de pago con el día en curso, y NO DEBE
  guardarse en una columna. El vencimiento del CAE NO DEBE influir en este cálculo.
- **FR-042**: El sistema DEBE permitir registrar el cobro de una factura `pendiente` o `vencida`,
  con su fecha de cobro, dejándola en estado `pagada`. La fecha de cobro NO DEBE ser anterior a la
  fecha de facturación.
- **FR-043**: El sistema DEBE permitir únicamente las transiciones `pendiente | vencida → pagada` y
  `pendiente | vencida → anulada`. Los estados `pagada` y `anulada` DEBEN ser **terminales**: no
  existe reversión del cobro ni ningún otro camino de retroceso. La pantalla NO DEBE ofrecer ninguna
  acción que lo intente, y el sistema DEBE rechazarla igual si se la invoca directamente.
- **FR-043a**: El rechazo de anular una factura `pagada` DEBE informar que la factura está cobrada,
  con la fecha del cobro, y NO DEBE ofrecer ni sugerir revertirlo: no hay ninguna acción que lo
  haga.
- **FR-044**: Cada cambio de estado DEBE ser un **recurso propio** y nunca un campo del formulario de
  edición de la factura, de modo que corregir un CAE no pueda cobrar ni anular una factura en
  silencio.
- **FR-045**: El sistema DEBE registrar el historial de estados de cada factura, con el estado
  anterior, el estado nuevo, el usuario que produjo el cambio y la fecha y hora en que ocurrió,
  empezando por la emisión. El historial NO DEBE ser editable ni borrable.

#### Anulación y refacturación

- **FR-046**: La anulación de una factura DEBE exigir un motivo escrito obligatorio de hasta 500
  caracteres y una confirmación explícita; sin motivo, la confirmación NO DEBE habilitarse. Cancelar
  la confirmación NO DEBE modificar nada. El motivo DEBE quedar visible en la ficha de la factura y
  en el listado filtrado por estado `anulada`.
- **FR-047**: El sistema DEBE registrar quién anuló la factura y cuándo.
- **FR-048**: Al anular una factura, el sistema DEBE restituir a estado `rendido` **todos** sus
  viajes, en la misma operación que cambia el estado de la factura: o vuelven todos o no vuelve
  ninguno y la factura no se anula.
- **FR-049**: El sistema DEBE permitir que una factura de tipo `Refacturación` referencie a una
  factura **anulada del mismo cliente**, DEBE exigir esa referencia para confirmar la emisión, y DEBE
  ofrecer para elegir únicamente facturas anuladas de ese cliente **que todavía no hayan sido
  reemplazadas** por otra Refacturación. Una factura de tipo `Original` NO DEBE pedir ni admitir esa
  referencia.
- **FR-049a**: Una factura anulada NO DEBE poder ser reemplazada por más de una Refacturación. La
  garantía DEBE estar en el guardado y en una restricción de unicidad de la base de datos, no sólo en
  el desplegable; el rechazo DEBE identificar la Refacturación que ya la reemplaza, con su número.
- **FR-050**: La referencia entre una Refacturación y la factura anulada que reemplaza DEBE mostrarse
  en **ambas** fichas: la nueva indica a cuál reemplaza y la anulada indica cuál la reemplazó.

#### Efecto sobre el viaje (cambios al Módulo 5)

- **FR-051**: El sistema DEBE agregar al ciclo de vida del viaje el estado `facturado`, posterior a
  `rendido`, y DEBE admitir exactamente dos transiciones nuevas: `rendido → facturado`, al confirmar
  la emisión de una factura que lo incluye, y `facturado → rendido`, al anular esa factura. Ninguna
  otra transición desde o hacia `facturado` DEBE permitirse.
- **FR-052**: Un viaje `facturado` DEBE ser inmutable para **todos** los roles, con el mismo alcance
  que ya rige para un viaje `rendido` en el Módulo 5: el sistema NO DEBE permitir modificar sus
  datos, reasignarlo, anularlo ni devolverlo a un estado anterior, NO DEBE ofrecer esas acciones y
  DEBE rechazarlas igual si se las invoca directamente.
- **FR-053**: Un viaje NO DEBE poder pertenecer a más de una factura vigente al mismo tiempo. La
  garantía DEBE estar en el guardado y en una restricción de la base de datos, no sólo en la
  pantalla; el rechazo DEBE indicar qué viaje ya fue facturado y en qué comprobante.
- **FR-054**: Al confirmar la emisión, **todos** los viajes incluidos DEBEN pasar a `facturado` en
  una única operación: o se facturan todos y la factura se crea, o no se factura ninguno y la factura
  no se crea.
- **FR-055**: El listado y la ficha de viajes del Módulo 5 DEBEN mostrar, para cada viaje
  `facturado`, el **número y la fecha** de la factura que lo incluye.
- **FR-055a**: El **número de remito DEBE pasar a ser obligatorio para rendir un viaje** en el Módulo
  5: el sistema NO DEBE permitir la transición `en curso → rendido` de un viaje sin remito cargado, y
  DEBE marcar ese campo con el motivo puntual. Sigue siendo opcional en los estados `pendiente` y `en
  curso`, y se mantiene su unicidad entre los viajes no anulados. El paso a `rendido` es el último
  momento en que el viaje todavía admite edición (FR-018 del Módulo 5), y exigirlo ahí es lo que
  garantiza que todo viaje facturable traiga su remito sin tocar la inmutabilidad del viaje rendido.
- **FR-056**: Los cambios de este módulo sobre el Módulo 5 DEBEN limitarse a los **seis** requisitos
  anteriores (FR-051 a FR-055a). Los viajes `facturado` DEBEN seguir contando en los listados, filtros y totales del
  Módulo 5 con el mismo criterio que los `rendido`: sólo los `anulado` quedan excluidos.

#### Consulta y reportes

- **FR-057**: El listado de facturas DEBE mostrar número, fecha, cliente, tipo de comprobante,
  período, importe total, estado y vencimiento de pago.
- **FR-058**: El listado DEBE permitir filtrar por cliente, rango de fechas, período, estado y tipo
  de comprobante, en cualquier combinación. Cliente, estado, período y tipo de comprobante DEBEN ser
  una selección exacta entre las opciones ya cargadas en el sistema.
- **FR-058a**: El filtro por estado DEBE operar sobre el **estado derivado** —el que la fila muestra
  (FR-041)— y sus cuatro valores DEBEN ser **excluyentes**: `pendiente` DEBE devolver únicamente las
  impagas cuyo vencimiento de pago no pasó, y `vencida` únicamente las impagas cuyo vencimiento ya
  pasó. Una misma factura NO DEBE aparecer bajo los dos filtros. La derivación DEBE escribirse como
  **predicado de la consulta** y no como un filtrado posterior, y DEBE dar el mismo resultado que la
  regla equivalente evaluada en el dominio, verificado con un test que compare las dos sobre el mismo
  dato.
- **FR-059**: El listado de facturas DEBE paginarse del lado del servidor con 20 filas por página,
  informando el total de coincidencias, con los filtros aplicados **antes** de paginar. El orden DEBE
  ser fecha de facturación descendente y, a igual fecha, número de comprobante descendente: un
  criterio total que no permita que dos facturas del mismo día se intercambien entre páginas.
- **FR-060**: La ficha de una factura DEBE mostrar los datos de la empresa emisora congelados al
  emitirla, los datos del cliente, el tipo de comprobante, el tipo de facturación, el período, la
  fecha, el detalle, la lista de los viajes incluidos con su importe, el neto, el IVA, el total, el
  CAE con su vencimiento, el vencimiento de pago, la fecha de cobro cuando corresponda, el motivo de
  anulación cuando corresponda, la referencia de refacturación cuando corresponda, el acceso al
  documento generado y el historial completo de cambios de estado.
- **FR-061**: El sistema DEBE ofrecer una pantalla propia de totales, distinta del listado, que
  muestre por cliente el importe facturado, el importe cobrado y el pendiente de cobro dentro de un
  rango de fechas. El rango DEBE ser obligatorio: mientras no haya uno elegido, el sistema NO DEBE
  calcular ni mostrar totales y DEBE decir que falta elegirlo. La fecha de corte DEBE ser la fecha de
  facturación.
- **FR-062**: Las facturas en estado `anulada` DEBEN excluirse de **toda** cantidad y de todo importe
  acumulado, y esa exclusión DEBE escribirse como predicado de la consulta y no como un filtrado
  posterior.
- **FR-063**: El sistema DEBE ofrecer un panel con las facturas `vencida` y las que vencen dentro de
  los próximos **7 días corridos**, indicando de cada una el cliente, el número, el importe total y
  los días de atraso o de plazo. Las facturas `pagada` y `anulada` NO DEBEN figurar.
- **FR-064**: El sistema DEBE mostrar un mensaje explícito de "sin resultados" cuando un listado, un
  cuadro de totales o el panel de vencimientos no tiene filas, en vez de una tabla vacía sin
  explicación. Cuando el listado esté filtrando por un estado, el control DEBE mostrar explícitamente
  cuál: ninguna fila DEBE quedar oculta sin que la pantalla lo diga.
- **FR-065**: Ningún estado DEBE comunicarse sólo por color, y todo elemento atenuado DEBE llevar
  además la palabra que lo explica. Todo resultado que aparezca sin que la pantalla cambie —el
  guardado de un formulario, una carga de archivo, un cambio de página, un cambio de estado— DEBE
  anunciarse de forma accesible.

#### Acceso

- **FR-066**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados y DEBE
  resolverlo con **tres permisos**: uno de gestión de facturación, uno de consulta de facturación y
  uno de anulación. La autorización DEBE evaluarse por permiso y nunca por rol, y el menú DEBE
  resolver cada entrada sin código nuevo.
- **FR-067**: El permiso de **gestión** —configurar la empresa emisora, emitir, corregir y registrar
  el cobro— DEBE corresponder a los roles *Administración de la empresa* y *Administrador del
  sistema*. El de **consulta** —listado, ficha, descarga del documento de la factura, panel de
  vencimientos y totales— DEBE
  corresponder además a *Gerencia*. El de **anulación** DEBE corresponder únicamente a *Administrador
  del sistema*.
- **FR-068**: Quien no tenga el permiso correspondiente NO DEBE ver la acción, ni en el listado ni en
  la ficha, y el sistema DEBE rechazarla igual si se la invoca directamente. Ocultar el botón es una
  cortesía; la restricción es la del servidor.

### Key Entities *(include if feature involves data)*

- **FacturaCliente**: comprobante emitido a un cliente. Incluye número de comprobante (punto de venta
  + número, único entre las no anuladas), fecha de facturación, tipo de comprobante, tipo de
  facturación, condición de venta, período (mes y año), detalle, neto, IVA e importe total en pesos,
  CAE, vencimiento del
  CAE, fecha de vencimiento de pago, fecha de cobro cuando corresponde, motivo de anulación cuando
  corresponde, la copia congelada de los datos de la empresa emisora —CBU incluido (FR-034)—, la
  copia congelada de la razón social, el CUIT y el domicilio del cliente (FR-034a) y la referencia al
  **documento generado por el sistema**. Es la entidad principal del módulo: pertenece a exactamente
  un cliente y agrupa uno o más viajes.
- **EmpresaEmisora**: datos fiscales y de contacto de G&T Logística S.A. con los que sale toda
  factura. Incluye razón social, CUIT, domicilio, condición de IVA, número de ingresos brutos, fecha
  de inicio de actividades, punto de venta, CBU, teléfono, email y la referencia al logo. Es **única en
  todo el sistema**: se edita, nunca se crea una segunda ni se borra. Todos sus datos de texto —el
  CBU incluido— se copian a cada factura al emitirla; el logo no (FR-034).
- **CambioDeEstadoFactura**: registro de un cambio de estado o de una corrección de una factura.
  Incluye el estado anterior, el estado nuevo —los dos vacíos cuando la entrada es una corrección
  (FR-037)—, el usuario que lo produjo y el instante en que ocurrió. Pertenece a una única factura;
  una factura tiene muchos, empezando por el de su emisión. No se edita ni se borra.
- **Cliente**: empresa o persona a la que se factura. Es la misma entidad que administra el Módulo 5;
  este módulo la consume para elegir a quién se factura y no la administra. La factura guarda dos
  cosas: la **copia congelada** de su razón social, su CUIT y su domicilio al momento de emitir —que
  es lo que muestran la ficha, el listado y el documento— y la **referencia** al padrón, que sirve
  para vincular, filtrar y totalizar por cliente. Si después le corrigen la razón social en el
  padrón, la factura sigue mostrando la que tenía al emitirse (FR-034a).
- **Viaje**: unidad de trabajo facturada. Es la misma entidad que administra el Módulo 5; este módulo
  la consume para armar la factura y le agrega el estado `facturado`, la referencia a la factura que
  lo incluye y la exigencia de número de remito para rendirlo (FR-055a). Un viaje pertenece a lo sumo
  a una factura vigente.

### Enumerations

- **TipoComprobante**: `facturaA`, `facturaB`, `facturaC`. Lo elige el usuario al emitir (FR-008) y
  determina la alícuota de IVA (FR-023). Viaja en el JSON en camelCase, con su traducción al español
  en la capa de nombres de estado.
- **TipoFacturacion**: `original`, `refacturacion`. Lo elige el usuario al emitir (FR-009);
  `refacturacion` exige la referencia a una factura anulada del mismo cliente (FR-049). Su valor
  DEBE salir impreso en la banda de ejemplar del documento (FR-031).
- **CondicionDeVenta**: `contado`, `cuentaCorriente`, `tarjeta`, `cheque`. Lo elige el usuario al
  emitir (FR-009a) y queda congelado en la factura. Es un dato del comprobante y no del cliente: el
  mismo cliente puede pagar de una forma esta factura y de otra la siguiente.
- **EstadoFactura**: `pendiente`, `pagada`, `anulada` **almacenados**, más `vencida` **derivado**. La
  factura guarda uno de los tres primeros; `vencida` se calcula al leer, comparando la fecha de
  vencimiento de pago con el día en curso sobre una factura `pendiente` (FR-041). No es una columna y
  no existe un proceso que la escriba: es la misma decisión con la que el Módulo 5 resuelve
  `demorado` y los Módulos 3 y 4 los estados de vencimiento. `pagada` y `anulada` son **terminales**:
  ninguna transición sale de ellos (FR-043).
- **EstadoViaje** (del Módulo 5, ampliado): a los cuatro valores existentes —`pendiente`, `enCurso`,
  `rendido`, `anulado`— se les agrega `facturado`, posterior a `rendido` (FR-051). El valor nuevo se
  **agrega al final** de la enumeración y no reordena los existentes, porque los índices únicos
  filtrados de la tabla de viajes llevan esos valores escritos en su predicado.

### Relationships

- **EmpresaEmisora 1 — * FacturaCliente**: la configuración es única y sus datos se copian a cada
  factura al emitirla; la factura no vuelve a leerlos de la configuración (FR-034).
- **Cliente 1 — * FacturaCliente**: toda factura pertenece obligatoriamente a exactamente un cliente;
  un cliente puede tener muchas facturas o ninguna. Un cliente dado de baja conserva sus facturas y
  no se ofrece para emitir facturas nuevas (FR-011).
- **FacturaCliente 1 — * Viaje**: una factura incluye uno o más viajes, todos del mismo cliente y del
  mismo período; un viaje pertenece a lo sumo a una factura vigente (FR-053). Al anular la factura,
  sus viajes dejan de pertenecerle y vuelven a `rendido` (FR-048).
- **FacturaCliente 0..1 — 0..1 FacturaCliente**: una Refacturación referencia a exactamente una
  factura anulada del mismo cliente, y una factura anulada puede ser reemplazada por a lo sumo una
  Refacturación. El desplegable ofrece sólo las anuladas sin reemplazo y una restricción de unicidad
  sostiene el `0..1` (FR-049, FR-049a, FR-050).
- **FacturaCliente 1 — * CambioDeEstadoFactura**: toda factura tiene al menos un registro —el de su
  emisión— y acumula uno por cada cambio; cada registro pertenece a una única factura.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Partiendo de la empresa emisora sin configurar, Administración puede cargarla, emitir
  una factura agrupando viajes rendidos, registrar su cobro y consultar los totales del período sin
  intervención técnica.
- **SC-002**: El 100% de las facturas emitidas tiene un neto exactamente igual a la suma de los
  importes de los viajes que la componen, y ningún usuario puede escribir un neto, un IVA o un total
  distinto desde ninguna pantalla ni invocando la acción directamente.
- **SC-003**: El 100% de los viajes incluidos en una factura vigente queda en estado `facturado` y
  deja de ofrecerse para facturar; el 0% de los viajes figura en más de una factura vigente.
- **SC-004**: El 100% de los intentos de cargar un número de comprobante ya usado por una factura no
  anulada es rechazado con un mensaje que identifica la factura en conflicto, y ninguno crea nada.
- **SC-005**: Cuando dos administrativos confirman al mismo tiempo facturas que comparten un viaje,
  exactamente una se crea y la otra es rechazada nombrando el viaje y el comprobante que lo incluye;
  en ningún caso queda una factura creada con viajes sin marcar ni viajes marcados sin factura.
- **SC-006**: Ninguna factura queda emitida sin CAE, sin vencimiento de CAE, sin vencimiento de pago
  o sin al menos un viaje; el 100% de los intentos incompletos marca el campo faltante y no crea
  nada.
- **SC-007**: El 100% de las facturas emitidas muestra los datos de la empresa emisora y los del
  cliente tal como estaban al emitirse, aunque después se los haya corregido en la configuración o en
  el padrón del Módulo 5.
- **SC-007a**: El 100% de las facturas emitidas tiene su documento generado y descargable desde la
  ficha sin que nadie haya subido un archivo, y ese documento coincide dato por dato con la ficha,
  incluso después de corregirle el CAE, el detalle o los vencimientos y después de anularla.
- **SC-007b**: Lo que la vista previa muestra antes de confirmar coincide dato por dato y bloque por
  bloque con el documento que queda guardado al emitir, sin que la vista previa haya creado la
  factura ni guardado ningún archivo.
- **SC-008**: El 100% de los cambios de estado y de las correcciones queda registrado con el usuario
  que lo produjo y el instante en que ocurrió, y ese historial se puede leer desde la ficha sin
  consultas técnicas.
- **SC-009**: Ningún paso irreversible del módulo —emitir con un viaje en importe cero, emitir con
  fecha anterior a la de un viaje, anular— se ejecuta sin una confirmación explícita previa, y
  cancelar cualquiera de ellos deja los datos exactamente como estaban.
- **SC-010**: El 100% de las anulaciones tiene un motivo escrito, devuelve a `rendido` todos los
  viajes de la factura, y deja esos viajes disponibles otra vez para facturar.
- **SC-011**: El 0% de los importes de facturas anuladas figura en los totales facturado, cobrado o
  pendiente de cobro; y para cualquier filtro aplicado, la suma de los importes totales de las filas
  mostradas coincide con el importe facturado del cuadro de totales.
- **SC-012**: Una factura impaga cuyo vencimiento de pago ya pasó figura como `vencida` sin que nadie
  haya ejecutado ninguna acción y sin que exista ningún proceso programado que la actualice.
- **SC-013**: El 100% de los intentos de modificar el cliente, los viajes o los importes de una
  factura emitida es rechazado, cualquiera sea el rol de quien lo intente; y el 100% de los intentos
  de editar o anular un viaje `facturado` también.
- **SC-014**: Un usuario con el permiso de consulta únicamente no puede emitir, corregir, cobrar ni
  anular ninguna factura, ni desde la pantalla ni invocando la acción directamente; y un usuario con
  el permiso de gestión pero sin el de anulación no puede anular ninguna.

## Assumptions

- La autenticación, el catálogo de roles (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) y el esquema de permisos con menú resuelto por el servidor ya existen
  (Módulos 1 y 2); este módulo sólo agrega sus tres permisos y los asigna a los roles.
- El **empleado administrativo** del enunciado es el rol *Administración de la empresa* que ya existe
  en el sistema; no se crea un rol nuevo.
- El padrón de clientes y el registro de viajes provienen del Módulo 5 y se consumen tal como están:
  este módulo no agrega pantallas de alta, edición o baja de clientes ni de viajes.
- **Este módulo sí modifica el Módulo 5**, y es la única excepción a lo anterior: le agrega el estado
  `facturado` con sus dos transiciones, la referencia del viaje a su factura, la inmutabilidad
  correspondiente y la exigencia del número de remito para rendir (FR-051 a FR-055a). Lo primero es
  inevitable: sin el estado nuevo no hay forma de garantizar que un viaje se facture una sola vez. Lo
  del remito también: sale impreso en el detalle de la factura y el paso a `rendido` es el último
  momento en que el viaje se puede editar. Los cambios se acotan a esos **seis** requisitos (FR-056).
- El **domicilio del cliente y el número de remito del viaje son opcionales en el Módulo 5** y este
  módulo los necesita. Se resuelven distinto a propósito: el domicilio se exige **al facturar**
  (FR-011a), porque el cliente se puede editar siempre y no hace falta tocar el padrón; el remito se
  exige **al rendir** (FR-055a), porque después el viaje ya no admite edición. La contrapartida es que
  un viaje rendido sin remito de antes de esta regla queda sin poder facturarse, y se acepta como
  limitación conocida antes que reabrir la inmutabilidad del viaje rendido.
- El **estado `facturado` se agrega al final** de la enumeración de estados del viaje. Reordenarla no
  falla al compilar y dejaría los tres índices únicos filtrados de la tabla de viajes protegiendo el
  estado equivocado.
- El estado **`vencida` es derivado y no se almacena** (FR-041). El enunciado pide que la factura pase
  "automáticamente" a `vencida`; se resuelve calculándolo al leer, como el Módulo 5 resuelve
  `demorado` y los Módulos 3 y 4 los estados de vencimiento de documentación. Evita el proceso
  nocturno que habría que mantener al día y evita que una columna discrepe del hecho.
- La **anulación y la refacturación entran en esta versión**, con `anulada` como cuarto valor del
  estado de la factura. Es la primera de las tres decisiones que el enunciado dejó pendientes: sin
  ella, una factura mal emitida no tiene corrección posible y los viajes quedan facturados para
  siempre.
- La **fecha de vencimiento de pago se ingresa a mano** y es obligatoria (FR-030). Es la segunda
  decisión pendiente del enunciado: el `Cliente` del Módulo 5 no tiene condición de venta ni plazo de
  pago, y agregársela sería alcance de otro módulo. El sistema propone la fecha de facturación más 30
  días corridos y el usuario puede cambiarla.
- La entidad de la factura incorpora, respecto del modelo que traía el enunciado, el **cliente**, el
  **tipo de comprobante**, el **tipo de facturación**, el **período (mes y año)**, la **fecha de
  vencimiento de pago**, la **referencia de refacturación**, la **copia de los datos de la empresa
  emisora** y la **relación con los viajes incluidos**. Es la tercera decisión pendiente del
  enunciado.
- El **documento de la factura lo genera el sistema** en formato PDF, con una biblioteca, en el
  servidor (FR-031). El atributo de archivo que traía el modelo del enunciado —"guardar únicamente su
  URL"— es el **lugar donde se guarda ese documento generado**, no un adjunto que sube el usuario: la
  carga manual del comprobante de AFIP queda fuera del alcance. El archivo se guarda con el mismo
  mecanismo de los Módulos 3 y 4 —volumen fuera del repositorio, nombre generado por el sistema,
  descarga por endpoint autorizado que lo sirve en línea—, porque guardar una URL escrita a mano
  dejaría el archivo fuera de todo control de acceso.
- El documento generado **no reemplaza al comprobante fiscal** (FR-031c). La emisión ante AFIP/ARCA y
  la obtención del CAE siguen ocurriendo por fuera del sistema; el CAE se carga a mano y el documento
  lo lleva impreso. Es la representación de la factura, que es lo que se le manda al cliente.
- El documento **se regenera cuando se corrige un dato que aparece en él y al anular la factura**, y
  reemplaza al anterior (FR-031b). No se conservan versiones: el archivo y la ficha dicen siempre lo
  mismo. Guardar el histórico de documentos generados no lo pide ningún criterio de aceptación.
- El documento se puede descargar en **cualquier estado** de la factura, y cuando está `anulada` el
  propio documento lo indica junto con el motivo (FR-031d): un PDF anulado que circula sin decirlo es
  peor que no poder descargarlo.
- La **disposición del documento** (FR-031) sigue el formato de comprobante argentino que aportó el
  cliente del proyecto: banda de ejemplar, bloque del emisor con logo, recuadro de letra con su
  código, bloque de identificación, bandas de vencimiento de pago y de CBU, bloque del cliente, tabla
  de detalle y pie de importes con el CAE.
- La **condición de IVA del cliente es un texto fijo** en el documento, `Responsable Inscripto`
  (FR-031h). No se agrega al padrón del Módulo 5 ni se elige al emitir, porque todos los clientes de
  la empresa son empresas. Si algún día hubiera que facturarle a un consumidor final, agregarla al
  padrón es una spec aparte.
- La **condición de venta es un dato de la factura, no del cliente** (FR-009a). Las cuatro opciones
  son las formas de pago que maneja la empresa. No implica manejar cuenta corriente: el módulo sigue
  registrando únicamente si la factura está cobrada o no, y el saldo acumulado queda fuera de alcance.
- Los **códigos de comprobante** son `001` para `Factura A`, `006` para `Factura B` y `011` para
  `Factura C` (FR-031i). El comprobante de referencia que aportó el cliente del proyecto era una
  *Factura de Crédito A MiPyMEs* (código `201`), que es un régimen distinto: se tomó de ahí la
  **disposición**, no el tipo de comprobante, porque el alcance fija las tres opciones A, B y C.
- El campo `Remito` del bloque del cliente **queda vacío** (FR-031h): una factura agrupa varios
  viajes y cada uno lleva su propio remito en su fila del detalle.
- La columna `% Bonif.` sale siempre en `0,00` y la columna `Cantidad` siempre en `1`, con `UNIDAD`
  como unidad de medida: las bonificaciones están fuera de alcance y un viaje es indivisible.
- Los **subtotales por fila son informativos** (FR-031f). Los importes que mandan son los tres del
  pie, calculados según FR-022 y FR-023 sobre el neto entero; sumar los subtotales redondeados fila
  por fila puede dar unos centavos de diferencia, y en ese caso el pie es el que vale.
- El **logo no se congela en la factura**: se lee siempre de la configuración vigente. Congelar los
  datos de texto (FR-034) alcanza para lo que la regla protege —que una factura emitida no cambie de
  domicilio ni de CUIT—, y guardar una copia del archivo por cada factura agregaría complejidad sin
  un caso de uso que la pida.
- El **número de comprobante se ingresa a mano**, no lo genera el sistema: el comprobante se emite por
  fuera, en AFIP/ARCA, y el número que corresponde es el que quedó allá. El sistema propone el punto
  de venta configurado y valida el formato y la unicidad.
- La lista desplegable del **mes** del período ofrece los doce valores `01` a `12` y la del **año**
  ofrece exactamente `2025` y `2026` (FR-010). Son los años con operación cargada en el sistema: no
  hay facturación de períodos futuros porque no hay viajes rendidos en ellos, ni de años anteriores a
  2025 porque no hay viajes cargados. **Consecuencia declarada**: al empezar 2027 la lista se amplía
  en el código. Se acepta a cambio de no agregar una pantalla de configuración de períodos que ninguna
  FR pide, y por eso el año tampoco lleva una restricción en la base de datos, que exigiría una
  migración cada vez.
- La ventana del panel de vencimientos es de **7 días corridos** (FR-063). El enunciado dice "los
  próximos días" sin fijar un número; siete cubre la semana de trabajo con la que se organiza una
  cobranza y no exige configuración.
- El redondeo de los importes es **comercial a dos decimales** (la mitad va para arriba), y los
  importes se representan con un tipo decimal exacto, nunca con punto flotante: un total que alguien
  va a comparar contra una planilla no puede acumular error de representación.
- La paginación de 20 filas por página, el formato de respuesta paginada y el orden total del listado
  siguen la convención adoptada desde el Módulo 3.
- El **detalle** de la factura es un texto libre opcional que describe el concepto facturado; el
  detalle de los viajes incluidos no depende de él y sale siempre de los viajes.
- Un cliente dado de baja con viajes `rendido` sin facturar no se puede facturar hasta darlo de alta
  de nuevo. No se agrega una excepción al desplegable de clientes: el Módulo 5 ya permite dar de alta
  de nuevo a un cliente, y agregar clientes inactivos al desplegable de facturación abriría la puerta
  a facturarle a quien dejó de operar con la empresa.
- Las alícuotas de IVA están **fijas en el sistema** por tipo de comprobante —A 21%, B 21%, C 0%— y
  no se configuran desde ninguna pantalla. No hay caso de uso que pida cambiarlas, y una alícuota
  editable convertiría el IVA en un dato que alguien puede escribir, que es justamente lo que RN5 del
  enunciado prohíbe.
- Los estados `pagada` y `anulada` son **terminales** (FR-043) en cuanto al **estado**: la reversión
  de un cobro queda fuera de esta versión, porque el enunciado la prometía en el mensaje de rechazo
  de RN9 pero no la describía en ninguna transición ni en ningún criterio de aceptación. Habilitarla
  con registro de quién y cuándo queda anotada como candidata para una spec futura. Terminal no
  significa cerrado a la **corrección**: el CAE, su vencimiento, el detalle y el vencimiento de pago
  de una factura `pagada` se siguen pudiendo corregir (FR-035); sólo la `anulada` cierra también eso
  (FR-038).
- El registro de una corrección guarda **quién y cuándo**, no qué campos cambiaron ni sus valores
  anteriores (FR-037). Es lo que CL7 del enunciado pide literalmente, y una auditoría de valores
  sería una entidad que ningún otro módulo del sistema tiene.
- Quedan fuera del alcance de este módulo, tal como indica el enunciado: la emisión electrónica ante
  AFIP/ARCA y la obtención del CAE por web service, las
  notas de crédito y de débito, las facturas de varios períodos o de varios clientes en un mismo
  comprobante, las percepciones, retenciones, IIBB, descuentos, bonificaciones y recargos por mora,
  la facturación de conceptos que no sean viajes, la cuenta corriente del cliente con saldos
  acumulados, imputación parcial de pagos, recibos y medios de pago, la liquidación al transportista
  con sus órdenes de pago y movimientos de caja, el envío automático de la factura por email y el
  portal de autoconsulta, el registro contable, el libro IVA ventas y la exportación a sistemas
  contables, la facturación en moneda extranjera y la facturación recurrente automática.
