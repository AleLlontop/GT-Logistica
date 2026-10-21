# Plan de implementación — 015 Ampliación de reportes

**Rama**: `015-ampliacion-reportes` | **Fecha**: 2026-10-19 | **Spec aprobada**: [spec.md](spec.md)

Diseño técnico para revisión antes de `/speckit.tasks`. AC-01–AC-05/C-01–C-04 permanecen cerradas, spec sin modificaciones. Sin tareas, código, instalaciones, migraciones, commits ni push en esta etapa. Fuentes documentales UI ausentes: cotejo pendiente R10, sin afirmar aprobación documental plena.

## Resumen y contexto comprobado

Ampliar Reportes con consultas analíticas SQL, dashboard y siete perspectivas de actividad/documentos/flujos, reutilizando cinco reportes 012 y sus armadores. Dominio conserva reglas; Application incorpora DTOs/casos de consulta; Infrastructure proyecta sobre el mismo SQL Server y materializa una foto Snapshot por respuesta; Api declara políticas compuestas. No entidades de negocio nuevas, base analítica, CQRS/framework, caché distribuida ni servicios nuevos.

.NET10/SDK10.0.302, EF Core SQL Server10.0.10; QuestPDF2026.7.3/ClosedXML0.105.1; React19.2.8/Router7.18.2/TS6.0.2/Tailwind4/Node24. xUnit/WebApplicationFactory SQL Server real, Vitest/Testing Library. Recharts3 estable propuesto, no instalado. Objetivos exactos: NFR-003/004 de spec, sin latencias adicionales inventadas. Contexto activo 015 en `.specify/feature.json`; pwsh no disponible, equivalente documental del workflow con destinos explícitos.

Artefactos: [research](research.md), [modelos de lectura](data-model.md), [contratos](contracts/README.md), [validación](quickstart.md). Ninguno es tasks.md. No actualizar AGENTS en planificación: mantenimiento previsto al cierre de implementación.

## Constitution Check antes y después del diseño

| Principio | Resolución |
|---|---|
| Simplicidad | Proyecciones y repositorio específico, mismo DbContext, modelo tabular existente; snapshot justificado por consistencia, sin infraestructura analítica nueva |
| Español/ARS | Textos argentinos, DateOnly/UTC→Argentina, dinero exacto en cadenas nuevas; PDF ARS y XLSX numérico verificado |
| Cero alcance fantasma | No empleados/stock/conciliación, no cambios de reglas fuente; extensiones acotadas enumeradas; AC/C no se reabren |
| Aceptación verificable | Quickstart y matriz FR/SC, tablas visibles explican gráficos; mediciones reproducibles, no pruebas supuestas |
| Respeto de datos | Permisos antes de consulta, sin CBU/adjuntos ni storage/bitácora nueva; referencias mínimas |
| UI gobernada | Tablas de primarias/campos completas sobre componentes y tokens reales; fuentes gt-ui ausentes, R10 documental explícito |

No Sites ni skill de generación de sitios: se extiende el frontend existente. AGENTS establece exportación secundaria en lectura y prevalece sobre ejemplo desactualizado de plantilla que la promueve por descarte. Cotejo documental gt-ui sigue pendiente antes de afirmar cumplimiento integral del sistema de diseño.

## Estructura propuesta

