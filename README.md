# Sistema Integral de Gestión — G&T Logística

Aplicación web para que G&T Logística administre su flujo organizacional: viajes, facturación,
liquidaciones y flota.

**Estado**: Módulo 1 (autenticación de usuarios) implementado. El Módulo 2 (gestión de usuarios y
roles) está especificado y todavía no construido.

## Levantar el sistema

La primera vez hay que definir dos contraseñas. **No están en el repositorio y nunca deben estarlo.**

```bash
cp .env.template .env    # y completá las dos variables
podman compose up -d     # SQL Server + backend + frontend
```

La aplicación queda en `http://localhost:5173`. Al arrancar, el backend aplica las migraciones y
crea el catálogo de roles y permisos junto con el usuario `admin`.

| Variable | Para qué sirve |
|---|---|
| `GT_ADMIN_PASSWORD_INICIAL` | Contraseña del administrador inicial. Obligatoria **sólo mientras el usuario `admin` no exista**: una vez creado podés borrarla, porque la contraseña ya vive hasheada en la base |
| `GT_SQL_PASSWORD` | Contraseña de `sa` en el SQL Server de desarrollo |

Si falta `GT_ADMIN_PASSWORD_INICIAL` justo cuando hacía falta crear el administrador, el backend se
detiene con un mensaje explicando qué falta. Es el comportamiento buscado: preferimos que no arranque
antes que quedarnos con una contraseña por defecto que nadie cambia.

De ahí en más alcanza con `podman compose up -d`.

## Probar

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # tests de frontend
```

Los tests de integración necesitan el SQL Server del compose corriendo. Crean su propia base por
corrida (`GtLogistica_Test_<guid>`) y la borran al terminar, así que no tocan la base de desarrollo.
Leen `GT_SQL_PASSWORD` del `.env`.

## Estructura

```
backend/
├── src/
│   ├── GT.Api/             Endpoints, autenticación por cookie, autorización por permiso
│   ├── GT.Application/     Casos de uso y mensajes en español rioplatense
│   ├── GT.Domain/          Entidades y reglas puras
│   └── GT.Infrastructure/  EF Core, hasheo, datos iniciales
└── tests/

frontend/src/
├── modules/autenticacion/  Un directorio por módulo de negocio, no por tipo de archivo
└── compartido/             Layout, menú y cliente HTTP

specs/                      Una carpeta por módulo: spec, plan y tareas
.specify/memory/            Constitución del proyecto
```

## Cómo se toman las decisiones

Las reglas de producto viven en `.specify/memory/constitution.md` y el estado de cada módulo en
`specs/`. Cada módulo pasa por spec → clarificación → plan → tareas → implementación.

Dos decisiones del Módulo 1 que conviene conocer antes de tocar el código:

- **La sesión es una cookie, no un token.** Los permisos se recalculan contra la base en cada
  petición, así que quitarle un rol a alguien con la sesión abierta surte efecto en su operación
  siguiente. Con un token autocontenido eso exigiría una lista de revocación. El razonamiento
  completo está en `specs/001-autenticacion-usuarios/research.md` §1.
- **El límite de intentos fallidos cuenta por origen *y* cuenta.** Contando sólo por origen, cinco
  errores de tipeo de personas distintas dejarían fuera a toda la oficina, porque todos salen por la
  misma conexión.
