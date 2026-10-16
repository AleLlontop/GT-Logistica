# Implementation Plan: Gestión de proveedores y facturas de proveedores

**Branch**: `013-gestion-proveedores` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: `specs/013-gestion-proveedores/spec.md`, sus cuatro aclaraciones obligatorias y AGENTS.md.

## Summary

Incorporar un módulo Proveedores con padrón administrable y GFP_01/GFP_02. Proveedor normal guarda identidad y contacto propios; Fletero sólo referencia Transportistas para esos datos y requiere exactamente una liquidación Pendiente disponible de ese transportista. Neto obligatorio positivo, IVA fijo 21% y total calculado; coincidencia exacta con liquidación al registrar. Factura conserva identificación del proveedor e importe de liquidación históricos. No hay pagos ni edición/anulación de factura en esta entrega.

Reutilizar capas, sesión, repositorios EF, índices, UPDATE condicional, archivos privados y primitivas UI. El PDF se escribe primero y la factura/vínculo se confirman juntos en SQL; fallos previos al commit compensan el archivo. El residuo admitido es un huérfano invisible ante caída/borrado fallido, conforme AGENTS [003]; no se promete una transacción distribuida. Diseño detallado en [research](research.md), [data-model](data-model.md), [contracts](contracts/README.md) y [quickstart](quickstart.md).

**Ajustes técnicos 2026-10-16**: el protocolo compartido toma coordinación global, luego recursos SQL por CUIT normalizado anterior/propuesto en orden ordinal, incluso sin filas existentes, y recién después locks de filas; se detalla en research §2. Alta/edición de Proveedor y modificación de Transportista no pueden usar mecanismos u órdenes distintos. Metadata de archivo hereda HasMaxLength: ruta 400, nombre 255, tipo de contenido 100. POST de factura devuelve detalle completo; GET detalle/listado/PDF requieren exclusivamente consultar. La navegación postalta muestra el DTO del POST guardado sólo en un contexto/store en memoria del runtime React, por Id coincidente y sin GET; si no existe, ejecuta GET con consultar, sin ampliar cargar. No transportar el DTO en location.state/history.state ni persistirlo; una recarga completa lo pierde y el GET da 403 con sólo cargar. Todos los importes HTTP de 013 son cadenas canónicas de exactamente dos decimales (ImporteDecimal en TypeScript); C# mantiene decimal(18,2). El selector nativo de Transportista tiene búsqueda y páginas de 20 con Anterior/Siguiente.

## Technical Context

**Language/Version**: C#/.NET 10; TypeScript ~6.0.2, React 19.2; Node >=24 <25.

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core SqlServer 10.0.10; React Router 7.18.2, Tailwind 4, Vite 8, Radix Dialog, date-fns y primitivas existentes. Sin nuevas bibliotecas, sin QuestPDF para el PDF recibido.

**Storage**: SQL Server del compose; archivos bajo GT_ARCHIVOS_RUTA en volumen externo. Dos tablas nuevas: Proveedores y FacturasProveedor; vínculo escalar de factura a liquidación.

**Testing**: xUnit 2.9.3 y WebApplicationFactory en GT.UnitTests/GT.IntegrationTests; SQL Server real. Vitest 4.1, Testing Library, user-event/jsdom; build TypeScript y oxlint.

**Target Platform**: web de escritorio, ancho mínimo 1280 px heredado de 008; backend Linux en Podman/Docker, desarrollo Windows. Mismo compose, sin modificaciones.

**Project Type**: aplicación web por módulo de negocio, backend Api/Application/Domain/Infrastructure.

**Performance Goals**: SC-001: 95% de búsquedas visibles <2 segundos desde fin de escritura con 1.000 proveedores, incluyendo debounce/red/render. SQL filtra antes de paginar; 20 filas por página.

**Constraints**: no duplicar identidad/contacto de Fletero; sin nuevas reglas de pagos o CAE; sin secretos; fechas de factura/CAE DateOnly y hoy Argentina; índices y coordinación SQL para concurrencia; no guardar una factura parcial ni PDF referenciado cuya escritura haya fallado.

**Scale/Scope**: seis tipos de pantalla (padrón, formulario alta/edición, ficha proveedor, carga factura, listado factura, ficha factura), tres permisos; 37 FR y 7 SC. Sin cifras de disponibilidad/concurrencia adicionales no solicitadas.

