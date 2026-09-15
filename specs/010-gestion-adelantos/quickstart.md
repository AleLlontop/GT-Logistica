# Quickstart: Gestión de adelantos de sueldo (Módulo 10)

Cómo levantar el sistema y **comprobar a mano** que el módulo hace lo que la spec dice. El recorrido está
pensado para una persona de negocio: se opera la aplicación y se leen pantallas, sin abrir el código, los
logs ni la base (Principio IV).

Al final está **lo que este recorrido no puede verificar y por qué**, con el test que lo cubre.

Los textos exactos de cada pantalla están en [`contracts/README.md`](./contracts/README.md); acá se citan
sólo cuando son lo que se verifica.

---

## Antes de empezar

```bash
cp .env.template .env      # completá GT_SQL_PASSWORD y GT_ADMIN_PASSWORD_INICIAL
podman compose up -d       # SQL Server + backend + frontend
```

La aplicación queda en <http://localhost:5173>. La migración `Modulo10Adelantos` y los dos permisos nuevos
se aplican solos al arrancar el backend. **El módulo no agrega dependencias ni variables de entorno**: si el
build falla resolviendo nombres, es el problema de DNS de `AGENTS.md`, no de este módulo.

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

### Usuarios que hacen falta

Desde *Gestión de usuarios*, con el `admin` inicial:

| Usuario | Rol | Qué tiene que poder hacer |
|---|---|---|
| `admin` | Administrador del sistema | todo |
| `admin.empresa` | Administración de la empresa | todo |
| `gerencia` | Gerencia | mirar — **nada más** |
| `trafico` | Tráfico | **nada** de este módulo |

### Datos que hacen falta

Los CUIT y CUIL de abajo **pasan la validación**: el dígito verificador está calculado, no inventado.

> **Fechas relativas.** El recorrido nombra `{hoy}` y `{piso}`, que es el **primer día del mes anterior a
> hoy**. Si hoy es 14/09/2026, `{piso}` es 01/08/2026 y `{piso − 1}` es 31/07/2026. Anotalos antes de
> empezar.

1. **Empresa emisora** (Módulo 6): dejala **sin configurar** hasta el paso 7.
2. **Transportistas** (Módulo 3), con `admin`:
   - `G&T Logística S.A.`, CUIT `30-71234567-1` —**el mismo** que vas a cargar en la empresa emisora—.
   - `Transportes Díaz`, CUIT `20-12345678-6`.
3. **Personas** (Módulo 2), con `admin`:

| Persona | DNI | Tipo en el padrón |
|---|---|---|
| Pérez, Juan | `30123456` | Chofer |
| Gómez, Carlos | `28765432` | Chofer |
| Díaz, Luis | `31456789` | Chofer |
| Torres, Ana | `33222111` | Empleado |
| Ruiz, Marta | `35111222` | Empleado |
| Sosa, Pedro | `29888777` | Empleado |

4. **Choferes** (Módulo 3), con `admin`:

| Persona | CUIL | Transportista | Después |
|---|---|---|---|
| Pérez, Juan | `20-30123456-3` | G&T Logística S.A. | — |
| Gómez, Carlos | `20-28765432-5` | G&T Logística S.A. | **dalo de baja** |
| Díaz, Luis | `20-31456789-8` | Transportes Díaz | — |

---

## Recorrido

### US6 — Acceso

1. **Sin sesión**, abrí `/adelantos`, `/adelantos/nuevo` y `/adelantos/1`: las tres llevan a *Ingresar*,
   aunque el adelanto 1 todavía no exista. ✅ FR-044, US6 esc. 1
2. Entrá como `trafico`: el menú **no** tiene *Consultar adelanto* ni *Registrar adelanto*. Escribí
   `/adelantos` en la barra: la pantalla dice `No tenés permiso para ver los adelantos.` y a quién
   pedírselo, sin filtros ni tabla y sin ofrecer volver a intentar. ✅ FR-043, US6 esc. 4, 5
3. Entrá como `gerencia`: el menú tiene **sólo** *Consultar adelanto*, en la sección *Operación*.
   Escribí `/adelantos/nuevo` en la barra: dice `No tenés permiso para registrar adelantos.` y no muestra
   el formulario. ✅ FR-042, FR-043, US6 esc. 3, 5