- `GT.Domain`: no nuevas entidades; reutilizar reglas/clasificaciones puras de Viajes, Facturación, Flota y fechas. No mudar reglas de negocio a Reportes.
- `backend/src/GT.Application/Reportes/Analisis/`: Contexto/Periodo/DTOs tipados, ConsultarDashboard/ConsultarAnalisis por perspectiva, IRepositorioAnalisisReportes, autorizaciones de secciones y traductores de filtros, reglas puras de series/clasificación. Interfaces no exponen IQueryable.
- `backend/src/GT.Infrastructure/Persistencia/RepositorioAnalisisReportes*.cs`: parciales por perspectiva, predicados compartidos para agregado/detalle/exportación, AsNoTracking, agrupación SQL, coordinación de foto Snapshot. Reusar GtDbContext scoped; sin llamadas a repositorios que abran transacciones propias.
- `backend/src/GT.Application/Reportes/Fuentes/`: nuevas fuentes analíticas a ReporteTabular; conservar las cinco actuales. Contexto capturado alimenta encabezado para que generación/hoy no se desalineen al cambiar de día; sobrecarga aditiva de ArmadoDeReporte para instante preexistente, comportamiento legacy sin cambios.
- `backend/src/GT.Infrastructure/Reportes/`: extensión común ClosedXML con VerificadorMonetarioXlsx y excepción tipada de aplicación; QuestPDF/modelo tabular reutilizados. No cálculo financiero en double; sólo frontera numérica XLSX explícita verificada.
- `backend/src/GT.Api/Reportes/`: AnalisisReportesEndpoints, validación/respuestas/converter monetario local; Program registra DI/políticas y nuevas rutas. Constantes de permiso/semillas/catálogo menú existentes se extienden.
- `frontend/src/modules/reportes/{paginas,componentes,servicios}/`: Dashboard, Viajes, Vencimientos, Facturacion, Liquidaciones, Caja, UtilizacionFlota; componentes pequeños SelectorPeriodo/KpiReporte/GraficoAccesible/ResumenReporte, tablas por perspectiva; servicios tipos/cliente/hook de solicitudes por secuencia. Conservar componente GenerarReporte y tipos legacy mediante extensión compatible.
- Tests: `backend/tests/GT.UnitTests/Reportes/`, `GT.IntegrationTests/Reportes/`; frontend tests colocados junto a componentes/páginas. Fixture real SQL extendido para habilitar snapshot en bases de prueba. Script de habilitación administrado e índices sólo durante implementación.

## Decisiones de arquitectura y contraste

| Decisión | Requisitos | Evidencia prevista |
|---|---|---|
| Snapshot por respuesta, DTO materializado antes de render | NFR005, FR004/024 | Escritor concurrente entre conteo/series/página, foto anterior completa; otro request ve nueva |
| Snapshot opt-in SQL, readiness sin fallback | NFR005, R5 | Opción OFF→503 nuevas consultas; ON→consistencia, legacy intacto |
| Separar documentos/cobros y liquidaciones/pagos | AC02/03,C03, FR007–011 | Documentos anteriores cobrados/pagados en rango; pendiente nunca resta flujos ajenos |
| OP única, Vigente y origen; no suma adicional OPP | FR010/011/013 | Pago parcial/manual/OPP, reversión posterior al rango, Caja independiente |
| Flota LEFT JOIN de conteos(2,4), atributos actuales | C01/02, FR014/015/028 | Ceros/inactivos/tipo cambiado/sinVehiculo, empate estable |
| Agregados SQL + pagina independiente + top10/Otros declarado | FR004/017–019,NFR002/003 | >20/200 filas, categorías >10, totales íntegros y consultas acotadas |
| Guardar/reabrir XLSX con manifest monetario | AC05,FR023,SC010 | Celdas y totales ordinarios/límite, rechazo único antes de entregar bytes |
| Nuevos permisos, políticas compuestas y dashboard autorizado | AC04,C04,FR025/026 | Base aislada, fuente aislada, ambas, emisión, revocación, sin fugas |
| Recharts3 estable lazy + tabla exacta | FR018/019/030,SC007 | React19/peers, foco/lector/375px, gráficos no autoritativos, bundle medido |
| Datos transitorios/hook por secuencia | FR029 | Resolver respuesta vieja después de nueva sin reemplazarla, incluso errores y cambio sesión |

Detalles operativos y justificaciones en research; campos y parámetros completos en contracts. No instalar biblioteca ni activar opción SQL durante planning.

## Modelo de API y consultas

Nuevas consultas bajo `/api/reportes`: dashboard, viajes, facturacion, facturacion/cobros, liquidaciones, liquidaciones/pagos, caja, flota, vencimientos/choferes|flota|facturas; correspondientes `/exportacion` salvo dashboard, referencias mínimas y `/grupos` paginados. Contratos definen parámetros, DTOs y errores completos antes de tareas.

