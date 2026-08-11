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
| [005 — Gestión de viajes](005-gestion-viajes/) | Implementado, falta el recorrido manual | 133 / 134 |

## Qué queda abierto

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

**Módulo 5.** Queda **el recorrido manual del quickstart** (`T131`), que hay que hacer con las tres
cuentas —`admin`, un usuario de *Tráfico* y uno de *Gerencia*—. Los cuatro módulos anteriores
encontraron ahí cosas que ningún test veía, incluidas dos en las que la spec pedía lo que no había que
hacer, así que no es un trámite de cierre. Todo lo demás está hecho y en verde: 217 tests unitarios,
544 de integración y 195 de frontend.

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

## Lo que encontraron los recorridos

Vale anotarlo porque justifica seguir haciendo la validación manual aunque los tests estén en verde.

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
