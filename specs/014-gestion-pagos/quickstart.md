# Validación planificada — 014

Este archivo es una guía a ejecutar durante implementación; no certifica resultados. No se levantaron servicios, ejecutaron tests, aplicaron migraciones ni generaron código en `/speckit.plan`.

## Entorno

Contexto local Spec Kit: `.specify/feature.json` apunta a `specs/014-gestion-pagos`; entradas y destino futuro `tasks.md` se resuelven allí. Comprobar ausencia de overrides de entorno al siguiente comando. Verificación actual estática: PowerShell no está instalado, no se ejecutó `/speckit.tasks` ni se creó tasks.md.

- SDK de global.json (10.0.302 con rollForward configurado), Node 24, dependencias del lockfile y SQL Server 2022 de `docker-compose.yml`. Docker Compose en Linux o Podman compose según host; no modificar compose para los tests.
- Secretos sólo de variables/configuración local externa, nunca transcribir `.env` en evidencia. `ConfiguracionEntorno` y `AplicacionDePrueba` usan credenciales de entorno, conexión a SQL real y base distinta por corrida. No suplantar fixture por base de producción ni SQLite/InMemory. Inspeccionar salud SQL antes de correr suites.
- Antes de modificar código, ejecutar regresiones de 009/011/013 con fixture original y guardar conteos reales. Ausencia de SDK o SQL healthy es bloqueo de ejecución, no prueba aprobada.

Comandos desde raíz o directorio indicado:

```bash
docker compose up -d
# alternativa del entorno Podman: podman compose up -d
```

Desde `backend`:

```bash
dotnet test tests/GT.UnitTests/GT.UnitTests.csproj
dotnet test tests/GT.IntegrationTests/GT.IntegrationTests.csproj --filter 'FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores'
# Una vez implementada 014:
dotnet test tests/GT.UnitTests/GT.UnitTests.csproj --filter 'FullyQualifiedName~Pagos'
dotnet test tests/GT.IntegrationTests/GT.IntegrationTests.csproj --filter 'FullyQualifiedName~Pagos'
dotnet test
```

Desde `frontend`:

```bash
npm test
npm run build
npm run lint
```

Migración en implementación: generar una nueva migración con proyecto Infrastructure y startup Api según herramientas EF locales; revisar SQL Up/Down y snapshot. Probar migración desde base nueva y base con datos legados de OP, Caja y facturas de proveedores. No editar migraciones anteriores. Registrar comandos efectivos, versión, fecha y conteos, sin inventar equivalencia entre un harness y el fixture completo.

## Matriz técnica obligatoria

