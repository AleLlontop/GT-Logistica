# Registro de implementación — 014 Gestión de pagos

## T001 — Línea base anterior a cambios de código

Fecha: 2026-10-18, Argentina. Contexto activo: `specs/014-gestion-pagos/`.
Se ejecutó la línea base con los tests originales, sin modificar código ni aserciones.
T001 registra el estado observado; su finalización no significa que la regresión esté íntegramente verde.

Entorno comprobado: SDK .NET 10.0.302 en `/home/joaco/.dotnet/dotnet`, Node v24.22.0,
npm 11.19.1 y SQL Server del compose en estado `healthy`, publicado en 1433.
Se utilizó la configuración externa y el fixture SQL real existente; no se copiaron secretos.

| Verificación | Resultado |
|---|---|
| Suite unitaria backend completa | 545 aprobados, 0 fallos, 0 omitidos |
| Integración SQL Server filtrada a Liquidaciones/Caja/Proveedores | 242 aprobados, 1 fallo, 0 omitidos; 243 totales |
| Frontend Liquidaciones/Caja/Proveedores | 159 aprobados en 20 archivos, 0 fallos |
| Repetición aislada del test concurrente que falló | 1 aprobado, 0 fallos; no sustituye la corrida inicial |

Comandos ejecutados desde `backend/`:

```bash
/home/joaco/.dotnet/dotnet test tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --logger 'trx;LogFileName=baseline014-unit.trx' --results-directory /tmp/gt014-baseline
/home/joaco/.dotnet/dotnet test tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores' --logger 'trx;LogFileName=baseline014-integration.trx' --results-directory /tmp/gt014-baseline
/home/joaco/.dotnet/dotnet test tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Una_EdicionYUnPagoALaVez_NuncaDejanEditadaUnaLiquidacionConPagos' --logger 'trx;LogFileName=baseline014-concurrencia-repeticion.trx' --results-directory /tmp/gt014-baseline
```

Desde `frontend/`:

```bash
npm test -- src/modules/liquidaciones src/modules/caja src/modules/proveedores
```

La salud del contenedor se comprobó con `docker compose ps --format json`.
Los TRX permanecen en `/tmp/gt014-baseline/` como evidencia local temporal, sin versionarlos.

### Fallo observado en 009

`GT.IntegrationTests.Liquidaciones.EdicionConcurrenteTests.Una_EdicionYUnPagoALaVez_NuncaDejanEditadaUnaLiquidacionConPagos`,
[EdicionConcurrenteTests.cs](../../backend/tests/GT.IntegrationTests/Liquidaciones/EdicionConcurrenteTests.cs:90).
Falló `entrada.OcurridoEn <= orden.RegistradaEn` tras pasar las aserciones previas de pago e importes.
La repetición aislada pasó. Es un fallo intermitente observado antes de modificar producción;
no se ha demostrado su causa ni se declara resuelto. Conservarlo como referencia al extraer/verificar
primitivas de 009 en T023/T024; no debilitar la aserción ni confundirlo con una regresión nueva de 014.

### Incidencias del entorno

El `dotnet` resuelto dentro de `backend/` inicialmente encontraba sólo SDK 10.0.112;
se usó la ruta explícita del SDK requerido sin modificar global.json. MSBuild falló dentro
del sandbox por sockets locales bloqueados; las corridas con resultados se ejecutaron con
la autorización correspondiente fuera del sandbox. La compilación informó advertencias existentes
NU1902/NU1903 de System.Security.Cryptography.Pkcs 10.0.0 y advertencias de nulabilidad/analizadores;
no se actualizaron dependencias ni se cambiaron tests para este registro.

Estado: T001 completada como registro de línea base. T002–T098 pendientes.
Spec y plan no se modificaron. R1 documental UI se mantiene como estaba aprobado.


## T002–T003 — Reglas puras de Pagos

Implementadas exclusivamente T002 y T003. T004 no iniciada.

Archivos creados:

- `backend/src/GT.Domain/Pagos/EstadosDePago.cs`: estados OPP/cheque y tipos de medio con valores explícitos acordes al modelo aprobado.
- `backend/src/GT.Domain/Pagos/ReglasDePagos.cs`: importes/sumas, fechas recibidas, normalización, medios admitidos, validación de cheque/transferencia/efectivo, motivo y transiciones.
- `backend/src/GT.Domain/Pagos/ElegibilidadFacturaFletero.cs`: proyección de entrada inmutable y motivo de rechazo puro; no es entidad de persistencia.
- `backend/tests/GT.UnitTests/Pagos/ReglasDePagosTests.cs`: 129 casos unitarios, incluidos los 36 pares posibles de estado de cheque.

Se actualizan únicamente los estados de T002/T003 en tasks y este registro. Spec, plan,
endpoints, frontend, entidades/configuraciones EF, migraciones y código/tests de 009 permanecen intactos.

### Decisiones técnicas

Se sigue el patrón real de reglas estáticas booleanas y motivos enum de GT.Domain.
Se reutilizan TipoProveedor de 013, EstadoLiquidacion de 009 y el máximo decimal(18,2)
de ReglasFacturaProveedor. No hay un helper monetario público general que cubra estas
validaciones; no se extrae una abstracción transversal para una comparación de escala.
La comparación decimal.Round(importe,2)==importe sólo comprueba escala representable:
no modifica el importe ni redondea sumas para aceptar diferencias. Las sumas validan
cada importe y capacidad restante antes de acumular, sin exponer totales parciales al fallar.

NumeroCheque y Banco.Nombre devuelven null si son inválidos y sólo aplican trim exterior;
no se decide unicidad/collation. No se transforma CBU ni se aplica algoritmo bancario.
Todas las fechas reciben hoyArgentina y las funciones no consultan reloj ni infraestructura.
La proyección Fletero recibe FacturaPendiente (sin extender el enum de factura reservado
para T011), estado nullable de liquidación, pagos/total/saldo e indicador Activo informativo:
la baja del proveedor nunca es motivo de rechazo. Existencia/estado bancario, Caja/saldo,
idempotencia y persistencia se mantienen fuera de estas reglas.

### Validación ejecutada

Desde la raíz, con SDK 10.0.302:

```bash
/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter 'FullyQualifiedName~GT.UnitTests.Pagos' --logger 'trx;LogFileName=pagos.trx' --results-directory /tmp/gt014-reglas
/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=unitarios-completos.trx' --results-directory /tmp/gt014-reglas
```

Desde `backend/`:

```bash
/home/joaco/.dotnet/dotnet build --no-restore
```

| Verificación | Resultado |
|---|---|
| Nuevos unit tests Pagos | 129 aprobados, 0 fallos, 0 omitidos |
| Suite completa GT.UnitTests | 674 aprobados, 0 fallos, 0 omitidos (545 anteriores + 129 nuevos) |
| Build backend completo | Correcto, 0 errores, 12 warnings existentes |
| Revisión de whitespace | git diff --check correcto; archivos nuevos revisados también |

Se utilizaron permisos aprobados para los sockets locales de MSBuild. No se ejecutó
integración SQL; el build compila su proyecto pero no ejecuta sus tests. TRX temporales
en `/tmp/gt014-reglas/`. Los warnings son NU1902/NU1903 de Pkcs y CS8604/xUnit2000/xUnit2031
en archivos anteriores; ningún warning nuevo en Pagos. La primera compilación de tests
también reprodujo CS8604 ya registrado en EnviadorCorreoSmtp; no se modificó ese archivo.

Desviaciones de alcance: ninguna. El fallo concurrente baseline de 009 no se modificó,
no se reejecutó en este bloque y no se declara resuelto. R1 UI sigue igual.
Estado al detenerse: T001–T003 completadas, T004–T098 pendientes.


## T004 — Implementación y validación

Archivos: GT.Domain/Pagos/OrdenPagoProveedor.cs y OrdenPagoProveedorFactura.cs.

Documento OPP independiente, Fecha/RegistradaEn separados y vínculo histórico con Vigente; la colección de medios se agrega al definir T006.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T005 — Implementación y validación

Archivos: backend/src/GT.Domain/Pagos/Banco.cs.

Banco con Nombre/Activo/Version, sin decidir collation en dominio ni crear operación de borrado.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T006 — Implementación y validación

Archivos: backend/src/GT.Domain/Pagos/MedioPagoProveedor.cs, Cheque.cs, Transferencia.cs, Efectivo.cs y colección Medios en OrdenPagoProveedor.cs.

TPH previsto con discriminador explícito, instrumentos separados y campos propios; ningún importe en punto flotante.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T007 — Implementación y validación

Archivos: backend/src/GT.Domain/Pagos/CambioEstadoCheque.cs.

Historial separado sin evento inicial ni situación persistida; tipo de medio en la FK compuesta impide historia de transferencias.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T008 — Implementación y validación

Archivos: backend/src/GT.Domain/Pagos/ResultadoOperacionPago.cs.

Resultado completo con operación/clave global, autor auditado, versión/huella/contenido y JSON original; no incorpora estados de borrador.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T009 — Implementación y validación

Archivos: backend/src/GT.Domain/Liquidaciones/OrdenDePago.cs, CambioDeLiquidacion.cs y OperacionDeLiquidacion.cs.

Origen OPP nullable y vigencia/reversión separada; eventos nuevos 4/5 sin renumerar 0–3 ni cambiar OP manuales.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T010 — Implementación y validación

Archivos: backend/src/GT.Domain/Caja/MovimientoDeCaja.cs.

FK/navegación OPP independientes; OrdenDePagoId conserva significado de 009.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T011 — Implementación y validación

Archivos: backend/src/GT.Domain/Proveedores/EstadoFacturaProveedor.cs.

Pagada=1 mantiene Pendiente=0. Las escrituras de estado se coordinan después desde Pagos.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Domain/GT.Domain.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T012 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/Configuraciones/ConfiguracionesPagos.cs y backend/tests/GT.UnitTests/Pagos/ModeloPagosTests.cs.

Configuraciones TPH/FKs/índices y CHECKs de trim Unicode por DATALENGTH; Banco.Nombre hereda collation, cheque BIN2. Modelo EF validado sin conectar a SQL; ejecución de restricciones reales corresponde a T018.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ModeloPagosTests --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 1 test aprobado; compilación correcta, sólo NU1902/NU1903 y CS8604 preexistentes. Sin desviaciones funcionales.


## T013 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/Configuraciones/OrdenDePagoConfiguracion.cs y CambioDeLiquidacionConfiguracion.cs.

Vigencia y origen de OP con defaults legados; filtro histórico 0/2/3 e índice por OP/operación conservan historial manual.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ModeloPagosTests --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 1 test aprobado y compilación correcta; warnings preexistentes. Sin desviaciones funcionales.


## T014 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/Configuraciones/MovimientoDeCajaConfiguracion.cs.

