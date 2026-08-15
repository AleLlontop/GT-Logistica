# Quickstart: Gestión de facturación (Módulo 6)

Cómo levantar el sistema y **comprobar a mano** que el módulo hace lo que la spec dice. El recorrido
está pensado para que lo haga una persona de negocio: se opera la aplicación, se leen pantallas y no
hace falta abrir el código, mirar logs ni consultar la base (Principio IV).

Al final hay una sección con **lo que este recorrido no puede verificar y por qué**, con el test que
lo cubre en cada caso.

---

## Antes de empezar

```bash
cp .env.template .env      # completá GT_SQL_PASSWORD y GT_ADMIN_PASSWORD_INICIAL
podman compose up -d       # SQL Server + backend + frontend
```

La aplicación queda en <http://localhost:5173>. Las migraciones y los datos iniciales —los tres
permisos nuevos incluidos— se aplican solos al arrancar el backend.

**Verificación de humo del generador de PDF.** Es lo primero, porque si falla nada del módulo sirve y
el síntoma aparece recién al emitir:

```bash
cd backend && dotnet test --filter FullyQualifiedName~ArmadorDocumentoFacturaTests
```

Si falla con un error de fuentes o de biblioteca nativa, falta el `apt-get install libfontconfig1
libfreetype6` de la etapa final de `backend/Dockerfile` (research §1). Rehacé la imagen con
`podman compose build backend`.

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

### Usuarios que hacen falta

Desde *Gestión de usuarios* (Módulo 2), con el `admin` inicial, creá **tres** cuentas y asignales un
rol cada una. Sin las tres no se puede comprobar el reparto de permisos:

| Usuario | Rol | Qué tiene que poder hacer |
|---|---|---|
| `admin` | Administrador del sistema | todo, **incluida la anulación** |
| `admin.empresa` | Administración de la empresa | emitir, corregir, cobrar — **no anular** |
| `gerencia` | Gerencia | mirar — **nada más** |

### Datos del Módulo 5 que hacen falta

Desde *Clientes* y *Viajes*, cargá:

- **Cliente A** con domicilio cargado, y **Cliente B sin domicilio**.
- Del Cliente A, en **septiembre de 2026**: tres viajes con remito e importes `$ 30.000,00`,
  `$ 30.000,00` y `$ 22.644,63`, llevados hasta `rendido`; un cuarto viaje `en curso`; un quinto
  `anulado`.
- Del Cliente B, un viaje rendido con remito en el mismo período.

> Al rendir vas a comprobar de paso el cambio de FR-055a: **sin número de remito el viaje no pasa a
> `rendido`** y el sistema marca ese campo (paso 18).

---

## Recorrido

### US1 — Configurar la empresa emisora

1. Entrá como `admin.empresa` y abrí *Facturas* → *Nueva factura*. **Todavía no se puede emitir**: el
   sistema nombra los datos que faltan de la empresa emisora y no deja continuar. ✅ FR-006
2. Abrí *Empresa emisora*. El formulario está vacío **con el mensaje que lo explica**, no en blanco.
   ✅ US1 esc. 1
3. Escribí un CUIT de diez dígitos y guardá: el campo queda marcado con el motivo y no se guarda nada.
   ✅ US1 esc. 3
4. Completá razón social, CUIT (`30-71234567-1`, con guiones), domicilio, condición de IVA, punto de
   venta `0014` y CBU. Guardá: la pantalla **no cambia** y confirma el guardado. ✅ US1 esc. 2
5. Subí un PDF como logo: se rechaza diciendo qué formatos acepta y la configuración queda igual.
   Subí un PNG: queda cargado. ✅ US1 esc. 6, 9

### US2 — Emitir la primera factura

6. *Facturas* → *Nueva factura*. Elegí **Cliente A**, mes **septiembre**, año **2026**. Aparecen
   **sólo los tres viajes rendidos**: ni el `en curso`, ni el `anulado`, ni los del Cliente B.
   ✅ FR-015, FR-017, US2 esc. 3, 4, 6
7. Marcá los tres. La cantidad y el importe acumulado se actualizan en cada clic. Con `Factura A` el
   neto da `$ 82.644,63`, el IVA `$ 17.355,37` y el total `$ 100.000,00`. ✅ US2 esc. 7, 8
8. Cambiá a `Factura C`: el IVA pasa a `$ 0,00` y el total a `$ 82.644,63`. Cambiá a `Factura B`:
   vuelven a los valores del paso 7. **No hay ningún campo donde escribir los tres importes.**
   ✅ FR-023, FR-024, US2 esc. 9, 10
9. Volvé a `Factura A`. Completá condición de venta `Cuenta Corriente`, número `0014-00000001`
   —el punto de venta viene propuesto—, CAE y su vencimiento, y dejá el vencimiento de pago propuesto.
   ✅ FR-027, US2 esc. 14, 26
