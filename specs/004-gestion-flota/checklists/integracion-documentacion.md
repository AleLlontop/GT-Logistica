# Checklist de calidad de requisitos: integración con el Módulo 3 y documentación de vehículos

**Purpose**: Validar que los requisitos **estén bien escritos** —completos, claros, consistentes y
medibles— en las dos áreas de mayor riesgo del módulo: lo que hereda o modifica del Módulo 3, y la
documentación de los vehículos con sus adjuntos.

**Created**: 2026-08-08

**Feature**: [spec.md](../spec.md)

**Audiencia y momento**: el autor, **antes de empezar a implementar**. Es cuando corregir un hueco de
la spec sale barato: `tasks.md` ya está escrito y todavía no hay una línea de código apoyada en una
suposición.

**Qué NO es esto**: no verifica que el sistema funcione —de eso se ocupan `quickstart.md` y los
tests—. Cada ítem pregunta si el **requisito está bien escrito**, no si el código lo cumple.

**Cómo usarlo**: si un ítem se responde con "no está escrito, pero yo sé cuál es la respuesta", eso es
un hueco. Anotalo abajo y decidí si va a la spec o si alcanza con que quede documentado en
`research.md`. El checklist complementa `requirements.md`, que ya validó la spec en general.

---

## A. Catálogo de tipos de documentación compartido (FR-017 a FR-017d)

- [ ] CHK001 ¿Está definido si el nombre de un tipo de documentación debe ser único en **todo el catálogo** o único **por ámbito**? [Gap, Spec §FR-009, §FR-017]
- [ ] CHK002 ¿Está especificado si la pantalla de mantenimiento del Módulo 3 debe seguir mostrando por defecto los tipos de **ambos** ámbitos, o sólo los de chofer? [Clarity, Spec §FR-017, §FR-017a]
- [ ] CHK003 ¿Es medible el "informando cuántos son" de FR-017b cuando los documentos vienen de dos orígenes: se informa un total único o discriminado entre choferes y vehículos? [Clarity, Spec §FR-017b]
- [ ] CHK004 ¿Está definido qué permiso rige el ABM de tipos de documentación ahora que el catálogo sirve a dos módulos con permisos distintos? [Gap, Spec §FR-017, §FR-039]
- [ ] CHK005 ¿Está especificado qué debe ver un usuario habilitado en flota pero **no** en choferes cuando necesita un tipo de documentación de ámbito vehículo que no existe? [Coverage, Gap]
- [ ] CHK006 ¿Están definidos los requisitos de **reversión** de la migración del ámbito si hay que volver atrás? [Gap, Exception Flow, Spec §FR-017c]
- [ ] CHK007 ¿Está definido el comportamiento del formulario de documento de vehículo cuando **no hay ningún tipo de ámbito vehículo cargado**, al modo en que US2 esc. 6 y 7 lo definen para tipo de vehículo y transportista? [Gap, Coverage, Spec §FR-017a]

## B. Baja de transportista extendida (FR-008d)

- [ ] CHK008 ¿El requisito especifica si el mensaje informa las dos cantidades **por separado** o una suma de dependientes? [Clarity, Spec §FR-008d]
- [ ] CHK009 ¿Está definido el comportamiento cuando un transportista se da de baja **mientras** alguien está registrando un vehículo para él? [Coverage, Concurrencia, Gap]
- [ ] CHK010 ¿La spec explica por qué la baja de transportista mira sólo dependientes **activos** mientras la de tipo de vehículo (FR-010) y la de tipo de documentación (FR-017b) miran **todos**? [Consistency, Spec §FR-008d, §FR-010, §FR-017b]
- [ ] CHK011 ¿Están alineados FR-002 y FR-008f sobre cuál de los dos mensajes corresponde a cada caso de patente ya ocupada? [Consistency, Spec §FR-002, §FR-008f]

## C. Frontera del cambio sobre el Módulo 3

