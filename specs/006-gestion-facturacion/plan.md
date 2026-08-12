# Implementation Plan: Gestión de facturación (Módulo 6)

**Branch**: `006-gestion-facturacion` | **Date**: 2026-08-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/006-gestion-facturacion/spec.md`

## Summary

El Módulo 6 cobra el trabajo que el Módulo 5 registró. Hoy la factura se arma a mano en una planilla y
los viajes se transcriben uno por uno, con tres consecuencias: importes que no cierran con los viajes,
viajes facturados dos veces y viajes que nunca se facturan.

El módulo permite emitir una factura a un cliente agrupando viajes rendidos de un período: el sistema
propone los facturables, calcula neto, IVA y total, **genera el documento en PDF**, y marca los viajes
como `facturado` para que no vuelvan a ofrecerse. Trae además la configuración de la empresa emisora
con su logo, la vista previa antes de confirmar, los estados `pendiente / vencida / pagada / anulada`
con registro del cobro, la anulación con motivo que devuelve los viajes a `rendido`, la refacturación,
el listado con filtros, el panel de vencimientos y los totales por cliente.

**El valor central es la trazabilidad viaje ↔ factura**, y de ahí sale casi todo el diseño.

Es el primer módulo del sistema con **tres cosas nuevas**: un **documento generado por el sistema**
que tiene que decir siempre lo mismo que la pantalla, **datos congelados** —una factura dice a quién
se le facturó ese día, no quién es hoy— y **una modificación a un módulo de negocio anterior**, que
hasta ahora ninguna feature había hecho.

**Enfoque técnico**, con seis decisiones que definen el módulo:

1. **Un único armador de documento en el servidor, invocado por la vista previa y por la emisión,
   sobre la misma entrada.** La vista previa arma la entidad `FacturaCliente` que todavía no existe,
   la mapea y renderiza; la emisión arma la misma entidad, la persiste, y mapea y renderiza con la
   misma función. FR-033 pide no **persistir**, no dejar de **producir**: dos maquetas paralelas se
   separan sin que nadie lo note, y entonces revisar la vista previa deja de servir para algo
   (research §2).
2. **Una sola dependencia nueva, QuestPDF**, detrás de una interfaz de la capa de aplicación. Es C#
   puro, renderiza a memoria —que es lo que hace posible la vista previa que no guarda nada— y su
   tabla se corta sola entre páginas, que es lo que exige una factura de muchos viajes. **Trae un
   cambio de infraestructura y hay que decirlo**: el `Dockerfile` necesita `libfontconfig1`, y sin él
   el backend arranca perfecto y falla recién al emitir la primera factura (research §1).
3. **`vencida` se deriva al leer y además se filtra.** Es la cuarta vez que el sistema resuelve así un
   estado derivable, pero la primera en que el derivado es también un **filtro**: sus cuatro valores
   son excluyentes y el predicado va escrito en la consulta, no como filtrado posterior. La regla se
   escribe dos veces —dominio y SQL— y un test compara las dos sobre el mismo dato (research §3).
4. **La exclusividad viaje ↔ factura la sostiene una columna escalar, no un índice.** `Viajes.FacturaId`
   no puede apuntar a dos facturas: la unicidad ya es estructural y no hay nada que un índice agregue.
   Lo que queda por cerrar es la **carrera**, y eso lo cierra un `UPDATE` condicional cuyo número de
   filas afectadas se verifica dentro de la transacción. La garantía sigue estando en la base, que es
   lo que la convención [005] pide; cambia el mecanismo porque el dato tiene otra forma (research §4).
5. **Se congela lo que sale impreso, y el logo es la única excepción.** Diez datos del emisor y tres
   del cliente se copian a la factura al emitirla. La alícuota **no** se congela: se deriva del tipo
   de comprobante, que sí está congelado, y agregarle una columna sería un campo que ninguna FR pide
   (research §5).
6. **Los seis cambios al Módulo 5 están enumerados uno por uno** y la lista es verificable en la
   revisión. Cinco son aditivos; uno solo cambia el comportamiento de una operación existente —rendir
   ahora exige el remito—. La limitación que eso deja —un viaje rendido sin remito de antes no se
   puede facturar— queda declarada y **no se le abre un camino de corrección**, porque sería revertir
   la decisión que el Módulo 5 tomó a propósito (research §8).

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10
sobre SQL Server, MailKit, React 19 + React Router + Vite) **más una nueva: `QuestPDF` 2026.7.3**,
para generar el documento de la factura. Es la primera dependencia que agrega un módulo de negocio
desde el 2, y va detrás de `IArmadorDocumentoFactura` en la capa de aplicación: la capa de dominio y
los casos de uso no la conocen. Su licencia es gratuita para organizaciones de menos de USD 1M de
facturación anual, que es el caso (research §1). **Ninguna variable de entorno nueva**: el documento
va al mismo volumen que los escaneos de los Módulos 3 y 4

**Storage**: SQL Server 2022. Una migración nueva (`Modulo6Facturacion`) crea `EmpresaEmisora`,
`Facturas` y `CambiosDeEstadoFactura`, y **modifica `Viajes`**: le agrega `FacturaId` anulable y su
índice. **Es la primera migración del sistema que toca una tabla de un módulo anterior**, y está
autorizada explícitamente por FR-051 a FR-056 de la spec. No hay migración de datos: `FacturaId` nace
nula y el estado `Facturado` no cambia el de ninguna fila existente. Los tres permisos nuevos los
siembra `SembradorInicial`, que ya corre en cada arranque y es idempotente

**Testing**: xUnit en `GT.UnitTests` (reglas puras: cálculo de neto/IVA/total con el ejemplo de la
propia spec, derivación de `vencida` a una fecha dada, transiciones de la factura, validación del
formato del número de comprobante, alícuota por tipo) y `GT.IntegrationTests`
(`WebApplicationFactory` contra el SQL Server del compose: los dos índices únicos filtrados incluida
la carrera entre dos operadores, la atomicidad de la emisión y de la anulación, la igualdad byte a
byte entre vista previa y documento guardado, la coincidencia entre la derivación en SQL y la del
dominio, la generación real de un PDF —que es lo que detecta la falta de `libfontconfig1`— y los tres
permisos); Vitest + React Testing Library en el frontend, con un caso nuevo de patrón: la vista previa
en PDF sobre una URL de `Blob`

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales. **`backend/Dockerfile` se modifica** para instalar `libfontconfig1` y `libfreetype6` en la
etapa de ejecución (research §1)

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: los listados responden en menos de 1 segundo (p95) con el volumen real. El
filtro por estado derivado, la exclusión de anuladas, el panel de vencimientos y los totales se
resuelven **dentro de la consulta SQL**. El listado pagina de a 20 filas con el total, filtrando antes
de paginar (FR-059). La generación del PDF ocurre en la emisión, en la corrección y en la anulación
—operaciones de una por vez, no de listado—, y una factura de decenas de viajes se arma en menos de un
segundo

**Constraints**: ningún viaje puede pertenecer a dos facturas vigentes, ni siquiera con dos operadores
simultáneos, y la garantía está en la base (FR-053, SC-005); o se facturan todos los viajes de la
factura o no se factura ninguno, y lo mismo al anular (FR-048, FR-054); el documento guardado y la
ficha **nunca discrepan**, lo que obliga a regenerar en las tres operaciones que cambian lo impreso y
a que la regeneración de la anulación viva dentro de su transacción (FR-031b, SC-007a); la vista previa
no crea la factura ni guarda ningún archivo (FR-033); ningún paso irreversible se ejecuta sin
confirmación previa (SC-009); una factura emitida no cambia de cliente, de viajes ni de importes por
ningún camino (FR-036, SC-013); un viaje `facturado` es inmutable para todos los roles (FR-052); el
historial no es editable ni borrable (FR-045); los importes son `decimal`, nunca punto flotante

**Scale/Scope**: una única empresa — decenas de facturas por mes, cientos de clientes. En este módulo:
**7 pantallas nuevas, 17 endpoints nuevos, 3 endpoints modificados (del Módulo 5), 3 tablas nuevas,
1 tabla modificada, 3 permisos nuevos y 1 dependencia nueva**, que cubren **86 requisitos
funcionales** (FR-001 a FR-068, más FR-009a, FR-011a, FR-019a, FR-031a a FR-031j, FR-034a, FR-043a,
FR-049a, FR-055a y FR-058a) y **16 criterios de éxito**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | **Una sola dependencia nueva**, justificada abajo, y cero infraestructura propia: el almacén de archivos, el validador por firma, la paginación, la autorización por permiso, el menú resuelto por el servidor, `TimeProvider` y los dos formateadores del frontend se consumen tal como están (research §13). El módulo **no construye** una tabla intermedia viaje↔factura, ni una columna `Vencida`, ni una columna `AlicuotaIva`, ni una columna espejo de refacturación, ni una auditoría de valores, ni un historial de documentos generados: seis piezas evaluadas y descartadas, cada una con su motivo escrito (data-model §*Lo que este modelo deliberadamente no tiene*) |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes en español rioplatense con voseo, definidos textualmente en `contracts/README.md`. **El documento generado también**: es texto visible del producto y sale con formato de comprobante argentino, importes con `$ 1.240.000,00` y fechas `dd/MM/yyyy`. Los importes en pantalla usan `compartido/moneda` y las fechas `compartido/fechas`, nunca `toFixed(2)` ni `toLocaleDateString` |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 86 requisitos y nada más. Queda explícitamente afuera, tal como fija la spec: la emisión electrónica ante AFIP/ARCA y el CAE por web service, las notas de crédito y débito, las facturas de varios períodos o clientes, percepciones, retenciones, IIBB, descuentos y recargos, la facturación de conceptos que no sean viajes, la cuenta corriente con saldos, la liquidación al transportista, el envío por email, el portal de autoconsulta, el registro contable, el libro IVA ventas, la moneda extranjera y la facturación recurrente. **Tres tentaciones concretas se anotaron y no se construyen**: la reversión del cobro (`pagada` es terminal, FR-043), la auditoría de valores anteriores en la corrección (FR-037) y la alícuota configurable (*Assumptions*). Ninguna se hace acá |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 7 historias se validan operando la app, con el recorrido de `quickstart.md`: 46 pasos, cada uno con el criterio que verifica. **Tres cosas no se pueden verificar a mano y se declara por qué**: la carrera de SC-005, la igualdad byte a byte entre vista previa y documento, y la coincidencia entre la derivación en SQL y en C#. Las tres quedan en tests y el quickstart lo dice, en vez de pedirle a quien valida algo que no puede hacer |
| V. Datos del Usuario con Respeto | ✅ Pasa | De la empresa emisora se piden exactamente los diez datos que salen impresos en el comprobante, y seis de ellos son opcionales. **El historial guarda quién y cuándo, y nada más** (FR-037): no registra qué campos cambiaron ni sus valores anteriores, porque la spec no lo pide y sería recolectar por las dudas. **El documento se sirve sólo por endpoint autorizado**, nunca por URL pública: guardar una URL escrita a mano —como sugería el enunciado— dejaría el comprobante de un cliente fuera de todo control de acceso. Ningún dato se borra físicamente y ningún secreto entra al código |

**Sobre el Principio I y la dependencia nueva**: es la primera que agrega un módulo de negocio desde
el 2, y merece justificarse por escrito. FR-031 pide **generar** el documento, no recibirlo: sin
biblioteca, la alternativa es escribir un generador de PDF a mano, que es órdenes de magnitud más
complejo que integrar uno. Las tres opciones se compararon en research §1 y QuestPDF es la única que
no arrastra licencia AGPL, binario nativo de gran tamaño ni un motor HTML que volvería técnicamente
viable la segunda maqueta que FR-033 prohíbe.

**Sobre el Principio III y el Módulo 5**: es la primera vez que una feature modifica un módulo de
negocio anterior, y la spec lo autoriza acotándolo a **seis** requisitos. El diseño los enumera uno
por uno en research §8 para que la lista sea verificable en la revisión, y agrega **una sola cosa que
ninguna FR del Módulo 6 pide**: la línea de `CambioDeEstadoViaje` al facturar y al desfacturar. No es
alcance nuevo sino cumplimiento de una regla vigente —FR-035 del Módulo 5 exige historial para todo
cambio de estado, y FR-051 declara que estos dos lo son—; omitirla dejaría la ficha del viaje
mostrando `facturado` sin una línea que lo explique.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los cinco principios se sostienen. Seis cosas que el diseño confirmó o
descubrió, y que conviene tener a la vista al implementar:

- **La vista previa obligó a invertir el diseño obvio, y salió más simple.** El primer impulso es
  armar un DTO desde el formulario para la vista previa y otro desde la fila guardada para el
  documento. Eso son dos traducciones al mismo destino, que pueden diferir sin que nadie lo note: el
  mismo problema de FR-033 un escalón más abajo. Con la **entidad en memoria** como entrada única de
  las dos, el mapeo es uno solo y SC-007b se vuelve verificable con un test que compara byte a byte.
- **La convención [005] sobre exclusividad no se aplica literal acá, y está bien.** Allá la
  exclusividad se escribía como índice único filtrado; acá una columna escalar ya la garantiza por
  construcción y lo que falta cerrar es sólo la carrera. Lo que [005] realmente pide —que la garantía
  esté en la base y no en la pantalla— se cumple con el `UPDATE` condicional. Vale como precedente:
  **la convención nombra el objetivo, no el mecanismo**.
- **`libfontconfig1` es el único punto del módulo que falla en producción sin fallar antes.** El
  backend compila, restaura, arranca y sirve todo; la primera factura revienta. Va un test de
  integración que genera un PDF de verdad, para que la falta se note en CI. Está anotado como trampa
  en research §15 y como primer paso del quickstart.
- **La corrección y la anulación regeneran el documento, y no es simetría gratuita.** La anulación
  regenera **dentro** de su transacción porque FR-031b lo exige explícitamente: si el documento no se
  puede armar, la anulación no queda aplicada a medias y los viajes no vuelven a `rendido`. La
  corrección puede regenerar fuera, pero se escribe igual, porque una regla con una excepción es dos
  reglas.
- **El orden del listado tiene una salvedad chica y real**: dos facturas **anuladas** pueden compartir
  número, porque el índice único las excluye. Dos anuladas del mismo día y número podrían
  intercambiarse entre páginas. Exige emitir, anular y reemitir con el mismo número, y el resultado no
  confunde a nadie. Se deja anotado y no se agrega un desempate por `Id`, que sería ruido en el 100%
  de los casos restantes (research §10).
- **Los tres permisos hacen del 6 el módulo con la autorización más granular del sistema**, y no
  agregó una línea de maquinaria: el `PermisoHandler`, las políticas y el catálogo de menú del Módulo
  1 los absorbieron sin cambios. Es la confirmación de que autorizar por permiso y nunca por rol
  —convención [004]— escala.

## Project Structure

### Documentation (this feature)

```text
specs/006-gestion-facturacion/
├── plan.md                    # Este archivo
├── research.md                # Decisiones técnicas y alternativas descartadas
├── data-model.md              # Tablas, campos, reglas derivadas y transacciones
├── quickstart.md              # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md              # Contrato de UI: pantallas, mensajes y textos
│   └── facturacion-api.yaml   # Contrato HTTP (OpenAPI 3.0)
├── checklists/                # Ya presentes, de /speckit-checklist
└── tasks.md                   # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**.

