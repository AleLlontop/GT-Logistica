# Quickstart: Rediseño de la aplicación (Módulo 7)

Cómo comprobar **a mano** que el rediseño hace lo que la spec dice — y, más importante que en
cualquier módulo anterior, que **no cambió nada de lo que el sistema hacía**. El recorrido está
pensado para que lo haga una persona de negocio: se opera la aplicación y se leen pantallas
(Principio IV).

Este quickstart tiene una forma distinta a los seis anteriores. Los otros verifican una
funcionalidad nueva; éste verifica **dos cosas a la vez**: que el sistema se ve como tiene que verse,
y que sigue haciendo exactamente lo que hacía. Por eso la Parte C es el recorrido de los seis
quickstarts previos, que acá es la prueba principal y no un trámite.

Al final hay una sección con lo que este recorrido no puede verificar y por qué.

---

## Antes de empezar

```bash
cp .env.template .env      # completá GT_SQL_PASSWORD y GT_ADMIN_PASSWORD_INICIAL
podman compose up -d       # SQL Server + backend + frontend
```

La aplicación queda en <http://localhost:5173>.

```bash
cd backend && dotnet test    # tiene que quedar en verde
cd frontend && npm test      # los 41 archivos existentes, en verde
```

**Si algún test de frontend falla, no sigas.** Esos 41 archivos son la prueba de que el rediseño no
se llevó nada puesto; con uno en rojo, el resto del recorrido no significa nada.

### Cuentas que hacen falta

Las mismas tres del Módulo 6, porque el reparto de permisos es lo que decide qué secciones de
navegación se ven:

| Usuario | Rol | Para qué sirve acá |
|---|---|---|
| `admin` | Administrador del sistema | Ve las cinco secciones de navegación |
| `trafico` | Tráfico | Ve un subconjunto: es lo que prueba que las secciones vacías no aparecen |
| `gerencia` | Gerencia | Sólo consulta: prueba que las acciones de escritura no aparecen |

### Datos que hacen falta

Para ver todos los estados hay que tener en la base, como mínimo: **una factura anulada**, una
pagada, una vencida y una pendiente; **un viaje rendido** y uno anulado; **un chofer dado de baja**;
**un documento vencido** y uno próximo a vencer. Si no están, se cargan siguiendo los quickstarts de
los Módulos 3, 5 y 6.

---

## Parte A — El marco (User Stories 1 y 2)

1. Abrí <http://localhost:5173>. **La pestaña del navegador** dice el nombre del sistema y muestra un
   ícono propio, no el genérico violeta de la herramienta con la que se creó el proyecto
2. Ingresá con `admin`. La pantalla de ingreso ya se ve rediseñada
3. En la pantalla de inicio: además del saludo y los roles, hay **accesos a lo que podés usar**. No
   hay que ir a la barra a buscar por dónde empezar
4. Mirá la navegación: las opciones están **agrupadas en secciones** con sus rótulos —*Operación*,
   *Padrones*, *Seguimiento*, *Configuración*, *Administración*—, no en una fila de catorce nombres
5. En *Seguimiento* están las tres entradas de vencimientos, **incluidas las de choferes y flota**,
   que antes no figuraban en ningún menú
6. Entrá a *Viajes*. La opción **y su sección** se distinguen de las demás. Tapá la pantalla con la
   mano dejando ver sólo el menú: se sigue sabiendo dónde estás sin depender del color
7. Mirá la pestaña del navegador: ahora dice `Viajes · Sistema Integral de Gestión`. Navegá a
   *Facturas* y confirmá que **cambia**
8. Recorré las cinco secciones. En todas las pantallas el encabezado tiene el mismo tratamiento y
   *Cerrar sesión* **no pesa más** que la acción principal de la pantalla
9. Achicá la ventana del navegador a 1280 px de ancho: nada se rompe. Agrandala a pantalla completa
   en un monitor ancho: el contenido **no se estira de borde a borde**