- [ ] CHK012 ¿La spec declara explícitamente qué del Módulo 3 **no** debe modificarse, o sólo enumera los dos cambios que sí? [Clarity, Spec §Assumptions]
- [ ] CHK013 ¿Existe algún requisito que defina cómo se detecta una **regresión** del Módulo 3 causada por estos dos cambios? [Gap, Non-Functional]
- [ ] CHK014 ¿Hay criterios de éxito medibles que cubran la parte de integración —la migración del ámbito (FR-017c) y la corrección de ámbito (FR-017d)—, o los SC sólo cubren el alcance propio del módulo? [Coverage, Gap, Spec §SC-001–SC-011]
- [ ] CHK015 ¿Están documentados los requisitos de rendimiento o volumen para el conteo de documentos por tipo, que ahora recorre dos orígenes? [Non-Functional, Gap, Spec §FR-017b]

## D. Cálculo del estado de un documento (FR-018 a FR-024)

- [x] CHK016 ¿Está definido si los "días de aviso" de un tipo son días **corridos** o **hábiles**? [Ambiguity, Spec §FR-019]
      → **Resuelto (2026-08-08): días corridos.** Sábados, domingos y feriados cuentan igual, y no se
      mantiene ningún calendario de feriados. Con 30 días de aviso las dos lecturas difieren en 12
      días de calendario. Asentado como **FR-019a** en la spec, más `data-model.md` y
      `contracts/flota-api.yaml`. Documenta la conducta que `CalculadorEstadoDocumento` ya tenía: no
      cambia el comportamiento de la documentación de choferes ya cargada.
- [ ] CHK017 ¿Está especificado el rango válido de días de aviso —mínimo y máximo— más allá de que cero es un caso límite declarado? [Gap, Spec §FR-019]
- [ ] CHK018 ¿Está definido si la fecha de **emisión** puede ser futura, o FR-018 sólo fija la relación entre emisión y vencimiento? [Gap, Edge Case, Spec §FR-018]
- [ ] CHK019 ¿Hay límites definidos para la fecha de vencimiento, o cualquier fecha futura es aceptable? [Gap, Edge Case, Spec §FR-018]
- [ ] CHK020 ¿Es objetivamente verificable FR-022 —"recalcular frente al día en curso"— por alguien que opera la aplicación, dado que no hay ningún proceso que ejecutar? [Measurability, Spec §FR-022]
- [ ] CHK021 ¿Está definido el criterio de desempate cuando dos documentos del mismo tipo tienen la **misma** fecha de vencimiento, que es lo que decide cuál manda según FR-024? [Gap, Spec §FR-024]
- [ ] CHK022 ¿Está especificado el orden en que la ficha debe mostrar los documentos de un vehículo? [Gap, Spec §FR-038]

## E. Archivos adjuntos (FR-016a, FR-025, FR-026, FR-029, FR-038)

- [x] CHK023 ¿Está definido qué pasa con el archivo **anterior** cuando se reemplaza el adjunto de un documento con éxito? [Gap, Spec §FR-026, §FR-029]
      → **Resuelto (2026-08-08): el archivo anterior se borra.** El borrado va *después* de confirmar
      la fila, igual que en la eliminación: si falla, queda un archivo huérfano —invisible para quien
      opera— y nunca una fila que apunta a un archivo que no está (convención [003]). Asentado en
      `contracts/README.md`, `contracts/flota-api.yaml` y las tareas T063 y T065.
- [ ] CHK024 ¿Está cuantificado el límite de "10 MB" —decimales o binarios— y en qué momento se mide? [Clarity, Spec §FR-025]
- [x] CHK025 ¿Está definido el comportamiento cuando el documento existe pero su archivo **no se puede leer**? FR-029 cubre la escritura, no la lectura. [Gap, Exception Flow, Spec §FR-029]
      → **Resuelto (2026-08-08): fuera de alcance, sólo se contempla la escritura.** No se agrega
      ningún requisito ni ninguna tarea. El camino de lectura cae en el `404` genérico que el contrato
      ya declara para la descarga, que es el mismo que recibe un documento sin archivo. Queda anotado
      para no reabrirlo.
