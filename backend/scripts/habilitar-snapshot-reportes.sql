-- Operación administrativa explícita. Nunca se ejecuta desde el runtime ni desde una migración EF.
-- Base015 la valida el wrapper; no colocar contraseñas en variables SQLCMD ni en este archivo.
SET NOCOUNT ON;
IF @@TRANCOUNT <> 0
    THROW 50015, 'La habilitación de Snapshot debe ejecutarse fuera de una transacción.', 1;
DECLARE @base sysname = N'$(Base015)';
DECLARE @id int = DB_ID(@base);
IF @id IS NULL OR @id <= 4
    THROW 50015, 'Elegí explícitamente una base de aplicación existente, no una base del sistema.', 1;
IF HAS_PERMS_BY_NAME(@base, 'DATABASE', 'ALTER') <> 1
    THROW 50015, 'La cuenta administrativa no tiene permiso para configurar la base destino.', 1;
DECLARE @rcsi bit = (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = @id);
DECLARE @estado tinyint = (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = @id);
IF @estado IN (2,3)
    THROW 50015, 'La base está cambiando su configuración Snapshot. Esperá que termine y reintentá.', 1;
IF @estado = 0
BEGIN
    DECLARE @sql nvarchar(4000) = N'ALTER DATABASE ' + QUOTENAME(@base) + N' SET ALLOW_SNAPSHOT_ISOLATION ON';
    -- SQL Server espera las transacciones previas necesarias, sin expulsar conexiones ni forzar rollback.
    EXEC sys.sp_executesql @sql;
END;
IF (SELECT snapshot_isolation_state FROM sys.databases WHERE database_id = @id) <> 1
    THROW 50015, 'Snapshot aún no está ON; no habilites las nuevas consultas de Reportes.', 1;
IF (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE database_id = @id) <> @rcsi
    THROW 50015, 'RCSI cambió durante la operación; revisá la configuración antes de continuar.', 1;
SELECT snapshot_isolation_state_desc AS EstadoSnapshot,
       is_read_committed_snapshot_on AS ReadCommittedSnapshot
FROM sys.databases WHERE database_id = @id;
