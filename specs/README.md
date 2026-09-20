# Estado del producto

Una carpeta por módulo. Cada uno pasa por spec → clarificación → plan → tareas → implementación, y
su `tasks.md` es la fuente de verdad de qué está hecho y qué no.

> **Los identificadores de tarea se numeran desde uno en cada módulo.** `T059` es una cosa en
> `001-autenticacion-usuarios/tasks.md` y otra distinta en `003-gestion-choferes/tasks.md`. Nunca
> los nombres sueltos: van con su carpeta, como `[001] T059`.

| Módulo | Estado | Tareas |
|---|---|---|
| [001 — Autenticación de usuarios](001-autenticacion-usuarios/) | Implementado y validado | 63 / 63 |
| [002 — Gestión de usuarios y roles](002-gestion-usuarios-roles/) | Implementado y validado | 92 / 92 |
| [003 — Gestión de choferes y su documentación](003-gestion-choferes/) | Implementado y validado | 126 / 126 |
| [004 — Gestión de flota](004-gestion-flota/) | Implementado y validado | 121 / 121 |
| [005 — Gestión de viajes](005-gestion-viajes/) | Implementado y validado | 134 / 134 |
| [006 — Gestión de facturación](006-gestion-facturacion/) | Implementado y validado | 125 / 125 |
| [007 — Rediseño de la aplicación](007-diseno-interfaz/) | Implementado, **falta la validación manual** | 110 / 126 |
| [008 — Adopción del sistema de diseño gt-ui](008-diseno-gt-ui/) | Implementado, **falta la validación manual** | 132 / 144 |
| [009 — Liquidación a transportistas](009-gestion-liquidacion/) | Implementado y validado | 105 / 105 |
| [010 — Gestión de adelantos de sueldo](010-gestion-adelantos/) | Implementado y validado | 93 / 93 |
| [011 — Gestión de caja](011-gestion-caja/) | Implementado, **falta la validación manual** | 88 / 89 |
| [012 — Emitir reportes](012-emitir-reportes/) | Implementado, **falta la validación manual** | 77 / 78 |

## Qué queda abierto

**Módulo 12.** Implementado con backend y frontend completos: **106 tests unitarios** del módulo —las
palabras de los estados, el nombre del archivo, la línea de filtros, el encabezado de FR-006, la traducción
a HTTP de los cuatro cuerpos de error y las cinco fuentes—, **38 de integración** —las dos bibliotecas
ejercitadas de verdad resolviendo el servicio del contenedor, la paridad fila a fila contra los cinco
listados, el `409` del tope con el tope bajado por configuración, el `500` con el armador doblado y la
matriz de cuatro roles por cinco reportes—, más **42 de frontend**. Las suites enteras en verde —500
unitarios y 958 de integración en backend, 492 en frontend—, con typecheck y lint limpios. **Falta el
recorrido manual de los 32 pasos del quickstart (T067)**, incluidos los que se comprueban con la calculadora
de la planilla.

Tres cosas anotadas, ninguna bloqueante:

- **El panel de Flota dice `Vigente` donde el de Choferes dice `Al día`** para el mismo estado del
  documento. `data-model.md` §3.3 daba por hecho que los dos mapas de TypeScript eran iguales; no lo son.
  Manda la regla operativa —cada `EnPantalla` copia literalmente el mapa de **su** pantalla—, así que el
  reporte de flota dice lo que dice su panel. La palabra no llega a ningún archivo: los dos paneles excluyen
  los documentos vigentes.
- **El `403` de los cinco endpoints lleva el texto del contrato del módulo.** El cuerpo del `403` lo arma el
  pipeline una sola vez para todo el sistema, así que un endpoint ahora puede declarar el suyo como
  metadata. No mueve ninguna decisión de autorización al handler: quien decide sigue siendo la política.
- **Dos llamadas existentes pasaron a nombrar el argumento de cancelación.** El parámetro opcional
  `tamanioPagina` va antes del `CancellationToken`, que es el orden de este código, y eso rompe una llamada
  posicional. Son dos líneas, en `ViajesEndpoints` y en `MovimientosDeCajaEndpoints`, sin cambio de
  comportamiento.