- [ ] CHK026 ¿Están definidos los requisitos sobre el **nombre** de archivo que se muestra cuando el original es muy largo o trae caracteres no admitidos? [Gap, Edge Case]
- [ ] CHK027 ¿Es medible SC-011 —"ningún archivo se abre desde fuera del sistema"— con un criterio que quien valida pueda ejecutar sin herramientas técnicas? [Measurability, Spec §SC-011]
- [ ] CHK028 ¿Son consistentes FR-016a y FR-033 respecto a que la ausencia de adjunto **no** altera el estado general del vehículo? [Consistency, Spec §FR-016a, §FR-033]
- [ ] CHK029 ¿Está definido si el archivo adjunto de un documento sobrevive a la baja y posterior reactivación del vehículo, o sólo se afirma para el documento? [Coverage, Spec §FR-008, §FR-008e]

## F. Corrección y eliminación de documentos (FR-026 a FR-028)

- [ ] CHK030 ¿Está definido el comportamiento cuando se elimina un documento **mientras** otro usuario lo está corrigiendo? [Coverage, Concurrencia, Gap]
- [ ] CHK031 ¿La exclusión de auditoría declarada en Assumptions cubre explícitamente la **eliminación física** de documentos, que es irreversible? [Consistency, Spec §Assumptions, §FR-027]
- [ ] CHK032 ¿Está especificado si corregir el **tipo** de un documento ya cargado puede cambiarlo a un tipo de otro ámbito? [Gap, Spec §FR-026, §FR-017a]

## G. Trazabilidad y criterios de aceptación

- [ ] CHK033 ¿Todos los requisitos de documentación tienen un escenario de aceptación asociado, y las excepciones están **declaradas como tales** en vez de simplemente faltar? [Traceability, Spec §FR-029]
- [ ] CHK034 ¿Cada término del glosario de estados —`vigente`, `enRegla`, `sin documentación`— está definido una sola vez y usado con el mismo significado en toda la spec? [Consistency, Spec §Enumerations]

---

## Notes

- Tildar con `[x]` a medida que se resuelven; anotar el hallazgo debajo del ítem.
- **Un ítem sin tildar no bloquea la implementación por sí solo.** Bloquea si la respuesta cambia una
  tarea de `tasks.md`; si no, alcanza con dejar la decisión escrita en `research.md`.
- Complementa a [`requirements.md`](./requirements.md), que ya validó la spec en general y quedó
  cerrado. Éste mira dos áreas puntuales con más detalle.

### Dónde está la respuesta cuando la spec no la da

Varios de estos ítems los resolvió el diseño sin que la spec los dijera. Si al revisarlos coincidís
con lo decidido, la acción es **anotarlo en la spec o dar por buena la decisión de diseño**, no
reabrirla:

| Ítem | Ya resuelto en | Qué se decidió |
|---|---|---|
| CHK001 | `research.md` §3 | Nombre único global, no por ámbito |
| CHK003 | `contracts/flota-api.yaml` | Un total único (`cantidadDocumentos`) |
| CHK004 | `research.md` §7 | El ABM sigue bajo `choferes.gestionar` |
| CHK006 | `data-model.md` §Migración | La migración es reversible |
| CHK010 | `research.md` §8 | La asimetría es deliberada, con motivo |
| CHK016 | ✅ 2026-08-08 | **Días corridos**, sin calendario de feriados (FR-019a) |
| CHK021 | `research.md` §12 | Desempate por `Id` mayor |
| CHK022 | `contracts/README.md` | Agrupados por tipo, vencimiento descendente |
| CHK023 | ✅ 2026-08-08 | El archivo anterior **se borra**, después de confirmar la fila |
| CHK025 | ✅ 2026-08-08 | **Fuera de alcance**: sólo se contempla la escritura |

Los **tres huecos reales** que este checklist encontró quedaron cerrados el 2026-08-08, y dos de ellos
sumaron requisito a la spec:

| Hueco | Decisión | Dónde quedó |
|---|---|---|
| CHK016 | Días corridos, sin calendario de feriados | **FR-019a** (nuevo), `data-model.md`, `contracts/flota-api.yaml` |
| CHK023 | El archivo anterior se borra al reemplazarlo | **FR-026a** (nuevo), `contracts/`, tareas T063 y T065 |
| CHK025 | Fuera de alcance: sólo la escritura | Anotado acá, sin requisito ni tarea nuevos |

Los ítems que quedan sin tildar no son huecos: son decisiones ya tomadas en `research.md` o en los
contratos que faltaba confirmar contra la spec. Ver la tabla de arriba.
