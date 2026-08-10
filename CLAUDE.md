# CLAUDE.md

## Qué es esto
Sistema Integral de Gestión: app web para que G&T Logística administre su flujo organizacional (viajes, facturación, liquidaciones, flota).

## Stack
- Frontend: React
- Backend: ASP.NET Core, en capas (Api / Application / Domain / Infrastructure)
- Base de datos: SQL Server
- Contenedores: Podman (modo compatible Docker) en desarrollo local, Docker nativo en CI — un único `docker-compose.yml` funciona en ambos entornos sin modificaciones

## Cómo arrancar y probar en local
```bash
podman compose up -d        # levanta SQL Server + backend + frontend
cd backend && dotnet test   # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test     # tests de frontend
```

## Convenciones
- Todo el producto (UI, mensajes, docs) en español argentino; moneda en pesos (ARS)
- Frontend por módulo de negocio en `frontend/src/modules/<modulo>/`, no por tipo de archivo
- Backend por capa (`GT.Api` / `Application` / `Domain` / `Infrastructure`); cada módulo tiene carpeta espejo en `GT.Application`
- Cero alcance fantasma: nada que no esté en la spec; ante dos soluciones, la más simple
- Sin claves ni secretos en el código; solo variables de entorno / config externa
- Al ejecutar `/speckit.plan`, SIEMPRE incluye en `plan.md`, como último paso de la fase final, un paso de mantenimiento: “Actualizar `CLAUDE.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (p. ej. ‘[003] ...’). No incluyas entradas por incluir, asegúrate siempre de que es información transversal y relevante para el proyecto que pueden aprovechar futuras features.”

## Decisiones transversales ya tomadas
Cada línea nació en una feature pero rige para todo el sistema. Antes de resolver algo parecido, mirar acá.

- [004] Un estado que el operador elige y el sistema puede contradecir se **guarda y además se deriva**: la columna conserva el motivo real —parado por reparación— y el valor mostrado se calcula al leer. Devolver los dos en la ficha, el derivado para mostrar y el guardado para editar, evita pisarle en silencio el motivo a quien opera
- [004] Cuando un módulo nuevo necesita la misma clase de dato que otro ya construido (documentación, adjuntos), se comparte la **regla** y el **almacén**, no necesariamente la **tabla**: una clave foránea anulable con un `CHECK` cambia una garantía de la base por una convención escrita a mano
- [004] Cambiar el estado de una entidad —darla de baja, darla de alta— es un **recurso propio**, nunca un campo del `PUT` de edición: así corregir un nombre no puede reactivar en silencio algo que estaba dado de baja. El alta no pide confirmación aparte —no destruye nada y se deshace con la baja, que sí la pide— y es idempotente
- [004] Un módulo con dos niveles de acceso adentro lleva **dos permisos**, no un permiso y un chequeo de rol en el endpoint: la autorización se evalúa por permiso y nunca por rol, y el menú resuelve cada entrada sin código nuevo
- [004] Un filtro de estado cuyos valores son complementarios dentro de un universo se escribe como **predicado único en la consulta**: así la exclusión es una garantía y no un filtrado posterior que alguien puede olvidar
- [004] Un rechazo por dependencias dice **cuántas y de qué clase**, en el mensaje y en el cuerpo del error; saber que hay dependientes sin saber cuántos no ayuda a resolverlo
- [003] Los enums viajan en el JSON en camelCase (`enRegla`, `proximaAvencer`), no en PascalCase; la traducción vive en `NombresDeEstado`
- [003] Los parámetros booleanos de query se declaran `bool?` con `?? false`: como `bool` a secas, pedir el listado sin el parámetro falla al enlazar en vez de tomar el valor por defecto
- [003] Toda paginación usa `{ items, total, pagina, tamanioPagina }` y ordena por un criterio **total** —terminando en `Id`—; sin eso, dos homónimos se intercambian entre páginas
- [003] Los estados derivables (vencimientos, semáforos) se calculan al leer, nunca se guardan en columna: evita el proceso nocturno que los mantendría al día
- [003] Cuando una regla derivada se ejecuta en dos lados —dominio en C# y consulta en SQL— va un test que compara las dos sobre el mismo dato
- [003] Los archivos cargados por el usuario van a un volumen fuera del repositorio, con nombre generado por el sistema, y se sirven por endpoint autorizado; el tipo se valida por la firma del archivo, no por la extensión ni por el `Content-Type`
- [003] Ese endpoint responde **`Content-Disposition: inline`** con el nombre original, más `X-Content-Type-Options: nosniff`: quien abre un adjunto lo quiere ver, no bajarlo y abrirlo a mano. Quién decide es el backend y no el enlace, así que la misma acción se comporta igual en todas las pantallas. Sólo es seguro porque el tipo salió de la firma y se limita a PDF/JPG/PNG — nada que el navegador pueda ejecutar como página
- [003] Entre disco y base: el archivo se escribe antes de confirmar la fila y se borra después. Deja como único estado roto posible un archivo huérfano, nunca una fila que dice tener adjunto sin tenerlo
- [003] Los repositorios traducen las violaciones de índice único a excepciones de la capa de aplicación; las consultas previas cierran la ventana normal y el índice cierra la carrera
- [003] Las expresiones que EF Core tiene que traducir van escritas en el árbol, no extraídas a un método propio: extraerlas rompe la traducción y la consulta pasa a evaluarse en memoria
- [003] Las fechas se formatean con `date-fns` desde `compartido/fechas`, nunca con `new Date(iso).toLocaleDateString()`: eso interpreta un `yyyy-MM-dd` como medianoche UTC y en UTC−3 muestra el día anterior
- [002] Todo instante sale del API con la `Z` que lo declara UTC, por una conversión declarada una sola vez en `GtDbContext.ConfigureConventions`: las columnas `datetime2` no guardan zona, EF Core devuelve el `DateTime` sin `Kind` y sin eso el JSON viaja sin zona horaria y el frontend lo lee como local. Un instante sin zona es un instante mal informado, aunque el número sea correcto
- [003] Un listado nunca oculta filas en silencio: si filtra por estado, el control muestra cuál
- [003] Los estados nunca se comunican sólo por color, y un elemento atenuado lleva además la palabra que lo explica

---
Las reglas de producto viven en `.specify/memory/constitution.md` y el estado del producto en `specs/README.md`.