## Constitution Check

*Puerta anterior a investigación y revisión posterior al diseño.*

| Principio | Antes de Phase 0 | Después de Phase 1 | Evidencia |
|---|---|---|---|
| I Simplicidad | Cumple | Cumple | Dos tablas, FK escalar; sin biblioteca nueva, sin pagos futuros |
| II Idioma/mercado | Cumple | Cumple | ARS, voseo, moneda/fechas compartidas |
| III Cero alcance fantasma | Cumple | Cumple | Trazabilidad FR, sólo dos cambios enumerados por FR-037 |
| IV Verificable sin código | Cumple | Cumple | quickstart con recorridos GFP_01/GFP_02 y los SC |
| V Respeto de datos | Cumple | Cumple | Contacto del origen no copiado; snapshots mínimos; archivos privados |
| VI Diseño gobernado | Dependencia restaurada antes de diseño | Cumple | GT UI y tokens leídos; primarias y estados por campo debajo |
| Estructura fija | Cumple | Cumple | Un módulo proveedores y carpetas espejo por capa |

La restauración autorizada usa primero specs/008-diseno-gt-ui, luego compartido/ui, index.css y pantallas consistentes. Archivos restaurados: `.claude/skills/gt-ui/SKILL.md` y `references/tokens.md`. No se modifica Constitución ni plan-template; patrones/componentes/contenido se documentan en SKILL para no depender de referencias o imágenes ausentes. Las decisiones técnicas están resueltas en research; no hay aclaraciones pendientes.

## UI Design Check

**Fuente aplicada**: [GT UI](../../.claude/skills/gt-ui/SKILL.md), [tokens](../../.claude/skills/gt-ui/references/tokens.md), feature 008 y primitivas vigentes. No agregar colores, CSS global ni cambiar interfaces existentes. Texto ink/ink-soft sobre canvas, dim sólo no textual, texto secundario faint sobre superficies admitidas. Reusar Campo, SeccionNumerada, BarraDeAcciones, Estado, Listado, Filtros, TablaDesplazable, Paginacion, FilaNavegable, FichaEncabezado/Cuerpo, AsideDeFicha y DialogoConfirmacion. Controles nativos conservados; autocomplete nuevo sólo dentro de proveedores.

### Acción primaria por pantalla

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| /proveedores | Nuevo proveedor | Buscar, filtros, limpiar, abrir ficha, menú de fila | Baja en menú con diálogo |
| /proveedores/nuevo | Guardar proveedor | Cancelar, Volver | — |
| /proveedores/:id/editar | Guardar cambios | Cancelar, Volver | — |
| /proveedores/:id | ninguna | Editar, Reactivar si corresponde, Volver | Dar de baja, confirmación |
| /proveedores/facturas/nueva | Guardar factura | Cancelar, Volver, quitar PDF antes de guardar | — |
| /proveedores/facturas | ninguna | Filtros, limpiar, abrir detalle, paginar | — |
| /proveedores/facturas/:id | ninguna | Abrir PDF, referencia de origen si autorizada, Volver | — |
| Diálogo de baja | Confirmar baja, variante destructiva | Cancelar | Confirmar baja |

La acción destructiva no es un segundo primario. La carga es entrada de menú independiente; no inventar primario en listado de consulta. Sin exportación ni impresión en esta feature.

### Estados de campo

Convención común: Campo con etiqueta exacta, required coherente con marca CSS, ayuda/error enlazados; `clasesDeControl`, anillo visible brand/offset y aria-invalid. Error después de interacción o envío, nunca al abrir; deshabilitar mientras guarda/carga sin borrar los valores. Cada fila aplica a ambas variantes alta/edición indicadas, incluyendo campo oculto cuando no corresponde. Datos calculados de sólo lectura no son entradas editables.

