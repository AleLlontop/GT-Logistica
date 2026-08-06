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

- [003] Los enums viajan en el JSON en camelCase (`enRegla`, `proximaAvencer`), no en PascalCase; la traducción vive en `NombresDeEstado`
- [003] Los parámetros booleanos de query se declaran `bool?` con `?? false`: como `bool` a secas, pedir el listado sin el parámetro falla al enlazar en vez de tomar el valor por defecto
- [003] Toda paginación usa `{ items, total, pagina, tamanioPagina }` y ordena por un criterio **total** —terminando en `Id`—; sin eso, dos homónimos se intercambian entre páginas
- [003] Los estados derivables (vencimientos, semáforos) se calculan al leer, nunca se guardan en columna: evita el proceso nocturno que los mantendría al día
- [003] Cuando una regla derivada se ejecuta en dos lados —dominio en C# y consulta en SQL— va un test que compara las dos sobre el mismo dato
- [003] Los archivos cargados por el usuario van a un volumen fuera del repositorio, con nombre generado por el sistema, y se sirven por endpoint autorizado; el tipo se valida por la firma del archivo, no por la extensión ni por el `Content-Type`
- [003] Entre disco y base: el archivo se escribe antes de confirmar la fila y se borra después. Deja como único estado roto posible un archivo huérfano, nunca una fila que dice tener adjunto sin tenerlo
- [003] Los repositorios traducen las violaciones de índice único a excepciones de la capa de aplicación; las consultas previas cierran la ventana normal y el índice cierra la carrera
- [003] Las expresiones que EF Core tiene que traducir van escritas en el árbol, no extraídas a un método propio: extraerlas rompe la traducción y la consulta pasa a evaluarse en memoria
- [003] Un listado nunca oculta filas en silencio: si filtra por estado, el control muestra cuál
- [003] Los estados nunca se comunican sólo por color, y un elemento atenuado lleva además la palabra que lo explica

---
Las reglas de producto viven en `.specify/memory/constitution.md` y el estado del producto en `specs/README.md`.
