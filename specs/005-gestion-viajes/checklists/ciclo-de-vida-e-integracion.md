# Checklist de calidad de requisitos: ciclo de vida, concurrencia e integración con los Módulos 3 y 4

**Purpose**: Validar que los requisitos **estén bien escritos** —completos, claros, consistentes y
medibles— en las dos áreas de mayor riesgo del módulo: el ciclo de vida del viaje con su historial y
su exclusividad de recursos compartidos, y todo lo que este módulo consume de los Módulos 3 y 4.

**Created**: 2026-08-10

**Feature**: [spec.md](../spec.md)

**Audiencia y momento**: el autor, **antes de correr `/speckit-tasks`**. Es el momento más barato para
cerrar un hueco: todavía no hay tareas descompuestas ni código apoyado en una suposición.

**Qué NO es esto**: no verifica que el sistema funcione —de eso se ocupan `quickstart.md` y los
tests—. Cada ítem pregunta si el **requisito está bien escrito**, no si el código lo cumple.

**Cómo usarlo**: si un ítem se responde con "no está escrito, pero yo sé cuál es la respuesta", eso es
un hueco. Anotalo en *Hallazgos* y decidí si va a la spec, si alcanza con `research.md`, o si queda
como decisión consciente. Varios ítems ya tienen respuesta en `plan.md` o en `contracts/`: en ese caso
lo que hay que decidir es si la respuesta merece subir a la spec, porque **una decisión que sólo vive
en el diseño no la protege ningún criterio de aceptación**.

---

## A. Ciclo de vida: completitud y claridad de las transiciones (FR-031 a FR-034, FR-037)

- [ ] CHK001 ¿Está definido qué debe informar el sistema cuando se intenta **editar un viaje `anulado`**? FR-018 fija el mensaje sólo para `rendido` y FR-017 acota la edición a `pendiente`/`en curso` sin decir qué se responde en el otro caso [Gap, Spec §FR-017, §FR-018]
- [ ] CHK002 ¿Está especificado si `anulado` es tan inmutable como `rendido` —datos, remito, importe, motivo— o sólo irreversible en su estado? US6 esc. 7 habla del estado, no de los datos [Clarity, Spec §FR-018, §FR-036]
- [x] CHK003 ¿Está definido si poner un viaje `en curso` exige que el chofer y el vehículo asignados sigan **activos** en sus padrones, o si alcanza con que estén asignados? FR-025 exige asignación y FR-030 admite explícitamente que la asignación sobreviva a la baja [Gap, Conflict, Spec §FR-025, §FR-030]
- [ ] CHK004 ¿Está definido qué significa exactamente "quedar disponibles para otros viajes" de FR-037, dado que `disponible` es además el nombre de un estado operativo del Módulo 4 con reglas propias? [Ambiguity, Spec §FR-037]
- [ ] CHK005 ¿Está especificado si poner un viaje `en curso` debe reflejarse en el **estado operativo del vehículo** en el Módulo 4, o si la flota sigue mostrando la unidad como disponible mientras está en la ruta? [Gap, Spec §FR-037, §Assumptions]
- [ ] CHK006 ¿Están definidos los requisitos de recuperación si un cambio de estado falla a mitad de camino —estado cambiado sin su fila de historial, o al revés—? [Gap, Recovery, Spec §FR-035]

## B. Historial de cambios de estado (FR-035)

- [ ] CHK007 ¿Está definido qué valor lleva el "estado anterior" en el registro **del alta**, que por definición no tiene uno? [Gap, Spec §FR-035]
- [ ] CHK008 ¿Está especificado si **asignar o reasignar** chofer y vehículo debe dejar rastro en algún historial? FR-035 sólo cubre cambios de estado, y la asignación es la operación que más cambia de manos [Gap, Coverage, Spec §FR-019, §FR-035]
- [ ] CHK009 ¿Está definido de qué reloj sale "la fecha y hora en que ocurrió": el del servidor, o un instante que el operador pueda informar? [Clarity, Spec §FR-035]
- [ ] CHK010 Para un viaje **retroactivo**, ¿está especificado si el historial debe reflejar cuándo ocurrieron realmente las transiciones o cuándo se cargaron? Tiene consecuencia directa sobre FR-039 [Gap, Spec §FR-016, §FR-035, §FR-039]
- [ ] CHK011 ¿Está definido qué muestra el historial cuando el **usuario** que produjo el cambio fue dado de baja o cambió de nombre de usuario en el Módulo 2? [Coverage, Gap, Spec §FR-035]