Referencia OPP excluye referencias legacy, permite egreso/ingreso con unicidad por tipo; sin OPP conserva el CHECK anterior.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ModeloPagosTests`.

Resultado: 1 test aprobado; build correcto y warnings existentes. Sin desviaciones funcionales.


## T015 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs y Configuraciones/FacturaProveedorConfiguracion.cs.

DbSets/sequence OPP independiente y CHECK de FacturaProveedor ampliado a 0/1, extensión necesaria de FR-024 al conservar la configuración 013; sin tocar migración histórica.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~GT.UnitTests.Pagos --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 130 tests aprobados; sin warnings nuevos. Sin desviaciones funcionales.


## T016 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/Migraciones/20261018164612_Modulo14GestionPagos.cs, .Designer.cs y GtDbContextModelSnapshot.cs.

Migración incremental revisada sin alteraciones de datos manuales: nuevas columnas nullable/default true y CHECK FacturaProveedor 0/1. Secuencia OPP NO CACHE. Down rechaza documentos/bancos antes de borrar historia. La primera generación con startup Api falló porque Design reside en Infrastructure; se utilizó su fábrica existente, sin dependencias nuevas.

Comando real: `DOTNET_ROOT=/home/joaco/.dotnet PATH=/home/joaco/.dotnet:$PATH /home/joaco/.dotnet/tools/dotnet-ef migrations add Modulo14GestionPagos --project backend/src/GT.Infrastructure --startup-project backend/src/GT.Infrastructure --output-dir Persistencia/Migraciones --no-build; /home/joaco/.dotnet/dotnet build backend/src/GT.Infrastructure/GT.Infrastructure.csproj --no-restore --nologo`.

Resultado: Generación y build correctos, 0 errores, 4 warnings preexistentes; aplicación SQL aislada en T017. Sin desviaciones funcionales.


## T017 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/MigracionPagosTests.cs.

Dos pruebas SQL reales: creación desde vacío y downgrade/upgrade controlado en base efímera sin datos 014, conservando factura, OP manual, número, movimiento y saldo previo. Ningún dato de desarrollo tocado.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~MigracionPagosTests --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 2 tests aprobados, 0 fallos y 0 omitidos; warnings anteriores. Sin desviaciones funcionales.


## T018 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/RestriccionesPagosTests.cs y EscenarioPagos.cs.

INSERT directos verifican trim/Unicode/200/201 y semánticas de comparación separadas, clave global, vínculo vigente/historia, CHECK TPH/nulos/auditoría, historial sólo cheque, OP no reversible sin origen y movimientos OPP separados. Test de vínculo se aisló de la PK para afirmar el índice vigente exacto.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RestriccionesPagosTests --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 23 tests SQL aprobados, 0 fallos; primera tanda 20 también pasó; warnings previos. Sin desviaciones funcionales.


## T019 — Implementación y validación

Archivos: backend/src/GT.Application/Pagos/ContratosPagos.cs e IRepositorioPagos.cs.

DTOs tipados y unión discriminada estricta, importes decimal y paginación reutilizada PaginaDe. Contratos de lectura/escritura completos y errores diferenciados; serialización monetaria localizada en T021.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Application/GT.Application.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings. Sin desviaciones funcionales.


## T020 — Implementación y validación

Archivos: GT.Domain/Usuarios/Rol.cs; SembradorInicial.cs; CatalogoOpcionesMenu.cs; GT.Api/Autorizacion/PermisoRequirement.cs y Program.cs; PermisosBasePagosTests.cs y SemillasPagosTests.cs.

Siete permisos independientes y políticas, cuatro entradas y asignación explícita sólo a Administración/AdministradorSistema; semillas idempotentes. El nuevo módulo no se publica hasta completar sus pantallas.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~PermisosBasePagosTests; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SemillasPagosTests`.

Resultado: 7 tests unitarios y 1 test SQL de semillas aprobados; warnings existentes. Sin desviaciones funcionales.


## T021 — Implementación y validación

Archivos: backend/src/GT.Api/Pagos/SerializadorPagos.cs; backend/src/GT.Application/Pagos/ISerializadorPagos.cs; GT.Api/Program.cs.

Serializador inyectable impide referencia Infrastructure→Api; JSON monetario local reutiliza converter 013, enums camelCase, union discriminada estricta, error 400 por entrada y respuesta inmutable con Location. No se mapean endpoints sin caso de uso.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Api/GT.Api.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 6 warnings existentes. Sin desviaciones funcionales.


## T022 — Implementación y validación

Archivos: backend/tests/GT.UnitTests/Pagos/ContratosPagosTests.cs.

Dinero máximo y entradas canónicas estrictas, metadata de unión fuera de orden, campos cruzados rechazados y fechas/enums correctos sin convertir JSON legacy.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~GT.UnitTests.Pagos --logger trx --results-directory /tmp/gt014-persistencia`.

Resultado: 149 tests Pagos aprobados, incluidos 12 de contrato; sin warnings nuevos. Sin desviaciones funcionales.


## T023 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/PrimitivasPagosLiquidacion.cs; RepositorioLiquidaciones.cs; GT.Application/Liquidaciones/EditarLiquidacion.cs y AnularLiquidacion.cs.

Pago manual conserva su transacción exterior y UPDATE original; primitiva exige transacción caller-owned y no hace Begin/Commit. Reversión exige origen propio/vigente y conserva OP/evento. Conteos de bloqueo excluyen pagos revertidos, sin quitar historia.

Comando real: `/home/joaco/.dotnet/dotnet build --no-restore --nologo (desde backend)`.

Resultado: Build completo correcto, 0 errores, 14 warnings preexistentes; regresión de primitivas antes de consumo en T024. Sin desviaciones funcionales.

### Checkpoint fase 1

Antes del consumo de primitivas desde Pagos: 694 unit tests completos aprobados y 26 tests
SQL de migraciones/restricciones/semillas aprobados, sin fallos/omitidos. Comandos reales:
`/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=fase1-unit-completa.trx' --results-directory /tmp/gt014-persistencia`
y `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Pagos' --logger 'trx;LogFileName=fase1-sql.trx' --results-directory /tmp/gt014-persistencia`.
Build completo posterior de T023 también correcto. Las líneas por tarea que abreviaron
`--logger trx` se refieren a las corridas con archivos nombrados `modelo.trx`,
`modelo-t013.trx`, `modelo-t014.trx`, `pagos-t015.trx`, `migracion.trx`,
`restricciones-completas.trx`, `permisos-base.trx`, `semillas.trx` y `fase1-unit.trx`
respectivamente en `/tmp/gt014-persistencia/`; no se trata de nuevas corridas.
R1 permanece documental y no impide backend/datos. No se desplegó el módulo incompleto.


## T024 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Liquidaciones/PrimitivasPagoTests.cs.

Cuatro pruebas SQL nuevas pasan: exige transacción, manual parcial/total, rollback externo, reversión propia/historia/nuevo pago. Legacy: 89 de 90 pasan; reaparece sólo la aserción temporal baseline y su repetición aislada pasa, igual que T001. Revisión del diff conserva el UPDATE de pago y captura anterior al lock; no se modifica la aserción ni se declara resuelto. Se continúa conforme a excepción expresa del usuario para baseline esporádica.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Liquidaciones --logger trx --results-directory /tmp/gt014-primitivas; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~Una_EdicionYUnPagoALaVez_NuncaDejanEditadaUnaLiquidacionConPagos --logger trx --results-directory /tmp/gt014-primitivas`.

Resultado: Suite: 93 aprobados, 1 fallo baseline; aislado: 1 aprobado. TRX regresion009-t024.trx y 009-aislado-t024-1.trx. Ningún fallo nuevo de primitivas; warnings existentes. Sin desviaciones funcionales.


## T025 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/PrimitivasMovimientosCaja.cs y RepositorioCaja.cs.

Lock no-op compartido de caja abierta con propietario opcional sólo para compensación interna; manual conserva responsable, validación de factura y su transacción. Inserción sin Begin/Commit; saldo sólo bajo transacción externa y sin prohibir negativo manual.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Infrastructure/GT.Infrastructure.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores, 4 warnings existentes. Sin desviaciones funcionales.


## T026 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Caja/PrimitivasMovimientoTests.cs.

Primitivas caller-owned verificadas con rollback, propietario manual/compensación interna, saldo negativo manual y caja cerrada; suites originales de 011 ejecutadas antes de consumo por Pagos.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Caja --logger trx --results-directory /tmp/gt014-primitivas`.

Resultado: 88 tests aprobados, 0 fallos/omitidos, incluidos 4 nuevos; TRX regresion011-t026.trx; warnings preexistentes. Sin desviaciones funcionales.


## T027 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/BloqueosPagos.cs y backend/tests/GT.IntegrationTests/Pagos/BloqueosPagosTests.cs.

Coordinador por operación exige transacción, verifica categorías crecientes/Id ascendente, applock global y conflictos tipados. Errores SQL 1205/timeout traducidos como transitorios, índices distinguidos por nombre; pruebas profundas de carreras en T046/T083.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Infrastructure/GT.Infrastructure.csproj --no-restore --nologo; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~BloqueosPagosTests --logger trx --results-directory /tmp/gt014-primitivas`.

Resultado: Build y 2 tests SQL aprobados; warnings existentes. Sin desviaciones funcionales.


## T028 — Implementación y validación

Archivos: backend/src/GT.Application/Pagos/ComandoCanonicoPago.cs.

Versión 1 con JSON UTF-8 de propiedades fijas, SHA-256, facturas ordenadas y multiconjunto de medios ordenado por bytes; excluye usuario/clave/renglón UI/confirmado y datos calculados. Sólo trims aprobados, importe con dos decimales sin redondeo.

Comando real: `/home/joaco/.dotnet/dotnet build backend/src/GT.Application/GT.Application.csproj --no-restore --nologo`.

Resultado: Build correcto, 0 errores y 0 warnings; tests específicos en T029. Sin desviaciones funcionales.


## T029 — Implementación y validación

Archivos: backend/tests/GT.UnitTests/Pagos/ComandoCanonicoTests.cs.

Equivalencias por orden incidental/UUID UI, duplicados semánticos preservados, cambios significativos distintos, trims aprobados, comando Anular separado y SHA-256 de UTF-8.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ComandoCanonicoTests --logger trx --results-directory /tmp/gt014-primitivas`.

Resultado: 15 tests aprobados, 0 fallos; TRX canonico-t029.trx; warnings existentes. Sin desviaciones funcionales.


## T030 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/ResultadosIdempotentesPago.cs y backend/tests/GT.IntegrationTests/Pagos/ResultadosIdempotentesPagoTests.cs.

Store bajo applock/tx externa con clave global y autorización antes de comparar; JSON/status/Location originales y recuperación de sólo lectura en nuevo scope, sin repetir pago. Simulación de commit incierto completa en T045; pruebas actuales verifican recuperación confirmada/ausente y rollback.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ResultadosIdempotentesPagoTests --logger trx --results-directory /tmp/gt014-primitivas`.

Resultado: 2 tests SQL aprobados; TRX resultados-t030.trx; warnings previos. Sin desviaciones funcionales.


## T031 — Implementación y validación

Archivos: backend/src/GT.Infrastructure/Persistencia/ElegibilidadPagosSql.cs y backend/tests/GT.IntegrationTests/Pagos/ElegibilidadPagosTests.cs.

Predicado SQL compartido para oferta/confirmación, Inactivos permitidos y restricciones Fletero completas. La primera prueba detectó proyección record no traducible; se corrigió con proyección anónima SQL y DTO sólo al final, sin filtrado en memoria. Se añadió liberación de vínculo histórico.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ElegibilidadPagosTests --logger trx --results-directory /tmp/gt014-primitivas; repetición del caso NormalInactivoConDeudaSeOfrece_PagadaOVinculadaNo con vínculo añadido`.