| Pantalla | Campo | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Alta/edición proveedor | Tipo | select medio con valor actual | Anillo común | Tipo inválido al guardar | Sin elección exige tipo |
| Alta/edición proveedor Fletero | Búsqueda de Transportista | input largo q, página 1 al cambiar texto | Anillo común | Error de consulta recuperable, conserva texto | Búsqueda sin restricción; carga/vacío anunciados |
| Alta/edición proveedor Fletero | Transportista | select nativo largo, sólo 20 de página actual; Anterior/Siguiente asociados | Anillo común, teclado nativo y foco en paginación | Vínculo inválido/falta configuración con salida; bloquear selección desactualizada | Obligatorio; limpiar Id si no está en el conjunto actual; sin candidatos se explica |
| Alta/edición proveedor normal | Razón social | input largo | Anillo común | Obligatorio no vacío | Etiqueta y ayuda; no error inicial |
| Alta/edición proveedor normal | CUIT | input corto, normalización existente | Anillo común | Inválido/duplicado en cuit | Obligatorio |
| Alta/edición proveedor | Nombre comercial | input largo | Anillo común | Mensaje de servidor si corresponde | Opcional |
| Alta/edición proveedor normal | Teléfono | input medio | Anillo común | Mensaje de servidor si corresponde | Opcional |
| Alta/edición proveedor normal | Email | input email medio | Anillo común | Formato informado inválido | Opcional |
| Alta/edición proveedor | Dirección | input largo | Anillo común | Mensaje de servidor si corresponde | Opcional |
| Padrón | Búsqueda general | input completo q | Anillo común | Fallo de consulta, conservar texto | Sin restricción textual |
| Padrón | Razón social (filtro) | input largo | Anillo común | Fallo de consulta | Sin restricción |
| Padrón | Nombre comercial (filtro) | input largo | Anillo común | Fallo de consulta | Sin restricción |
| Padrón | CUIT (filtro) | input corto | Anillo común | Error de consulta/filtro | Sin restricción |
| Padrón | Tipo (filtro) | select compacto | Anillo común | Valor desconocido | Todos |
| Padrón | Estado (filtro) | select compacto | Anillo común | Valor desconocido | Todos, mostrado explícitamente |
| Carga factura | Proveedor | combobox largo, texto + Id | Anillo común, flechas/Enter/Escape/Tab | Exigir selección; 403 sin reintento | Busca por los tres datos; sin coincidencias anunciado |
| Carga Fletero | Liquidación | select largo, número/período/total | Anillo común, teclado nativo | Selección obligatoria/disponibilidad | Sin disponibles explica y bloquea guardar; normal no dibuja campo |
| Carga factura | Número | input medio textual | Anillo común | Obligatorio/duplicado/capacidad | Obligatorio, sin formato fiscal impuesto |
| Carga factura | Fecha | input date corto | Anillo común | Obligatorio o posterior a hoy Argentina | Propuesta hoy Argentina editable |
| Carga factura | Detalle | textarea completo | Anillo común | Obligatorio | No error inicial |
| Carga factura | Neto | InputImporte corto, alineado derecha | Anillo común | Obligatorio, >0, precisión; diferencia contra liquidación | Sin importe; IVA/total no inventan un dato válido |
| Carga factura | CAE | input textual medio | Anillo común | Obligatorio | Sin validación fiscal adicional |
| Carga factura | Vencimiento CAE | input date corto | Anillo común | Obligatorio, >= fecha | Obligatorio |
| Carga factura | PDF | input file con ayuda PDF hasta 10 MB | Anillo común | Tipo/tamaño/escritura | Opcional, sin archivo elegido |
| Consulta facturas | Desde | input date corto | Anillo común | Fecha inválida/rango invertido | Sin límite inferior |
| Consulta facturas | Hasta | input date corto | Anillo común | Fecha inválida/rango invertido | Sin límite superior |
| Consulta facturas | Proveedor (filtro) | combobox largo incluye Inactivos | Anillo común y teclado | Texto sin selección no aplica Id; fallo recuperable conserva texto | Todos |
| Consulta facturas | Estado (filtro) | select compacto Pendiente/Todos | Anillo común | Estado desconocido | Todos |

Estado de proveedor no se edita como input: se muestra y cambia mediante acciones propias; Version viaja sin campo visual. Fletero muestra identidad/contacto de origen en bloque de lectura, nunca cuatro inputs editables. IVA/total se muestran formateados y no reciben tab stops de controles falsos.

Formulario proveedor con secciones Identificación y Contacto, omitiendo controles de origen según tipo. Carga factura con secciones Proveedor y liquidación, Datos de factura, Importes y comprobante; barra de acciones anclaje viewport. Ficha factura usa total como destacado en aside de 330 px; proveedor usa estado/identificación ya recibidos, sin inventar métricas. En consulta, cada número/razón social es el enlace real y fila entera sólo ayuda a mouse; sin tabIndex de fila. El detalle diferencia importe de liquidación al registrar y actual. Mensajes dinámicos en p con role status/alert, no tarjeta exterior.