Detalle pagina20, orden total y filtros SQL antes de contar/paginar. Contexto por respuesta distingue FechaFactura/FechaCobro/PeriodoLiquidado/FechaPago/FechaViaje/FechaMovimientoArgentina y situación actual de vencimientos. Importes nuevos string canónico dos decimales, incluidos negativos y agregados mayores que una fila. Reusar serializador local exacto, no cambiar global legacy.

Consulta mensual Liquida desde mes inicial hasta mes final completo. Flujos por días exactos sin filtrar mes del documento. Facturas/liquidaciones anuladas consultables por filtro explícito con totales financieros cero, estado visible correcto. Cliente snapshot en documento; nombre actual rotulado en agrupación por Id. Flota y transportista actuales no se presentan como históricos. Viajes EnCurso incluye final físico esperando remito; no nueva señal ni adjunto.

Secciones de dashboard sin permiso devuelven estado sinPermiso sin consulta/datos; fallo SQL invalida la foto de request completo. No ejecutar todos los SELECT y esconder luego. Políticas combinadas protegen recursos específicos; dashboard realiza autorización por permiso con servicio existente (sin rol) para construir conjunto permitido al repositorio. Catálogo agrega una entrada Reportes por reportes.consultar, sin convertir flota-consultar o emitir en pantallas nuevas. Todas las nuevas exportaciones agregan emitir; cinco legacy no agregan requisitos.

## Extensiones acotadas sobre módulos existentes

1. **012**: nuevas fuentes/consultas/endpoints aditivos y UI central; guardia de precisión común explícita, nuevo 409 tipado. Mantener rutas/políticas/columnas/formatos/errores previos, componentes/armadores. Reutilizar formato/archivo, no copiar motor PDF/Excel.
2. **003/004**: reutilizar paneles y calculador de disponibilidad con datos en lote, referencias mínimas para análisis; no documentación/adjuntos/ABM ni estados nuevos. Posible refactor de proyección de lectura compartida sólo con tests de regresión intactos.
3. **005/006/009/011**: extraer/reutilizar expresiones de filtros/proyecciones de lectura donde evite dos reglas, sin cambiar DTO/contratos legacy ni escrituras. Nuevas consultas de Reportes pueden proyectar tablas directamente en Infrastructure. Sólo índices aprobados por medición en migración incremental futura.
4. **013/014**: consumir vínculos/contexto/origen y vigencia ya existentes, sin nuevas columnas/operaciones. FacturaProveedor no es venta ni OP duplicada. Mantener test baseline009 sin cambios.
5. **Autenticación/autorización/menú**: dos permisos explícitos y semillas Gerencia/AdministradorSistema, políticas compuestas; entrada única y enlaces por permiso. Program/App son puntos de integración compartidos con Empleados: diff mínimo sin reordenamientos masivos.
6. **Infra operativa/tests**: habilitación explícita de snapshot sólo donde corresponde y en fixture, sin cambiar aislamiento global de escrituras ni RCSI. No nuevos secretos; mismo compose Docker/Podman. No tocar migraciones históricas.

## UI Design Check

Fuentes reales: `frontend/src/index.css` y compartido/ui (Lienzo, EncabezadoDePantalla, Filtros, Listado, Paginacion, Boton, Dialogo, Campo, EstadoVacio), pantallas 012/013/014. `.claude/skills/gt-ui/SKILL.md` y referencias no encontradas: R10 documental. No inventar tokens ni rediseñar app. Texto ink/ink-soft en canvas, contrastes >=4,5:1 comprobados sobre fondo real en aceptación; gráficos nunca sólo color. Contenedores en grid responsive, tablas con desplazamiento horizontal local a375px sin ocultar importes/acciones.

### Acción primaria por pantalla