Resultado: 8 tests corregidos aprobados y 1 caso ampliado aprobado. Primera corrida: 7 aprobados y 1 fallo de traducción ya corregido. TRX elegibilidad-t031-corregida.trx y elegibilidad-vinculo-t031.trx. Sin desviaciones funcionales.


## T032 — Implementación y validación

Archivos: GT.Application/Pagos/GestionBancosPagos.cs; Persistencia/RepositorioPagos.cs, RepositorioPagos.Bancos.cs y RepositorioPagos.Pendientes.cs; GT.Api/Program.cs; RepositorioBancosPagosTests.cs.

Banco normaliza/valida nombre en Application, compara según SQL heredado, UPDATE por Version, acciones separadas y respuestas releídas. Archivo Pendientes sólo reúne contratos de tareas aún no expuestas; cada cuerpo temporal se elimina al implementar su recorrido, y no habilita endpoints incompletos.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RepositorioBancosPagosTests --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 1 test SQL/DI aprobado; TRX bancos-repositorio-t032.trx; warnings previos. Sin desviaciones funcionales.


## T033 — Implementación y validación

Archivos: backend/src/GT.Api/Pagos/BancosPagosEndpoints.cs; GT.Api/Program.cs; RutasBancosPagosTests.cs.

GET listado y detalle con permiso independiente, páginas 20/orden total y errores 400/404/401, rutas id:int.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RutasBancosPagosTests --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 1 test HTTP/SQL aprobado; TRX bancos-rutas-t033.trx; warnings previos. Sin desviaciones funcionales.


## T034 — Implementación y validación

Archivos: backend/src/GT.Api/Pagos/BancosPagosEndpoints.cs y RutasBancosPagosTests.cs.

POST devuelve DTO completo/Location sin necesitar GET autorizado, nombre validado/trim y campos inválidos como 400, sin confirmación HTTP 409.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CrearDevuelveRecursoCompleto_YErroresDeNombreSonDeCampo --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 1 test HTTP/SQL aprobado; TRX bancos-post-t034.trx; warnings existentes. Sin desviaciones funcionales.


## T035 — Implementación y validación

Archivos: backend/src/GT.Api/Pagos/BancosPagosEndpoints.cs y RutasBancosPagosTests.cs.

PUT admite sólo nombre/version, devuelve DTO releído y 409 de versión real; campo Activo inyectado se rechaza 400, no cambia estado en edición.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EditarSoloNombreYVersion_ConflictoNoEsConfirmacion --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 1 test HTTP/SQL aprobado; TRX bancos-put-t035.trx; warnings existentes. Sin desviaciones funcionales.


## T036 — Implementación y validación

Archivos: backend/src/GT.Api/Pagos/BancosPagosEndpoints.cs; RepositorioPagos.Bancos.cs; RutasBancosPagosTests.cs.

Activación/desactivación PUT separadas e idempotentes, edición no reactiva; transitorios de escritura de estado traducidos. Confirmación reversible pertenece a UI, no a handshake 409.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ActivarDesactivarSonIdempotentes_EditarNoReactiva --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 1 test HTTP/SQL aprobado; TRX bancos-estado-t036.trx; warnings existentes. Sin desviaciones funcionales.


## T037 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/BancosTests.cs.

Límites después de trim, historia/renombre/inactividad y carreras HTTP con interceptor que libera sólo tras ambas preconsultas SQL. Sin sleeps; unicidad de índice y Version resuelven ventanas reales.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Pagos.BancosTests --logger trx --results-directory /tmp/gt014-bancos`.

Resultado: 6 tests aprobados, incluidos 3 concurrentes con barrera; TRX bancos-t037.trx; warnings anteriores. Sin desviaciones funcionales.


## T038 — Implementación y validación

Archivos: RepositorioPagos.Referencias.cs; ReferenciasPagosEndpoints.cs; GT.Api/Program.cs; ReferenciasPagosTests.cs; eliminación del contrato temporal ReferenciasAsync.

Hoy servido por TimeProvider/FechaHoyArgentina, caja propia mínima con saldo exacto y sólo bancos Activos sin Version de ABM. Usuario con sólo Generar sin acceso lateral. Primera prueba falló por longitud del Codigo del rol de fixture; se acortó sólo su identificador de prueba.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SoloGenerar_ReferenciasMinimasPropias_SinPermisosManualesNiAbm --logger trx --results-directory /tmp/gt014-referencias`.

Resultado: 1 test HTTP/SQL aprobado tras corrección del fixture; TRX referencias-t038-corregida.trx; warnings anteriores. Sin desviaciones funcionales.


## T039 — Implementación y validación

Archivos: RepositorioPagos.Referencias.cs; ReferenciasPagosEndpoints.cs; ReferenciasPagosTests.cs.

Proveedor sólo con factura elegible, Inactivo informativo; búsqueda/paginación SQL antes de DTO, orden razón social/Id y permiso Generar sin accesos laterales.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ProveedoresPaginados_MarcaInactivoYOrdenEstableSinDuplicarEntrePaginas --logger trx --results-directory /tmp/gt014-referencias`.

Resultado: 1 test aprobado con 21 proveedores y dos páginas; TRX proveedores-t039.trx; warnings existentes. Sin desviaciones funcionales.


## T040 — Implementación y validación

Archivos: RepositorioPagos.Referencias.cs; ReferenciasPagosEndpoints.cs; ReferenciasPagosTests.cs.

Facturas elegibles paginadas por proveedor, con importes exactos y exclusión de pagadas/ajenas.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~FacturasPorProveedor_PaginadasExactasYNoOfrecenPagadasNiAjenas --logger trx;LogFileName=facturas-t040.trx --results-directory /tmp/gt014-referencias`.

Resultado: 1 test HTTP/SQL aprobado, 0 fallos; warnings preexistentes; evidencia facturas-t040.trx. Sin desviaciones funcionales.


## T041 — Implementación y validación

Archivos: RepositorioPagos.Referencias.cs; ReferenciasPagosEndpoints.cs; ReferenciasPagosTests.cs.

Selección de referencias por ids estrictos, total servidor y noDisponibles explícitos; no persiste ni sustituye la intención.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SeleccionEntrePaginas_TotalServidorYNoDisponibles_SinReducirIntencion --logger trx --results-directory /tmp/gt014-referencias`.

Resultado: 1 test HTTP/SQL aprobado, 0 fallos/omitidos; TRX seleccion-t041.trx; warnings existentes. Sin desviaciones funcionales.


## T042 — Implementación y validación

Archivos: GT.Application/Pagos/GenerarOrdenProveedor.cs; GT.Domain/Pagos/NumeroVisiblePagoProveedor.cs; RepositorioPagos.Generar.cs; RepositorioPagos.Detalle.cs; RepositorioPagos.cs; RepositorioPagos.Pendientes.cs; Program.cs; TransaccionGenerarPagosTests.cs.

Transacción exterior única, replay previo a elegibilidad, locks globales, total exclusivamente de facturas y medios exactos. OP propia por liquidación, egreso único, proyección releída y resultado guardado en el mismo commit. Fecha 009 derivada del historial, no de columna inexistente; captura única de reloj en Application. Se corrigieron dos errores de compilación propios antes de validar: propiedad de fecha inexistente y LiquidacionId requerido del fixture.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TransaccionGenerarPagosTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 4 tests SQL aprobados, 0 fallos/omitidos; transaccion-t042-final-corregida.trx. Normal, replay, total no autoritativo, efectivo agrupado y Fletero con OP sin Caja; sólo warnings previos. Sin desviaciones funcionales.


## T043 — Implementación y validación

Archivos: GT.Api/Pagos/OrdenesPagosEndpoints.cs; Program.cs; RepositorioPagos.Generar.cs; RutasGenerarPagosTests.cs.

POST autorizado por permiso, confirmación true validada sin handshake, 201 detalle/Location y replay original. Código seleccion_modificada alineado con contrato.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RutasGenerarPagosTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 1 test HTTP/SQL aprobado: 400 sin confirmar, alta en un POST, replay exacto, conflicto real y 401; rutas-t043.trx; warnings anteriores. Sin desviaciones funcionales.


## T044 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/GenerarOrdenTests.cs; GT.Application/Pagos/ComandoCanonicoPago.cs.

Matriz normal/Fletero con tres liquidaciones, mezcla, efectivo conjunto, caja propia/ajena/cerrada, Inactivo, retroactividad, centavo/máximo y rechazo íntegro. Fixtures fiscales respetan CHECK 013 sin cambiar producción fiscal. La combinación Fletero sin liquidación ya es impedida por CHECK/FK de 013: no se deshabilitan restricciones para fabricar corrupción. Canonicalización informa longitud_excedida con path del renglón sin modificar bytes válidos.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Pagos.GenerarOrdenTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 22 pruebas SQL aprobadas, 0 fallos/omitidos; generar-t044-corregida.trx. Primera corrida 14 aprobadas/9 fallos propios corregidos (fixtures e identificación de campo); warnings previos. Sin desviaciones funcionales.


## T045 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/IdempotenciaGenerarTests.cs.

Claves globales concurrentes bajo barrera antes del applock; autorización previa a comparación entre autores; revocación HTTP; replay con otro día. Interceptor arroja después del commit SQL real; recuperación independiente devuelve original o 503 sin repetir economía, y posterior replay confirma una sola escritura.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Pagos.IdempotenciaGenerarTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 5 tests SQL/HTTP aprobados, 0 fallos/omitidos; idempotencia-t045.trx; warnings preexistentes. Sin desviaciones funcionales.


## T046 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/ConcurrenciaGenerarTests.cs.

Once carreras SQL con conexiones separadas y barreras antes del UPDATE/applock. Facturas compartidas/superpuestas, cheque trim y bancos distintos, saldo para una, Caja manual/cierre, baja banco y operaciones manuales 009. Se eliminaron accesos Task.Result señalados por xUnit1031; sólo await, sin sleeps.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ConcurrenciaGenerarTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 11 pruebas SQL aprobadas, 0 fallos/omitidos, concurrencia-t046-final.trx; primeras tandas 8 y 3 aprobadas; sin warnings nuevos tras corrección del analizador. Sin desviaciones funcionales.


## T047 — Implementación y validación

Archivos: AtomicidadGenerarTests.cs; BloqueosPagos.cs; RepositorioPagos.Generar.cs; RepositorioPagos.Detalle.cs.

Ocho fallos posteriores a escritura SQL real verifican rollback del grafo con contexto nuevo; timeout -2 y deadlock 1205 reales inducidos sólo por harness. Deadlock reveló envoltorios EF múltiples: traducción recorre toda la cadena sin activar reintentos económicos automáticos. Interceptor cierra su lector antes de arrojar. Afinación del modelo aprobado: conservar RenglonId de primera escritura, rechazar repetidos en intención nueva, ordenar medios por RenglonId y mutar facturas individualmente por Id ascendente ya bloqueado.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~AtomicidadGenerarTests --logger trx --results-directory /tmp/gt014-generar`.