4. Entrá como `admin.empresa`: el menú tiene **las dos**, en *Operación*. ✅ FR-042, US6 esc. 2

### US1 — Registrar

5. Como `admin.empresa`, abrí *Registrar adelanto*. La fecha viene con `{hoy}`. *Persona* no ofrece a nadie
   y dice que primero hay que elegir el tipo. ✅ FR-002, FR-005, US1 esc. 3, 5
6. Elegí *Chofer*: aparece el aviso de que falta configurar la empresa emisora, con el enlace, y *Persona*
   no ofrece a nadie. Cambiá a *Empleado*: aparecen `Ruiz, Marta`, `Sosa, Pedro` y `Torres, Ana`, con su
   DNI, y se anuncia cuántos hay. ✅ FR-002a, FR-040, US1 esc. 2, 12
7. Seguí el enlace del aviso y configurá la empresa emisora con el CUIT `30-71234567-1` y los otros
   obligatorios. Volvé a *Registrar adelanto*.
8. Elegí *Chofer*: aparece **sólo** `Pérez, Juan — 30123456`. **No** aparecen Gómez, que está dado de
   baja, ni Díaz, que es de Transportes Díaz. ✅ FR-002, US1 esc. 1, SC-003
9. Elegí a Pérez, escribí el motivo `gastos médicos` y el importe `150000`. Cambiá el tipo a *Empleado*: la
   persona se vacía y la fecha, el motivo y el importe **siguen ahí**. ✅ FR-003, US1 esc. 4
10. Borrá el motivo y, sin elegir persona, apretá *Guardar adelanto*: quedan marcados *Persona* y *Motivo*,
    no se registra nada, y la fecha y el importe siguen cargados. Escribí sólo espacios en el motivo y
    volvé a guardar: sigue marcado. ✅ FR-006, FR-008, US1 esc. 6
11. Poné `0` en el importe: se marca. Poné `-20000`: se marca. Poné `150000,555`: se marca por los
    decimales. ✅ FR-007, US1 esc. 7, SC-003
12. Poné la fecha de **mañana**: se marca con el rango `entre el {piso} y hoy`. Poné `{piso − 1}`: se marca
    igual. Poné `{piso}`: se acepta. Volvé a `{hoy}`. ✅ FR-005, US1 esc. 8
13. **Una baja mientras cargás, en dos navegadores.** Elegí *Empleado* → `Sosa, Pedro`, motivo `adelanto de
    vacaciones`, importe `50000`. **Sin guardar**, en otro navegador con `admin`, dá de baja a Sosa en
    *Personas*. Volvé y guardá: el aviso dice que Sosa se dio de baja, **no se registra nada** y lo cargado
    sigue ahí. ✅ FR-009, US1 esc. 10
14. Elegí *Chofer* → `Pérez, Juan`, motivo `gastos médicos`, importe `150000`, fecha `{hoy}`, y guardá. Te
    lleva al **detalle** —no queda el formulario— con el aviso de que se registró y queda pendiente, la
    pastilla `Pendiente` y el historial con `Registrado por …`. Es el adelanto **A**.
    ✅ FR-010, FR-011, FR-036, US1 esc. 9, SC-002
15. Registrá cuatro más **para Pérez**, todos con fecha `{hoy}`, y uno para Torres. **Ninguno avisa por
    acumulación**:

| Adelanto | Persona | Fecha | Motivo | Importe |
|---|---|---|---|---|
| B | Pérez, Juan | `{hoy}` | `alquiler` | `50000` |
| C | Pérez, Juan | `{hoy}` | `útiles escolares` | `30000` |
| D | Pérez, Juan | `{hoy}` | `error de carga` | `20000` |
| E | Pérez, Juan | `{hoy}` | `anticipo de aguinaldo` | `40000` |
| F | Torres, Ana | `{piso}` | `mudanza` | `80000` |

    ✅ FR-012

### US4 — Aprobar y rechazar

