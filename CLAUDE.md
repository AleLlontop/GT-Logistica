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

---
Las reglas de producto viven en `.specify/memory/constitution.md` y el estado del producto en `specs/README.md`.
