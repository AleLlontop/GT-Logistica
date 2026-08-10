<!--
Sync Impact Report
==================
Version change: 1.0.0 → 2.0.0
Modified principles: ninguno (los 5 principios core quedan sin cambios)
Removed sections:
  - Stack Tecnológico y Contenedores → relocada a /AGENTS.md (memoria técnica de sesiones), ya no
    vive en la constitución. Se clasifica como MAJOR porque elimina contenido de gobernanza que
    antes era autoritativo desde este documento.
Added sections: ninguna
Templates requiring review:
  - .specify/templates/plan-template.md — ⚠ pendiente revisión manual, sin acción en este comando.
Follow-up TODOs: ninguno
-->

<!--
Sync Impact Report (histórico)
==================
Version change: TEMPLATE → 1.0.0 (initial ratification)
Modified principles:
  - [PRINCIPLE_1_NAME] → I. Simplicidad Ante Todo
  - [PRINCIPLE_2_NAME] → II. Idioma y Mercado Argentino
  - [PRINCIPLE_3_NAME] → III. Cero Alcance Fantasma
  - [PRINCIPLE_4_NAME] → IV. Verificable por una Persona No Técnica
  - [PRINCIPLE_5_NAME] → V. Datos del Usuario con Respeto
Added sections:
  - Stack Tecnológico y Contenedores (reemplaza [SECTION_2_NAME])
  - Estructura del Repositorio (reemplaza [SECTION_3_NAME])
  - Governance (contenido completo)
Removed sections: ninguna (primer llenado del template)
Follow-up TODOs:
  - TODO(RATIFICATION_DATE): se asume 2026-08-04 (fecha de esta sesión) como fecha de ratificación
    original, ya que no existía una constitución previa ni fecha documentada anterior.
-->

# Sistema Integral de Gestión (G&T Logística) Constitution

## Core Principles

### I. Simplicidad Ante Todo
Ante dos soluciones que resuelven el mismo problema, el equipo DEBE elegir siempre la más
simple de implementar y mantener. Este proyecto se encuentra en su versión 1 (v1): NO se debe
anticipar complejidad para necesidades futuras hipotéticas (sin abstracciones prematuras, sin
configurabilidad "por las dudas", sin generalizar antes de tener un segundo caso de uso real).
Toda desviación de este principio DEBE justificarse explícitamente por escrito en la spec o en la
Pull Request correspondiente.
**Rationale**: anticipar complejidad antes de validar el producto con usuarios reales aumenta el
costo de desarrollo, el tiempo de entrega y el riesgo de construir algo incorrecto.

### II. Idioma y Mercado Argentino
Todo texto visible en la interfaz (etiquetas, mensajes de validación, notificaciones, correos,
documentos generados) DEBE estar en español de Argentina. Toda referencia monetaria DEBE
expresarse en pesos argentinos (ARS), con formato de moneda argentino (punto como separador de
miles, coma para decimales, símbolo "$").
**Rationale**: el producto se desarrolla exclusivamente para G&T Logística, una empresa
argentina; usar otro idioma o formato de moneda generaría fricción y errores de interpretación
para los usuarios finales.

### III. Cero Alcance Fantasma
NO se debe implementar ninguna funcionalidad, endpoint, campo, pantalla o regla de negocio que no
esté escrita explícitamente en la spec de la feature correspondiente. Si durante el desarrollo
surge una idea de mejora o funcionalidad no especificada, DEBE registrarse como propuesta para una
futura spec y NO debe construirse dentro del alcance actual.
**Rationale**: previene la expansión de alcance no controlada ("scope creep"), mantiene cada
entrega alineada a lo acordado con G&T Logística y facilita estimaciones y revisiones confiables.

### IV. Verificable por una Persona No Técnica
Cada criterio de éxito (aceptación) definido en una spec DEBE poder comprobarse operando la
aplicación —haciendo clic, completando formularios, leyendo pantallas— sin necesidad de leer
código fuente, logs técnicos ni ejecutar consultas SQL.
**Rationale**: quienes validan el sistema son usuarios de negocio de G&T Logística, no
desarrolladores; los criterios de aceptación deben ser ejecutables por ellos de forma autónoma.

### V. Datos del Usuario con Respeto
El sistema DEBE solicitar únicamente los datos imprescindibles para cumplir la funcionalidad
especificada (minimización de datos). Las claves, tokens, cadenas de conexión y cualquier secreto
NO deben incluirse en el código fuente ni quedar versionados en el repositorio; deben gestionarse
mediante variables de entorno o mecanismos de configuración externos al código.
**Rationale**: reduce la superficie de riesgo de seguridad y respeta la privacidad de los usuarios
y de la empresa.

## Estructura del Repositorio

La estructura de carpetas del repositorio es fija y todo módulo nuevo DEBE respetarla. NO se
crean carpetas ad-hoc fuera de este esquema sin antes actualizar esta constitución.

- `/frontend/src/modules/<nombre-modulo>/` — organizado por módulo de negocio (viajes,
  facturacion, liquidaciones, flota, etc.), nunca por tipo de archivo.
- `/backend/src/GT.Api`, `GT.Application`, `GT.Domain`, `GT.Infrastructure` — separados por capa;
  cada módulo de negocio tiene su carpeta espejo dentro de `GT.Application`.
- `/backend/tests/GT.UnitTests` y `GT.IntegrationTests` — proyectos de test separados.
- `/specs/<numero>-<modulo>/` — una carpeta por módulo, alineada 1 a 1 con las carpetas de módulo
  del frontend y del backend.

## Governance

Esta constitución tiene precedencia sobre cualquier otra práctica, convención o preferencia
individual del equipo. Toda Pull Request DEBE verificar cumplimiento de los cinco principios y de
las restricciones técnicas antes de ser aprobada; cualquier complejidad que se aparte del
Principio I DEBE justificarse explícitamente en la descripción de la PR o en la spec.

**Procedimiento de enmienda**: cualquier cambio a esta constitución (agregar, quitar o redefinir
principios, cambiar restricciones técnicas o la estructura de carpetas) requiere: (1) una
propuesta escrita del cambio, (2) la actualización de este documento con el nuevo número de
versión según versionado semántico, y (3) el registro del cambio en el Sync Impact Report al
inicio del archivo.

**Política de versionado semántico**:
- MAJOR: eliminación o redefinición incompatible de principios o gobernanza existentes.
- MINOR: adición de un nuevo principio o sección, o expansión material de una guía existente.
- PATCH: aclaraciones, correcciones de redacción o ajustes no semánticos.

**Revisión de cumplimiento**: los artefactos generados por Spec Kit (`/specs/<numero>-<modulo>/`)
y sus plantillas heredan y deben respetar estos principios; toda spec, plan o tarea que los
contradiga DEBE corregirse antes de avanzar a implementación.

**Version**: 2.0.0 | **Ratified**: 2026-08-04 | **Last Amended**: 2026-08-04