Resultado: 10 pruebas SQL aprobadas, 0 fallos/omitidos; atomicidad-t047-corregida.trx. Primera corrida 2 aprobadas/8 fallos corregidos en traducción y harness; sin warnings nuevos. Whitespace revisado también en 36 archivos nuevos. Sin desviaciones funcionales.


## T048 — Implementación y validación

Archivos: frontend/src/modules/pagos/servicios/{tipos,pagos,importesPagos}.ts; importesPagos.test.ts; componentes/{resultadoPago.ts,ResultadoPagoContexto.tsx}.

Contratos completos y cliente HTTP existente; parser exacto reutilizado de 013 sin IVA, suma BigInt con capacidad y contexto runtime para resultado OPP/Banco. El montaje por key de sesión se integra en T055. Ubicación inicial duplicó frontend por cwd: se corrigió antes de validación final, sin restos.

Comando real: `cd frontend: npm test -- src/modules/pagos/servicios/importesPagos.test.ts src/modules/proveedores/servicios/importesFactura.test.ts; npm run build`.

Resultado: 12 tests aprobados en 2 archivos; build/tsc correctos. Primer intento falló resolución por ubicación ya corregida; build conserva advertencia de chunk >500 kB existente. Sin desviaciones funcionales.

### Regresión unitaria tras generación backend T042–T047

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~GT.UnitTests.Pagos --logger 'trx;LogFileName=pagos-regresion-generar.trx' --results-directory /tmp/gt014-generar`.
Resultado: 164 pruebas de Pagos aprobadas, 0 fallos/omitidos. Conserva reglas, contratos y canonicalización después de las correcciones. Sólo warnings preexistentes. Esta verificación no sustituye el checkpoint completo de fase 2, que se ejecutará al terminar T056.


## T049 — Implementación y validación

Archivos: frontend/src/modules/pagos/paginas/{Bancos,DetalleBanco,FormularioBanco}.tsx; servicios/normalizacionPagos.ts.

Listado/detalle/alta/edición/activar/desactivar con permisos separados, confirmación destructiva UI y resultado runtime sin GET adicional. Versión obsoleta requiere recarga explícita. Normalización exterior reproduce Unicode de String.Trim de .NET, sin truncar ni tocar contenido interno. Rutas y menú se incorporan en T055; pruebas de pantalla en T050.

Comando real: `cd frontend: npm run build; npm run lint`.

Resultado: Build/tsc y lint correctos. Se corrigieron una llamada a clasesDeControl y una advertencia de regex antes de validar; sólo advertencia conocida de chunk >500 kB. Sin desviaciones funcionales.


## T050 — Implementación y validación

Archivos: frontend/src/modules/pagos/paginas/Bancos.test.tsx; DetalleBanco.tsx.

Doce pruebas cubren CRUD, permisos, resultado runtime/recarga403, nombre200/201/Unicode, versión, errores, paginación y diálogo. La prueba de retorno de foco detectó disparador externo a Radix Root: se restaura explícitamente con ref en Banco, sin modificar UI compartida.

Comando real: `cd frontend: npm test -- src/modules/pagos/paginas/Bancos.test.tsx; npm run build; npm run lint`.

Resultado: 12 pruebas aprobadas; build/tsc y lint correctos. Tanda inicial10 aprobadas; ampliada11 aprobadas/1 fallo de foco corregido. Advertencia de chunk existente. Sin desviaciones funcionales.


## T051 — Implementación y validación

Archivos: frontend/src/modules/pagos/componentes/{EditorCheque.tsx,EditorCheque.test.tsx,tiposEditor.ts}.

Editor modal temporal con select nativo de bancos Activos, número sólo trim exterior Unicode sin maxlength, fechas normal/Fletero, importe exacto y detalle independiente. UUID de renglón estable; guardar sólo callback local sin red.

Comando real: `cd frontend: npm test -- src/modules/pagos/componentes/EditorCheque.test.tsx; npm run build`.

Resultado: 3 tests aprobados; build/tsc correcto; advertencia de chunk existente. Sin desviaciones funcionales.


## T052 — Implementación y validación

Archivos: frontend/src/modules/pagos/componentes/{EditorTransferencia.tsx,EditorTransferencia.test.tsx}.

Editor temporal de transferencia con fecha propia, CBU como texto ASCII22 sin parsing y comprobante/observaciones separados. Valida entrada local, conserva ceros y sólo llama callback de formulario; disponibilidad normal se aplica en Generar.

Comando real: `cd frontend: npm test -- src/modules/pagos/componentes/EditorTransferencia.test.tsx; npm run build`.

Resultado: 2 tests aprobados; build/tsc correcto; advertencia de chunk existente. Sin desviaciones funcionales.


## T053 — Implementación y validación

Archivos: frontend/src/modules/pagos/componentes/{EditorEfectivo.tsx,EditorEfectivo.test.tsx}.

Editor temporal de efectivo con importe exacto, fecha argentina servida sólo informativa y sin selector de caja/datos bancarios. Fecha efectiva la decide servidor al Generar; conserva identidad al editar y múltiples renglones se mantienen en formulario.

Comando real: `cd frontend: npm test -- src/modules/pagos/componentes/EditorEfectivo.test.tsx; npm run build`.

Resultado: 2 tests aprobados; build/tsc correcto; advertencia de chunk existente. Sin desviaciones funcionales.


## T054 — Implementación y validación

Archivos: frontend/src/modules/pagos/paginas/{GenerarOrden.tsx,GenerarOrden.test.tsx}; componentes/AutocompleteProveedorPago.tsx.

Proveedor elegible/Inactivo con autocomplete paginado y teclado; selección conserva ids entre páginas, relectura de total servidor y medios sólo temporales. Fecha servida/Detalle opcional, preview exacto, una petición intencional. Red incierta congela payload/clave para replay; éxito retira formulario. Cancelación previa no escribe. Errores de medio del servidor se vinculan al editor.

Comando real: `cd frontend: npm test -- src/modules/pagos/paginas/GenerarOrden.test.tsx; npm run build; npm run lint`.

Resultado: 3 pruebas de flujo aprobadas; build/tsc y lint correctos; advertencia de chunk existente. Cobertura ampliada prevista en T056. Sin desviaciones funcionales.


## T055 — Implementación y validación

Archivos: frontend/src/App.tsx; compartido/{Menu.tsx,seccionesDeMenu.ts}; autenticacion/servicios/sesion.ts; pagos/componentes/ResultadoOrden.tsx; pagos/servicios/{menuPagos.ts,menuPagos.test.ts}.

Rutas Generar/Bancos/resultado propio y provider por key de sesión; siete constantes sin roles directos. Pagos se agrupa en menú preservando desconocidos. Sólo los dos códigos conocidos aún sin pantalla se omiten temporalmente hasta T073. Resultado propio completo sin GET adicional; lectura GP05 autorizada se incorpora en T061/T071.

Comando real: `cd frontend: npm test -- src/modules/pagos/servicios/menuPagos.test.ts src/compartido/seccionesDeMenu.test.ts; npm run build; npm run lint`.

Resultado: 6 tests menú aprobados; build/tsc y lint correctos tras agregar el icono de nueva sección exigido por tipo Record. Bundle735.70 kB con advertencia de chunk ya existente. Sin desviaciones funcionales.


## T056 — Implementación y validación

Archivos: frontend/src/modules/pagos/paginas/GenerarOrden.test.tsx; Bancos.test.tsx.

Once recorridos de generación junto a editores cubren selección entre páginas, precisión máxima/centavo, Fletero/Inactivo, cambios temporales/cancelación, replay congelado, conflictos reales/403, fecha servida y storage/history vacío. Se usa resultado OPP real en pruebas. Test nuevo de límite Banco se optimizó ingresando texto completo con change/blur: conserva aserciones, evita >400 eventos por carácter bajo carga.

Comando real: `cd frontend: npm test -- src/modules/pagos src/modules/liquidaciones src/modules/caja src/modules/proveedores src/compartido/seccionesDeMenu.test.ts; npm run build; npm run lint`.

Resultado: 197 pruebas aprobadas en28 archivos tras corregir timeout exclusivo de test nuevo (corrida previa196 aprobadas/1 timeout). Build/tsc y lint correctos; sólo advertencia de chunk existente. Checkpoint backend detallado a continuación. Sin desviaciones funcionales.

## Checkpoint de fase 2 — antes de iniciar T057

- Unitarios backend completos: **709 aprobados**, 0 fallos/omitidos.
- SQL real Pagos + regresiones 009/011/013 (incluye 5 casos de Reportes por filtro): **356 aprobados, 1 fallo baseline**, 357 totales. Pagos106/Caja88/Proveedores64/Reportes5 aprobados; Liquidaciones93 aprobados y el fallo temporal conocido de línea90. Ningún fallo nuevo de014.
- Repetición aislada del fallo009: **1 aprobado**, mismo patrón observado en T001/T024. No se modificó ese test, no se declara resuelto y no se afirma suite SQL completamente verde. Se continúa bajo la excepción explícita del usuario para ese baseline intermitente.
- Frontend módulos nuevos + regresiones y agrupación menú: **197 aprobados**, 28 archivos. Build (incluye tsc) y lint correctos; advertencia de chunk >500kB ya existente, bundle actual735.70kB.
- Build backend completo: correcto, **0 errores, 8 warnings NU1902/NU1903 existentes** en esta corrida incremental.
- `git diff --check`: correcto. R1 documental UI sigue pendiente; no equivale a aceptación UI final.

Comandos reales desde raíz:
```bash
/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=fase2-unit-completa.trx' --results-directory /tmp/gt014-fase2
/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Pagos|FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores' --logger 'trx;LogFileName=fase2-sql-regresion.trx' --results-directory /tmp/gt014-fase2
/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~Una_EdicionYUnPagoALaVez_NuncaDejanEditadaUnaLiquidacionConPagos --logger 'trx;LogFileName=fase2-baseline009-aislada.trx' --results-directory /tmp/gt014-fase2
```
Desde backend: `/home/joaco/.dotnet/dotnet build --no-restore --nologo`.
Desde frontend: `npm test -- src/modules/pagos src/modules/liquidaciones src/modules/caja src/modules/proveedores src/compartido/seccionesDeMenu.test.ts`, `npm run build`, `npm run lint`.
Los TRX permanecen en `/tmp/gt014-fase2/`. La primera corrida frontend196+1timeout no se oculta; la corrida posterior197 aprobados es la validación final del checkpoint.
Estado: T001–T056 completadas, T057–T098 pendientes. Spec/plan no modificados, sin commits/push.


## T057 — Implementación y validación

Archivos: Repositorios FacturasProveedor/Liquidaciones/Caja; DTOs y NombresDeEstadoLiquidacion en Application; ConsultarFacturasProveedor; TrazabilidadModulosPagosTests; ConsultarFacturasProveedorTests; frontend Proveedores estado/filtros y Liquidaciones origen/vigencia/eventos con tres tests nuevos.

Lecturas muestran factura Pagada real, OP con origen/vigencia y eventos nuevos, movimiento con FK/referencia OPP independiente. Moneda numérica legacy 009/011 intacta; 013 conserva strings. Se reemplaza sólo estado=pagada del conjunto inválido por estado=desconocido y se agrega aceptación positiva del nuevo estado; extensión explícita FR-024/T057. Frontend anterior se extiende para mostrar esos estados/orígenes sin tocar snapshots/PDF/operaciones manuales. Corregida ruta ficticia inicial del test de Caja al listado existente.

Comando real: `dotnet test GT.IntegrationTests --no-build --no-restore --filter namespaces Liquidaciones/Caja/Proveedores y TrazabilidadModulosPagosTests; dotnet test GT.UnitTests --no-restore; frontend npm test módulos+menú, npm run build y npm run lint`.

Resultado: SQL247 aprobados/1 baseline de248; tres repeticiones aisladas adicionales1/1 aprobadas cada una, misma línea90 sin tocar test. Unitarios709 aprobados; frontend200 aprobados; build/tsc/lint correctos. TRX regresion-t057.trx, unit-lecturas-t057.trx y t057-baseline009-1/2/3.trx en /tmp/gt014-consultas. Warnings existentes; baseline no declarado resuelto. Sin desviaciones funcionales.


## T058 — Implementación y validación

Archivos: GT.Application/Pagos/ConsultarOrdenesProveedor.cs; RepositorioPagos.Ordenes.cs; RepositorioPagos.Pendientes.cs; Program.cs; RepositorioConsultasOrdenesTests.cs.

Filtros SQL inclusivos sobre Fecha de negocio, proveedor y estados; orden Fecha/Id descendente y página20. Proveedores presentes en órdenes históricas usan Id estable/identidad vigente e incluyen Inactivos, sin duplicados ni padrón completo. Detalle reutiliza proyección completa releída ya implementada.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RepositorioConsultasOrdenesTests --logger 'trx;LogFileName=consultas-t058.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 prueba SQL aprobada, 0 fallos/omitidos; warnings preexistentes. Sin desviaciones funcionales.


## T059 — Implementación y validación

Archivos: GT.Api/Pagos/OrdenesPagosEndpoints.cs y ParametrosConsultaPagos.cs; RutasConsultarOrdenesTests.cs; compartido/moneda.ts y moneda.test.ts; Pagos tipos y GenerarOrden.test.tsx.

GET GP05 exige sólo pagos.consultar, usa filtros tipados con errores de campo y dinero exacto. Corrección complementaria de AC-10: saldo manual negativo se presenta como cadena exacta, separado del importe positivo de medios; se amplía sólo formatearPesos para cadenas firmadas sin modificar cálculos ni validaciones.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ListadoSoloConsulta_CadenasExactasYErroresDeCampo --logger 'trx;LogFileName=rutas-t059.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; frontend npm test -- src/compartido/moneda.test.ts src/modules/pagos/paginas/GenerarOrden.test.tsx:19 aprobados; build/tsc/lint correctos. Warnings backend y chunk conocidos. Sin desviaciones funcionales.


## T060 — Implementación y validación

Archivos: GT.Api/Pagos/OrdenesPagosEndpoints.cs; RutasConsultarOrdenesTests.cs.

GET de proveedores históricos con búsqueda/paginación SQL, Id estable e Inactivos; sólo pagos.consultar y sin acceso a referencias GP01.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ProveedoresHistoricos_IncluyeInactivoPorIdYSoloPermisoConsultar --logger 'trx;LogFileName=rutas-t060.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 prueba HTTP/SQL aprobada; warnings preexistentes. Sin desviaciones funcionales.


## T061 — Implementación y validación

Archivos: GT.Api/Pagos/OrdenesPagosEndpoints.cs; RutasConsultarOrdenesTests.cs.

GET detalle OPP con ruta id:int, DTO completo releído y autorización pagos.consultar independiente de Generar. Literales de proveedor no colisionan; consulta no otorga acceso a factura general/PDF ni otros módulos.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~DetalleCompleto_PermisoIndependienteY404SinAccesoLateral --logger 'trx;LogFileName=rutas-t061.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; warnings preexistentes. Sin desviaciones funcionales.


## T062 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/ConsultarOrdenesTests.cs.

Tres recorridos HTTP/SQL: orden mixta con snapshot vs proveedor actual/renombre banco y Caja, Fletero con tres OP distintas sin doble importe, filtros inclusivos/páginas con empates y Anulada histórica. Moneda exacta y referencias OP/OPP verificadas.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Pagos.ConsultarOrdenesTests --logger 'trx;LogFileName=gp05-t062.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 3 tests HTTP/SQL aprobados, sin fallos/omitidos; warnings existentes. Sin desviaciones funcionales.


## T063 — Implementación y validación

Archivos: GT.Domain/Pagos/VencimientoCheque.cs; GT.Application/Pagos/ConsultarChequesPagos.cs; RepositorioPagos.Cheques.cs, Detalle.cs y Pendientes.cs; Program.cs; RepositorioChequesPagosTests.cs.

Situación pura sin reloj/estado persistido; predicado SQL universo <=hoy+30, filtros sólo restringen, seis estados y Banco histórico Activo/Inactivo. Detalle permite fuera de alerta e incluye historial ordenado. Mapper de situación compartido con detalle OPP.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RepositorioChequesPagosTests --logger 'trx;LogFileName=cheques-t063.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 prueba SQL de24 cheques aprobada; dominio y SQL coinciden; warnings existentes. Sin desviaciones funcionales.


## T064 — Implementación y validación

Archivos: GT.Api/Pagos/ChequesPagosEndpoints.cs y ParametrosConsultaPagos.cs; Program.cs; RutasChequesPagosTests.cs.

GET GP06 protegido sólo por pagos.cheques.consultar; filtros/rangos/página con errores400 de campo, estados finales incluidos. Fixture de reloj UTC19/10 01:00 prueba día argentino18/10 y límite +30; no concede Bancos ni OPP.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ListadoAlertasConPermisoAislado_UsaDiaArgentinoYEstadosFinales --logger 'trx;LogFileName=rutas-t064.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; warnings previos. Sin desviaciones funcionales.


## T065 — Implementación y validación

Archivos: GT.Api/Pagos/ChequesPagosEndpoints.cs; RutasChequesPagosTests.cs.

GET bancos históricos de GP06 entrega sólo referencias usadas por cheques, incluye Inactivos y omite Version/ABM; permiso independiente cheques.consultar.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~BancosHistoricos_SoloUsadosIncluidosInactivos_SinAbmImplicito --logger 'trx;LogFileName=rutas-t065.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; warnings existentes. Sin desviaciones funcionales.


## T066 — Implementación y validación

Archivos: GT.Api/Pagos/ChequesPagosEndpoints.cs; RutasChequesPagosTests.cs.

Detalle con historial completo incluso fuera de alerta; abrir OPP mantiene permiso independiente.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~DetalleConHistorialInclusoFueraAlerta_OrdenRequiereSuPermiso --logger 'trx;LogFileName=rutas-t066.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; warnings preexistentes. Sin desviaciones funcionales.


## T067 — Implementación y validación

Archivos: CambiarEstadoChequePago.cs; RepositorioPagos.EstadoCheque.cs y Pendientes.cs; Program.cs; CambioEstadoChequePagoTests.cs.

Transacción con lock OPP→cheque, estado anterior condicional e historial atómico; respuesta releída completa. No produce reversión económica. Primera compilación corrigió uso del helper de fixture sin cambiar producción.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CambioEstadoChequePagoTests --logger 'trx;LogFileName=estado-t067.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test SQL aprobado, 0 fallos; warnings preexistentes. Sin desviaciones funcionales.


## T068 — Implementación y validación

Archivos: ChequesPagosEndpoints.cs; RutasChequesPagosTests.cs.

POST de estado protegido exclusivamente por pagos.cheques.cambiarEstado, fuera del grupo GET. Devuelve historial releído sin conceder consulta; conflictos reales 409, entrada inválida400 e inexistente404.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CambiarEstadoSoloConPermisoPropio_SinConcederConsulta --logger 'trx;LogFileName=estado-t068.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 1 test HTTP/SQL aprobado; warnings anteriores. Sin desviaciones funcionales.


## T069 — Implementación y validación

Archivos: backend/tests/GT.UnitTests/Pagos/EstadoYSituacionChequeTests.cs.

Matriz completa de36 pares, límites inclusivos y cambio de día argentino sin mutar instrumento. Se corrigió únicamente inicializador required del nuevo fixture unitario.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~EstadoYSituacionChequeTests --logger 'trx;LogFileName=estado-t069.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 41 tests aprobados; warnings preexistentes. Sin desviaciones funcionales.


## T070 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/ChequesTests.cs.

Carrera estado contra estado con conexiones/barrera antes de lock OPP; sólo un historial. Fallos reales tras UPDATE e INSERT revierten ambos. Factura pagada/vínculo/OPP siguen vigentes al rechazar o anular cheques individualmente; páginas sin repetición y bancos usados únicamente. Complementa bordes/seis estados/dominioSQL/HTTP ya probados.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Pagos.ChequesTests|FullyQualifiedName~RepositorioChequesPagosTests|FullyQualifiedName~RutasChequesPagosTests|FullyQualifiedName~CambioEstadoChequePagoTests' --logger 'trx;LogFileName=cheques-t070-completo.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 10 tests SQL/HTTP aprobados (primera tanda de carreras/rollback3 también aprobada); warnings anteriores. Sin desviaciones funcionales.


## T071 — Implementación y validación

Archivos: ConsultarOrdenes.tsx y tests; DetalleOrden.tsx y tests; DatosOrden.tsx/ResultadoOrden.tsx; AutocompleteProveedorPago.tsx; App.tsx; contextos runtime Pagos/Factura; GenerarOrden.test.tsx.

Filtros/orden estable/páginas y detalle íntegro con enlaces sólo autorizados. Sin DTO runtime hace GET y respeta403; POST propio sigue sin GET. Guardar pago invalida resultado de factura afectada, sin storage. Test previo de nueva UI adaptado a esperar GET403 según diseño aprobado; aserciones de seguridad preservadas. Primeros builds corrigieron prop type y marca monetaria del nuevo fixture.

Comando real: `Desde frontend/: npm test -- src/modules/pagos/paginas/ConsultarOrdenes.test.tsx src/modules/pagos/paginas/DetalleOrden.test.tsx src/modules/pagos/paginas/GenerarOrden.test.tsx src/modules/proveedores; npm test -- src/modules/pagos/paginas/DetalleOrden.test.tsx; npm run build; npm run lint`.

Resultado: 77 tests aprobados y3 del detalle con caso adicional de invalidación aprobados; build/typecheck/lint correctos, warning Vite preexistente de chunk>500kB; git diff --check correcto. Sin desviaciones funcionales.


## T072 — Implementación y validación

Archivos: ChequesPorVencer.tsx; DetalleCheque.tsx; Cheques.test.tsx; servicios/estadosCheque.ts.

Listado con seis estados, situación separada, bancos históricos y rango que restringe alerta. Detalle/historial y diálogo con select nativo/aristas, foco inicial/retorno, conflicto pide actualizar. Cambiar descarta DTO OPP desactualizado; enlace OPP sólo con consulta.

Comando real: `Desde frontend/: npm test -- src/modules/pagos/paginas/Cheques.test.tsx; npm run build; npm run lint`.

Resultado: 3 tests UI aprobados; build/typecheck/lint correctos, único warning Vite de tamaño existente. Sin desviaciones funcionales.


## T073 — Implementación y validación

Archivos: frontend/src/App.tsx; compartido/seccionesDeMenu.ts; servicios/menuPagos.ts y tests.

Rutas de Consultar órdenes y Cheques/listado/detalle publicadas. Cuatro entradas del catálogo agrupadas sin filtrar desconocidos; acciones sin pantalla no agregan menú.

Comando real: `Desde frontend/: npm test -- src/modules/pagos/servicios/menuPagos.test.ts src/compartido/seccionesDeMenu.test.ts; npm run build`.

Resultado: 6 tests aprobados; build/typecheck correcto con warning Vite de tamaño existente. Sin desviaciones funcionales.


## T074 — Implementación y validación

Archivos: Cheques.test.tsx; ConsultarOrdenes.test.tsx; DetalleOrden.test.tsx y contextos/pruebas de resultado de T071.

Cobertura de filtros/páginas/rango/vacío, seis estados/situación, permiso consulta vs acción/enlace, historial releído, cancelación con foco, conflicto requiere actualizar, estado final sin salida y403 sin datos. Incluye regresión de Proveedores y menú.

Comando real: `Desde frontend/: npm test -- src/modules/pagos src/modules/proveedores src/compartido/seccionesDeMenu.test.ts; npm test -- src/modules/pagos/paginas/Cheques.test.tsx; npm run lint`.

Resultado: 112 tests en19 archivos aprobados; corrida ampliada final de Cheques8 aprobados (añade final sin salida); lint correcto; build/typecheck de T073 correcto. Sin desviaciones funcionales.


## T075 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/PermisosPagosTests.cs.

Matriz de18 rutas por cada permiso aislado,401 sin sesión,403 laterales de módulos y revocación; menú de cuatro entradas. Semillas de roles y Generar mínimo/replay con revocación reejecutados. Se corrigieron imports y Count() del nuevo test antes de la corrida válida.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~PermisosPagosTests|FullyQualifiedName~SemillasPagosTests|FullyQualifiedName~SoloGenerar_ReferenciasMinimasPropias|FullyQualifiedName~PermisoRevocado_ImpideReplay' --logger 'trx;LogFileName=permisos-t075.trx' --results-directory /tmp/gt014-consultas`.

