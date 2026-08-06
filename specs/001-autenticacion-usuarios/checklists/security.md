# Checklist de Seguridad: Autenticación de usuarios (Módulo 1)

**Purpose**: Validar la **calidad de los requisitos de seguridad** de la spec y el plan antes de
empezar a implementar. Cada ítem pregunta si algo está bien especificado, no si el sistema funciona:
lo que se revisa acá es el texto, no el código.

**Created**: 2026-08-04

**Feature**: [spec.md](../spec.md) · [plan.md](../plan.md) · [research.md](../research.md)

**Profundidad**: liviana (13 ítems, priorizados por riesgo)

**Momento de uso**: antes de `/speckit-tasks`

---

## Completitud de los requisitos

- [x] CHK001 ¿Está definido algún requisito sobre el canal por el que viajan las credenciales? FR-018 prohíbe la contraseña en URLs y logs, pero ningún requisito exige que la conexión sea cifrada [Gap, Spec §FR-018]
  → **Resuelto**: se agregó FR-024, que exige conexión cifrada para toda credencial —contraseña y dato de sesión— y prohíbe aceptar una que llegue sin cifrar. SC-006 lo incorporó como criterio verificable.
- [x] CHK002 ¿Está especificado cómo se protege el identificador de sesión contra robo o reutilización? La spec describe cuándo la sesión vence, no cómo se resguarda mientras vive [Gap, Spec §Sesión]
  → **Resuelto**: se agregó FR-023, que fija las tres condiciones (inaccesible desde la página, sólo por conexión cifrada, no viaja hacia otros sitios) para toda la vida de la sesión.
- [ ] CHK003 ¿Está definido qué se registra al procesar un intento de ingreso (usuario, origen, resultado)? FR-018 dice qué **no** debe registrarse, pero nada dice qué sí [Gap, Spec §FR-018]
- [ ] CHK004 ¿Están definidos requisitos sobre el origen y la fortaleza de la contraseña del administrador inicial? FR-019 exige que la cuenta exista, sin decir nada sobre su credencial [Gap, Spec §FR-019]

## Claridad y ambigüedades

- [x] CHK005 ¿Está definido qué constituye "el mismo origen" en el límite de intentos? El requisito no aclara si se identifica por dirección de red, por navegador, o por una combinación [Ambiguity, Spec §FR-021]
  → **Resuelto**: FR-021 ahora dice que el origen se identifica por la dirección de red desde la que llega la petición, y que el contador se lleva por la combinación de ese origen con la cuenta.
- [x] CHK006 ¿Está especificado qué cuenta como hasheo aceptable? FR-002 exige "de forma hasheada" sin fijar ningún criterio; el algoritmo concreto sólo aparece en el plan [Ambiguity, Spec §FR-002]
  → **Resuelto**: FR-002 ahora exige una función pensada para contraseñas, con valor aleatorio por contraseña, costo de cómputo alto y posibilidad de endurecer los parámetros sin invalidar lo ya almacenado.
- [ ] CHK007 ¿Está explícito que la contraseña temporal admite más de un uso dentro de sus 24 horas? La clarificación fijó el vencimiento por tiempo pero no descartó por escrito el uso único [Ambiguity, Spec §FR-017]

## Consistencia entre requisitos

- [ ] CHK008 ¿Es consistente el corte de sesión con las sesiones simultáneas? FR-009 habla de "esa sesión" en singular mientras FR-014 permite varias abiertas a la vez; no queda escrito si se cortan todas [Conflict, Spec §FR-009 §FR-014]
- [ ] CHK009 ¿Están alineados el mensaje genérico de credenciales y el tiempo de respuesta? FR-003 exige no distinguir qué dato falló, pero ningún requisito impide que la demora de la respuesta lo delate [Consistency, Spec §FR-003]

## Cobertura de escenarios y casos límite

- [x] CHK010 ¿Está cubierto el caso de varios usuarios legítimos compartiendo una misma salida a internet? En una oficina con una sola IP, el límite de FR-021 podría dejar afuera a gente que no se equivocó [Coverage, Gap, Spec §FR-021]
  → **Resuelto**: el contador pasó a llevarse por origen **y** cuenta, así que el error de una persona no frena a las demás. Cubierto por el escenario 6 de la User Story 3, un caso límite propio y SC-007. Riesgo residual documentado en `research.md` §4.
- [x] CHK011 ¿Está definido un tope absoluto de duración de sesión, además del vencimiento por inactividad? Con la renovación deslizante de FR-010, una sesión puede vivir indefinidamente si hay actividad cada 8 horas [Gap, Spec §FR-010]
  → **Resuelto como decisión explícita**: no hay tope absoluto. FR-010 ahora lo dice con todas las letras: la inactividad y el cierre del navegador (FR-022) son las únicas causas de vencimiento. Un tope corto echaría a alguien en plena jornada y uno largo casi nunca se dispararía, porque FR-022 ya acota la sesión a un día de trabajo.

## Supuestos y decisiones de riesgo asumido

- [ ] CHK012 ¿Está documentado el riesgo que se acepta al no auditar los intentos de ingreso? Sin bloqueo automático (FR-016) ni registro de intentos, no queda forma de detectar un ataque sostenido [Assumption, Spec §FR-016]
- [ ] CHK013 ¿Está validado el supuesto de que todo usuario tiene al menos un rol? La spec lo da por garantizado por el Módulo 2, sin definir qué hace este módulo si igual llega una cuenta sin roles [Assumption, Spec §Key Entities]

## Notas

- Marcá los ítems resueltos con `[x]` y anotá al lado la decisión tomada.
- Un ítem puede cerrarse de dos maneras válidas: agregando el requisito que falta, o dejando escrito
  por qué se decide no cubrirlo. Las dos cierran el hueco; lo que no sirve es dejarlo sin responder.
- CHK001, CHK002 y CHK006 quedaron cerrados el 2026-08-04: eran requisitos que el plan ya resolvía o
  daba por obvios pero la spec no exigía, y se subieron a la spec (FR-002 ampliado, FR-023 y FR-024
  nuevos, SC-006 ajustado) para que no dependan de que alguien lea `research.md`.
- CHK005 y CHK010 se cerraron juntos el mismo día: el contador de intentos fallidos pasó de contarse
  por origen a contarse por origen **y** cuenta (FR-021 reescrito, SC-007 ajustado), lo que de paso
  obligó a definir qué se entiende por origen.
- CHK011 se cerró sin agregar ningún mecanismo: la respuesta fue dejar por escrito que **no** hay
  tope absoluto y por qué. Un hueco también se cierra decidiendo explícitamente no cubrirlo.
- **Estado**: 6 de 13 ítems resueltos. Los 7 restantes siguen abiertos y son decisiones de
  producto pendientes, no trabajo de implementación.
