# Evidencia de aceptación — 015 Ampliación de reportes

Fecha: 2026-10-21, Argentina. Operador: agente mediante Chrome real, HTTP y SQL Server real. Esta evidencia no representa aprobación humana. Datos sintéticos en bases efímeras; no datos de desarrollo. Herramientas, capturas y archivos en `/tmp/gt015-aceptacion/`, no versionados.

Entorno: SDK10.0.302/.NET10.0.10, Node24.22.0/npm11.19.1, SQL16.0.4265.3, React19.2.8/Recharts3.10.1. Snapshot explícito ON; RCSI heredado sin cambios. Fuentes aprobadas sin modificar; R10 documental gt-ui pendiente.

## T084 — Dashboard, Viajes y Vencimientos

- Dashboard leído en UI y HTTP con octubre2026; cotejo SQL directo independiente:921viajes,810realizados,1pendienteRendición,110pendientes,importe92111.31; facturado48404.00,cobrado24202.00,liquidado40004.00,pagado30002.00,ingresos/egresos505.00cada y30unidades. Documentos y flujos mantienen sus perspectivas.
- Cliente sintético inactivo con21viajes:10Rendidos,10Pendientes,1EnCurso,importe2102.31. UI seleccionó referencia inactiva, página1mostró20 y página2una; totales completos iguales. Generación Excel desde página2 entregó21, comprobado en XML numérico/fechas/importes del libro real, igual que XLSXlegacy.
- Cinco exportaciones012 invocadas por HTTP en ambos formatos: diez archivos reales con firmas PDF/ZIP, sin cambio de rutas. Nuevos y legacy deViajes21 conservan21filas.
- Tres paneles Vencimientos cotejados contra sus APIs originales: mismo universo/orden e identidades de primeras20filas, sin período global. Choferes200alertas yFlota24: seis unidades inactivas no alertan según004, mientras R6síincluye las30. Primera referenciaSQL contó todas30erróneamente; corregida al universo activo sin modificar producto, nuevas consultas ya coincidían con originales.
- Borde UTC real simulado sólo en Date del navegador:2026-11-01T01:30Z propone01/10–31/10Argentina; temporizadores no falseados. Período y contexto visibles, enlaces mantienen días; vencimientos siguen actuales.
- Agregados SQL antes/después de consultas/exportación idénticos. Preparación21viajes fue operación explícita de fixture, anterior a la foto de contraste; Reportes no mutó negocio.

Comandos: compilación `/tmp/gt015-control/Control015.csproj`; `Control015.dll preparar21 <base-efímera>` una vez, `contrastar <base-efímera>` antes/después; `node /tmp/gt015-browser/t084.cjs`. JSONreal`t084.json`, capturas`t084-*.png`, diez archivoslegacy y nuevo-viajes21.xlsx en temporal. Host sigue activo para escenarios siguientes; limpieza se registra al cierre.

## Validaciones finales relacionadas ya ejecutadas

T081:848unitarios y1306integraciónSQL completos aprobados; T082:699frontend/101archivos,build/typecheck/lint correctos. T083:11recursos×20muestras cumplieron20/20<=5s, sinN+1entre20/200; PDF1000filas1203ms yXLSX2010ms con reapertura monetaria.64planes/IO, sin índices nuevos; base de medición eliminada. Detalle de comandos, corridas iniciales/focales y warnings en [implementation-log.md](implementation-log.md).

## T085 — Documentos y flujos financieros

Nueva base efímera: la anterior deT084 fue eliminada y verificada por metadataSQL. Fixture creó facturas históricas y ejecutó Generar/Anular014 reales con fechasSept30/Oct15, anteriores a hoy; no cambios de producto. Compilación temporal inicial usó un evento manual inexistente; se corrigió al modelo009real (parcial sóloOP, eventoPagada sólo al completar), sin código productivo.

- Factura25/09 cobrada05/10por121.01, factura10/10pendiente242.02 y anulada12/10nominal363.04. Octubre documental facturado/pendiente242.02, flujo121.01de documento anterior; snapshot «histórico» distinto del nombreactual e inactivo visible. API/UI/PDF/XLSX correctos.
- Transportista con LQ septiembre300,pago parcial50; octubre200sinpago y agosto100pagadaenOctubre. Rango15/09–10/10 incluye LQ09/10completas (total500,pagado50,pendiente450) y flujos exactos150porOPmanual, incluyendoagosto. Ambas pestañas/UI y cuatro archivos reales cotejados.
- OPPvigente con3OPaporta363una vez; OPPanulada15/10con3OPde30/09aporta0vigente y363histórico al consultarseptiembre. UI identifica3Revertido/OPP/origen y conserva referencia/fecha/autor. PDF/XLSX de pagosrevertidos incluyen historia y totales separados.
- SQLglobal independiente corroboró cambios esperados del fixture: octubre facturado48646.02,cobrado24323.01,liquidado40204.00,pagado30152.00; cajaingresos626/egresos505trascompensación histórica. No se usa ese neto como saldo.

