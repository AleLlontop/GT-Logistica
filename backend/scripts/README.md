# Preparación administrativa de Snapshot para Reportes015

Se ejecuta por un operador con permiso ALTER sobre una base de aplicación explícita, después
de crear/migrar el esquema y antes de habilitar las consultas analíticas nuevas. No forma parte
del arranque de GT.Api ni de una migración EF. El usuario de runtime no necesita ALTER DATABASE.
No modifica READ_COMMITTED_SNAPSHOT, niveles de aislamiento de escrituras, datos ni tablas.

Usar `bash backend/scripts/habilitar-snapshot-reportes.sh` con SQLCMD_SERVER, SQLCMD_DATABASE,
SQLCMD_USER y SQLCMDPASSWORD definidos mediante configuración externa. Se requiere sqlcmd
instalado por el operador; la contraseña sólo va en su variable de entorno, nunca argumentos,
documentación, historial compartido ni repositorio. No hacer source de .env en este script.
La cadena de configuración y la contraseña no se imprimen. El wrapper valida el nombre y
el script SQL comprueba existencia, destino no sistema y permisos administrativos.

La operación es idempotente: ON sólo se verifica; OFF solicita ON y espera el cambio de SQL
Server sin SINGLE_USER/ROLLBACK IMMEDIATE. Transición ya iniciada produce error y exige
reintentar cuando termine. Timeout60s falla sin forzar conexiones: revisar transacciones
activas con el operador y reintentar. La salida exitosa muestra ON y RCSI sin cambios.
No reiniciar ni degradar la aplicación para ocultar una preparación incompleta; readiness015
rechaza sólo análisis nuevos mientras Snapshot no esté ON, conservando operaciones/012.

Los tests ejecutan el mismo SQL con cuenta administrativa exclusivamente en bases efímeras,
incluidos repetición y rechazos; no suponer que una base de desarrollo quedó habilitada.
Los procedimientos verificados de base nueva y upgrade existente se registrarán al completar
T012/T013. El despliegue de índices, si se justifican, permanece separado de esta opción SQL.

## Base nueva (T012)

1. Crear la base destino y aplicar las migraciones actuales mediante el procedimiento del proyecto.
2. Con la cuenta administrativa y destino explícito, ejecutar el wrapper anterior; comprobar ON y
   que RCSI conserva su valor. No ejecutar dentro de una transacción de migración.
3. Comprobar readiness específico de Reportes015 antes de habilitar sus consultas. La API de
   operación/los cinco reportes012 no dependen de ese readiness.
4. Mantener la cuenta de runtime con sus permisos habituales; no agregarle ALTER DATABASE.

`InstalacionSnapshotAnalisisTests` prueba esquema desde vacío, script real, escritura normal y
cinco exportaciones legacy. No habilitar esta opción automáticamente en todas las bases del servidor.

## Base existente (T013)

Usar el mismo script sobre el destino explícito después de aplicar las migraciones pendientes.
No reemplazar esquema ni hacer downgrade para preparar Snapshot. Registrar antes/después el
estado de opción y RCSI; confirmar la invariancia de documentos, OP manuales, OP con origen OPP,
movimientos y saldos. No activar READ_COMMITTED_SNAPSHOT ni otorgar ALTER al runtime.
`UpgradeSnapshotAnalisisTests` ensaya ese cambio con pagos y movimientos reales sintéticos,
comparando el grafo y las migraciones antes/después en una base efímera.