La ficha posterior al alta usa el detalle completo del POST desde ResultadoFacturaContexto, montado sobre carga/detalle y estrictamente en memoria React: sólo el último DTO y su Id, limpiados al cerrar/cambiar sesión. Navegar por URL sin DTO en location.state/history.state. Si el Id coincide mostrar sin GET; si falta o difiere ejecutar GET, que exige consultar. La recarga completa crea contexto vacío y con sólo cargar el GET devuelve 403. No usar localStorage, sessionStorage, IndexedDB ni otra persistencia. No ofrecer PDF si falta consultar; mensaje 403 indica pedir permiso, sin reintento. El selector de Transportista usa q/pagina del endpoint: 20 por página, select nativo, Anterior/Siguiente, reset a página 1 al cambiar q y limpieza del Id si ya no pertenece al conjunto recibido. En edición iniciar q con CUIT actual; no precargar todos ni sustituir por autocomplete.

**Resultado posterior al diseño**: cumple; todas las entradas de contrato tienen estados, cada pantalla tiene como máximo una acción primaria y el patrón restaurado no depende de documentación inexistente.

## Project Structure

### Documentation (this feature)

```text
specs/013-gestion-proveedores/
  spec.md
  plan.md
  research.md
  data-model.md
  quickstart.md
  contracts/README.md
  checklists/requirements.md
```

`tasks.md` se genera después con speckit-tasks, no en este comando. Fuentes restauradas en `.claude/skills/gt-ui/SKILL.md` y `references/tokens.md`.

### Source Code (repository root)

```text
backend/src/GT.Domain/Proveedores/                 # Proveedor, FacturaProveedor, enums y reglas
backend/src/GT.Application/Proveedores/            # DTO/resultados, casos de uso, interfaces
backend/src/GT.Api/Proveedores/                    # endpoints, traducción HTTP
backend/src/GT.Infrastructure/Persistencia/        # repositorios y coordinación de identidad
backend/src/GT.Infrastructure/Persistencia/Configuraciones/
backend/src/GT.Infrastructure/Persistencia/Migraciones/
backend/tests/GT.UnitTests/Proveedores/
backend/tests/GT.IntegrationTests/Proveedores/
frontend/src/modules/proveedores/
  componentes/AutocompleteProveedor.tsx
  componentes/ResultadoFacturaContexto.tsx        # DTO temporal sólo en memoria React
  paginas/                                       # seis tipos de pantallas
  servicios/                                     # tipos, HTTP, validación/formato de entradas
```

**Structure Decision**: una feature y un módulo proveedores con entidades proveedor/factura; no carpetas frontend por tipo global ni cuatro módulos nuevos. Datos de origen se consultan por proyección SQL sin duplicar entidades.

## Phase 0 — Investigación resuelta

[research.md](research.md) cubre identidad/contacto, unicidad cruzada, representación de asociación, orden de locks, PDF/SQL, cálculos/fechas, permisos/UX y fuente restaurada. La investigación independiente ya verificó repositorios reales; todas las decisiones tienen alternativas y justificación. Fuente oficial para detalles EF y SQL enlazada en research, sin cambiar versiones instaladas.

## Phase 1 — Diseño y contratos

[data-model.md](data-model.md) define campos y garantías; [contracts/README.md](contracts/README.md) define rutas, cuerpos, permisos, errores y selección; [quickstart.md](quickstart.md) define comandos y recorridos verificables. Puertas constitucional y UI reevaluadas arriba. No modificar spec para agregar funcionalidades ni ejecutar implementación en esta fase.

### Mapa de reutilización