**Módulo 11.** Implementado con backend, base y frontend completos: **25 tests unitarios** de las reglas
puras y **84 de integración** del módulo —las tres carreras (doble apertura, dos cierres, movimiento contra
cierre), cada `CHECK` violado de a uno, el índice filtrado, el corte de los días de Argentina y la
invocación directa sobre la caja de otro—, más **54 de frontend**. La suite entera de backend y de frontend
en verde, con build y lint limpios. **Falta el recorrido manual de los 19 pasos del quickstart (T087)**, con
las cuentas de los cuatro roles y el paso 12 de dos pestañas.

Cuatro cosas anotadas, ninguna bloqueante:

- **El responsable dado de baja con la caja abierta** deja una caja que nadie puede cerrar: sólo el
  responsable opera su caja (FR-035) y no hay reapertura ni cierre por otro. La spec lo admite
  (§Assumptions); resolverlo es una spec futura.
- **`compartido/tipos.ts` sumó los siete códigos de error nuevos**, como el Módulo 10: es el décimo archivo
  modificado, que el plan no listaba.
- **El saldo inicial negativo no se puede tipear**: `InputImporte` descarta el signo al escribir, así que el
  error *"El saldo inicial no puede ser negativo."* sólo lo da el servidor ante una invocación directa. El
  test de la pantalla afirma que el signo se descarta.
- **`detalleDeError` va por su cuarta copia** (facturación, liquidaciones, adelantos, caja). Es la
  conversión del cuerpo al tipo de error de cada módulo y no un formato de presentación, así que [009] no la
  alcanza; unificarla tocaría tres módulos cerrados.

**Módulo 10.** Implementado con backend, base y frontend completos: **36 tests unitarios** del módulo —la
tabla entera de elegibilidad y el piso de la fecha cruzando el año— y **86 de integración** —con las dos
carreras, cada `CHECK` violado de a uno y los tres cambios de estado invocados directamente—, más **55 de
frontend** del módulo. La suite entera de backend y de frontend en verde, con build y lint limpios. El
recorrido manual de los 43 pasos del quickstart (T091) se hizo con las cuatro cuentas, incluidos los dos
pasos de dos navegadores (13 y 20).

Cuatro cosas anotadas, ninguna bloqueante:

- **`compartido/tipos.ts` sumó los siete códigos de error nuevos** a `CodigoError`. El plan no lo listaba
  entre los archivos modificados, pero es un punto de extensión que cada módulo toca —el 9 sumó los suyos—:
  sin eso los tests que construyen un `ErrorHttp` con un código del módulo no compilan.
- **La pastilla de estado del detalle va en la línea de contexto y no en la del título**, como la del detalle
  de liquidación. `contracts/README.md` la pide en la misma línea que `Adelanto`, y `EncabezadoDePantalla`
  recibe el título como texto; ponerla ahí es un cambio a una primitiva que ninguna tarea pedía.
- **El historial nombra al usuario por su `username`**, igual que el Módulo 9. El ejemplo del contrato dice
  `Gómez, Ramona`, que es un apellido y nombre que el usuario no tiene salvo que esté asociado a una persona.
- **El callout del rechazado, para quien sólo consulta**, conserva *"No se corrige ni se vuelve a
  presentar."* y quita sólo la instrucción: la oración entera mezclaba un dato y una indicación.

**Módulo 9.** Implementado con backend, base y frontend completos: **118 tests de backend** del módulo
—29 unitarios y 89 de integración, incluidas las cuatro carreras— y la suite entera de frontend en verde,
con build y lint limpios. El recorrido manual de los 43 pasos del quickstart (T102) se hizo con las
cuatro cuentas, incluidos los dos pasos de dos navegadores (15 y 25).

Tres cosas anotadas, ninguna bloqueante:

- `009-gestion-liquidacion/checklists/ciclo-de-vida-y-pagos.md` queda con **16 ítems abiertos**, todos
  deuda de spec: preguntas de redacción o de alcance —el total por fletero, qué pasa al agotarse la lista
  de años, un saldo de centavos— que la implementación no necesita resolver.
