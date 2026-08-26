# Specification Quality Checklist: Adopción del sistema de diseño gt-ui (Módulo 8)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-25
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

### Iteración 1 — 2026-08-25

Dos ítems se revisaron con cuidado por la naturaleza de la feature:

- **"No implementation details"**: la spec nombra el sistema de diseño (`.claude/skills/gt-ui/`) y
  sus cuatro referencias. No es una fuga técnica: en una feature de diseño ese documento **es** el
  requisito de negocio, y la constitución lo declara obligatorio en su Principio VI. La spec no
  nombra ningún framework ni biblioteca; los valores se citan por su nombre de token.
- **"Success criteria technology-agnostic"**: SC-011 cuenta pruebas automáticas y FR-069 nombra cinco
  archivos de test. Se conservan porque la suite es el instrumento que la convención [007] fijó para
  demostrar que un rediseño no cambió el comportamiento, y porque nombrar los cinco archivos es lo
  que vuelve **contable** el único cambio autorizado sobre ella — la convención [006] pide
  exactamente eso: que la revisión pueda contar los cambios.

### Iteración 2 — 2026-08-25, después de las respuestas

Los dos marcadores quedaron resueltos y el checklist cierra completo.

- **FR-007 / FR-008 — contraste.** Se recalibran los cuatro valores que no llegan al mínimo y el
  cambio vuelve escrito a `tokens.md`. Los cuatro se midieron, no se estimaron: `faint` 2,63:1,
  `dim` 1,75:1, el gris de encabezado 2,90:1 y el texto de *Anulado* 4,32:1. El resto de la paleta
  pasa cómoda y se toma tal cual.
- **FR-040 a FR-047 — tablas.** Fusión completa y la columna `Acciones` desaparece. La entrada a la
  fila es el dato que se busca con la vista; la fila entera navega; lo secundario va a un menú `···`
  siempre presente y siempre enfocable. Lo fija la cuarta imagen de referencia,
  `references/choferes- padron.png`.

### Consecuencia registrada, no descubierta después

Mover las acciones de fila al menú `···` obliga a **20 líneas en 5 archivos de test** a abrir el menú
antes de tocar la acción. Está escrito en FR-069 con los cinco archivos nombrados, para que sea una
lista que se cuenta en la revisión y no una sorpresa durante la implementación. Cualquier otra prueba
que haya que tocar es señal de que algo se rompió.