| Necesidad | Fuente existente | Aplicación en 013 |
|---|---|---|
| CUIT/externos | NormalizadorDocumentoNumerico, ValidadorCuit, Transportista, consultas de Liquidaciones | Normalización, identidad efectiva y comparación con empresa emisora |
| Cálculos | Facturacion/CalculadorImportes.IvaSobre con FacturaA (21%) | Regla de 21%, sin adoptar FacturaCliente ni emitir PDF |
| Fecha actual | Choferes/FechaHoyArgentina y compartido/fechas | Día Argentina una vez por operación, DateOnly |
| Archivos | CargarDocumento, IAlmacenDeArchivos, ValidadorDeArchivoPorFirma, ResultadoArchivo.EnLinea | Firma compartida restringida a PDF, escritura previa, compensación, inline/nosniff |
| Paginación | PaginaDe, consultas AsNoTracking del padrón/listados | Count+Skip+Take SQL, 20, orden terminado en Id |
| Locks | RepositorioLiquidaciones y RepositorioCaja | UPDATE condicional y sin cambio en padre, filas afectadas y relectura |
| Permisos | CodigosPermiso, SembradorInicial, CatalogoOpcionesMenu, PermisoRequirement | Tres permisos explícitos, políticas de permiso, menú servidor |
| Frontend | clienteHttp, InputImporte, fechas/moneda y primitivas UI | FormData, ImporteDecimal string y centavos BigInt; sobrecarga de moneda exacta para strings preservando callers number (AGENTS [005]); contexto de resultado en memoria, accesibilidad y diseño vigente |

### Cambios acotados en módulos existentes (FR-037)

1. **Transportistas**: integrar coordinación SQL de identidad en modificación; impedir CUIT que duplique otro proveedor efectivo. Mantener CUIT propio, contactos actuales y las demás validaciones, sin modificar Choferes/Flota. Tests de regresión sobre altas/ediciones originales y conflictos normales/Fleteros en ambos órdenes.
2. **Liquidaciones**: la asociación nueva toma su fila con UPDATE condicionado y relee bajo lock, compatible con el protocolo existente. No agregar restricción a editar/anular/pagar liquidaciones asociadas ni campos de ocupación a la entidad. Sólo adaptar superficie de consulta/persistencia si es necesaria para integrar la lectura consistente; si el repositorio de Proveedores puede hacerlo sin cambios a Liquidaciones, preferirlo. Tests de carreras contra las tres operaciones y regresión de sus reglas originales.

Integraciones transversales necesarias: registrar endpoints/DI, DbSets/configuraciones/migración, constantes/sembrado/menú y rutas/agrupamiento frontend. No son cambios de reglas de otros módulos. La skill se restaura por autorización expresa de esta sesión; no constituye rediseño.

## Fase final — Secuencia para implementación y validación posterior

1. Definir dominio y validaciones puras, DTO y repositorios; implementar configuración/migración de dos tablas, índices y CHECK. Sin precargas de proveedores. Probar CUIT, neto/IVA/total, fecha actual recibida por parámetro y restricciones con NULL.
2. Implementar padrón con proyección efectiva, coordinación de identidad y Version y selector nativo de Transportistas con q/paginación; integrar el único rechazo nuevo de CUIT en Transportistas. Probar carreras y regresión de contactos/dependencias.
3. Implementar carga multipart, PDF previo, transacción con lock de identidad → proveedor → Transportista si corresponde → liquidación/factura, compensación correcta y relectura postcommit; integrar consulta, búsquedas y servir PDF protegido. Tests real SQL/DI y fallos antes/después del commit.
4. Integrar permisos explícitos, catálogo y menú; implementar páginas y autocomplete local reutilizado, cálculos/preview/formato exactos con ImporteDecimal string y centavos BigInt, filtros SQL, mensajes y navegación postalta con contexto sólo en memoria; adaptar borde HTTP de 013 a cadenas monetarias sin cambiar otros módulos. Verificar teclado, regiones vivas y 403.
5. Ejecutar dotnet test; npm test/build/lint, y quickstart incluyendo 1.000 proveedores, contraste sobre fondos reales, casos límite y ausencia de Gestión de Pagos. Guardar evidencia; no declarar éxito si falla una puerta.
6. Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (p. ej. ‘[013] ...’). No incluyas entradas por incluir, asegúrate siempre de que es información transversal y relevante para el proyecto que pueden aprovechar futuras features.

## Complexity Tracking

Sin violaciones constitucionales. La coordinación SQL de identidad se justifica porque la unicidad cruza dos fuentes sin copiar CUIT; no agrega entidades, configuración comercial ni nuevas reglas. La compensación de PDF respeta el límite de atomicidad de archivos vigente y se prueba explícitamente.