10. Cerrá sesión, ingresá con `trafico`. La navegación muestra **menos secciones**, y las que quedaron
    sin ninguna opción autorizada **no aparecen** — no aparecen vacías, no aparecen

---

## Parte B — Las pantallas (User Stories 3 a 7)

### Listados

11. Entrá a *Facturas*. El encabezado, los filtros, la tabla y la paginación se leen **como una sola
    pieza**, no como bloques apilados
12. Mirá la columna *Total*: los importes están alineados de modo que las comas y los puntos **caen
    en la misma vertical**. Compará dos importes de distinta cantidad de dígitos
13. Buscá una factura **anulada**: la fila se ve **atenuada** respecto de las demás y sigue
    diciendo *Anulada* con todas las letras. Leela: sigue siendo legible, no está borrosa
14. Mirá la celda del número de comprobante: se ve como acceso a un detalle, **no como el botón más
    importante de la pantalla**. Antes eran todos botones azules gruesos
15. Filtrá por un estado que no tenga ninguna factura: aparece el mensaje de sin coincidencias, con
    **tratamiento propio**, y no se confunde con una fila
16. Recargá la pantalla y mirá el instante de carga: el aviso de *cargando* se distingue del de
    listado vacío
17. Abrí *Choferes*, *Flota* y *Viajes*. Los cuatro controles de paginación **son el mismo control**.
    Antes eran cuatro implementaciones distintas
18. El control que dice qué se está mostrando —*"Mostrando todas las facturas, incluidas las
    anuladas"*— sigue diciendo lo mismo, palabra por palabra, integrado al listado

### Formularios

19. Entrá a *Facturas → Nueva factura*. Los campos están **agrupados por sentido** y los obligatorios
    se distinguen de los opcionales
20. Mirá los anchos: el punto de venta y el mes son cortos, el detalle es largo. Ningún campo ocupa
    el ancho completo porque sí
21. Apretá *Guardar* sin completar nada. Los errores aparecen **junto a cada campo**, el campo queda
    marcado por algo más que el color, y se ubican los tres sin leer el formulario entero
22. Mirá el pie: *Emitir* y *Cancelar* **se distinguen**. Antes eran el mismo botón azul
23. Entrá a *Nuevo viaje*, *Nuevo chofer* y *Nuevo vehículo*: las acciones están **en el mismo lugar**
    en los cuatro formularios

### Fichas

24. Abrí la ficha de una factura. El encabezado dice **qué número es, en qué estado está y qué se
    puede hacer con ella**, sin recorrer la pantalla. Antes las acciones estaban al pie
25. Las secciones —*Comprobante*, *Emisor*, *Cliente*, *Viajes incluidos*, *Importes*, *Documento*,
    *Historial*— se distinguen entre sí
26. El *Historial* se lee como una **secuencia en el tiempo**, distinta de las tablas de datos
27. Abrí la ficha de una factura **anulada**: se entiende de un vistazo que no ofrece acciones de
    escritura, y por qué. El motivo de anulación se lee como párrafo
28. Abrí la ficha de un **vehículo** cuyo estado guardado difiere del derivado: se distingue cuál es
    el que se muestra y cuál el que se edita

### Diálogos, avisos y estados

29. Dale de baja a un chofer, a un vehículo, a un cliente y a un usuario, y anulá un viaje y una
    factura, **cancelando las seis veces**. Los seis diálogos son **el mismo componente** con
    distinto contenido
30. Con un diálogo abierto, apretá `Tab` repetidas veces: el foco **cicla dentro del diálogo** y no se
    escapa al contenido de atrás. Apretá `Escape`: cierra, y el foco vuelve al botón desde el que
    abriste
31. Guardá algo con éxito: el aviso se ve **como confirmación**. Provocá un rechazo —un CUIT
    duplicado—: se ve **como rechazo**, y los dos se distinguen sin mirar el color
32. Fijate que al aparecer un aviso **no se corre de golpe** lo que estabas leyendo
33. Abrí los tres paneles de vencimientos —choferes, flota, facturas—. Un mismo estado se ve **igual
    en los tres**, y los semáforos de documentación **ahora tienen color**, cosa que nunca tuvieron