10. *Ver vista previa*. Fijate que estén los nueve bloques del comprobante: banda `ORIGINAL`, emisor
    con el logo, recuadro de letra `A` con el código `001`, bloque de identificación, banda de
    vencimiento de pago, banda de CBU, bloque del cliente con `Responsable Inscripto` y
    `Cuenta Corriente`, **tabla con tres filas —una por viaje—** y pie con neto, IVA, total y CAE.
    ✅ FR-031, FR-031e, FR-031i, US2 esc. 16, 25, 27
11. **Salí de la pantalla sin confirmar** y volvé al listado: la factura no existe y no se guardó
    ningún archivo. ✅ US2 esc. 33, SC-007b
12. Rehacé los pasos 6 a 10 y ahora sí confirmá. La pantalla **pasa a la ficha de la factura recién
    creada** y el formulario no queda abierto detrás. ✅ FR-014, US2 esc. 17
13. En la ficha, abrí *Ver el documento*: se **muestra** sin bajarlo y abrirlo a mano, y coincide
    bloque por bloque con lo que viste en la vista previa. ✅ FR-031a, US2 esc. 24, SC-007b
14. Abrí *Viajes* (Módulo 5): los tres viajes figuran **`Facturado`**, con el número y la fecha de la
    factura. ✅ FR-055, US2 esc. 18
15. Volvé a *Nueva factura* con el mismo cliente y período: **los tres viajes ya no se ofrecen**.
    ✅ FR-017, US2 esc. 5, SC-003

### US2 — Los rechazos y las dos confirmaciones

16. Intentá emitirle una factura al **Cliente B**: el sistema la rechaza diciendo que le falta el
    domicilio y dónde cargarlo. Cargáselo en *Clientes* y reintentá: la emisión procede.
    ✅ FR-011a, US2 esc. 31
17. Cargá un cuarto viaje del Cliente A con importe `$ 0,00`, rendilo y armá una factura que lo
    incluya. Al confirmar, el sistema **no emite**: advierte cuál es el viaje sin importe. Cancelá:
    no se creó nada. Reintentá y confirmá: recién ahí se emite. ✅ FR-032, US2 esc. 19, SC-009
18. Cargá un viaje **sin remito** y llevalo a `rendido`: el sistema marca ese campo y no completa la
    transición. Cargá el remito y reintentá: pasa a `rendido`. ✅ FR-055a, US2 esc. 30
19. Con número `0014-00000001` —ya usado— intentá emitir otra: el rechazo identifica la factura que lo
    usa y no se crea nada. ✅ FR-027, US2 esc. 15, SC-004
20. Probá un vencimiento de pago anterior a la fecha de facturación y un vencimiento del CAE anterior:
    los dos se rechazan marcando el campo. ✅ FR-029, FR-030, US2 esc. 21, 22

### US3 — Consultar, buscar y filtrar

21. Emití una segunda factura de otro cliente, de otro período y de otro tipo de comprobante, para
    tener con qué filtrar.
22. En *Facturas*, combiná filtros por cliente, rango de fechas, período, estado y tipo: el listado
    muestra sólo lo que cumple **todas** las condiciones. ✅ FR-058, US3 esc. 2
23. Sacá el filtro de estado: el control dice explícitamente que está mostrando todas, incluidas las
    anuladas. ✅ FR-064, US3 esc. 8
24. Filtrá por algo que no exista: aparece el mensaje de *sin resultados*, no una tabla vacía.
    ✅ FR-064, US3 esc. 4
25. En *Clientes*, corregile la razón social al Cliente A. Volvé a la ficha, al listado y al documento
    de su factura: **los tres siguen mostrando la razón social vieja**, y el filtro por ese cliente la
    sigue encontrando. ✅ FR-034a, US3 esc. 12, SC-007
26. En *Empresa emisora*, cambiá el domicilio. La factura emitida sigue mostrando el anterior; una
    factura nueva usa el nuevo. ✅ FR-034, US1 esc. 10

### US4 — Corregir una factura emitida

27. En la ficha de la primera factura, *Corregir datos*. Sólo se pueden tocar el detalle, el CAE, su
    vencimiento y el vencimiento de pago: **el cliente, los viajes y los importes no ofrecen dónde
    editarse**. ✅ FR-035, FR-036, US4 esc. 1, 2
28. Corregí el CAE y guardá. Abrí el documento: **ya trae el CAE corregido**. En el historial aparece
    una entrada `Corrección de datos` con quién y cuándo. ✅ FR-031b, FR-037, US4 esc. 3, 4, SC-007a
29. Intentá borrar el CAE dejándolo vacío: se rechaza. ✅ US4 esc. 6

### US5 — Cobro y vencimientos

30. Emití una factura con vencimiento de pago **de la semana pasada**. En el listado figura
    **`Vencida`** sin que nadie haya hecho nada, y filtrando por `Pendiente` **no aparece**; filtrando
    por `Vencida`, sí. ✅ FR-041, FR-058a, US3 esc. 11, US5 esc. 2, SC-012