| Pantalla | Primaria | Secundarias/terciarias | Destructiva |
|---|---|---|---|
| Dashboard `/reportes` | ninguna | Aplicar período, limpiar, accesos, tabla equivalente | — |
| Viajes `/reportes/viajes` | ninguna | Aplicar filtros, limpiar, paginar, detalle autorizado, Generar reporte | — |
| Vencimientos `/reportes/vencimientos` | ninguna | Cambiar panel, paginar, enlaces, Generar reporte | — |
| Facturación `/reportes/facturacion`, Documentos/Cobros | ninguna | Pestaña, aplicar, limpiar, paginar, detalle, Generar reporte de perspectiva | — |
| Liquidaciones `/reportes/liquidaciones`, Documentos/Pagos | ninguna | Pestaña, aplicar, limpiar, paginar, detalle, Generar reporte de perspectiva | — |
| Caja `/reportes/caja` | ninguna | Aplicar, limpiar, paginar, detalle autorizado, Generar reporte | — |
| Utilización `/reportes/flota` | ninguna | Aplicar, limpiar, orden/ranking, paginar, detalle autorizado, Generar reporte | — |
| Diálogo exportación | PDF | Excel, Cancelar | — |

Primarias como 012: sólo PDF dentro del diálogo, no en pantalla de lectura. Las acciones secundarias siguen siendo operables; no inventar primario por descarte. Vencimientos sin período global editable: aviso situación actual.

### Estados de campo (cobertura completa)

Grupos enumeran todos los campos de cada recorrido; error tras blur tocado o submit, nunca render inicial. Label/ayuda/error con aria-describedby, aria-invalid; anillo visible en foco, controles nativos y autocomplete accesible existentes; foco diálogo al contenedor, Escape y retorno al disparador.

| Pantalla | Campos | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Todas excepto Vencimientos | Desde, Hasta | dates nativos, mes actual Argentina por defecto; borrador separado de aplicados | anillo/teclado nativo | requerido, fecha inválida o rango invertido | requeridos, no sustituir silenciosamente al enviar |
| Viajes | Cliente, Transportista | autocomplete paginado con identidad/inactivo | flechas/Enter/Escape y anillo | selección inválida, error servidor | todos |
| Viajes | Estado | select nativo, todos no anulados por defecto explícito | anillo | código inválido | default no anulados |
| Viajes | Búsqueda | input texto | anillo | mensaje servidor vinculado | sin búsqueda |
| Facturación Documentos | Cliente, Estado, Número | autocomplete, select y texto; snapshot en fila | anillo/teclado propio | Id/código/entrada inválidos | todos no anulados/sin número |
| Facturación Cobros | Cliente, Número | mismos controles, sin Estado documental | anillo | entrada inválida | todos/sin número |
| Liquidaciones Documentos | Transportista, Estado | autocomplete y select; mes derivado del rango visible | anillo | dato inválido | todos no anulados |
| Liquidaciones Pagos | Transportista, Vigencia | autocomplete y select todos/vigentes/revertidos | anillo | dato/código inválido | todos, explicación de totales vigentes |
| Caja | Caja | selector mínimo paginado/búsqueda si crece | anillo | identificador inválido | todas |
| Flota | Vehículo, Tipo, Transportista | autocomplete paginado por referencia mínima | anillo/flechas/Enter/Escape | coincidencia inválida | todos |
| Flota | Activo, Orden | selects nativos todos/activo/inactivo y cantidadDesc/cantidadAsc | anillo | código inválido | todos/default mayor uso |
| Todas con detalle/grupos | Página; tamaño si se expone | Paginacion existente y selector20/50/100 | anillo/teclado | entero/rango inválido de URL | defaults1/20; no páginas negativas |
| Vencimientos | Panel | pestañas/botones accesibles Choferes/Flota/Facturas según permiso | foco/teclado y seleccionado visible | sin permiso/fallo distinto de vacío | primer panel autorizado o aviso sin acceso |
| Gráficos | Ver tabla/páginas de categorías | botón secundario y tabla equivalente | anillo; valores exactos accesibles | fallo de grupos, no mostrar cero | «Sin datos para el período» |
| Exportación | PDF, Excel (sin campos de texto) | botones tipados; deshabilitados sin filas/generando | foco retenido/retorno | mensaje de tope/precisión/fallo en párrafo alert | no aplica; Cancelar no solicita archivo |