34. Mirá cualquier estado del sistema: la **palabra sigue estando**. Ninguno se comunica sólo por
    color

### Densidad, foco y anchos

35. Poné el navegador al **200 % de zoom** en el listado de facturas: el texto no se corta, no se
    superpone y **no aparece desplazamiento horizontal de la página**
36. Volvé al 100 % y achicá la ventana hasta que la tabla de ocho columnas no entre: el
    desplazamiento **queda dentro de la tabla** y el resto de la pantalla no se mueve
37. Recorré el alta de una factura **entera con el teclado**, sin tocar el mouse, desde el menú hasta
    emitir: en todo momento se ve **dónde está el foco**, incluso dentro de la tabla de selección de
    viajes y dentro de los diálogos de confirmación

---

## Parte C — Que nada haya cambiado (SC-001)

**Esta es la parte importante.** Recorré enteros los quickstarts de los seis módulos:

| Módulo | Quickstart | Pasos |
|---|---|---|
| 1 | `specs/001-autenticacion-usuarios/quickstart.md` | — |
| 2 | `specs/002-gestion-usuarios-roles/quickstart.md` | 12 |
| 3 | `specs/003-gestion-choferes/quickstart.md` | — |
| 4 | `specs/004-gestion-flota/quickstart.md` | — |
| 5 | `specs/005-gestion-viajes/quickstart.md` | — |
| 6 | `specs/006-gestion-facturacion/quickstart.md` | 46 |

38. En cada paso, lo que la pantalla **dice** y lo que **hace** tiene que coincidir con lo que el
    quickstart describe: los mismos textos, los mismos pasos, los mismos resultados. Lo único que
    puede diferir es **dónde está** cada cosa en la pantalla
39. Anotá cualquier diferencia de comportamiento. **Una sola alcanza para que el rediseño no esté
    terminado**: quiere decir que se cambió algo que FR-001 a FR-005 congelan

---

## Parte D — Contraste

40. Abrí las herramientas de desarrollo del navegador y usá su verificador de contraste sobre la
    lista de pares de [data-model.md §1.1](./data-model.md). Son diez pares y son los mismos en las
    42 pantallas, porque ninguna puede usar un color fuera de la paleta (FR-008)
41. Verificá que el peor caso de texto llega a **4,5:1** y el de los elementos no textuales que
    comunican información, a **3:1**
42. Sacá una captura del listado de facturas y convertila a **escala de grises**: los estados, la
    fila anulada y los errores se siguen distinguiendo (SC-012)

---

## Lo que este recorrido no puede verificar

| Qué | Por qué | Qué lo cubre |
|---|---|---|
| Que los 41 tests sigan pasando | Es lo primero del recorrido, no lo último, y no se hace mirando | `npm test` antes de empezar |
| Que un botón nuevo no pueda salir azul sin pedirlo | No se ve en pantalla: se ve cuando alguien agrega un botón dentro de seis meses | Lo garantiza el código, no el recorrido: la variante de `Boton` es un parámetro obligatorio y tipado, y sin declararla no compila |
| Que ningún valor quede fuera del sistema de tokens | Un color inventado se ve tan bien como uno del sistema | Buscar valores arbitrarios entre corchetes en el código: con utilidades, salirse del `@theme` deja una marca visible en la revisión |
| Que un módulo futuro aparezca en el menú sin tocar el frontend | No hay un séptimo módulo todavía | Un test del mapa de secciones con un código inventado |
| Que la fuente se sirva desde la propia aplicación y no desde un tercero | Se ve igual de las dos formas | Abrir con la caché vacía y mirar en las herramientas de desarrollo que ninguna petición salga a otro dominio |
| Que la preferencia de movimiento reducido se respete | Hay que cambiar una opción del sistema operativo | Activarla en el sistema y recorrer la Parte B |
| Que el PDF de la factura no haya cambiado | Es un archivo, no una pantalla | El test de igualdad byte a byte del Módulo 6 |
