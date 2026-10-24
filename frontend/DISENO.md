# Identidad de interfaz G&T Logística

La propuesta usa naranja/ámbar como marca, grafito para navegación y blanco o
superficies oscuras para contenido. El rojo identifica el hover de navegación y los errores o acciones
destructivas. Los estados de negocio conservan sus textos e iconos; el color no
es su única señal. Esta dirección visual reemplaza el acento índigo del diseño
anterior, conservando los contratos funcionales y accesibles de los módulos.

## Fuente de colores

`src/index.css` es la única fuente de tokens de la aplicación. `@theme` define el
claro; `:root[data-theme='dark']` define las superficies, texto, bordes, estados y
sombras del oscuro. No se usan inversiones ni filtros para construir el tema.
La navegación tiene tokens propios de grafito en ambos modos. El SVG del favicon
es un recurso independiente y usa el mismo naranja de marca.

| Uso | Tokens |
|---|---|
| Lienzo y superficies | `bg`/`canvas`, `surface`, `surface-soft`, `surface-mute`, `surface-hover` |
| Texto | `text-primary`/`ink`, `ink-soft`, `muted`, `faint` |
| Marca en texto y foco | `brand`, `brand-deep`, `brand-soft` |
| Acción primaria | `brand-fill`, `brand-hover`, `on-brand` |
| Error/destrucción | `danger-text`, `danger-bg`, `danger-fill`, `danger-hover`, `on-solid` |
| Estados | `success`, `warning` y aliases `estado-*` |
| Navegación | `sidebar-*` |
| Banda superior | `topbar-bg`, `topbar-text`, `topbar-muted`, `topbar-hover`, `topbar-border`, `topbar-focus` |
| Gráficos | `chart-primary`, `chart-secondary`, `chart-accent` |
| Límites | `border`/`line`, `line-strong` |

El botón primario usa naranja con texto grafito: blanco sobre ese naranja no
alcanza el contraste mínimo. Texto y enlaces de marca usan un naranja más oscuro
en claro y más luminoso en oscuro. Las pruebas de contraste miden 4,5:1 para los
pares de texto y 3:1 para controles, trazos y foco. `dim` es sólo decorativo.

Los componentes consumen estos tokens, incluidos portales de menús y diálogos,
paginación, estados vacíos, formularios, campos de archivo y tooltips de Reportes.
Los blancos y colores fijos anteriores se retiraron de las primitivas. La variante
de `Boton` sigue siendo obligatoria; los controles nativos no se sustituyen.

## Preferencia de tema

`public/tema-inicial.js` se carga de forma bloqueante en el head, antes de React:
resuelve la elección antes del primer render para evitar un destello del tema
incorrecto. Es el único controlador de inicialización, persistencia y selección.

- Guarda únicamente `gyt.tema` (`light`/`dark`) en localStorage.
- Sin elección válida, usa `prefers-color-scheme` y sigue sus cambios.
- Una elección explícita tiene prioridad sobre el sistema.
- Sincroniza otras pestañas y tolera almacenamiento bloqueado sin impedir usar la app.
- `useTema` suscribe React al controlador; `SelectorTema` ofrece el control de teclado.
- `color-scheme` adapta también selects, casillas, fechas y otros controles nativos.
- Se respeta `prefers-reduced-motion`; las transiciones habituales duran 150–200 ms.

No se guardan datos de sesión ni de negocio en la preferencia visual. El selector
está en la topbar de las pantallas autenticadas; Ingreso usa la preferencia guardada
o la del sistema.

## Shell y navegación

Sidebar de grafito con desplazamiento independiente, contexto en la topbar,
usuario, selector de tema y cierre de sesión. La topbar usa rojo corporativo
`#b91c1c` en claro y `#a91b1b` en oscuro, con texto e iconos blancos y foco blanco;
sus controles usan un hover rojo más profundo. Los tokens se aplican también al
botón de menú móvil, sin alterar la navegación naranja del sidebar. El menú activo tiene fondo cálido,
texto e indicador naranja; el hover usa fondo rojo tenue y texto rojo claro. Se muestran todas
las opciones recibidas del servidor, incluidos códigos desconocidos. Agrupar no
agrega ni modifica permisos.

En móvil, el menú usa un drawer Radix con título accesible, foco retenido,
Escape y retorno del foco al disparador. El layout adapta todas las pantallas;
las tablas conservan desplazamiento horizontal dentro de sus contenedores. El
acceso «Ir al contenido» evita recorrer el menú al operar con teclado.

Los módulos se despliegan con botones nativos (`aria-expanded`/`aria-controls`),
un solo grupo abierto por vez. La ruta activa abre su grupo al navegar, incluidas
rutas de detalle. Los paneles cerrados no participan del foco. La expansión vive
en memoria; no se agrega almacenamiento persistente. El encabezado usa un lockup
tipográfico G&T Logística, ya que no hay un asset oficial en el repositorio.

Proveedores reúne padrón y facturas de proveedores; Personas incluye empleados,
choferes y sus vencimientos; Flota incluye vehículos, tipos y vencimientos.
Totales de viajes pertenece a Viajes; vencimientos de facturas y totales facturados,
a Reportes y seguimiento. Tipos de documentación queda en Configuración porque
es compartido por Choferes y Flota. No se duplican destinos.

Inicio muestra hasta ocho accesos de la sesión, con prioridad editorial para Viajes,
Facturas, Liquidaciones, Caja, Órdenes, Reportes, Flota y Proveedores. Si un rol recibe
otras opciones, se usan para completar sus accesos. No hay métricas inventadas ni
consultas nuevas. El resto de las opciones permanece en el sidebar. Los encabezados, etiquetas,
moneda, filtros, acciones y solicitudes de negocio no se renombran. Los gráficos
siguen mostrando sus tablas y valores exactos: sólo cambia su presentación.

## Verificación para revisión visual

```bash
npm run build
npm test -- --maxWorkers=2
```

La primera iteración pasó la suite completa con 108 archivos y 797 tests.
La segunda actualiza las aserciones de agrupación y plegado al nuevo pedido de UX,
y agrega cobertura de teclado, rutas activas y límites de accesos en Inicio. Incluye pruebas nuevas de preferencia, teclado, recarga, sincronización,
storage bloqueado, contraste, menú recibido y drawer móvil. La revisión en Chrome
cubrió claro/oscuro en Inicio, Clientes, Viajes, Flota, Empleados, Reportes,
formularios, modales, menús de fila, gráficos y tooltips. En 390 px se revisaron
listados, drawer, formularios y Reportes sin overflow de la página.

Para revisar el resultado en el entorno local, abrir la aplicación y alternar el
sol/luna de la topbar. Verificar también una recarga, una tabla con paginación,
«Generar reporte», un menú de fila y una pantalla a ancho móvil. Las capturas y
logs de ejecución son evidencia temporal y no forman parte del código fuente.
