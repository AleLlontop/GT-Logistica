# Checklist de Experiencia de Usuario: Autenticación de usuarios (Módulo 1)

**Purpose**: Validar la **calidad de los requisitos de interfaz** de la spec, el plan y los
contratos antes de empezar a implementar. Cada ítem pregunta si algo está bien especificado, no si
la pantalla funciona: lo que se revisa acá es el texto, no el código.

**Created**: 2026-08-04

**Feature**: [spec.md](../spec.md) · [contracts/README.md](../contracts/README.md)

**Profundidad**: liviana (13 ítems, priorizados por riesgo)

**Momento de uso**: antes de `/speckit-tasks`

---

## Completitud de los requisitos

- [x] CHK001 ¿Están definidos requisitos de accesibilidad para la pantalla de ingreso? Ni la spec ni el plan mencionan navegación por teclado, lectores de pantalla ni contraste [Gap]
  → **Resuelto**: FR-025 fija un piso mínimo de cuatro condiciones (teclado, etiquetas asociadas, errores anunciados, contraste) que rige también para los módulos siguientes; SC-008 lo hace verificable sin herramientas y el quickstart tiene el recorrido paso a paso.
- [x] CHK002 ¿Está definido a dónde llega el usuario después de ingresar si venía de una URL protegida? FR-007 lo manda a la pantalla de ingreso, pero nada dice si después vuelve a donde quería ir o queda en la pantalla de inicio [Gap, Spec §FR-007 §FR-020]
  → **Resuelto**: FR-026 lo lleva a la funcionalidad que había pedido si sus roles la autorizan; si no, o si el destino no es una pantalla de la aplicación, va a la pantalla de inicio. Nunca se acepta un destino externo.
- [ ] CHK003 ¿Está definido qué ve el usuario mientras la petición de ingreso está en curso? El contrato de UI sólo indica que el botón se deshabilita, sin especificar si hay señal de progreso [Gap, Contracts §Pantalla de inicio de sesión]
- [ ] CHK004 ¿Están definidos los tamaños de pantalla o dispositivos que la interfaz debe contemplar? El plan asume escritorio sin que ningún requisito lo fije [Gap, Plan §Target Platform]

## Claridad y ambigüedades

- [ ] CHK005 ¿Está especificado dónde aparece el botón de cerrar sesión en las pantallas que no son la de inicio? FR-013 exige que esté disponible "desde cualquier pantalla" sin definir su ubicación [Clarity, Spec §FR-013]
- [ ] CHK006 ¿Está definido si el mensaje de demasiados intentos muestra el tiempo restante real? El texto dice "esperá un minuto" sin aclarar si es una cuenta regresiva o una frase fija [Clarity, Contracts §Textos]
- [ ] CHK007 ¿Está definido cuánto permanece visible un mensaje de error y qué lo hace desaparecer? Ningún requisito dice si se borra al volver a escribir, al reintentar, o nunca [Clarity, Gap]

## Consistencia entre requisitos

- [ ] CHK008 ¿Coinciden los mensajes definidos en el contrato de UI con los que describe la spec en prosa? Los textos exactos viven sólo en `contracts/README.md`, mientras la spec los describe con otras palabras [Consistency, Spec §FR-003 §FR-004, Contracts §Textos]
- [ ] CHK009 ¿Es consistente el registro de tratamiento en todos los mensajes al usuario? El voseo rioplatense está fijado como principio, pero no hay un criterio escrito que impida mezclarlo con formas impersonales [Consistency, Constitution §II]

## Cobertura de escenarios y casos límite

- [ ] CHK010 ¿Está definido qué ve un usuario cuyo menú queda vacío? FR-020 dice que el menú se muestra vacío, sin especificar si algo le explica por qué no tiene opciones disponibles [Coverage, Gap, Spec §FR-020]
- [ ] CHK011 ¿Está definido qué pasa con los datos a medio cargar cuando la sesión expira? El caso límite describe la redirección pero no dice si lo escrito se pierde o se recupera [Coverage, Gap, Spec §Edge Cases]
- [ ] CHK012 ¿Está definido qué ve el usuario cuando el servidor no responde? El texto existe en el contrato de UI, pero ningún requisito de la spec cubre el escenario [Coverage, Contracts §Textos]

## Calidad de los criterios de aceptación

- [ ] CHK013 ¿Se puede verificar objetivamente que un mensaje "se entiende sin ayuda técnica"? SC-003 fija ese criterio sin definir cómo se comprueba ni quién lo juzga [Measurability, Spec §SC-003]

## Notas

- Marcá los ítems resueltos con `[x]` y anotá al lado la decisión tomada.
- Un ítem puede cerrarse de dos maneras válidas: agregando el requisito que falta, o dejando escrito
  por qué se decide no cubrirlo.
- CHK001 y CHK002 quedaron cerrados el 2026-08-04 (FR-025, FR-026 y SC-008 nuevos). El piso de
  accesibilidad se agregó como decisión de producto explícita, no de contrabando: rige desde este
  módulo y hacia adelante.
- CHK004 sigue apuntando a una dimensión que la spec **nunca menciona**. Antes de agregarla conviene
  decidir si entra en el alcance de este módulo o si es una decisión transversal del producto.
- **Estado**: 2 de 13 ítems resueltos.