- **`TokenDeIdentificador` ganó `sinNumeral`**, opcional y aditiva. Los números de liquidación y de orden
  de pago llegan armados con su prefijo, y dibujados con el `#` del token se leían `#LQ-12`. Ninguna
  llamada anterior cambió; es un parámetro de una primitiva existente, no un componente nuevo.
- **Un test del Módulo 2 cambió su aserción**: `AsignarRolesTests.Gerencia_RecibeSuPrimerPermisoConElModulo5`
  fijaba que Gerencia tiene exactamente dos módulos de permisos, y FR-064 le suma
  `liquidaciones.consultar`. Es el mismo test que el Módulo 6 ya había actualizado por la misma razón, y
  sigue protegiendo lo mismo: Gerencia no recibe ningún permiso de gestión.
- **Las secuencias `NumeroDeLiquidacion` y `NumeroDeOrdenDePago` llevan `NO CACHE`**, como la del número
  de viaje. El plan no lo pedía; es la misma trampa del Módulo 5 —un apagado sucio salta la numeración de
  a mil— sobre números que se ven y se nombran en los mensajes.

**Módulo 1.** Nada. El recorrido de teclado y la corrida completa del quickstart se hicieron, y las
cinco historias quedaron verificadas operando la aplicación.

**Módulo 2.** Nada. Los doce pasos de su quickstart se recorrieron de nuevo, después de que el
Módulo 3 destapara un defecto que era suyo. Apareció uno más, el de las horas, y quedó arreglado.

**Módulo 3.** Nada. El recorrido completo de su quickstart se hizo con las dos cuentas, `admin` y un
usuario de Tráfico, y las siete historias quedaron verificadas operando la aplicación.

Después de cerrado le entró una tarea más, `T126`, desde el recorrido del Módulo 4: los escaneos se
descargaban en vez de verse, y el comportamiento era el mismo en los dos módulos. Es el segundo caso
en que el Módulo 4 destapa algo que era del 3 —el primero fueron las fechas corridas— y vale la misma
lectura de siempre: un módulo cerrado no es un módulo que no se vuelve a tocar.

Además, `003-gestion-choferes/checklists/documentacion.md` tiene 25 ítems abiertos. Son deuda de
spec —preguntas que la especificación no responde— y no bloquean la implementación; si alguno se
resuelve, puede agregar tareas.

**Módulo 4.** Nada. El recorrido completo de su quickstart se hizo con las dos cuentas, `admin` y un
usuario de Tráfico, y las seis historias quedaron verificadas operando la aplicación. Encontró dos
cosas, las dos arregladas (T120 y T121).

Quedan dos cosas anotadas, ninguna bloqueante:

- `004-gestion-flota/checklists/integracion-documentacion.md` tiene 31 ítems sin tildar. No son
  huecos: son decisiones ya tomadas en `research.md` o en los contratos que faltaba confirmar contra
  la spec, y el propio checklist lo dice. Los tres huecos reales que encontró se cerraron.
- **Tres discrepancias entre `contracts/flota-api.yaml` y lo implementado**, todas sobre endpoints del
  Módulo 3 que este módulo modifica. El contrato declara códigos de error y estados HTTP nuevos
  (`tipo_documentacion_en_uso` y `transportista_con_dependencias`, los dos con `409`) para rechazos que
  ya existían con otro nombre y con `400`, y renombra `documentosAsociados` a `cantidadDocumentos`. Se
  conservaron los nombres y estados existentes porque la spec acota los cambios al Módulo 3 a dos y
  `tasks.md` no pide ninguna de esas renombradas; cambiarlos rompería el frontend y los tests del
  Módulo 3 sin ganancia funcional. **Las cantidades sí se agregaron al cuerpo del error**, que es lo
  que SC-008 necesita. Si se prefiere seguir el contrato al pie de la letra, es una tarea acotada.

**Módulo 5.** Nada. El recorrido manual del quickstart (`T131`) se hizo con las tres cuentas —`admin`,
un usuario de *Tráfico* y uno de *Gerencia*— y las historias quedaron verificadas operando la
aplicación.

Tres cosas anotadas, ninguna bloqueante:

