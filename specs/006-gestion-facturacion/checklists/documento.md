# Documento y vista previa — Checklist de calidad de requisitos: Gestión de facturación (Módulo 6)

**Purpose**: Validar que los requisitos del **documento de la factura** y de su **vista previa** estén
completos, claros, consistentes y verificables antes de descomponer el módulo en tareas. No verifica
el sistema construido: verifica lo que la spec dice —y lo que no dice— sobre el documento.
**Created**: 2026-08-12
**Feature**: [spec.md](../spec.md)
**Alcance**: FR-003, FR-004, FR-031 … FR-031j, FR-033, FR-034, FR-034a, FR-035, SC-007a, SC-007b
**Uso**: recorrido por el autor de la spec antes de `/speckit-tasks`; cada ítem que falla se arregla
editando `spec.md`, no la implementación.

## Completitud de la disposición del documento

- [x] CHK001 ¿Está especificado en qué bloque del documento sale el **período (mes y año)**? FR-031 enumera nueve bloques y ninguno lo incluye, mientras FR-033 y el escenario 16 de la User Story 2 lo dan por presente en la vista previa [Gap, Spec §FR-031, §FR-033] — **resuelto**: sale en el bloque de identificación, en formato `MM/AAAA` (FR-031, bloque 4)
- [x] CHK002 ¿Está especificado dónde sale impreso el **detalle** de la factura? FR-031b manda regenerar el documento cuando el detalle cambia —lo que supone que sale impreso— pero ningún bloque de FR-031 lo ubica [Conflict, Spec §FR-031, §FR-031b, §FR-013] — **resuelto**: en el pie de importes, a su izquierda, bajo el rótulo `Observaciones`, omitido entero cuando está vacío (FR-031, bloque 9)
- [ ] CHK003 ¿Está definido si el **teléfono y el email** del emisor salen impresos y en qué bloque? FR-034 los congela junto con los demás datos del emisor, y FR-031 imprime sólo siete de los diez congelados [Gap, Spec §FR-034, §FR-031]
- [ ] CHK004 ¿Están especificadas la **posición y la forma** de la leyenda de anulada y de su motivo dentro del documento? "De forma visible" no ubica el texto ni acota su extensión frente a un motivo de 500 caracteres [Ambiguity, Spec §FR-031d, §FR-046]
- [ ] CHK005 ¿Está definido el comportamiento del documento cuando la tabla de detalle **abarca más de una página** —repetición del encabezado de columnas, numeración de páginas, ubicación del pie de importes—? La spec sólo dice que "sigue de largo las páginas que haga falta" [Gap, Spec §FR-031e]
- [ ] CHK006 ¿Están especificados el **tamaño y la orientación de página** del documento generado, dado que es un comprobante pensado para imprimirse y mandarse al cliente? [Gap, Spec §FR-031]
- [ ] CHK007 ¿Está definido cómo se compone la celda `Producto / Servicio` a partir del origen, el destino y el remito —orden, separadores y comportamiento ante textos largos—? [Clarity, Spec §FR-031e]
- [ ] CHK008 ¿Está especificado si las columnas de importe de la tabla de detalle llevan símbolo de moneda o sólo el número, y si `Precio unit.` e `Importe` se muestran idénticos por definición? [Clarity, Spec §FR-031e, §FR-026]

## Claridad y medibilidad del formato

- [ ] CHK009 ¿Está definido el formato exacto de la columna `% IVA` para los tres tipos? La spec escribe el valor de una `Factura C` (`0,00 %`) y no el de una `A` o una `B` [Clarity, Spec §FR-031j, §FR-023]
- [ ] CHK010 ¿La exigencia de usar "el formato de moneda y de fecha del resto del sistema" identifica esos formatos de manera verificable por alguien que no lee código? [Measurability, Spec §FR-031]
- [ ] CHK011 ¿Puede verificarse objetivamente que la vista previa coincide "dato por dato y bloque por bloque" con el documento guardado, o el criterio depende de una comparación a ojo? [Measurability, Spec §SC-007b]
- [ ] CHK012 ¿Está definido qué constituye "un nombre que identifique la factura" en el archivo servido —número de comprobante, cliente, fecha— de modo que dos personas escriban el mismo nombre? [Ambiguity, Spec §FR-031a]
- [ ] CHK013 ¿Está expresado de forma verificable el requisito de que el documento **no se presente como comprobante fiscal**: una leyenda impresa, un texto en pantalla, ninguna de las dos? [Measurability, Spec §FR-031c]