```text
backend/
├── Dockerfile                                    # MODIFICADO — libfontconfig1 + libfreetype6
├── src/
│   ├── GT.Api/
│   │   ├── Facturacion/                          # NUEVO
│   │   │   ├── EmpresaEmisoraEndpoints.cs        #   configuración + logo (5 endpoints)
│   │   │   ├── FacturasEndpoints.cs              #   listado, ficha, alta, corrección, documento
│   │   │   ├── ArmadoEndpoints.cs                #   facturables, anuladas sin reemplazo, vista previa
│   │   │   ├── CicloDeVidaFacturaEndpoints.cs    #   cobro y anulación (FR-044)
│   │   │   ├── ReportesFacturacionEndpoints.cs   #   vencimientos y totales
│   │   │   └── RespuestasDeFactura.cs            #   la traducción resultado → HTTP, en un solo lugar
│   │   ├── Viajes/ViajesEndpoints.cs             # MODIFICADO — la factura en listado y ficha
│   │   ├── Viajes/CicloDeVidaEndpoints.cs        # MODIFICADO — rendición exige remito
│   │   └── Program.cs                            # MODIFICADO — registra el grupo y los tres permisos
│   ├── GT.Application/
│   │   ├── Facturacion/                          # NUEVO — carpeta espejo del módulo
│   │   │   ├── EmpresaEmisora/                   #   consultar, guardar, logo (subir/quitar/servir)
│   │   │   ├── EmitirFactura.cs                  #   la operación central: valida, confirma, transacción
│   │   │   ├── ConsultarFacturables.cs           #   FR-015 a FR-019a
│   │   │   ├── VistaPreviaFactura.cs             #   arma y devuelve; no persiste nada
│   │   │   ├── ConsultarFacturas.cs              #   5 filtros + página de 20
│   │   │   ├── ConsultarFichaFactura.cs          #   incluye historial y las dos referencias
│   │   │   ├── CorregirFactura.cs                #   4 campos + regeneración + entrada de historial
│   │   │   ├── RegistrarCobro.cs
│   │   │   ├── AnularFactura.cs                  #   motivo + viajes a rendido + regeneración
│   │   │   ├── ConsultarVencimientos.cs          #   7 días corridos
│   │   │   ├── ConsultarTotalesFacturacion.cs    #   rango obligatorio, anuladas excluidas
│   │   │   ├── IArmadorDocumentoFactura.cs       #   la frontera con QuestPDF
│   │   │   ├── DatosDelDocumento.cs              #   el mapeo único que usan vista previa y emisión
│   │   │   ├── IRepositorioFacturas.cs
│   │   │   ├── IRepositorioEmpresaEmisora.cs
│   │   │   ├── Dtos.cs
│   │   │   ├── NombresDeEstadoFactura.cs
│   │   │   └── Mensajes.cs                       #   textos en es-AR y códigos de error
│   │   ├── Viajes/RendirViaje.cs                 # MODIFICADO — exige remito (FR-055a)
│   │   ├── Viajes/Dtos.cs                        # MODIFICADO — la factura del viaje (FR-055)
│   │   └── Autenticacion/CatalogoOpcionesMenu.cs # MODIFICADO — cuatro entradas del módulo
│   ├── GT.Domain/
│   │   ├── Facturacion/                          # NUEVO
│   │   │   ├── FacturaCliente.cs                 #   entidad principal, con las copias congeladas
│   │   │   ├── EmpresaEmisora.cs                 #   única fila del sistema
│   │   │   ├── CambioDeEstadoFactura.cs          #   historial + correcciones
│   │   │   ├── EstadoFactura.cs                  #   3 valores; su orden sostiene 2 índices
│   │   │   ├── EstadoFacturaVisible.cs           #   4 valores, uno derivado
│   │   │   ├── TransicionesDeFactura.cs          #   regla pura (FR-043)
│   │   │   ├── AlicuotasIva.cs                   #   21 / 21 / 0, fijas (FR-023)
│   │   │   ├── CalculadorImportes.cs             #   neto, IVA, total; redondeo comercial
│   │   │   ├── NumeroDeComprobante.cs            #   formato 0000-00000000 (FR-027)
│   │   │   └── DerivadorEstadoFactura.cs         #   `vencida` a una fecha dada (FR-041)
│   │   ├── Viajes/EstadoViaje.cs                 # MODIFICADO — Facturado = 4, al final
│   │   ├── Viajes/TransicionesDeViaje.cs         # MODIFICADO — dos pares nuevos + EsTerminal
│   │   ├── Viajes/Viaje.cs                       # MODIFICADO — FacturaId
│   │   └── Usuarios/Rol.cs                       # MODIFICADO — los tres códigos de permiso
│   └── GT.Infrastructure/
│       ├── Documentos/                           # NUEVO
│       │   └── ArmadorDocumentoFacturaQuestPdf.cs #  la única clase que conoce QuestPDF
│       ├── Persistencia/
│       │   ├── Configuraciones/                  # NUEVO — 3 configuraciones
│       │   ├── Configuraciones/ViajeConfiguracion.cs # MODIFICADO — FacturaId y su índice
│       │   ├── RepositorioFacturas.cs            # NUEVO — filtros, derivación y agregaciones en SQL
│       │   ├── RepositorioEmpresaEmisora.cs      # NUEVO
│       │   ├── GtDbContext.cs                    # MODIFICADO — 3 DbSet
│       │   └── Migraciones/                      # NUEVO — Modulo6Facturacion
│       └── DatosIniciales/SembradorInicial.cs    # MODIFICADO — tres permisos y su reparto
└── tests/
    ├── GT.UnitTests/Facturacion/                 # NUEVO — reglas puras
    └── GT.IntegrationTests/Facturacion/          # NUEVO — índices, transacciones, PDF, permisos

frontend/
└── src/
    ├── modules/facturacion/                      # NUEVO
    │   ├── paginas/                              #   empresa, listado, alta, ficha, corrección,
    │   │                                         #   vencimientos, totales
    │   ├── componentes/                          #   filtros, paginación, selector de viajes,
    │   │                                         #   vista previa en PDF, confirmaciones
    │   └── servicios/
    ├── modules/viajes/                           # MODIFICADO — estado `Facturado` y su factura
    └── App.tsx                                   # MODIFICADO — rutas del módulo
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Facturacion/` como carpeta espejo del módulo de negocio, alineada 1 a 1 con
`specs/006-gestion-facturacion/` y con `frontend/src/modules/facturacion/`, tal como fija la
constitución.