Resultado: 11 tests HTTP/SQL aprobados; warnings preexistentes. Sin desviaciones funcionales.

### Checkpoint fase 3 — antes de Anular

Regresión completa del alcance verificada: 750 unitarios backend, 384 integración SQL real
(Pagos/009/011/013 y dependencias alcanzadas por el filtro) y 221 frontend en32 archivos,
todos aprobados, sin fallos ni omitidos. El test temporal baseline009 pasó en esta corrida;
no se modificó ni se declara eliminada su intermitencia histórica.

Comandos:

- `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --logger 'trx;LogFileName=fase3-unit.trx' --results-directory /tmp/gt014-fase3`
- `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Pagos|FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores' --logger 'trx;LogFileName=fase3-sql.trx' --results-directory /tmp/gt014-fase3`
- Desde frontend: `npm test -- src/modules/pagos src/modules/liquidaciones src/modules/caja src/modules/proveedores src/compartido/seccionesDeMenu.test.ts src/compartido/moneda.test.ts`, `npm run build`, `npm run lint`.
- `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo`: correcto,0 errores,8 warnings NU1902/NU1903 preexistentes. Primer intento con extensión .slnx inexistente corregido a la solución real .sln.

El typecheck detectó un código de error inválido sólo en el nuevo fixture de cheque;
se corrigió al código real estado_cheque_modificado y se incorporaron los dos códigos de
conflicto de cheque al tipo compartido, sin cambiar el comportamiento de otros módulos.
Build/typecheck posterior correcto; Vite mantiene warning de chunk749,18kB. Lint y
git diff --check correctos. R1 documental UI permanece pendiente; spec/plan sin cambios.


