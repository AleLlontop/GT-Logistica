namespace GT.Domain.Facturacion;

/// <summary>
/// Los años que se pueden elegir como período de una factura (FR-010) y, por FR-002 del Módulo 9, de una
/// liquidación: desde <see cref="PrimerAnio"/> hasta el año en curso, los dos incluidos.
///
/// <b>La lista se deriva de hoy y no se escribe</b>: al empezar un año nuevo aparece sola, y el anterior
/// sigue disponible para facturar o liquidar diciembre en enero. El día llega por parámetro y nunca se
/// lee del reloj (convención [005]).
/// </summary>
public static class PeriodoAdmitido
{
    /// <summary>El primer año con viajes cargados en el sistema.</summary>
    public const int PrimerAnio = 2025;

    public static bool AnioValido(int anio, DateOnly hoy) => anio >= PrimerAnio && anio <= hoy.Year;
}