- `005-gestion-viajes/checklists/ciclo-de-vida-e-integracion.md` tiene **34 ítems sin resolver**. No
  son huecos confirmados: son preguntas de calidad de spec que el propio checklist dice que en varios
  casos ya están respondidas en `plan.md`, `research.md` o los contratos. Los seis que sí eran
  conflictos se resolvieron antes de implementar, y agregaron dos requisitos (FR-019b, FR-026a) y
  cuatro escenarios de aceptación.
- **La sesión pasó a devolver los permisos efectivos.** FR-052 pide que quien tiene sólo
  `viajes.consultar` no vea las acciones de escritura, y la sesión sólo traía los roles. Se agregó
  `permisos` a `SesionResponse` —por permiso y nunca por rol, según la convención [004]— porque sin
  ese dato la pantalla no puede cumplir el requisito. Toca un archivo del Módulo 1 que el plan no
  había previsto.
- **`DialogoConfirmacion` acepta la etiqueta del botón.** `contracts/README.md` fija el verbo de cada
  confirmación —`Dar de baja`, `Rendir sin importe`— y el diálogo compartido del Módulo 2 tenía
  `Confirmar` fijo. Es un parámetro opcional: ningún llamador anterior cambió.

**Módulo 8.** La adopción de gt-ui está implementada y la suite entera en verde —**285 tests en 43
archivos**, build y lint limpios—, pero **falta el recorrido manual**, que en esta feature es la
prueba principal por la misma razón que en el Módulo 7: lo que cambió es cómo se ve, y eso no lo mide
un test. Quedan 12 tareas, todas de verificación con la aplicación andando:

- **Los seis quickstarts anteriores con las tres cuentas (T107)** y el **recorrido completo sólo con
  teclado (T108)**: es lo único que puede descubrir que el rediseño se llevó puesto algo que la suite
  no ve — en particular todo lo que vivía en las diez columnas `Acciones`.
- **Lo que sólo se ve mirando**: las 42 pantallas contra las cuatro imágenes de referencia (T027), la
  salida visible en los 19 formularios (T055), los importes alineados en vertical (T091), lo atenuado
  con su palabra (T113a) y los bordes del quickstart a 1280 px (T120).
- **Lo que pide herramienta**: el contraste sobre las pantallas reales (T110) y el filtro de escala de
  grises sobre un listado (T113). La paleta ya está medida —los 18 pares de tokens y los 8 colores
  literales que quedan fuera de `@theme`, entre 4,51:1 y 18,88:1—; lo que falta es medirla compuesta.
- **Reducir movimiento** activado en el sistema operativo (T114), y el recorrido con teclado del `···`
  de una fila (T090).
- **Las 16 validaciones manuales que el Módulo 7 dejó pendientes (T122)**, ahora sobre el resultado de
  este módulo y no sobre el anterior.

Dos cosas que la medición con herramienta ya encontró y quedaron corregidas, anotadas porque son la
clase de defecto que sólo aparece midiendo:

- **El relleno del botón destructivo no llegaba al piso de contraste.** `componentes.md` decía
  *"idéntico al primario pero `bg-danger`"*, y `#E5484D` con la etiqueta en blanco encima mide
  **3,91:1** contra un piso de 4,5:1. Se pasó al rojo oscuro que la paleta ya tenía, `#C13A3E`
  (**5,33:1**): no cambia ningún valor, elige entre los dos rojos del sistema el que la medición
  admite. Volvió a la skill, en `componentes.md` y en `tokens.md`.
- **El lienzo se dibujaba dos veces en `/ingresar`.** `App` lo monta en la raíz, afuera de `Routes`, y
  la pantalla de ingreso montaba otro encima: los dos orbes se superponían y su alfa se duplicaba. Se
  ve como un fondo apenas más saturado y nada más — lo destapó contar los nodos, no mirar.

**Módulo 7.** El rediseño está implementado y la suite entera en verde —285 tests de frontend, 301 de
backend, build y lint limpios—, pero **falta el recorrido manual**, que en esta feature no es un
trámite: es la prueba principal. Quedan 16 tareas, todas de verificación con la aplicación andando:

- **La Parte C del quickstart (T124)**, que es recorrer enteros los seis quickstarts anteriores para
  comprobar que ningún comportamiento cambió. Es lo que mide SC-001 y lo único que puede descubrir
  que el rediseño se llevó algo puesto que los tests no ven.
- **Lo que sólo se ve mirando**: que los seis diálogos se vean iguales (T104), que el foco cicle
  dentro de un diálogo abierto (T103), que un mismo estado se vea igual en los tres paneles de
  vencimientos (T109), que un aviso no corra el contenido al aparecer (T110), y que una ficha
  inmutable comunique por qué no ofrece acciones (T097).
- **Densidad, anchos y teclado** (T111 a T117): 1280 px, 200 % de zoom, desplazamiento contenido en
  las tablas anchas, y el alta de factura completa sólo con teclado.
- **Contraste y escala de grises** (T120, T121): la paleta está calculada y verificada en frío —los
  diez pares dan entre 3,13:1 y 15,12:1—, pero falta medirla sobre las pantallas reales.

**Módulo 6.** Nada. Los 46 pasos de su quickstart se recorrieron con las tres cuentas —`admin`,
`admin.empresa` y `gerencia`— y las siete historias quedaron verificadas operando la aplicación.

Una cosa anotada, no bloqueante: `006-gestion-facturacion/checklists/documento.md` tiene **30 ítems
sin tildar**. Son deuda de spec sobre la disposición del documento —dónde sale el teléfono del
emisor, cómo se corta la tabla entre páginas, qué formato lleva el `% IVA`— y no bloquean nada de lo
implementado; los cinco que sí eran huecos reales se resolvieron editando la spec antes de las
tareas. `checklists/requirements.md` quedó cerrado, 16 de 16.

## Lo que encontraron los recorridos

Vale anotarlo porque justifica seguir haciendo la validación manual aunque los tests estén en verde.

**El Módulo 6**, una sola cosa, y no estaba en el código sino en el propio recorrido: **el CUIT de
ejemplo no pasaba la validación**. `contracts/README.md` y el paso 4 del quickstart pedían tipear
`30-71234567-8`, y el dígito verificador que cierra para `3071234567` es `1`. La regla estaba bien
implementada —rechazaba, como debía—; lo que estaba mal era el número que el recorrido mandaba
escribir. Corregido en los dos lugares. Sirve de recordatorio de que los datos de ejemplo de una spec
se validan igual que el código.

**El Módulo 4**, dos cosas, y las dos de una clase distinta a las anteriores: **no eran defectos**.
Estaban implementadas exactamente como la spec pedía, con sus tests en verde. Lo que el recorrido
mostró es que la spec pedía lo que no había que hacer, y eso ningún test lo puede ver, porque un test
verifica contra la spec:

- **Un tipo de vehículo dado de baja no se podía volver a dar de alta.** La spec pedía baja lógica y
  eso estaba: el tipo quedaba inactivo y no desaparecía del catálogo. Pero nunca dijo cómo se vuelve,
  así que un `Utilitario` bajado por error quedaba inactivo para siempre. Ahora el alta está en la
  edición del tipo, como acción propia y no como campo del formulario.
- **Los escaneos se descargaban en vez de verse.** *Abrir archivo* bajaba el PDF y había que abrirlo a
  mano. La spec sólo decía que el archivo se sirve por endpoint autorizado —que se cumplía—, y nadie
  había escrito qué tenía que pasar al hacer clic. Era una línea: `Results.File` con nombre de archivo
  escribe `Content-Disposition: attachment`. Afectaba también al Módulo 3, y se arregló en los dos.

La lección es distinta a la de los módulos anteriores: ahí el recorrido encontró código que no hacía
lo que la spec decía; acá encontró **spec incompleta**. Los dos cambios llevaron su ajuste de spec y
de contratos, no sólo de código.

**El Módulo 3**, tres defectos que ningún test veía:

- **El prefijo `/api` repetido** en los 19 servicios del frontend. Ninguna pantalla del módulo
  funcionaba. Los tests de pantalla mockean los servicios y los de backend no pasan por el cliente
  HTTP, así que entre los dos quedaba el hueco.