## C. Señales derivadas: demora y carga retroactiva (FR-016, FR-039)

- [ ] CHK012 ¿"5 días corridos contados desde el instante" es medible sin ambigüedad: son **120 horas exactas** o **cinco días de calendario**? Las dos lecturas dan resultados distintos y el sistema deriva "hoy" en UTC−3 [Measurability, Ambiguity, Spec §FR-039]
- [ ] CHK013 Para un viaje cargado retroactivamente y puesto `en curso` hoy, ¿está definido si debe aparecer como demorado según su **fecha** o según su **historial**? [Coverage, Gap, Spec §FR-016, §FR-039]
- [ ] CHK014 ¿Está especificado **dónde** se señala la carga retroactiva —al guardar, en el listado, en la ficha, o en los tres— y con qué palabra? FR-016 dice "señalar explícitamente" sin fijar el lugar [Clarity, Spec §FR-016]
- [ ] CHK015 ¿Está definido si `demorado` debe poder **filtrarse** en el listado o sólo destacarse? FR-041 enumera cuatro filtros y no lo incluye [Gap, Spec §FR-039, §FR-041]

## D. Concurrencia y exclusividad de recursos compartidos (FR-026, FR-027, SC-005)

- [x] CHK016 ¿SC-005 es consistente con FR-027 al hablar de "dos operadores que intentan **la misma asignación** al mismo tiempo", cuando la exclusividad no la crea la asignación sino el pase a `en curso`? [Conflict, Spec §SC-005, §FR-026, §FR-027]
- [ ] CHK017 ¿Está definido el comportamiento cuando dos operadores actúan sobre **el mismo viaje** a la vez —uno lo rinde y el otro lo anula—? La spec sólo cubre la carrera por chofer y por vehículo [Gap, Coverage, Spec §FR-033, §SC-005]
- [ ] CHK018 ¿Está especificado qué ocurre si, entre la advertencia de FR-038 y su confirmación, otro operador le **completa el importe** al viaje, o lo anula? [Coverage, Gap, Spec §FR-038]
- [ ] CHK019 ¿Está definido si el rechazo por unidad ocupada debe nombrar el número del viaje que la ocupa incluso cuando quien lo intenta no tendría motivo para verlo? [Coverage, Gap, Spec §FR-026, §FR-052]
- [ ] CHK020 ¿Está especificado que la exclusividad alcanza a un viaje `en curso` de **cualquier fecha**, incluido uno cuya fecha pasó hace meses y nunca se rindió? Es el caso que FR-039 declara plausible [Coverage, Spec §FR-026, §FR-027, §FR-039]

## E. Asignación: alcance, momento y unidades dadas de baja (FR-019 a FR-021, FR-030)

- [x] CHK021 ¿Está definido si se puede asignar **sólo el chofer** o **sólo el vehículo**? FR-019 los nombra juntos, pero US4 esc. 2 supone un viaje "sin chofer o sin vehículo", estado que sólo se alcanza con asignación parcial [Conflict, Spec §FR-019, §FR-025]
- [x] CHK022 ¿Está especificado si reasignar un viaje **ya `en curso`** debe verificar que la unidad nueva no esté ocupada? FR-026 describe la exclusividad en el contexto del pase a `en curso`, y FR-019 permite reasignar sin mencionarla [Gap, Conflict, Spec §FR-019, §FR-026]
- [ ] CHK023 ¿Está definido si **desasignar** —dejar sin chofer o sin vehículo un viaje que ya los tenía— es una operación válida, y desde qué estados? [Gap, Coverage, Spec §FR-019]
- [ ] CHK024 ¿Está especificado qué debe ver el operador en la pantalla de asignación cuando la unidad **actualmente asignada** ya no figura entre las asignables, por baja o por cambio de estado operativo? [Coverage, Gap, Spec §FR-021, §FR-030]
- [ ] CHK025 ¿Está definido si un viaje `pendiente` con chofer dado de baja debe seguir ofreciendo la acción de poner en curso, o si primero exige reasignar? [Gap, Spec §FR-025, §FR-030]
- [ ] CHK026 ¿"Señalada como inactiva" de FR-030 está definido con una palabra concreta y un lugar concreto, o queda a criterio de quien implemente? [Measurability, Spec §FR-030, §FR-049]

