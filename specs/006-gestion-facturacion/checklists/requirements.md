# Specification Quality Checklist: Gestión de facturación (Módulo 6)

**Purpose**: Validar que la especificación esté completa y sea de calidad antes de pasar a planificación
**Created**: 2026-08-12
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### Marcadores abiertos: ninguno

Hubo **tres rondas de clarificación** el 2026-08-12, con 17 preguntas en total, todas escritas en la
sección **Clarifications** de la spec. Las tres primeras cerraron los marcadores que traía el
borrador:

1. **FR-023 — Alícuota de IVA por tipo de comprobante.** `Factura A` 21%, `Factura B` 21%,
   `Factura C` 0%. Una Factura C tiene total igual al neto.
2. **FR-043 / FR-043a — `pagada` es terminal.** No se agrega reversión del cobro; el rechazo de
   anular una factura cobrada informa la fecha del cobro y no promete un camino que no existe.
3. **FR-037 — El registro de una corrección guarda quién y cuándo**, como una entrada más del
   historial de estados. Sin auditoría de valores anteriores.

### Cambio de alcance posterior: el sistema genera la factura

El enunciado dejaba fuera de alcance "la generación del PDF de la factura" y pedía adjuntar a mano el
comprobante emitido. Se corrigió: el atributo de archivo del modelo era el **lugar donde se guarda la
factura generada por el sistema**, no un adjunto que sube el usuario.

- El sistema **genera el documento en PDF** con una biblioteca, en el servidor, al confirmar la
  emisión (FR-031), y lo guarda con el mecanismo de archivos de los Módulos 3 y 4 (FR-031a).
- **Se eliminó la carga manual del comprobante.** El CAE se sigue cargando a mano porque sale de
  AFIP.
- El documento **se regenera al corregir** el CAE, el detalle o los vencimientos, y reemplaza al
  anterior (FR-031b). No hay versiones guardadas.
- Lo que sigue fuera de alcance es la **emisión fiscal**: el documento generado es la representación
  impresa de la factura, no el comprobante ante AFIP/ARCA (FR-031c).

Es el único requisito del módulo que necesita una **biblioteca nueva**; conviene que `research.md`
elija cuál y verifique que resuelve el logo embebido, el formato de moneda argentino y el salto de
página de una tabla larga.

### Formato del comprobante

El cliente del proyecto aportó un comprobante de referencia y la disposición quedó escrita en FR-031
y FR-031e … FR-031i. Tres cosas que salieron de ahí y conviene no perder:

- **Una fila por viaje** en la tabla de detalle, nunca una fila consolidada (FR-031e). Es donde vive
  la trazabilidad viaje ↔ factura.
- El comprobante de referencia era una *Factura de Crédito A MiPyMEs* (código `201`), un régimen
  distinto al del alcance. Se tomó la **disposición**, no el tipo: los códigos son `001` / `006` /
  `011` para A / B / C (FR-031i).
- **El padrón de clientes no se amplía.** La condición de IVA del cliente es texto fijo y la
  condición de venta es un dato de la factura (FR-009a, FR-031h). El domicilio, que sí hacía falta y
  es opcional en el padrón, se exige **al facturar** y no se vuelve obligatorio allá (FR-011a).
- **El Módulo 5 sí suma un sexto cambio**, resuelto en la segunda sesión de clarificación: el número
  de remito pasa a ser obligatorio para **rendir** un viaje (FR-055a), porque sale impreso en el
  detalle de la factura y el paso a `rendido` es el último momento en que el viaje admite edición.
  Los cambios sobre el Módulo 5 son ahora los seis de FR-051 … FR-055a (FR-056).

### Decisiones tomadas sin marcador

Se resolvieron con la convención transversal ya escrita del proyecto o con el principio de
simplicidad, y quedan documentadas en la sección **Assumptions** de la spec: `vencida` derivado y no
almacenado; `facturado` agregado al final de la enumeración de estados del viaje; el adjunto y el
logo por el mecanismo de archivos de los Módulos 3 y 4 en vez de una URL escrita a mano; el logo no
congelado en la factura; vencimiento de pago manual obligatorio con propuesta de +30 días; ventana
del panel de vencimientos en 7 días corridos; número de comprobante ingresado a mano; tres permisos
en vez de dos.

### Alcance sobre el Módulo 5

Esta es la primera feature que **modifica un módulo de negocio anterior**. Los cambios están acotados
a FR-051 … FR-056 y declarados en Assumptions. Conviene que `/speckit-plan` los trate explícitamente
y que `research.md` verifique el impacto sobre los tres índices únicos filtrados de la tabla de
viajes.

- Los ítems sin tildar requieren actualizar la spec antes de `/speckit-clarify` o `/speckit-plan`.