## T076 — Implementación y validación

Archivos: AnularOrdenProveedor.cs; ValidarAnulacionPagoTests.cs.

Entrada valida motivo/clave/confirmado400 sin handshake; reloj capturado una vez. Regla situacional para aplicar con datos releídos bajo locks: Pendiente, todos cheques Pendientes, sin transferencias, caja original apta y pagos propios reversibles.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ValidarAnulacionPagoTests --logger 'trx;LogFileName=anular-t076.trx' --results-directory /tmp/gt014-anular`.

Resultado: 10 tests aprobados; warnings preexistentes. Sin desviaciones funcionales.


## T077 — Implementación y validación

Archivos: RepositorioPagos.Anular.cs; eliminación de RepositorioPagos.Pendientes.cs; TransaccionAnularPagosTests.cs.

Transacción caller-owned, replay global Anular antes de estado, locks OPP→facturas→liquidaciones→OP propias→cheques→caja original. Reversión sólo origen propio, factura Pendiente/vínculo histórico liberado y cheque con historial; resultado200 guardado en mismo commit y recuperación incierta. Efectivo aún rechazado antes de mutar hasta T078; endpoint no publicado.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TransaccionAnularPagosTests --logger 'trx;LogFileName=anular-t077.trx' --results-directory /tmp/gt014-anular`.

Resultado: 1 test SQL aprobado, conservación histórica y replay sin duplicados; warnings preexistentes. Sin desviaciones funcionales.


## T078 — Implementación y validación

Archivos: RepositorioPagos.Anular.cs; TransaccionAnularPagosTests.cs.

Ingreso exacto por suma de renglones efectivo, egreso histórico conservado y FK OPP compartida, caja original abierta bajo lock sin exigir que anulador sea responsable. Compensación y JSON200/idempotencia mismo commit; eliminado rechazo temporal de efectivo.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~TransaccionAnularPagosTests --logger 'trx;LogFileName=anular-t078.trx' --results-directory /tmp/gt014-anular`.

Resultado: 2 tests SQL aprobados, replay no duplica ingreso y anulador distinto registrado; warnings previos. Sin desviaciones funcionales.


## T079 — Implementación y validación

Archivos: OrdenesPagosEndpoints.cs; Program.cs; RutasAnularPagosTests.cs.

POST anulación autorizado sólo por pagos.anular, GET previo mantiene pagos.consultar. Petición intencional devuelve200 completo; replay200 mismo JSON, clave nueva sobre Anulada409/contenido cambiado409, entrada400 e inexistente404. Sin handshake409.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RutasAnularPagosTests --logger 'trx;LogFileName=anular-t079.trx' --results-directory /tmp/gt014-anular`.

Resultado: 1 recorrido HTTP/SQL aprobado; warnings existentes. Sin desviaciones funcionales.


## T080 — Implementación y validación

Archivos: backend/tests/GT.UnitTests/Pagos/AnularOrdenTests.cs.

Complementa todas las aristas de T076 con Anulada final, transferencia sola/mixta, condiciones independientes de caja original/reversión y comando canónico Anular distinto de Generar, con motivo e Id significativos.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter 'FullyQualifiedName~AnularOrdenTests|FullyQualifiedName~ValidarAnulacionPagoTests' --logger 'trx;LogFileName=anular-t080.trx' --results-directory /tmp/gt014-anular`.

Resultado: 13 tests aprobados; warnings existentes. Sin desviaciones funcionales.


## T081 — Implementación y validación

Archivos: EscenarioAnulacion.cs; backend/tests/GT.IntegrationTests/Pagos/AnularOrdenTests.cs.

Normal cheque/efectivo y Fletero tres liquidaciones: historia conservada, sólo OP propias revertidas, agregados/saldos restaurados y selección elegible. Transferencia sola/mixta, cinco estados no Pendientes, caja cerrada y reversión imposible rechazan íntegramente. OP manual y de otra OPP permanecen vigentes e intactas.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Pagos.AnularOrdenTests|FullyQualifiedName~TransaccionAnularPagosTests' --logger 'trx;LogFileName=anular-t081.trx' --results-directory /tmp/gt014-anular; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SoloRevierteOPPropias_ConservaPagosManualesYDeOtraOPP --logger 'trx;LogFileName=anular-t081-origen.trx' --results-directory /tmp/gt014-anular`.

Resultado: 14 tests aprobados y1 adicional de aislamiento de origen aprobado; warnings anteriores. Sin desviaciones funcionales.


## T082 — Implementación y validación

Archivos: IdempotenciaAnularTests.cs; fixtures internos de IdempotenciaGenerarTests.cs.

Misma clave global concurrente devuelve resultado200 idéntico; usuario no autorizado403 antes de comparar contenido y autorizado obtiene replay. UUID Generar/Anular separado, motivo distinto409 y clave nueva sobre Anulada409. Commit real perdido recupera200 o503misma clave sin doble ingreso/reversión; Generar posterior reproduce201original sin reaplicar. Sólo visibilidad interna de fixtures previos cambió, no aserciones.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~IdempotenciaAnularTests --logger 'trx;LogFileName=anular-t082.trx' --results-directory /tmp/gt014-anular`.

Resultado: 4 tests SQL aprobados; warnings anteriores. Sin desviaciones funcionales.


## T083 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/ConcurrenciaAnularTests.cs.

Barreras SQL antes del lock padre/caja/liquidación y conexiones separadas: cheque vs anular, dos anulaciones distintas, cierre/movimiento manual contra compensación, pago/edición/anulación009 contra reversión propia. Resultados coherentes con serialización, historial único y OP manual posterior preservada; sin sleeps.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ConcurrenciaAnularTests --logger 'trx;LogFileName=anular-t083.trx' --results-directory /tmp/gt014-anular`.

Resultado: 7 tests SQL concurrentes aprobados; warnings preexistentes. Sin desviaciones funcionales.


## T084 — Implementación y validación

Archivos: AtomicidadAnularTests.cs; fixtures internos de AtomicidadGenerarTests.cs.

Fallo tras diez mutaciones SQL reales compara grafo completo nuevo contexto: factura/vínculo/OP/liquidación/evento/cheque/historial/ingreso/OPP/resultado. Timeout-2 y deadlock1205 reales producen503 y mismo comando/clave después completa una sola compensación. Interceptor de LQ/OP ajustado para fallar tras negocio y no locks iniciales; fixtures anteriores sólo visibilidad interna.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~AtomicidadAnularTests --logger 'trx;LogFileName=anular-t084.trx' --results-directory /tmp/gt014-anular; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~AtomicidadAnularTests&DisplayName~fletero|FullyQualifiedName~TimeoutODeadlockSqlReal_503SinCompensacionParcial' --logger 'trx;LogFileName=anular-t084-reversion-reintento.trx' --results-directory /tmp/gt014-anular`.

Resultado: 12 pruebas iniciales aprobadas y5 revalidadas tras ajuste de punto/reintento; warnings previos. Sin desviaciones funcionales.


## T085 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/CicloPagoReversionTests.cs.