## F. Habilitación por documentación contra la fecha del viaje (FR-022 a FR-024, SC-004)

- [ ] CHK027 Si hay **más de un** documento vencido, ¿está definido si el rechazo los nombra todos o sólo uno? FR-022 usa el singular "qué documento" [Clarity, Ambiguity, Spec §FR-022]
- [ ] CHK028 Ídem para la advertencia de FR-023 cuando hay **varios** documentos próximos a vencer, y para el caso en que chofer y vehículo tengan cada uno el suyo [Clarity, Spec §FR-023]
- [ ] CHK029 ¿Está definido si el bloqueo evalúa chofer y vehículo **por separado**, informando los dos motivos de una sola vez, o si corta en el primero que encuentra? [Clarity, Coverage, Spec §FR-022]
- [x] CHK030 ¿SC-004 es alcanzable dado que este módulo no controla los Módulos 3 y 4? Un documento **eliminado o corregido** después de la asignación puede dejar un viaje guardado con documentación vencida a su fecha sin que nadie de este módulo intervenga, y SC-004 dice "en ningún momento" [Conflict, Spec §SC-004, §FR-022a, §Assumptions]
- [ ] CHK031 ¿Está definido si FR-022a debe revalidar cuando el viaje tiene **una sola** de las dos unidades asignadas? [Coverage, Gap, Spec §FR-022a]
- [ ] CHK032 ¿Está especificado qué hacer con un viaje que **ya quedó guardado** con una asignación bloqueada a su fecha por un camino no previsto: se muestra advertido, se corrige, se ignora? [Recovery, Gap, Spec §FR-022a, §SC-004]

## G. Consistencia con las reglas de los Módulos 3 y 4 (FR-021, FR-024, FR-028)

- [ ] CHK033 ¿Está resuelta la aparente contradicción entre FR-024 —un vehículo sin documentos **no** bloquea— y la regla del Módulo 4 según la cual una unidad sin documentación no puede quedar guardada como `disponible`, que es el único filtro de la lista de asignables? Si el caso es inalcanzable para vehículos, ¿la spec lo dice? [Consistency, Spec §FR-021, §FR-024]
- [ ] CHK034 ¿Está especificado si la pantalla de asignación debe advertir que un vehículo ofrecido por su estado **guardado** figura hoy fuera de servicio en el Módulo 4 por documentación vencida? [Gap, Coverage, Spec §FR-021]
- [ ] CHK035 ¿Está definido si el **transportista** referenciado por un viaje debe señalarse como inactivo cuando se lo da de baja en el Módulo 3? FR-030 cubre chofer y vehículo, no transportista [Gap, Spec §FR-028, §FR-030]
- [ ] CHK036 ¿Está especificado qué transportista queda registrado si se asigna un chofer cuyo transportista está **dado de baja** en ese momento? [Coverage, Gap, Spec §FR-028]
- [ ] CHK037 ¿Está definido si el filtro por transportista y el cuadro de totales deben ofrecer transportistas **inactivos** que igual tienen viajes históricos? Sin eso, SC-010 no se puede cumplir para un transportista dado de baja [Gap, Spec §FR-041, §FR-046, §SC-010]

## H. Supuestos y conflictos por resolver antes de `/speckit-tasks`

- [x] CHK038 ¿Es sostenible el supuesto de FR-006 de que un cliente con viajes **rendidos** nunca pueda darse de baja, cuando US1 justifica la baja con "el que dejó de operar con la empresa" —que por definición tiene historial—? Tal como está escrito, el único cliente dado de baja posible es el que nunca operó [Conflict, Assumption, Spec §FR-006, §US1]
- [ ] CHK039 ¿Está validado el supuesto de que los Módulos 3 y 4 **no cambian**, incluido que un chofer con un viaje `en curso` se puede dar de baja sin que este módulo se entere ni lo impida? [Assumption, Spec §Assumptions, §FR-030]
- [ ] CHK040 ¿Existe un esquema de identificadores para los escenarios de aceptación de las historias, de modo que un requisito o un ítem de esta lista pueda apuntar a uno sin citarlo por su texto? [Traceability]