## Consistencia entre ficha, vista previa y documento

- [ ] CHK014 ¿Los datos que FR-033 exige en la vista previa coinciden exactamente con los bloques que FR-031 exige en el documento, dado que la spec obliga a que sean el mismo armado? [Consistency, Spec §FR-031, §FR-033]
- [ ] CHK015 ¿Los nueve bloques de FR-031 cubren todos los datos que FR-060 exige en la ficha, siendo que SC-007a obliga a que ficha y documento coincidan dato por dato? [Consistency, Spec §FR-060, §SC-007a]
- [x] CHK016 ¿Está resuelto sin ambigüedad **qué logo lleva el documento regenerado** de una factura vieja después de haberse cambiado el logo en la configuración? FR-034 excluye al logo del congelamiento y FR-031b obliga a regenerar [Conflict, Spec §FR-034, §FR-031b] — **resuelto**: lleva el logo vigente, escrito como consecuencia declarada en FR-034 y como caso de borde propio
- [ ] CHK017 ¿Es consistente la lista de datos del cliente congelados en FR-034a —razón social, CUIT y domicilio— con lo que el bloque del cliente imprime, que incluye además condición de IVA y condición de venta? [Consistency, Spec §FR-031, §FR-031h, §FR-034a]
- [ ] CHK018 ¿Está especificado que la banda de vencimiento de pago del documento refleja el valor corregido después de una corrección, con el mismo criterio con que FR-031b lo exige para el CAE? [Consistency, Spec §FR-031, §FR-031b, §FR-035]

## Vista previa: alcance, precondiciones y permisos

- [ ] CHK019 ¿Está especificado **con qué permiso** se pide la vista previa? FR-067 asigna la emisión al permiso de gestión y la descarga del documento al de consulta, sin nombrar la vista previa [Gap, Spec §FR-067, §FR-033]
- [ ] CHK020 ¿Está definido si la vista previa aplica los mismos rechazos que la emisión —empresa emisora incompleta (FR-006), cliente sin domicilio (FR-011a), viaje sin remito (FR-019a)— o si se puede previsualizar una factura que después no se va a poder emitir? [Gap, Spec §FR-033]
- [ ] CHK021 ¿Está definido qué hace la vista previa con los datos que el documento imprime pero que todavía pueden estar vacíos al mirarla —CAE, vencimiento del CAE, número de comprobante—: los exige, los deja en blanco o los marca? [Gap, Spec §FR-033, §FR-031]
- [ ] CHK022 ¿Está especificado qué ocurre cuando la vista previa **no se puede producir**, sabiendo que en ese momento no hay factura ni archivo que revertir? [Coverage, Exception Flow, Gap, Spec §FR-033]
- [ ] CHK023 ¿La prohibición de mantener "una segunda maqueta del comprobante" está escrita como requisito verificable sobre el producto, o queda como intención de diseño que nadie puede comprobar desde afuera? [Measurability, Spec §FR-033]

## Ciclo de vida del archivo: generación, regeneración y fallas