`node /tmp/gt015-browser/t085.cjs`: tres bloques aprobados,10archivos financieros reales, JSON`t085.json` y capturas en temporal. Sin nuevas suites completas; código productivo intacto después deT081/T082.

## T086 — Caja, Flota, permisos y archivos límite

- OPPefectivo30/09 generaegreso121; anulación15/10 generaingreso121 en la cajaoriginal abierta. Reporte septiembre neto-121, octubre+121, cada movimiento conFKOPPdistinta deOPmanual. Autor del egreso y anulador son distintos; API/UI y cuatroPDF/XLSX reflejan fechas y referencia.
- R6incluye inactivas con viajes y con0; cambio de tipo/propietario del fixture se muestra comoactual sin cambiar conteo de viajes porVehiculoId. API yUI cotejaron Ids/nombres/conteo SQL, sin requerir flota.gestionar para consultar.
- Cliente exclusivo con5000viajes01/10 yuno02/10: XLSX5000real abierto completo;5001rechazado409confilas/tope y mensaje idéntico en UI.21filas ya comprobadasT084. Ningún archivo parcial exitoso.
- Otro viaje03/10conimporte9999999999999999.99 conserva cadena/centavos en consulta yUI; XLSX409precision_monetaria_xlsx, PDF200disponible. Al abrirlo se detectó partición visual del importe; se amplió sólo el peso de columna monetaria en FuenteAnalisisViajes, sin cambiar nombres/tipos/celdas/legacy. PDFregenerado con servicios actuales porDI: importe ytotal íntegros en una línea, texto extraído y captura revisados.
- Cuatro perfiles: base sola sin cifras de sección; base+facturaciónconsulta sin emisión; base+flota propia singestión/ficha; emisión+Viajes sinbase mantiene012y nuevo015403. Logoutrevoca acceso yborra vista al recargar. Storage local/sesión vacíos, history sinDTO. Node del bloque final24.22.0.
-18XLSXreales abiertos conClosedXML: celdas monetariasNumber,5000filas yneto-121numérico.17PDF renderizados/abiertos porfamilia, incluido máximo corregido. Capturas/JSONlibros-abiertos/t086 en temporal.

Pruebas tras corrección PDF local:2unitariosFuentesActividad y14integración fuentes/armadores/PDF/rutasexportación aprobados. No código frontend cambiado trasT082, ni motores/transacciones/contratos/consultas backend; no suites completas redundantes. Primer script buscó etiquetaCaja completa aunque endpointbuscaresponsable, y llamó /api/flota inexistente: referencias corregidas a responsable y/api/flota/vehiculos, luego cuatrobloques ypermisos focalizados aprobados. Sin corrección de producto por esos errores del script.

La revisión ampliada del PDFmáximo exigió reservar también anchoFecha1.4: tres pruebas SQLdirectas adicionales aprobadas y PDFfinal porDI inspeccionado. Fecha03/10/2026 eimporte completo comparten una línea, igualque total; sólo pesos visuales de fuente nuevaViajes, sin cambiar valores/tipos/legacy. Imagenfinal precision-final.png yTRXt086-fecha-pdf-final.trx temporales.

## T087 — Revisión final visual, teclado y solicitudes antiguas

Chrome/Node24 repitió siete pantallas a1280/375, incluidosCobros/Pagos:14recorridos sin overflow de documento, valores/acciones y scroll local, sinfilas tabIndex, tabla porEnter y diálogo foco/Escape/retorno. NoerroresJS. Capturas de encabezado, tablas desplazadas y flujos, JSONvisual-t087 en temporal.

Barrera de transporte retuvo respuestaA real deViajes, Bse aplicó/respondió primero y Ase liberó después: KPI5002/contextoB intactos. ErrorA500inyectado en transporte después de Béxito tampoco reemplaza estado ni se dibuja. Sin sleeps, promesas de retención y barrera de render; no datos de negocio ficticios para probar una suma. Rango invertido no emitió request y vinculó error/aria-invalid.

Contraste calculado con colorCSSreal y píxel del fondo efectivo de capturas: título15.183,label10.313,ayuda4.544,error5.272,encabezado4.591,importe18.720,secundario9.297. Todas las muestras>=4.5:1; no se afirma auditoría exhaustiva de cada píxel. Tokens intactos. Foco visible y tabla equivalente permiten teclado sin color. R10documental continúa: gt-ui ausente, sin aprobación documental simulada.

Scripts /tmp/gt015-browser/recorrido-visual.cjs y t087-interaccion.cjs con rutaNode24; contrastes-finales.json guarda colores/fondos/ratios. Frontend sin cambios desdeT082; ninguna suite completa repetida.