16. Abrí **A**: están *Aprobar adelanto* y *Rechazar adelanto*, y el aviso dice que todavía no suma en el
    total adelantado. ✅ US4 esc. 1
17. Aprobá **A**: pasa a `Aprobado`, se anuncia, el historial suma `Aprobado por …`, ya **no** están aprobar
    ni rechazar, y aparece *Anular adelanto*. ✅ FR-023, US4 esc. 2, 6
18. Abrí **D** y apretá *Rechazar adelanto*. Apretá el botón **sin motivo**: se marca. Escribí sólo
    espacios: sigue marcado. Apretá *Volver*: **D** sigue `Pendiente` y el historial no cambió.
    ✅ FR-024, FR-025, US4 esc. 3, 5
19. Rechazá **D** con `Ya tiene un adelanto pendiente del mes anterior.`: pasa a `Rechazado`, el aviso
    muestra el motivo y el historial suma `Rechazado por …` con el motivo. ✅ FR-024, US4 esc. 4, SC-006
20. **Dos resoluciones, en dos navegadores.** Abrí **B** con `admin` y con `admin.empresa`. En el primero,
    aprobalo. En el segundo, **sin recargar**, rechazalo con cualquier motivo: se rechaza diciendo que
    **ya está aprobado**, y la pantalla pasa a mostrarlo aprobado. ✅ FR-026, US4 esc. 7, SC-008
21. Aprobá **E** y **F**.

### US5 — Anular

22. En **E**, apretá *Anular adelanto*: pide motivo y el botón dice *Anular adelanto*. Apretá *Volver*:
    sigue `Aprobado`. ✅ FR-029, FR-030, US5 esc. 1, 2
23. Volvé a abrirlo y apretá *Anular adelanto* **sin motivo**: se marca y no anula. ✅ US5 esc. 3
24. Anulá **E** con `Cargado sobre la persona equivocada.`: pasa a `Anulado`, el aviso muestra el motivo,
    no queda ninguna acción, y el historial tiene **tres** entradas —registro, aprobación y anulación—, cada
    una con usuario y hora, la última con el motivo. ✅ FR-031, FR-036, US3 esc. 3, US5 esc. 4
25. Abrí **C** (pendiente) y **D** (rechazado): **ninguno** tiene *Anular adelanto*. ✅ FR-027, US5 esc. 6

### US3 — Detalle

26. Abrí **A**: persona con apellido, nombre y DNI, tipo `Chofer`, fecha, motivo, importe y estado.
    ✅ FR-019, US3 esc. 1, SC-009
27. Abrí **D**: muestra el motivo del adelanto **y** el del rechazo. ✅ US3 esc. 2
28. En *Personas*, con `admin`, corregí el apellido de Pérez a `Pérez Gil`. Abrí **A**: dice
    `Pérez Gil, Juan`. ✅ FR-020, US3 esc. 4

### US2 — Listado y filtros

29. Abrí *Consultar adelanto*. Cada fila: fecha, persona con DNI, motivo, importe y estado. **D** y **E**
    siguen ahí, **atenuados y con la palabra** `Rechazado` y `Anulado`. ✅ FR-013, FR-035, US2 esc. 1, 8,
    SC-007
30. Sin filtro de estado, el control dice `Mostrando todos los adelantos, incluidos los rechazados y los
    anulados.` ✅ FR-018
31. Desplegá el filtro de persona: están `Pérez Gil, Juan` y `Torres, Ana`. **No** están Ruiz, que no tiene
    adelantos, ni Sosa, cuyo registro se rechazó, ni Díaz. Elegí a Pérez: sólo **A** a **E**.
    ✅ FR-014, US2 esc. 2, 12
32. Con Pérez elegido, poné *Desde* `{piso del mes en curso}` —el primero de este mes— y *Hasta* `{hoy}`:
    siguen los cinco, y el **total adelantado es `$ 200.000,00`** —A y B—: no suma C pendiente, D rechazado
    ni E anulado. ✅ FR-016, US2 esc. 7, SC-004, SC-005
33. Poné *Desde* `{hoy}` y *Hasta* `{piso}`: los dos campos se marcan como rango inválido y **no filtra**.
    ✅ FR-015, US2 esc. 4