| Grupo | Pruebas necesarias |
|---|---|
| Dominio/aplicación | total exacto y checked overflow; medios por tipo; todas las aristas de cheque y finales; fecha OPP vs medios; trim exclusivamente exterior de cheque; CBU con ceros; fechas límite Argentina; motivo/detalle; elegibilidad Fletero e Inactivo |
| Persistencia SQL | CHECK de nulos y cada restricción violada aisladamente; índice efectivo con historia anulada ilimitada; Banco nombre duplicado incluidos inactivos según collation textual heredada de la base (mayúsculas/minúsculas equivalentes si así lo define), preconsulta e índice coincidentes; cheque mismo banco/número normalizado con Latin1_General_100_BIN2; 200/201 después del trim (incluidas entradas crudas mayores que se reducen a 200), paridad de blancos de Trim entre CHECK y aplicación, Unicode/ceros/separadores/espacios internos sin nuevas equivalencias, sin truncado y campo 400; OP origen/vigencia; FK OPP distinta de OP; unicidad movimiento por OPP/tipo; bool false realmente insertado |
| Concurrencia | misma factura dos selecciones parcialmente superpuestas; dos confirmaciones misma clave; clave igual contenido distinto; banco duplicado alta/edición; cheque número duplicado; saldo para una operación; movimiento manual/cierre contra Generar; transición cheque contra transición/anulación; cierre contra compensación; adquisición por Id ascendente de cada tipo y matriz global de research, SQL 1205/timeout → rollback completo/503 y reintento misma clave; pago/edición/anulación 009 contra OPP; desactivar banco contra confirmación |
| Idempotencia | misma clave mismo resultado/status; orden distinto de propiedades/facturas/medios y renglonId UI distinto conserva huella, duplicados de medios conservan multiplicidad; modificación semántica produce conflicto; cuerpo diferente conflicto; misma operación/clave entre usuarios jamás duplica efectos, replay con consulta autorizada y 403 sin filtración cuando no la tiene; claves Generar/Anular independientes; pérdida de respuesta después de commit; commit incierto resolución por lectura; replay original tras anular; nueva intención tras liberar factura; doble ingreso/reversión imposible |
| Atomicidad | inyectar fallo después de OPP, vínculo, medios, cambio factura, OP/evento, egreso, cambios de cheque, reversión, ingreso compensatorio e inserción resultado; inspeccionar TODO el grafo con nuevo contexto tras rollback, cero restos económicos |
| Autorización | 401 sin sesión; 403 sin permiso, uno por uno; siete permisos aislados y cuatro entradas de menú; revalidación tras quitar permiso; sólo generar puede referencias y resultado sin GET; anular con consulta previa sin gestionar módulos ajenos; bancos combo sin permiso ABM; no enlaces externos autorizados implícitamente |
| Regresión | 009 pagos manuales/saldos/edición/anulación y OP sin origen; 011 OP histórica, movimientos manuales con saldo negativo y cierre/resumen; 013 alta/identidad/PDF/snapshot y nuevos estados reales de consulta; 012 reportes que lean esas proyecciones; sesión y menú desconocido |
| Frontend | formulario multi página sin perder selección, sumas BigInt al máximo, campos exactos, medios temporales, cancelar sin escrituras, replay conserva payload/clave; confirmación UI antes de un único POST intencional (Generar tras revisión, Anular tras motivo/diálogo); cancelar Anular no envía POST; confirmado ausente/false → 400 sin efectos; 409 sólo ante conflicto real, sin handshake de confirmación; errores de campo/403; límites 200/201 normalizados en Nombre/NumeroCheque sin recorte del input; rutas refresh sin permiso; filtros/paginación; historial/estados; nombres accesibles, teclado, foco/retorno y anuncios |

Concurrencia reproducible: scopes/DbContext/conexiones independientes; barreras/interceptores que retengan adquisición específica, comprobar espera y liberar commit/rollback; no dormir para adivinar carreras. SQL queries de cierre comparan ImportePagado con sum(OP WHERE Vigente), totales OPP con vínculos y medios, saldo con movimientos, Vigente de vínculos con estado OPP y exactamente un historial por transición aceptada. Exigir rechazo de segundo confirmador completo, no selección reducida.

Testing de dependencia: resolver servicios reales del contenedor, converter/DI/políticas y transacciones reales. Inyección de fallos sólo en tests, sin opciones de producción para saltar validaciones. No modificar aserciones antiguas para esconder regresiones. Cuando una extensión aprobada cambia estado visible, registrar explícitamente el caso nuevo y el FR que la exige antes de adaptar expectativa correspondiente.

## Aceptación operando la aplicación