`EmpresaEmisora` vive **dentro** del módulo `facturacion` y no como módulo hermano: existe para que la
factura salga con los datos del emisor, no tiene spec propia y comparte sus permisos. Es el mismo
criterio con el que `Cliente` quedó dentro de `viajes` y `TipoVehiculo` dentro de `flota`.

`GT.Infrastructure/Documentos/` es carpeta nueva y **la única del sistema que conoce QuestPDF**. La
capa de aplicación habla con `IArmadorDocumentoFactura` y el dominio no sabe que existe un PDF.

**Lo que llama la atención de esta lista**: la columna de MODIFICADOS tiene **doce archivos**, y es la
primera vez que no son todos puntos de extensión. Ocho lo son —el registro de servicios, el catálogo
de menú, los códigos de permiso, el sembrador, el `DbContext`, `App.tsx`—; los otros cuatro son
**archivos de negocio del Módulo 5**, y están ahí porque FR-051 a FR-055a lo autorizan explícitamente.
Más el `Dockerfile`, que es infraestructura y se toca por primera vez desde el Módulo 1.

## Complexity Tracking

Cinco piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Una dependencia nueva (QuestPDF)** y un cambio en el `Dockerfile` | FR-031 pide **generar** el documento. Sin biblioteca hay que escribir un generador de PDF a mano | iText (AGPL: obliga a liberar todo el sistema o a pagar), wkhtmltopdf o Chromium (binario nativo pesado, y un motor HTML vuelve técnicamente viable la segunda maqueta que FR-033 prohíbe), PdfSharp (hay que paginar la tabla a mano). Comparación completa en research §1 |
| **Trece columnas de datos congelados** en `Facturas` —diez del emisor, tres del cliente— | FR-034, FR-034a y SC-007: una factura dice a quién se le facturó ese día, no quién es hoy | Referenciar la configuración y el padrón, y leerlos al mostrar. Descartada porque es exactamente lo que la spec prohíbe: corregir un domicilio cambiaría facturas ya emitidas, y la ficha diría algo distinto del documento ya generado |
| **La regla de `vencida` escrita dos veces** —función pura en el dominio y predicado en la consulta— | FR-058a exige **filtrar** por el estado derivado, y filtrar en memoria después de paginar devolvería páginas incompletas | Filtrar por la columna `Estado`. Descartada porque devolvería bajo `pendiente` facturas que el propio listado muestra como `vencida` en la fila de al lado. La duplicación se cubre con un test que compara las dos sobre el mismo dato, que es la convención [003] |
| **Un `UPDATE` condicional con verificación de filas afectadas** para marcar los viajes | Es lo que cierra la carrera de SC-005, y la unicidad ya la garantiza la columna escalar | Una tabla intermedia `FacturaViajes` con índice único filtrado. Descartada en research §4: daría la misma garantía y agregaría una tabla que puede desincronizarse del estado del viaje — la "tabla de ocupaciones aparte" que [005] ya había descartado |
| **La entidad en memoria como entrada única del armador** | Es lo que hace que SC-007b sea verificable y no una intención | Dos DTO, uno por camino. Descartada en research §2: son dos traducciones al mismo destino que pueden diferir sin que nadie lo note, que es el problema de FR-033 un escalón más abajo |

