using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Marca como UTC todo instante que vuelve de la base.
///
/// Existe por un error concreto: las columnas `datetime2` de SQL Server no guardan zona horaria, así
/// que EF Core materializa el <see cref="DateTime"/> con <c>Kind = Unspecified</c>. Un
/// <c>DateTime</c> sin `Kind` lo serializa `System.Text.Json` **sin la `Z` final**, y el frontend
/// recibe `2026-08-07T17:07:00`: una hora sin zona, que interpreta como local. En Argentina (UTC−3)
/// eso mostraba las 17:07 cuando eran las 14:07.
///
/// Es el mismo error que el corrimiento de un día del padrón, un escalón más abajo: allá el frontend
/// interpretaba de más una fecha sin hora, acá el backend informa de menos un instante. La diferencia
/// es que este no se nota por tres horas de desfase salvo que uno mire el reloj: en `fechaAlta`, que
/// se muestra sin hora, un alta cargada después de las 21 aparecía directamente al día siguiente.
///
/// Todo lo que el sistema guarda en una columna de fecha y hora es un instante UTC —lo escribe
/// <c>TimeProvider.GetUtcNow()</c>—, así que la conversión se declara una sola vez para todas las
/// propiedades <c>DateTime</c> del modelo en <see cref="GtDbContext.ConfigureConventions"/>, y no
/// entidad por entidad, donde la próxima que se agregue nacería con el error de nuevo.
///
/// Hacia la base no cambia nada: se guarda el mismo instante que se guardaba. Por eso el arreglo no
/// lleva migración ni toca una fila.
/// </summary>
internal sealed class ConversorInstanteUtc : ValueConverter<DateTime, DateTime>
{
    public ConversorInstanteUtc()
        : base(
            haciaLaBase => haciaLaBase.Kind == DateTimeKind.Utc
                ? haciaLaBase
                : haciaLaBase.ToUniversalTime(),
            desdeLaBase => DateTime.SpecifyKind(desdeLaBase, DateTimeKind.Utc))
    {
    }
}