---

## Hallazgos

Anotá acá lo que cada ítem destape, con qué se decidió y dónde queda registrado.

**Resueltos el 2026-08-10** — seis ítems, todos los marcados `[Conflict]` de la primera pasada.

| Ítem | Hallazgo | Decisión | Dónde queda |
|---|---|---|---|
| CHK038 | FR-006 rechazaba la baja por cualquier viaje **no anulado**, incluidos los rendidos: el único cliente dado de baja posible era el que nunca operó, mientras US1 justifica la baja con "el que dejó de operar con la empresa" | La restricción mira sólo `pendiente` y `en curso`. Es el mismo criterio de "dependientes vivos" con que el M3 rechaza la baja de un transportista | `spec.md` FR-006, US1 esc. 6 y 8, SC-009, Edge Cases · `data-model.md` §Cliente · `contracts/` · `quickstart.md` paso 20 |
| CHK030 | SC-004 prometía "en ningún momento existe un viaje con documentación vencida a su fecha", cosa que este módulo no puede sostener: un documento corregido o borrado en el M3/M4 produce ese estado sin que intervenga | La garantía se acota a las operaciones propias —asignar, reasignar, mover la fecha—. El límite queda declarado, no implícito | `spec.md` SC-004, FR-022a, Assumptions · `research.md` §15 · `plan.md` Constraints |
| CHK021 | FR-019 asignaba chofer y vehículo juntos, pero US4 esc. 2 suponía un viaje "sin chofer o sin vehículo", estado que sólo se alcanza con asignación parcial | **No hay asignación parcial**: los dos se asignan juntos, un viaje tiene los dos o ninguno. Nuevo FR-019b | `spec.md` FR-019b, US3 esc. 17, US4 esc. 2 · `data-model.md` §Lista de asignables · `contracts/` |
| CHK022 | La exclusividad estaba escrita alrededor del pase a `en curso`; reasignarle un chofer ocupado a un viaje ya andando dejaba dos viajes con la misma persona, y el índice único lo habría rechazado con un error que ninguna regla explicaba | La verificación corre en **los dos caminos**. Reasignar un `pendiente` no verifica: un pendiente no ocupa. Nuevo FR-026a | `spec.md` FR-026a, US3 esc. 16 · `research.md` §2 · `data-model.md` §Índices · `contracts/` · `quickstart.md` paso 12 |
| CHK016 | SC-005 hablaba de "dos operadores que intentan la misma asignación", pero por FR-027 la asignación no ocupa a nadie | Se reescribe nombrando el momento real de la carrera: poner en curso, o reasignar un viaje ya en curso | `spec.md` SC-005 |
| CHK003 | FR-025 pedía sólo que estuvieran asignados, y FR-030 admite que la asignación sobreviva a la baja de la unidad | Los dos tienen que estar **activos** para arrancar; si alguno se dio de baja, hay que reasignar. La documentación y el estado operativo **no** se revalidan: se controlaron al asignar | `spec.md` FR-025, US4 esc. 14 y 15, Edge Cases · `data-model.md` §Transiciones · `contracts/` · `quickstart.md` paso 19 |

**Consecuencia para `/speckit-tasks`**: la spec sumó **dos requisitos** (FR-019b, FR-026a) y **cuatro
escenarios de aceptación** (US3 esc. 16 y 17, US4 esc. 14 y 15). Los cuatro necesitan tarea propia y
test propio; ninguno estaba cubierto por el diseño anterior salvo por accidente del índice único.

**Quedan 34 ítems sin resolver.** No son huecos confirmados: son preguntas que todavía no se
respondieron. Varias ya tienen respuesta en `plan.md`, `research.md` o `contracts/`, y lo que hay que
decidir en cada caso es si esa respuesta merece subir a la spec.

## Notes

- Tildá los ítems a medida que se resuelven: `[x]`
- Un ítem tildado significa "el requisito está bien escrito", no "la funcionalidad anda"
- Un ítem que se resuelve modificando la spec **puede agregar tareas**: revisalo antes de dar el
  módulo por cerrado
- Los ítems marcados `[Conflict]` son los que conviene mirar primero: son los que, sin resolver,
  producen dos implementaciones defendibles y ningún criterio que decida entre ellas