- [x] CHK024 ¿Está especificado qué ocurre con la factura si el documento **no se puede generar al emitir**? FR-031b resuelve explícitamente el caso de la anulación y deja el de la emisión sin decir [Gap, Exception Flow, Spec §FR-031, §FR-031b] — **resuelto**: la emisión se rechaza entera, con el criterio de todo o nada de FR-054. De paso, FR-031b extendió la misma regla a la corrección, que tampoco la tenía
- [ ] CHK025 ¿"No conservar el documento viejo" está definido como **borrado del archivo anterior**, y está previsto qué pasa si ese borrado falla después de escribirse el nuevo? [Ambiguity, Spec §FR-031b]
- [ ] CHK026 ¿La lista de campos que disparan la regeneración (FR-031b) coincide exactamente con la lista de campos corregibles (FR-035), sin que sobre ni falte ninguno en una de las dos? [Consistency, Spec §FR-031b, §FR-035]
- [x] CHK027 ¿Está definido si registrar el cobro regenera o no el documento? Es el único cambio de estado que FR-031b no nombra, y la spec no dice si la fecha de cobro sale impresa [Coverage, Gap, Spec §FR-031b, §FR-042] — **resuelto**: la fecha de cobro no sale impresa y cobrar no regenera. Las operaciones que regeneran son exactamente tres: emitir, corregir y anular
- [ ] CHK028 ¿Está definido el efecto de **quitar el logo** (FR-003) sobre los documentos ya generados que lo llevan impreso y sobre una regeneración posterior de esas facturas? [Coverage, Gap, Spec §FR-003, §FR-034]

## Cobertura de borde y requisitos no funcionales

- [ ] CHK029 ¿Hay requisitos sobre el **tamaño máximo y las proporciones del logo** aceptado, para que un archivo desproporcionado no rompa el bloque del emisor? FR-003 fija sólo los formatos admitidos [Gap, Edge Case, Spec §FR-003, §FR-031g]
- [ ] CHK030 ¿Existe algún requisito de **tiempo de respuesta**, o alguna cota de cantidad de viajes por factura, para generar el documento y la vista previa? [Non-Functional, Gap, Spec §FR-031, §FR-033]
- [ ] CHK031 ¿Están definidos los requisitos de **accesibilidad de la vista previa** —alternativa para quien no puede leer un visor de PDF embebido, anuncio accesible del resultado— con el mismo criterio que FR-065 aplica al resto del módulo? [Coverage, Gap, Spec §FR-065, §FR-033]
- [ ] CHK032 ¿Está especificado cómo se ve en el documento una fila de **viaje con importe cero** y su subtotal, más allá de la confirmación previa que exige FR-032? [Edge Case, Spec §FR-031f, §FR-032]

## Trazabilidad y supuestos

- [ ] CHK033 ¿Tiene FR-031f —subtotales informativos y diferencia por redondeo contra el pie— algún escenario de aceptación o criterio de éxito que lo verifique, o es el único requisito del documento sin cobertura? [Traceability, Gap, Spec §FR-031f]
- [ ] CHK034 ¿Está validado el supuesto de que una única biblioteca de generación resuelve las tres exigencias del documento —logo embebido, formato de moneda argentino y tabla que salta de página—? [Assumption, Spec §Assumptions, §FR-031]
- [ ] CHK035 ¿Está escrito como **requisito**, y no sólo como decisión técnica, que el documento se guarda con el mecanismo de archivos de los Módulos 3 y 4 —volumen fuera del repositorio, nombre generado por el sistema, endpoint autorizado, apertura en línea—? [Traceability, Spec §FR-031a]

## Notes

- Cada ítem pregunta por lo que la **spec dice o no dice**, no por lo que el sistema hace. Un ítem que
  falla se resuelve editando `spec.md`; si al resolverlo aparece una decisión de negocio nueva, va a
  la sección **Clarifications**.
- **Los cinco de mayor impacto ya están resueltos** —CHK001, CHK002, CHK016, CHK024 y CHK027— y las
  decisiones quedaron escritas en la sección *Clarifications* de la spec, sesión "recorrido del
  checklist `documento.md`". Quedan abiertos los 30 restantes, ninguno de los cuales cambia qué se
  implementa: son precisiones de redacción, de formato y de cobertura.
- Marcar con `[x]` a medida que se resuelven, anotando al lado el FR que quedó modificado.