- **Las fechas corridas un día**, por interpretar un `yyyy-MM-dd` como medianoche UTC. Venía del
  Módulo 2 y afectaba a todo el padrón.
- **El aviso de renovación prometía** que el documento que se está cargando pasa a ser el vigente,
  cuando manda el de vencimiento más lejano.

**El Módulo 2**, uno solo, y es el mismo error que el anterior un escalón más abajo:

- **Las horas corridas tres**, o sea UTC−3: el último acceso decía 17:07 cuando eran las 14:07. Las
  columnas `datetime2` no guardan zona horaria, EF Core devolvía el `DateTime` con `Kind` sin
  especificar y el JSON salía sin la `Z`, así que el frontend leía como local una hora que era UTC.
  En `fechaAlta`, que se muestra sin hora, un alta cargada después de las 21 aparecía directamente al
  día siguiente. Sirve como aviso: que el padrón mostrara bien el día no significaba que los
  instantes estuvieran bien.

## Lo que cada módulo dejó como precedente

Decisiones que exceden a su módulo y que conviene conocer antes de empezar el siguiente:

- **Módulo 1** — la sesión es una cookie con permisos revalidados en cada petición, no un token
  autocontenido. Quitarle un rol a alguien con la sesión abierta surte efecto en su operación
  siguiente.
- **Módulo 2** — el menú lo calcula el servidor: el frontend dibuja lo que recibe y no tiene lógica
  propia de permisos.
- **Módulo 3** — primera paginación del sistema (`items` + `total` + `pagina` + `tamanioPagina`) y
  primer módulo cuyo acceso no es exclusivo del administrador. También el primero que guarda
  archivos cargados por el usuario, con el volumen fuera del repositorio y la descarga por endpoint
  autorizado.
- **Módulo 5** — primer módulo con **ciclo de vida cerrado**, **historial de quién hizo qué**,
  **recursos compartidos que se ocupan y se liberan** y **dinero**. Las cuatro cosas se resolvieron sin
  maquinaria propia: un `switch` de transiciones, una tabla de tres columnas útiles, dos índices únicos
  filtrados y un `decimal` con un formateador de nueve líneas. También el primero que se apoya sobre
  **dos** módulos de negocio anteriores sin modificarles una tabla, una columna ni una pantalla, y el
  primero en el que una confirmación vive en el backend porque el paso no se deshace.
- **Módulo 4** — primer módulo que se apoya sobre otro módulo de negocio en vez de sobre la
  infraestructura común, y primero con **dos niveles de acceso adentro**: dos permisos, no un permiso
  y un chequeo de rol. También el primero que guarda un estado **y** lo deriva al leer, para conservar
  el motivo real de una parada sin necesitar un proceso nocturno. De su recorrido manual salieron dos
  reglas que rigen para todo el sistema: **cambiar el estado de una entidad es un recurso propio**, no
  un campo del `PUT` de edición, y **los adjuntos se sirven en línea**, con la decisión en el backend y
  no en el enlace, para que la misma acción se comporte igual en todas las pantallas.
- **Módulo 7** — primer módulo que **no agrega funcionalidad**: rediseña las 42 pantallas ya
  construidas sin cambiar qué hace ninguna. Su hallazgo transferible es de método: **congelando los
  textos, la suite existente pasa a ser la prueba de que el comportamiento no cambió**, porque las
  285 pruebas consultan por rol, etiqueta y texto y sólo tres líneas dependen de la estructura. Con
  esa red se reestructuraron 42 pantallas, se movieron las acciones de las cinco fichas del pie al
  encabezado y se migró el diálogo a Radix sin una sola regresión. También el primero que **incorpora
  dependencias de interfaz** —Tailwind, Radix, Lucide— con un límite escrito: ninguna puede
  reemplazar un control nativo que los tests operan.
