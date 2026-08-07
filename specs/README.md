# Estado del producto

Una carpeta por módulo. Cada uno pasa por spec → clarificación → plan → tareas → implementación, y
su `tasks.md` es la fuente de verdad de qué está hecho y qué no.

> **Los identificadores de tarea se numeran desde uno en cada módulo.** `T059` es una cosa en
> `001-autenticacion-usuarios/tasks.md` y otra distinta en `003-gestion-choferes/tasks.md`. Nunca
> los nombres sueltos: van con su carpeta, como `[001] T059`.

| Módulo | Estado | Tareas |
|---|---|---|
| [001 — Autenticación de usuarios](001-autenticacion-usuarios/) | Implementado | 61 / 63 |
| [002 — Gestión de usuarios y roles](002-gestion-usuarios-roles/) | Implementado | 92 / 92 |
| [003 — Gestión de choferes y su documentación](003-gestion-choferes/) | Implementado y validado | 125 / 125 |

## Qué queda abierto

**Módulo 1.** Dos validaciones manuales, ambas en
[`001-autenticacion-usuarios/tasks.md`](001-autenticacion-usuarios/tasks.md):

- **`[001] T059`** — recorrer ingreso, error, reintento y cierre de sesión **sin tocar el mouse**, y
  corregir lo que falle.
- **`[001] T061`** — la corrida completa de su quickstart, anotando cada criterio de éxito.

**Módulo 3.** Nada. El recorrido completo de su quickstart se hizo con las dos cuentas, `admin` y un
usuario de Tráfico, y las siete historias quedaron verificadas operando la aplicación.

Además, `003-gestion-choferes/checklists/documentacion.md` tiene 25 ítems abiertos. Son deuda de
spec —preguntas que la especificación no responde— y no bloquean la implementación; si alguno se
resuelve, puede agregar tareas.

## Lo que el recorrido del Módulo 3 encontró

Vale anotarlo porque justifica seguir haciendo la validación manual aunque los tests estén en verde.
Tres defectos que ningún test veía:

- **El prefijo `/api` repetido** en los 19 servicios del frontend. Ninguna pantalla del módulo
  funcionaba. Los tests de pantalla mockean los servicios y los de backend no pasan por el cliente
  HTTP, así que entre los dos quedaba el hueco.
- **Las fechas corridas un día**, por interpretar un `yyyy-MM-dd` como medianoche UTC. Venía del
  Módulo 2 y afectaba a todo el padrón.
- **El aviso de renovación prometía** que el documento que se está cargando pasa a ser el vigente,
  cuando manda el de vencimiento más lejano.

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