34. Limpiá la persona. Poné *Desde* y *Hasta* los dos en `{piso}`: aparece **sólo F** —los extremos se
    incluyen—. Dejá sólo *Hasta* en `{piso}`: sigue F. Dejá sólo *Desde* en `{hoy}`: aparecen A a E.
    ✅ FR-014, US2 esc. 3
35. Limpiá las fechas y filtrá por cada estado: `Pendiente` trae C con total `$ 0,00`; `Aprobado`, A, B y F
    con total `$ 280.000,00`; `Rechazado`, D; `Anulado`, E. **Ningún adelanto aparece en dos**, y el control
    dice qué está mostrando. ✅ FR-033, US2 esc. 5, 9
36. Combiná `Pérez Gil, Juan` + `Aprobado` + *Desde* `{piso del mes en curso}`: A y B, total
    `$ 200.000,00`. Cambiá a `Torres, Ana` sin tocar lo demás: `Ningún adelanto coincide con los filtros
    aplicados.` ✅ US2 esc. 6, 9
37. Como `gerencia`: el listado y el detalle se ven iguales, **sin** *Registrar adelanto* ni ninguna acción,
    y los avisos del detalle sin la oración que indica qué hacer. ✅ FR-043, US2 esc. 11, SC-010

### Casos límite del padrón

38. Con `admin`, en *Choferes*, **dá de baja la ficha** de Pérez. Abrí **A**: sigue diciendo `Chofer`.
    En *Consultar adelanto*, el filtro de persona **sigue** ofreciendo a Pérez. ✅ FR-020, US3 esc. 5,
    US2 esc. 12
39. En *Registrar adelanto*, elegí *Chofer*: no queda ninguno, y el mensaje lo dice en vez de mostrar el
    desplegable vacío. Elegí *Empleado*: Pérez **tampoco** aparece, porque tiene ficha. ✅ FR-004, spec
    §Edge Cases
40. **Paginación (opcional).** Registrá adelantos hasta superar 20 y recorré las páginas: se muestran de a
    20, del más reciente al más antiguo, sin repetir ni saltear ninguno. ✅ FR-017, US2 esc. 10

### Cambios a los Módulos 6 y 9

Los tres cambios enumerados en spec §Assumptions. **No cambian nada visible**: estos pasos comprueban que
la fecha propuesta sigue siendo la de hoy.

41. En la ficha de una factura sin cobrar, abrí el registro del cobro: *Fecha de cobro* viene con `{hoy}`.
    ✅ cambio 1
42. En *Alta de factura*, *Fecha de facturación* viene con `{hoy}`. ✅ cambio 2
43. En el detalle de una liquidación pendiente, abrí *Registrar orden de pago*: *Fecha de pago* viene con
    `{hoy}`. ✅ cambio 3

---

## Lo que este recorrido no puede verificar

| Qué | Por qué no a mano | Test |
|---|---|---|
| Aprobar y rechazar, o dos anulaciones, en el mismo instante | el paso 20 prueba la segunda operación rechazada en secuencia; el cruce en el mismo milisegundo no se puede provocar | `ResolucionConcurrenteTests`, `AnulacionConcurrenteTests` |
| Los `CHECK` rechazan un rechazado sin motivo, un anulado sin motivo o un tipo desconocido | ninguna pantalla permite intentarlo | `RestriccionesDeAdelantoTests` |
| Una invocación directa sin `confirmado` no rechaza ni anula; una con persona de otro tipo o chofer externo no registra | la pantalla siempre manda la confirmación y sólo ofrece personas elegibles | `AprobacionYRechazoTests`, `AnulacionTests`, `RegistroTests` |
| El piso de la fecha el 1 de enero es el 1 de diciembre del año anterior | habría que esperar a enero | `ReglasDeAdelantoTests` |
| Las rutas literales no las captura `{id:int}` | si fallara, los pasos 6 y 31 fallarían, pero sin decir por qué | `RutasAdelantosTests` |
| Las tres pantallas de los Módulos 6 y 9 no cambiaron nada más que la importación | los pasos 41 a 43 miran la fecha, no el resto de la pantalla | sus suites existentes, sin modificar |