- **Módulo 8** — primer módulo que **adopta un sistema de diseño ya escrito** en vez de inventar el
  suyo, y el que descubrió que un sistema de diseño **escrito mirando** no sobrevive a medirlo: la
  rampa de cinco grises tiene lugar para dos tonos de texto por debajo del secundario, no para cuatro,
  y el rojo que la skill daba como relleno del botón destructivo no llega al piso con su propia
  etiqueta encima. Su regla transferible es que **la skill que es fuente de un sistema de diseño
  también es su destino**: los cinco valores que la medición corrigió volvieron a `tokens.md` y a
  `componentes.md` con su medición y su fondo de medición al lado, porque una corrección que vive sólo
  en el código deja a la skill diciendo una cosa y a la aplicación haciendo otra.

- **Módulo 6** — primer módulo que **genera un artefacto**: el documento de la factura se arma con el
  mismo armador que la vista previa y sobre la misma entidad, y es **función de sus datos y de nada
  más** —ni siquiera del reloj—, así que dos armados del mismo comprobante dan los mismos bytes.
  También el primero que **congela** en un documento lo que salió impreso, con copia de los datos más
  referencia a la entidad de origen, y el primero que **cambia el comportamiento de operaciones de
  otro módulo** ya cerrado: la spec acota esos cambios a una lista numerada, y romper tests del
  Módulo 5 es la señal de que la lista hacía falta.
- **Módulo 9** — primer módulo con **cuatro carreras que cerrar** —generar, pagar, editar y anular—, y el
  que mostró que tres de ellas son sobre la misma fila: se cerraron con un único mecanismo, un `UPDATE`
  condicional sobre la liquidación con verificación de filas afectadas, más una `Version` para las dos
  ediciones que las otras condiciones no distinguen. También el primero en el que **el precedente del
  módulo anterior no se aplicaba**: copiar `Viajes.FacturaId` habría tocado el Módulo 5 y perdido los
  viajes de una anulada, y el vínculo fue a una tabla propia con marca de vigencia e índice filtrado. Y el
  primero que **lleva a `compartido` un formato copiado** en dos pantallas de módulos cerrados, con sus
  suites sin modificar como prueba —y un caso nuevo donde la suite no miraba el dato—.
- **Módulo 10** — primer módulo con una **regla sobre datos de tres módulos anteriores** —persona, ficha de
  chofer y transportista, empresa emisora— que la pantalla ofrece y el guardado valida con motivo: se
  escribió **una vez como función pura** sobre una proyección, y el desplegable la aplica en memoria. Es el
  primero que aplica las convenciones del 009 **sin inventar mecanismo**: sin edición, el `UPDATE`
  condicional sobre el estado cierra las cuatro carreras y no hizo falta `Version`. También el primero cuyas
  pantallas **distinguen el `403` de un error de carga**, y el que destapó que en un `CHECK` un `LEN` sobre
  una columna anulable deja pasar la fila si no va detrás de su `IS NOT NULL`.
- **Módulo 12** — primer módulo que **no agrega ninguna pantalla**: son cinco botones sobre pantallas que
  ya existen, y por eso su permiso es el primero que **no lleva entrada de menú**. El primero cuyo reparto de
  permisos **se invierte** —Gerencia y el administrador sí, los dos roles operativos no—, y el primero que
  exige **dos permisos juntos** sobre un mismo endpoint, como conjunción de requirements en una política y
  sin un `if` adentro del handler. También el primero que **declara una excepción a una regla anterior**: el
  artefacto lleva el instante de generación, al revés del documento de la factura, porque un reporte es la
  foto de un listado que cambia. Y el que descubrió que **las palabras en español de los estados no existen
  en el backend**: las clases `NombresDeEstado*` devuelven el código del JSON y las palabras viven sólo en
  TypeScript.
- **Módulo 11** — primer módulo cuyo candado protege **un `INSERT` en una tabla hija** y no una escritura
  sobre la misma fila: registrar un movimiento y cerrar empiezan con un `UPDATE` que no cambia nada sobre la
  caja, sólo para tomar su lock. El primero cuya confirmación **viaja con el número que confirma**
  —`saldoFinalConfirmado`—, porque no hay valor elegido por el usuario contra el cual confirmar. Y el primero
  que filtra **días locales sobre instantes UTC**, cortando el rango con el desplazamiento de Argentina.