Cuatro ciclos pagar/anular/volver a pagar normal y Fletero conservan cinco asociaciones históricas por factura y una sola vigente, quince OP con sólo tres vigentes. Generar original reproduceJSON inicial sin reaplicar. Tras liberar, dos nuevas intenciones concurrentes sólo crean una OPP efectiva y sus tres OP; rollback completo de la otra.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CicloPagoReversionTests --logger 'trx;LogFileName=anular-t085.trx' --results-directory /tmp/gt014-anular`.

Resultado: 3 tests SQL aprobados; warnings preexistentes. Sin desviaciones funcionales.


## T086 — Implementación y validación

Archivos: componentes/AnularOrden.tsx y test; DetalleOrden.tsx; App.tsx.

Acción sólo con consulta+anular, Pendiente y medios localmente admisibles; servidor revalida caja/OP/estados bajo locks. Diálogo motivo obligatorio/confirmación y un POST intencional, cancelar sin efectos, red incierta congela payload/clave para replay. Éxito muestra DTO íntegro Anulada y compensa trazabilidad, sin editar originales.

Comando real: `Desde frontend/: npm test -- src/modules/pagos/componentes/AnularOrden.test.tsx src/modules/pagos/paginas/DetalleOrden.test.tsx; npm run build; npm run lint`.

Resultado: 5 tests aprobados; build/typecheck/lint correctos; warning Vite de tamaño preexistente. Sin desviaciones funcionales.


## T087 — Implementación y validación

Archivos: AnularOrden.test.tsx; DetalleOrden.test.tsx.

Permisos consulta/anular separados, motivo/confirmación/cancelar y foco, respuesta incierta conserva exactamente objeto/clave, conflicto real obliga actualizar y403sin reintento. Integración de detalle muestra Anulada/motivo/autor/factura Pendiente/medio original y retira acción después del éxito.

Comando real: `Desde frontend/: npm test -- src/modules/pagos/componentes/AnularOrden.test.tsx src/modules/pagos/paginas/DetalleOrden.test.tsx; npm run build; npm run lint`.

Resultado: 10 pruebas aprobadas; build/typecheck/lint correctos, warning Vite preexistente. Sin desviaciones funcionales.


## T088 — Implementación y validación

Archivos: backend/tests/GT.IntegrationTests/Pagos/PermisosPagosTests.cs.

Matriz de19 rutas incluye POST Anular aislado. Consulta sin anular403sin efectos; consulta+anular compensa efectivo/Fletero sin permisos manuales y conserva403de otros módulos. Revocar sesión impide replay y registro confirmado sigue único.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~PermisosPagosTests --logger 'trx;LogFileName=anular-t088.trx' --results-directory /tmp/gt014-anular`.

Resultado: 10 pruebas HTTP/SQL aprobadas; warnings existentes. Sin desviaciones funcionales.

### Checkpoint fase 4 — antes de revisión final

Todos aprobados:763 unitarios backend,428 integración SQL real Pagos/009/011/013 y228frontend
(33archivos). No fallos ni omitidos. El test baseline temporal009 volvió a pasar sin cambios;
no se declara resuelta su intermitencia histórica. Comandos:

- `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --logger 'trx;LogFileName=fase4-unit.trx' --results-directory /tmp/gt014-fase4`
- `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Pagos|FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores' --logger 'trx;LogFileName=fase4-sql.trx' --results-directory /tmp/gt014-fase4`
- Desde frontend: `npm test -- src/modules/pagos src/modules/liquidaciones src/modules/caja src/modules/proveedores src/compartido/seccionesDeMenu.test.ts src/compartido/moneda.test.ts`, `npm run build`, `npm run lint`.

Build/typecheck/lint correctos; Vite conserva aviso de chunk751,76kB. Backend sólo warnings
preexistentes NU1902/NU1903, CS8604 y analizadores de tests anteriores. `git diff --check`
correcto. R1 documental UI pendiente y sin cambios de spec/plan. T001–T088 validadas;
se inicia revisión finalT089 antes de declarar cierre.

### T089 — Matriz de contraste final de implementación

Spec,plan,AC01–14 y contratos permanecen como fuentes cerradas. Revisión de código real y
pruebas; la aceptación operativa de quickstart se registrará en T094, no se supone ejecutada.

| FR | Implementación verificada | Evidencia |
|---|---|---|
|001/020/043/044|Siete políticas,semillas explícitas,lecturas mínimas y cuatro entradas|PermisosPagosTests,SemillasPagosTests,PermisosBasePagosTests,revocación/replay|
|002/003/004/005/025/045|Elegibilidad SQL compartida,Inactivo informativo,Fletero estricto,total sólo de facturas|ElegibilidadPagosTests,GenerarOrdenTests,RevisionContratosPagosTests|
|006/007/021|OPP independiente,Fecha/UTC separados,snapshots,única transición Anulada y sin edición/delete|ConsultarOrdenesTests,TransaccionAnularPagosTests,modelos/FKs/secuencias|
|008/009/010/011/012|TPH estricto,CBU textual,cheque normal/diferido,un solo egreso propio|ReglasDePagosTests,RestriccionesPagosTests,GenerarOrdenTests|
|013/014/015/016|GP05/06 SQL,página20,orden total,día argentino y filtros inclusivos|ConsultarOrdenesTests,RepositorioChequesPagosTests,RutasChequesPagosTests|
|017/018|decimal(18,2),JSONstring/BigInt/InputImporte sin Number monetario ni IVA nuevo|ContratosPagosTests,unitarios importes,frontend Generar y moneda|
|019/024/026/027/028/029|Transacción exterior,índice vigente,historia,no borrador,resultado inmutable global|AtomicidadGenerarTests,ConcurrenciaGenerarTests,IdempotenciaGenerarTests,CicloPagoReversionTests|
|022/023|Controles nativos,acciones por permiso,errores y confirmación UI sin handshake409|Tests frontend,checkpoints build/lint; cotejo documental R1 continúa pendiente|
|030/031/032|Bancos sin borrado,trim200,collation heredada,índice incluidos Inactivos y Version|BancosTests,RestriccionesPagosTests,RutasBancosPagosTests|
|033/034/035/036|Seis estados,aristas explícitas,historial atómico,situación derivada sin revertir deuda|EstadoYSituacionChequeTests,ChequesTests,CambioEstadoChequePagoTests|
|037/038/039/040|Anular sólo elegible,compensación exacta,OP propias,atomicidad e idempotencia separada|AnularOrdenTests,IdempotenciaAnularTests,ConcurrenciaAnularTests,AtomicidadAnularTests|
|041/042|OP/OPP/FKs separadas,una OP por liquidación,origen y reversión sin borrar|TrazabilidadPagosTests,AnularOrdenTests,CicloPagoReversionTests y migración SQL|

AC01/02/03/04/05/06 se verifican en Generar,índice vigente,atomicidad,replay y ciclo; AC07/08
con Bancos y Cheques; AC09/10 con Anular y Caja; AC11 con referencias tipadas/origen; AC12 con
matriz de políticas; AC13 con proveedor Inactivo y AC14 con fechas/trim/centavos. US1–5 tienen
API y UI implementadas. SC001–015 se corresponden con los recorridos de Generar/consultas,
permisos,medios temporales,Bancos,Cheques,Anular,origen,Inactivo y fechas de quickstart; la
validación de usabilidad/visual se distingue de estas pruebas automatizadas.

Hallazgos técnicos corregidos dentro del contrato aprobado: campos estadoAnterior/estadoNuevo
y total informado deben estar presentes en JSON; alta de Banco sólo recibe Nombre y edición
exige Version. El servidor conserva cálculo monetario propio. Duplicado de cheque retorna campo
medios[i].numeroCheque manteniendo índice SQL como respaldo; lecturas se hacen con bancos ya
bloqueados. El formulario refresca referencias de caja/bancos tras409 sin reemplazar Fecha ni
medios. Tipo de códigos de error frontend incorpora los códigos reales de Pagos.

Riesgos revisados:R3 continúa cubierto con transacciones y pruebas,no se afirma CHECK de sumas
intertabla; R4 recuperación incierta probada en ambos comandos; R5 pagos manuales/intactos y
revertidos excluidos del saldo probados; R6 primitivas caller-owned sin commits internos;
R7 compensación por anulador distinto probada en SQL y HTTP. R1 sigue como único riesgo
documental UI. Sin decisión de negocio nueva ni contradicción entre fuentes.


## T089 — Implementación y validación

Archivos: ContratosPagos.cs; BancosPagosEndpoints.cs; RepositorioPagos.Generar.cs; RevisionContratosPagosTests.cs; GenerarOrden.tsx y tests; compartido/tipos.ts; matriz en implementation-log.md.

Contraste FR001–045/AC01–14/US1–5/SC001–015 registrado arriba. Corregidos campos JSON requeridos, contratos Banco alta/edición separados, duplicado cheque por renglón y refresh de referencias sin perder formulario; no reglas nuevas.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RevisionContratosPagosTests|FullyQualifiedName~ConcurrenciaGenerarTests|FullyQualifiedName~RutasBancosPagosTests' --logger 'trx;LogFileName=revision-t089.trx' --results-directory /tmp/gt014-final; desde frontend/: npm test -- src/modules/pagos/paginas/GenerarOrden.test.tsx; npm run build; npm run lint`.

Resultado: 22 SQL/HTTP y13 frontend aprobados; build/typecheck/lint correctos; warnings existentes, R1 documental UI pendiente. Sin desviaciones funcionales.


## T090 — Implementación y validación

Archivos: ProteccionMigracionPagosTests.cs; revisión de migración014/Designer/snapshot y SQL generado en /tmp/gt014-final/migracion014.sql.

Upgrade desde vacío y013 con datos legacy reejecutado; modelo sin cambios pendientes. Script idempotente incremental inspeccionado: secuencia/FKs/índices separados. Down con Banco uOPP rechazado50014 sin borrar datos en bases efímeras. No migraciones históricas editadas ni rollback sobre desarrollo.

Comando real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~MigracionPagosTests' --logger 'trx;LogFileName=migracion-t090.trx' --results-directory /tmp/gt014-final; docker compose ps --format json`.

Resultado: 5 pruebas SQL aprobadas; SQL Server healthy; warnings previos. Sin desviaciones funcionales.


## T091 — Implementación y validación

Archivos: Backend completo, todas las suites y TRX temporales /tmp/gt014-final/.

Build completo y suites sin filtros, con SQL Server real. Incluye Pagos,009/011/013,012 y todos los demás módulos; ningún test antiguo debilitado. Baseline009 aprobado en la corrida completa, sin declarar resuelta su intermitencia histórica.

Comando real: `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo; /home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=final-unit.trx' --results-directory /tmp/gt014-final; /home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --logger 'trx;LogFileName=final-integracion.trx' --results-directory /tmp/gt014-final`.

Resultado: Build0errores/8warnings NU1902/NU1903 preexistentes;763unitarios y1217integración aprobados,0fallos/omitidos. Sin desviaciones funcionales.


## T092 — Implementación y validación

Archivos: Frontend completo, todos los módulos y utilidades compartidas.

Suite completa sin filtros, regresión009/011/013 y demás módulos incluida. Build incluye tsc-b; no contratos monetarios legacy modificados fuera de extensiones aprobadas.

Comando real: `Desde frontend/: npm test; npm run build; npm run lint`.

Resultado: 611 pruebas en83archivos aprobadas,0fallos; build/typecheck/lint correctos; aviso Vite por chunk751,80kB. Sin desviaciones funcionales.


