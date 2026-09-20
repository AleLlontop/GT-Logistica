namespace GT.Application.Facturacion;

/// <summary>
/// La situación de una factura en palabras, a partir de los días que calculó el servidor (FR-063).
///
/// <b>Es la versión en C# de <c>situacion(dias)</c> de
/// <c>frontend/src/modules/facturacion/servicios/api.ts</c></b>, con sus tres ramas exactas. No se
/// puede llamar a la original porque está en TypeScript y el reporte se arma en el servidor
/// (data-model §3.4, research §14). Las dos las fija el test <c>PalabrasDeEstadoTests</c>.
///
/// <c>dias</c> es negativo cuando hay atraso y positivo cuando queda plazo. **El cero tiene su propio
/// texto**: <c>Vence en 0 días</c> sería técnicamente correcto y no es lo que nadie diría.
/// </summary>
public static class SituacionDeVencimiento
{
    public static string Para(int dias)
    {
        if (dias < 0)
        {
            var atraso = Math.Abs(dias);

            return $"Vencida hace {atraso} {Dias(atraso)}";
        }

        if (dias == 0)
        {
            return "Vence hoy";
        }

        return $"Vence en {dias} {Dias(dias)}";
    }

    private static string Dias(int cantidad) => cantidad == 1 ? "día" : "días";
}