Las cinco se resuelven con lo que ya viene en el marco de trabajo, en el proyecto o en una única
biblioteca de terceros —EF Core, índices filtrados de SQL Server, `TimeProvider`, `Intl`, QuestPDF—,
sin servicios externos ni infraestructura propia.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una
línea por decisión, con referencia a la spec (`[006] ...`), en la sección *Decisiones transversales ya
tomadas*. Sólo entran las que son **transversales y relevantes para el proyecto** y que futuras
features pueden aprovechar; nada por completar la lista.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[006]` Cuando **una misma información se produce por dos caminos** —una vista previa y un archivo
  guardado, una pantalla y un documento—, los dos llaman al **mismo armador sobre la misma entrada**,
  y esa entrada es la entidad, no un DTO por camino. Dos traducciones al mismo destino se separan sin
  que nadie lo note, y entonces revisar la vista previa deja de servir para algo.
- `[006]` **Lo que sale impreso en un comprobante se congela en él al emitirlo**, con copia de los
  datos **y** referencia a la entidad de origen: la copia es lo que se muestra, la referencia es lo
  que permite filtrar y totalizar. Un documento dice a quién se le facturó ese día, no quién es hoy.
- `[006]` Una **convención nombra el objetivo, no el mecanismo**. La exclusividad de [005] se escribía
  como índice único filtrado; cuando el dato ya es una columna escalar, la unicidad es estructural y
  lo único que falta cerrar es la carrera, con un `UPDATE` condicional cuyo número de filas afectadas
  se verifica. Lo que no cambia es que la garantía viva en la base y no en la pantalla.
- `[006]` Un **estado derivado que además se filtra** obliga a escribir la regla dos veces —dominio y
  consulta— y sus valores tienen que ser **excluyentes**: si `vencida` sale también bajo `pendiente`,
  el filtro contradice a la columna que la propia fila muestra.
- `[006]` Una **dependencia que sólo falla en tiempo de ejecución** —una biblioteca con requisitos
  nativos— lleva un test de integración que la ejercita de verdad. Compilar, restaurar y arrancar bien
  no prueban nada sobre ella, y el primer usuario que la despierte va a ser quien opera.
- `[006]` Cuando una feature **modifica un módulo anterior**, la spec acota los cambios a una lista
  numerada y el plan los enumera uno por uno, para que la revisión pueda contarlos. Lo que se agrega
  sin estar en la lista se justifica contra una regla **ya vigente** del módulo tocado, o no se
  agrega.