## T093 — Implementación y validación

Archivos: Revisión frontend/src/index.css, compartido/ui y pantallas Pagos; búsqueda .claude/.agents/.codex y skills locales.

gt-ui sigue ausente: R1 documental continúa explícito, sin aprobación documental supuesta. Reutilización de controles/botones/diálogos verificada; foco inicial/retorno/cancelación probado, filas sin tabIndex y sin almacenamiento del formulario/resultado. Contraste calculado desde tokens reales: ink/canvas16,27;ink-soft/canvas8,96;brand/canvas5,94;danger-text/canvas4,59;blanco/destructivo5,33;error/danger-bg4,94;secundario9,30. Sin cambios de tokens. Recorrido visual1280 y teclado operativo se registra en T094.

Comando real: `rg --files --hidden -g SKILL.md -g '*gt-ui*' .claude .agents .codex /home/joaco/.codex/skills; revisión de clases/Dialogo/Campo y cálculo WCAG con Python sobre colores de index.css`.

Resultado: Fuentes gt-ui no disponibles; R1 pendiente. Contrastes de texto revisados>=4,5:1; pruebas de UI y lint/build ya aprobadas, sin nuevas reglas. Sin desviaciones funcionales.


## T094 — Implementación y validación

Archivos: acceptance-evidence.md; DatosOrden.tsx,DetalleOrden.tsx,DetalleCheque.tsx,ChequesPorVencer.tsx; scripts/capturas temporales /tmp/gt014-*.

Doce recorridos operados en Chrome con UI/HTTP/SQL reales y datos sintéticos. Carreras201/409, respuestas perdidas tras commit, compensación/nuevo pago, permisos/storage,200caracteres y teclado/contraste real>=4,54:1. Corregidos espaciado/ubicación acción/enlaces/wrapping sin cambiar reglas. R1 documental no se presenta como aprobado. Host cerrado y lectura metadata confirmóBASE_EFIMERA_ELIMINADA.

Comando real: `Compilación /tmp/gt014-host/Aceptacion014.csproj; host Aceptacion014.dll; node /tmp/gt014-browser/aceptacion.cjs; comprobación Aceptacion014.dll verificar GtLogistica_Test_1ecf2aa406d3420aa1ac8204d16519c0; frontend npm test; npm run build; npm run lint`.

Resultado: Recorridos validados;611frontend/83archivos aprobados después de ajustes;build/typecheck/lint correctos,warning Vite752,64kB. Host temporal heredó warnings conocidosPkcs y avisoEF de múltiplescolecciones en consultas legadas de roles; no código de autenticación alterado. Sin desviaciones funcionales.

### T095 — Inventario de extensiones contrastado con el diff

1. 013:enum/CHECK Pagada,consulta/filtro/visualización reales y descarte del DTO de factura al pagar;sin cambios a carga,cálculo fiscal,PDF,snapshot ni estado/ABM de proveedores.
2. 009 escritura:primitiva caller-owned,origen/vigencia/reversión y eventos4/5;manual conserva UPDATE/transacción y valores0–3. Conteos de bloqueo sólo vigentes.
3. 009 lectura:DTO/historial/origen/revertido y etiquetas UI;moneda legacy sigue numérica,sin edición general de OP.
4. 011 escritura:FK OPP distinta,restricciones/índices y primitiva de lock;manual conserva dueño,saldo negativo y cierre. Comentario de MovimientoDeCaja aclara la excepción del anulador autorizado de AC09,sin cambiar código ejecutable.
5. 011 lectura:una proyección compartida agrega identificación OPP en referencia ya mostrada por listado/detalle/resumen y DTO tipado opcional;conceptos automáticos inequívocos. No modifica reportes012.
6. Común:siete políticas/semillas,cuatro entradas/rutas,contexto runtime y conversión exacta de cadenas negativas de saldo;la rama monetaria numérica anterior queda intacta.

No archivos productivos de Reportes012 modificados. TRX final:Pagos187,Liquidaciones94,Caja88,
Proveedores64 yReportes38 aprobados;los demás completan1217 integración. Unitarios763 y
frontend611 completos aprobados. El test baseline009 está Passed en ese TRX,sin modificarlo.
Única expectativa antigua ajustada:estado=pagada de013 ahora válido por FR024;se conserva
rechazo de estado desconocido y se agregan aserciones positivas,ya documentadas en T057.
Migraciones históricas sin cambios;únicamente nueva014 y snapshot. Spec/plan sin cambios.


## T095 — Implementación y validación

Archivos: Diff de009/011/013/común y dependencias012; comentario de MovimientoDeCaja; inventario arriba.

Seis extensiones del plan contrastadas con rutas/DTOs/primitivas/UI. Originales manuales,moneda legacy,cierre/saldo negativo,carga/PDF/IVA/snapshot/proveedores conservados;sin permisos laterales. Sólo precisión documental de excepción AC09 en comentario de dominio.

Comando real: `git diff --stat; git diff de repositorios/DTOs/configuraciones/UI/tests legados; análisis XML de /tmp/gt014-final/final-integracion.trx; git diff --check`.

Resultado: Regresión final previa válida:00994,01188,01364,01238 yPagos187 aprobados;sin cambios ejecutables posteriores de backend,611frontend verdes tras ajuste visual. Sin desviaciones funcionales.


## T096 — Revisión final de diff y archivos nuevos

Revisados 182 archivos modificados/nuevos, incluidos 137 untracked. Auditoría local en
`/tmp/gt014-final/auditoria-diff.json`: sin coincidencias de secretos configurados, claves,
tokens, archivos sensibles ni marcadores de conflicto. No se incluyen .env, TRX, capturas,
dumps, herramientas de aceptación, bin/obj, node_modules, dist ni coverage. Manifiestos de
dependencias y .env intactos; sin staging, commits ni push.

Comandos: `git status --short`, `git diff --check`, `git diff --cached --stat`, revisión
con `git ls-files --others --exclude-standard` y escáner local de secretos/whitespace.
Búsqueda de TODO/FIXME/debugger/console.log/NotImplementedException en el código de Pagos
sin coincidencias. Archivos nuevos sin whitespace accidental; las dos terminaciones de
línea con dos espacios en spec.md son saltos Markdown intencionales preexistentes y se
preservan. Alcance contrastado en T095; migraciones históricas y test baseline009 intactos.
Resultado: revisión limpia; ningún cambio funcional adicional.


## T097 — Cierre consolidado de evidencia

Fecha de cierre: 2026-10-19, Argentina. Entorno: SDK .NET 10.0.302, Node v24.22.0,
npm 11.19.1, SQL Server real del compose healthy; configuración externa sin copiar secretos.
Se actualiza el registro de resultados de quickstart; aceptación detallada en
[acceptance-evidence.md](acceptance-evidence.md). No se repiten suites largas: no hubo
cambios ejecutables posteriores de backend; los ajustes visuales finales sí tuvieron
nueva corrida completa de frontend.

| Validación final ya ejecutada | Resultado real |
|---|---|
| Build backend completo | 0 errores; 8 warnings NU1902/NU1903 preexistentes |
| Unitarios backend completos | 763 aprobados, 0 fallos/omitidos |
| Integración completa SQL Server | 1217 aprobados, 0 fallos/omitidos |
| Frontend completo posterior a ajustes visuales | 611 aprobados en 83 archivos |
| Build frontend / tsc -b / lint | Correctos; aviso previo de chunk Vite, 752,64 kB |
| Migración desde vacío, upgrade 013 con legacy y protecciones | 5 pruebas SQL aprobadas |
| Regresión Liquidaciones / Caja / Proveedores / Reportes | 94 / 88 / 64 / 38 aprobadas |
| Pagos en suite completa de integración | 187 aprobadas |
| Aceptación quickstart | 12 recorridos operados en Chrome por agente, no aprobación humana |

Comandos y TRX exactos: T090–T092; archivos `migracion-t090.trx`, `final-unit.trx` y
`final-integracion.trx` en `/tmp/gt014-final/`. En este cierre se comprobaron nuevamente
los contadores XML de los dos TRX finales, sin recalcular ni inferir totales. Frontend:
`npm test`, `npm run build`, `npm run lint` desde frontend, repetidos después de T094.
Migración incremental y SQL revisados, sin tocar migraciones históricas ni datos reales.

Concurrencia con conexiones independientes/barreras, idempotencia global de Generar/Anular,
recuperación de commit incierto y atomicidad con fallos tras escrituras reales verificadas.
Siete permisos aislados, revocación y accesos laterales comprobados; no chequeos directos
de rol. Compensación única y reversión de OP propias conservan historia y saldo vigente.
Host de aceptación cerrado; metadata confirmó `BASE_EFIMERA_ELIMINADA`. Herramientas,
capturas y TRX sólo temporales/ignorados. Revisión de secretos y whitespace limpia en T096.

Baseline009: falló esporádicamente en T001, T024, checkpoint fase2 y T057, siempre en la
aserción temporal conocida; las repeticiones aisladas pasaron. Pasó en checkpoints fase3,
fase4 y suite completa final. Test y aserción intactos; causa histórica no declarada resuelta.

| Riesgo | Estado final |
|---|---|
| R1 gt-ui | Residual documental: fuente ausente; cotejo con skill no realizado. UI real operada y contrastada con tokens/componentes existentes, sin fallo funcional atribuido a R1. |
| R3 sumas intertabla | Validado por transacciones y restricciones de unicidad más pruebas SQL; no existe ni se declara CHECK de sumas entre tablas. |
| R4 commit incierto/replay | Validado en ambos comandos con recuperación por lectura, misma clave y resultado original; 503 cuando no puede determinarse. |
| R5 reversión 009 | Validado con ciclos repetidos, historia intacta, exclusión de revertidos y pagos manuales preservados. |
| R6 coordinación transaccional | Validado: primitivas caller-owned sin commits internos y rollback completo, regresiones previas y finales aprobadas. |
| R7 compensación Caja | Validado con caja original abierta y anulador distinto; cierre concurrente y caja cerrada no permiten efecto parcial. |

Sin nuevas reglas, desviaciones funcionales ni modificación de spec/plan/research/modelo/contratos.
T097 completa; resta únicamente mantenimiento transversal T098 y comprobación final del estado.


## T098 — Mantenimiento transversal y cierre

Como último paso se actualizó AGENTS.md con tres convenciones [014]: coordinación con
transacción única y primitivas caller-owned, idempotencia económica canónica/global con
recuperación autorizada, y normalización exterior SQL sin trasladar BIN2 a nombres de negocio.
No se duplican reglas monetarias ni vínculos históricos ya documentados en otras features.
Se revisó específicamente `git diff -- AGENTS.md`; `git diff --check` correcto.

Comprobación final: T001–T098 completas, 98 IDs únicos, ninguna tarea pendiente. Hashes de
spec.md, plan.md, research.md, data-model.md y contracts/README.md sin cambios respecto del
inicio del cierre. Se conserva la evidencia de pruebas T090–T094; no funcionalidad adicional.
Git status muestra sólo cambios de la feature y su documentación; revisión de secretos limpia.
Sin commits ni push. Feature lista para revisión humana, con R1 documental pendiente y
advertencias preexistentes registradas; intermitencia histórica de 009 no declarada resuelta.