1. Con usuario administrativo, mantener Bancos: alta, duplicado, edición, baja y reactivación; comprobar Inactivo fuera de combo nuevo y visible en histórico. OP-25 y OPP-25 diferenciados.
2. Proveedor normal con facturas $121,00+$242,00: sumar $363,00 exactos, rechazar un centavo de diferencia. Detalle opcional, fecha hoy por defecto/ayer admitida/mañana rechazada, sin alterar fecha efectiva del efectivo. Agregar/editar/quitar medios y abandonar: no debe existir operación nueva.
3. Con saldo suficiente, dos efectivos $60+$50 quedan en orden y producen un egreso $110; orden mixta $1.000 con cheque $600/transferencia $250/efectivo $150 produce un egreso $150, concepto Pago OPP-n. Generar sin saldo/caja propia abierta debe dejar todo intacto.
4. Fletero con varias facturas y liquidaciones elegibles: sólo cheques diferidos, total exacto, facturas y liquidaciones Pagadas, OPP Pendiente y varias OP con origen. Caja no cambia. Pago previo vigente, diferencia histórico/saldo o cheque no diferido rechazan toda la orden.
5. Repetir los dos recorridos con proveedor Inactivo: marca visible, sin reactivar ni confirmación adicional; baja durante armado no impide pagar deuda aún elegible.
6. Consultar OPP y filtrar fecha/proveedor/estado, navegar páginas; comprobar snapshot, medios y trazabilidad autorizada sin llegar a pantallas ajenas sin permiso.
7. Cheques: ayer/hoy/+30/+31 y todos los estados; estado y situación separados. Ejecutar transiciones y verificar historial; finales sin salida; rechazo individual no reabre deuda. Validar con reloj de test sin cambiar reloj del host.
8. Anular OPP con cheques todos Pendientes: OPP Anulada, cheques Anulados, facturas Pendientes y OP propias revertidas con historia. Nuevo pago elegible crea OPP distinta; reintento de generación anterior no paga de nuevo. Cheque Entregado/Anulado individualmente impide anular conjunto.
9. Anular normal con efectivo/caja original abierta: ingreso compensatorio exacto con Anulación OPP-n, original intacto; caja cerrada o cualquier transferencia impide anulación. Motivo/autor/instante visibles. Cancelar confirmación no cambia nada.
10. Dos sesiones con misma factura y saldo compartido: como máximo una confirma; otra ve conflicto, sin orden parcial. Simular respuesta perdida después de commit: recuperar misma OPP, sin duplicados. Repetir anulación: un solo ingreso/una sola reversión.
11. Roles Gerencia/Tráfico sin permisos nuevos y roles personalizados con permisos aislados: menú y acciones correctos, direcciones directas 403. Sesión con sólo generar ve resultado de alta en runtime, refresh requiere consulta; datos no persisten en browser storage/history.
12. Revisar a 1280px, teclado, árbol accesible, contraste en fondo real y estados de campo del plan; recuperar referencias gt-ui ausentes para cotejo documental antes de aceptar UI. Evidenciar hallazgos/correcciones sin modificar el sistema de diseño por analogía.

## Resultados de implementación — 2026-10-19

Validación completada con .NET SDK 10.0.302, Node 24.22.0, npm 11.19.1 y SQL Server
real del compose saludable. Evidencia y comandos efectivos en [implementation-log.md](implementation-log.md)
y recorridos operativos en [acceptance-evidence.md](acceptance-evidence.md).

| Verificación final | Resultado |
|---|---|
| Unitarios backend completos | 763 aprobados, sin fallos ni omitidos |
| Integración SQL completa | 1217 aprobados, sin fallos ni omitidos |
| Frontend completo, después de ajustes visuales | 611 aprobados en 83 archivos |
| Backend build / frontend build y typecheck / lint | Correctos |
| Migración desde vacío y upgrade 013 con datos legacy; protecciones Down/modelo | 5 pruebas SQL aprobadas |
| Regresión 009 / 011 / 013 / 012 | 94 / 88 / 64 / 38 aprobadas |
| Quickstart | Doce recorridos operados por agente con Chrome, UI/HTTP/SQL reales |
| Concurrencia, idempotencia, atomicidad y siete permisos | Verificados en suites SQL/HTTP y recorridos de aceptación |
| Diff, secretos y archivos nuevos | Revisión limpia; git diff --check correcto |

Host de aceptación cerrado; eliminación de la base efímera confirmada por metadata.
No se afirma aceptación por un empleado: la aceptación operativa fue ejecutada por el agente.
El baseline intermitente de 009 pasó en la suite final; sus fallos esporádicos anteriores
se conservan en el registro, sin modificar el test ni declarar corregida su causa histórica.
R1 sigue pendiente por ausencia de gt-ui; UI real validada contra componentes/tokens existentes.
R3–R7 verificados mediante transacciones y pruebas; no se afirma un CHECK de sumas intertabla.
Persisten advertencias previas de Pkcs y tamaño de chunk Vite. Sin cambios a fuentes funcionales.
