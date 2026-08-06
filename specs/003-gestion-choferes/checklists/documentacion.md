# Checklist de requisitos: documentación del chofer (Módulo 3)

**Purpose**: Validar la **calidad de los requisitos escritos** sobre la documentación del chofer —
sus reglas de vigencia y estado, y la custodia de los archivos adjuntos—. No verifica que el sistema
funcione: cuestiona si lo que la spec dice está completo, es claro, es consistente y se puede
comprobar.

**Created**: 2026-08-06

**Feature**: [spec.md](../spec.md)

**Alcance**: sólo los requisitos propios del Módulo 3. La autenticación, los roles y el padrón de
Persona se dan por resueltos en los Módulos 1 y 2.

**Rigor**: estándar, para pasar antes de `/speckit-tasks`.

**Cómo se usa**: cada ítem es una pregunta sobre el texto de la spec. Si la respuesta es "sí, está
escrito y no admite dos lecturas", se marca. Si es "no está" o "depende de cómo se lea", queda sin
marcar y se resuelve en la spec —no en el código.

## Completitud: reglas de vigencia y estado

- [x] CHK001 ¿Está definido qué tipos de documentación necesita tener un chofer para considerarse en condiciones, o el estado general depende sólo de lo que alguien haya cargado? [Gap, Spec §FR-029] — **Resuelto 2026-08-06**: se declara fuera de alcance en FR-029a y en los supuestos; ningún tipo es obligatorio y el estado informa lo cargado, no lo que falta. Candidato para una spec futura
- [ ] CHK002 ¿Define la spec qué documento manda cuando dos del mismo tipo comparten la misma fecha de vencimiento? [Gap, Spec §FR-020a]
- [x] CHK003 ¿Existen requisitos para corregir o dar de baja un documento cargado con datos erróneos? [Gap, Spec §FR-015] — **Resuelto 2026-08-06**: FR-015b permite corregir con las validaciones del alta; FR-015c permite eliminar con confirmación y borrado definitivo del documento y su archivo; FR-015d deja escrito que es la única entidad del módulo que se borra físicamente
- [ ] CHK004 ¿Está especificado que la ficha debe distinguir el documento vigente de los reemplazados, o sólo que los muestre a todos? [Gap, Spec §FR-024, §FR-020a]
- [ ] CHK005 ¿Está definido el criterio de orden del panel de vencimientos? [Gap, Spec §FR-021]
- [x] CHK006 ¿Hay requisitos sobre el número de documento? [Gap, Spec §FR-015] — **Resuelto 2026-08-06**: FR-015 lo fija obligatorio, hasta 50 caracteres y sin unicidad, porque una licencia conserva su número al renovarse
- [ ] CHK007 ¿Está definido si la fecha de emisión puede ser posterior a hoy? FR-016 sólo relaciona emisión con vencimiento [Gap, Spec §FR-016]
- [x] CHK008 ¿Existen requisitos para reactivar a un chofer dado de baja? [Gap, Spec §FR-005] — **Resuelto 2026-08-06**: FR-005b agrega la reactivación con confirmación; sin ella el chofer que vuelve quedaba inmanejable, porque el DNI único impide registrarlo de nuevo

## Claridad y medibilidad

- [x] CHK009 ¿Está definido contra qué huso horario se evalúa "hoy"? [Ambiguity, Spec §FR-017, §FR-019] — **Resuelto 2026-08-06**: FR-017a fija el día en curso en hora de Argentina (UTC−3), sin importar la zona del servidor
- [ ] CHK010 ¿"Recalcular frente al día en curso" está expresado de forma que se pueda comprobar operando la aplicación, sin leer código ni consultar la base? [Measurability, Spec §FR-019]
- [ ] CHK011 ¿El "peor estado" del chofer tiene un orden de precedencia explícito entre los cuatro valores? [Clarity, Spec §FR-029]
- [ ] CHK012 ¿El límite de los adjuntos está cuantificado con formatos y unidad de tamaño concretos, sin adjetivos? [Clarity, Spec §FR-015a]
- [x] CHK013 ¿"Documentación no respaldada" está definida como un valor observable en pantalla, o queda como una descripción sin lugar donde mostrarse? [Ambiguity, Spec §Edge Cases] — **Resuelto 2026-08-06**: la noción existe sólo a nivel de documento (*Sin respaldo*), no a nivel de chofer
- [ ] CHK014 ¿Está definido cómo se cuentan los "días que faltan o que pasaron" —desde qué fecha y con qué redondeo—? [Clarity, Spec §FR-021]

## Consistencia entre requisitos

- [x] CHK015 ¿El caso límite del documento sin archivo es consistente con los cuatro valores de FR-029? [Conflict, Spec §Edge Cases vs §FR-029] — **Resuelto 2026-08-06**: se quitó del caso límite la afirmación de que el chofer figura con documentación no respaldada; el estado del chofer queda con los cuatro valores y FR-029 declara que el adjunto no lo altera
- [ ] CHK016 ¿FR-020 y FR-020a conviven sin contradicción: conservar el historial y a la vez ignorarlo para el estado y las alertas? [Consistency, Spec §FR-020, §FR-020a]
- [ ] CHK017 ¿El estado del documento (`vigente`) y el del chofer (`en regla`) están diferenciados de modo que ningún término signifique dos cosas según dónde aparezca? [Consistency, Spec §Enumerations]
- [ ] CHK018 ¿FR-024 y FR-027 coinciden en quién puede abrir el archivo de un documento, sin dejar un resquicio entre "acceder al módulo" y "abrir el archivo"? [Consistency, Spec §FR-024, §FR-027]
- [ ] CHK019 ¿El escenario 7 de la User Story 3 y FR-020a describen el mismo comportamiento ante una renovación? [Consistency, Spec §US3, §FR-020a]

