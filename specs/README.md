# Estado del producto

Una carpeta por módulo. Cada uno pasa por spec → clarificación → plan → tareas → implementación, y
su `tasks.md` es la fuente de verdad de qué está hecho y qué no.

| Módulo | Estado | Tareas |
|---|---|---|
| [001 — Autenticación de usuarios](001-autenticacion-usuarios/) | Implementado | 61 / 63 |
| [002 — Gestión de usuarios y roles](002-gestion-usuarios-roles/) | Implementado | 92 / 92 |
| [003 — Gestión de choferes y su documentación](003-gestion-choferes/) | Implementado, con validación manual pendiente | 121 / 126 |

## Qué queda abierto

**Módulo 1.** Dos tareas de validación manual: el recorrido de accesibilidad con teclado (T059) y la
corrida completa del quickstart anotando cada criterio de éxito (T061).

**Módulo 3.** Las siete historias funcionan de punta a punta y los tests están en verde. Queda lo
que sólo se puede hacer operando la aplicación:

- **T123** — recorrer `003-gestion-choferes/quickstart.md` con las dos cuentas, `admin` y un usuario
  de Tráfico. Es la verificación que pide el Principio IV y no la reemplaza ningún test.
- **T126** — cargar **G&T Logística S.A.** como transportista con sus datos reales, desde la
  pantalla. No se siembra por migración a propósito: se carga como cualquier otro (FR-004).

Además, `003-gestion-choferes/checklists/documentacion.md` tiene 25 ítems abiertos. Son deuda de
spec —preguntas que la especificación no responde— y no bloquean la implementación; si alguno se
resuelve, puede agregar tareas.

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