Loading/error/empty/403 presentes en todas; datos de una consulta anterior no se muestran con etiqueta nueva. KPI sin interactividad falsa, títulos en jerarquía, tabla equivalente al gráfico con caption/unidades. Tooltips monetarios conservan string original. No forms/DTO en storage ni history.state; filtros no sensibles sí pueden persistir en query URL.

## Fases de entrega (sin tasks)

1. **Línea base y fundamentos**: suites Reportes/source actuales, contrato y serialización; opción Snapshot/fixture/readiness, casos de foto concurrente. Fijar versión estable de gráficos y estrategia de guardia en tests, sin exponer pantallas incompletas.
2. **Consultas financieras y operativas**: predicados SQL por perspectiva, grupos/KPI/series/paginación, endpoints/referencias/permisos y seeds; documentar planes/IO y aprobar sólo índices necesarios. Verificar no mutaciones y consultas N+1 antes de UI.
3. **Exportación y compatibilidad**: fuentes comunes con ReporteTabular/Armado/QuestPDF/ClosedXML; guardia guardado/reapertura y nuevo error también legacy; suite012 intacta con escenarios adicionales de precisión/tope/columnas y bytes sin datos parciales.
4. **Frontend integrado**: dashboard/secciones/tablas/gráficos lazy, navegación única/permisos, solicitud obsoleta, equivalencia accesible y estados de campo; pruebas responsive375/1280 y lectura visual real.
5. **Validación final**: métricas NFR003/004, suites completas backend/SQL/frontend, builds/lint/typecheck, quickstart, revisión de índices/opción de base/seguridad/compatibilidad y diff. No debilitar tests ni declarar resultados no ejecutados. **Último paso**: Actualizar `AGENTS.md` con decisiones de diseño/convenciones nuevas transversales de esta feature, una línea por decisión con referencia “[015]”; sólo información útil para futuras features, sin duplicar convenciones existentes. Este mantenimiento se hará en implementación/cierre, no ahora.

## Cobertura final y riesgos

| Requisitos | Diseño que los concreta | Validación |
|---|---|---|
| FR001–004,AC01 | Dashboard/periodo/contexto/snapshot, menú | Default Argentina, filtros cruzados, página no altera KPI |
| FR005/006,C01 | Viajes estados2/4/1/0, proyección012 | Todos estados, Facturado no duplica realizado, legacy |
| FR007/008,AC02,C03 | Documentos vs FechaCobro independientes | Deuda anterior cobrada en rango, anulada/refacturada/inactivo |
| FR009–011,AC03,C03 | Meses completos, OP FechaPago/Vigente | Manual/parcial/OPP/revertida después del período, no doble suma |
| FR012/013 | Caja UTC→Argentina, movimientos reales | Neto negativo, egreso/compensación fechas distintas |
| FR014/015,C02 | LEFT JOIN unidades, conteo2/4, atributos actuales | Ceros/inactivos/sin vehículo/desempates/tipo actual |
| FR016 | Fachada de paneles actuales | Misma urgencia/universo, período no aplicado |
| FR017–019 | Paginación20, grupos SQL/top10+Otros, gráfico accesible | Categorías completas, tablas/texto, 375/1280 |
| FR020–024,AC05 | Fuentes/armadores/tope/guardia de reapertura | 5000/5001, valores ordinarios y límites, contexto de archivo |
| FR025/026,AC04,C04 | Políticas compuestas/nuevos permisos y legacy | Permisos aislados, no fugas, revocación, URLs originales |
| FR027–030 | Sin escrituras, snapshots/actuales rotulados, UI existente | No mutaciones, solicitudes obsoletas/403/error/teclado |
| NFR001–006,SC001–010 | Modelos/contratos/research/quickstart | Exactitud, N+1, latencias, foto y regresión medidos |

R1–R9 y R10 documental detallados en research §6. Puertas técnicas reales: habilitar Snapshot sin modificar aislamiento de escrituras; verificar round-trip de libro real con ClosedXML0.105.1; medir índices/latencias y bundle; conservar rutas/políticas012. No contradicción funcional ni necesidad de reabrir negocio detectada. No se declara ninguna prueba de aplicación ejecutada en planificación.