## Cobertura de escenarios y bordes

- [x] CHK020 ¿Están definidos los requisitos cuando la carga falla a mitad de camino: el documento se guarda y el archivo no, o al revés? [Gap, Exception Flow, Spec §FR-015a] — **Resuelto 2026-08-06**: FR-015e fija todo o nada, tanto al crear como al reemplazar el archivo de un documento existente
- [ ] CHK021 ¿Está especificado el comportamiento al cargar documentación cuando el catálogo de tipos está vacío o todos los tipos están inactivos? [Gap, Spec §FR-012, §US3]
- [ ] CHK022 ¿Los dos bordes ya declarados —vence exactamente hoy, y tipo con cero días de aviso— están escritos con criterio verificable y sin depender de la fecha en que alguien los pruebe? [Coverage, Spec §Edge Cases]
- [ ] CHK023 ¿Está escrito qué pasa con un documento cuyo tipo se quiso dar de baja después de cargarlo, y por qué esa situación no puede producirse? [Coverage, Spec §FR-014]
- [x] CHK024 ¿Se define qué pasa con los archivos adjuntos cuando el chofer se da de baja? [Gap, Spec §FR-005, §FR-024] — **Resuelto 2026-08-06**: FR-005a los conserva intactos; la baja no toca la documentación

## Seguridad y privacidad de los archivos

- [ ] CHK025 ¿La restricción de acceso al archivo está expresada como un requisito comprobable —por ejemplo, que la dirección del archivo no alcance sin sesión— y no sólo como una intención? [Measurability, Spec §FR-024]
- [ ] CHK026 ¿Está definido si el formato se valida por el contenido del archivo o alcanza con su extensión? Sin eso, "sólo PDF, JPG y PNG" admite dos implementaciones muy distintas [Clarity, Spec §FR-015a]
- [ ] CHK027 ¿La spec declara explícitamente si el registro de quién accede a la documentación sensible está dentro o fuera de alcance? El supuesto de exclusión menciona la auditoría de *cambios*, no la de *accesos* [Coverage, Spec §Assumptions]
- [ ] CHK028 ¿Está definido si todos los roles con acceso al módulo ven la misma información de un documento, o si algún dato requiere un permiso adicional? [Coverage, Spec §FR-027, Constitución §V]
- [x] CHK029 ¿Existe algún requisito de retención o eliminación de los archivos? [Gap, Spec §FR-015a] — **Resuelto 2026-08-06**: no hay plazo ni depuración automática, y se declara así en los supuestos; el archivo vive mientras exista su documento (FR-015c)
- [ ] CHK030 ¿Se especifica si el sistema inspecciona el contenido del archivo más allá de formato y tamaño —archivos dañados, contenido que no corresponde al documento declarado—, o si eso queda fuera de alcance? [Gap, Spec §FR-015a]

## Calidad de los criterios de aceptación

- [ ] CHK031 ¿SC-010 se puede comprobar operando la aplicación, sin mirar la base para saber qué documento quedó como vigente? [Measurability, Spec §SC-010]
- [ ] CHK032 ¿SC-011 define cómo se comprueba que un archivo "no se abre desde fuera del sistema"? [Clarity, Spec §SC-011]
- [ ] CHK033 ¿Cada requisito incorporado en la última clarificación —FR-015a, FR-020a, FR-029, FR-030— tiene al menos un escenario de aceptación que lo ejercite? [Traceability, Spec §Clarifications]

## Dependencias y supuestos

- [ ] CHK034 ¿El supuesto original sobre el almacenamiento de adjuntos quedó reemplazado en todo el documento, sin rastros del texto que daba por existente un mecanismo compartido? [Assumption, Spec §Assumptions]
- [ ] CHK035 ¿Está documentado el orden de dependencia que impone el módulo —transportistas antes que choferes, tipos antes que documentos— como requisito y no sólo como recorrido sugerido? [Dependency, Spec §US1, §US6]

## Notes

- Marcá cada ítem con `[x]` cuando el requisito esté escrito y no admita dos lecturas.
- Un ítem sin marcar es trabajo para la spec, no para el código: resolvelo con `/speckit-clarify` o
  editando `spec.md` antes de implementar.
- Los ítems con `[Gap]` señalan algo que **no está escrito**; los `[Conflict]` y `[Ambiguity]`,
  algo escrito que se contradice o se puede leer de dos maneras.
- **Estado al 2026-08-06**: 10 de 35 ítems resueltos (CHK001, CHK003, CHK006, CHK008, CHK009, CHK013,
  CHK015, CHK020, CHK024, CHK029).
  - **CHK003** y **CHK020** se resolvieron en una segunda pasada de clarificación: corregir y
    eliminar documentos (FR-015b a FR-015d) y atomicidad de la carga del archivo (FR-015e).
  - **CHK001** se resolvió acotando el alcance, no ampliándolo: ningún tipo es obligatorio y el
    estado informa lo cargado (FR-029a). El módulo ya no promete implícitamente decir quién está
    habilitado; lo dice de forma explícita, y el requisito faltante queda anotado para otra spec.
  - **CHK015** y **CHK013** se resolvieron quitando la afirmación sobre "documentación no
    respaldada" a nivel de chofer. La distinción con y sin archivo sigue existiendo, pero en el
    documento.
- Los 25 restantes siguen abiertos y son deuda de spec, no de código.