31. Emití otra con vencimiento dentro de los próximos días. Abrí *Vencimientos*: figuran las dos, con
    cliente, número, importe y los días de atraso o de plazo **en palabras**. ✅ FR-063, FR-065,
    US5 esc. 8
32. Registrá el cobro de la segunda con la fecha de hoy: queda `Pagada`, con la fecha visible, el
    historial lo registra, y **desaparece del panel**. ✅ FR-042, US5 esc. 3, 4
33. En su ficha: **no hay ninguna acción** para volverla a `pendiente`, a `vencida` ni a `anulada`.
    ✅ FR-043, US5 esc. 6
34. Corregile el CAE a esa factura `pagada`: se acepta, el documento se regenera, y **sigue `Pagada`
    con la misma fecha de cobro**. ✅ FR-035, US4 esc. 8

### US6 — Anular y refacturar

35. Como `admin.empresa`, abrí la ficha de una factura `pendiente`: **no ves la acción *Anular***.
    ✅ FR-067, US6 esc. 7
36. Entrá como `admin`. Anulá esa factura: sin motivo escrito el botón no se habilita; al cancelar,
    todo queda igual. ✅ FR-046, US6 esc. 1, 2, 3
37. Escribí el motivo y confirmá: queda `Anulada` con el motivo visible, el historial lo registra, y
    **sus viajes vuelven a `Rendido`** en el Módulo 5. ✅ FR-048, US6 esc. 4, SC-010
38. Abrí el documento de la factura anulada: **trae impresas la leyenda de anulada y el motivo**.
    ✅ FR-031d, US6 esc. 13
39. Armá una factura nueva del mismo cliente y período: **los viajes volvieron a ofrecerse**. Elegí
    tipo de facturación `Refacturación`: aparece el desplegable con la factura anulada. Sin elegirla
    no se puede confirmar. Elegila y emití. ✅ FR-049, US6 esc. 5, 8, 9
40. Abrí las dos fichas: **cada una muestra a la otra**. ✅ FR-050, US6 esc. 10
41. Armá otra Refacturación para el mismo cliente: **esa anulada ya no aparece** entre las elegibles.
    ✅ FR-049a, US6 esc. 14
42. Intentá anular la factura `pagada` del paso 32: se rechaza informando desde qué fecha está
    cobrada, **sin ofrecer revertir el cobro**. ✅ FR-043a, US6 esc. 6

### US7 — Totales y acceso de sólo lectura

43. Abrí *Totales facturados* sin elegir rango: no se calcula ni se muestra nada, y el sistema dice
    que falta elegirlo. ✅ FR-061, US7 esc. 2
44. Elegí un rango que incluya todo lo emitido: por cada cliente ves facturado, cobrado y pendiente.
    **La factura anulada no suma en ninguna columna.** ✅ FR-062, US7 esc. 1, 3, SC-011
45. Filtrá el listado por ese mismo cliente y rango, y sumá a mano los importes de las filas: la suma
    coincide con la columna *facturado* del cuadro. ✅ US7 esc. 4, SC-011
46. Entrá como `gerencia`. Podés abrir el listado, las fichas, el panel y los totales, y **no ves
    ninguna acción** de emitir, corregir, cobrar ni anular. En el menú tampoco aparece *Empresa
    emisora*. ✅ FR-067, FR-068, US7 esc. 6, SC-014

---

## Lo que este recorrido no puede verificar, y qué lo cubre

Tres cosas de este módulo no las puede comprobar una persona operando la aplicación. El Principio IV
obliga a declararlas en vez de pedirle a quien valida algo que no puede hacer (research §14).

| Qué | Por qué no se puede a mano | Qué lo cubre |
|---|---|---|
| **La carrera de SC-005**: dos administrativos confirmando en el mismo milisegundo facturas que comparten un viaje | Nadie puede hacer clic dos veces en el mismo instante | `EmisionConcurrenteTests` lanza las dos operaciones en paralelo contra el SQL Server del compose y verifica que se crea exactamente una, que la otra recibe el rechazo nombrando el viaje y el comprobante, y que no queda ninguna factura con viajes sin marcar |
| **Que la vista previa y el documento guardado sean idénticos** (SC-007b) | A ojo se comparan los bloques, no cada dato | `VistaPreviaTests` genera los dos PDF de la misma factura y compara byte a byte |
| **Que el filtro por estado en SQL y la derivación en C# den lo mismo** (FR-058a) | Exigiría reproducir la consulta a mano | `DerivacionVencidaTests` evalúa las dos sobre el mismo conjunto de facturas y compara |

Y una cuarta, que sí se puede comprobar pero conviene que además tenga test, porque se rompe en
silencio: **que reordenar `EstadoFactura` no deje los índices únicos protegiendo el estado
equivocado**. Lo cubre `IndicesDeFacturaTests`, que inserta una fila en cada estado y verifica dónde
cada índice acepta y dónde rechaza.
