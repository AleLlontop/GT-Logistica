using GT.Api.Autorizacion;
using GT.Domain.Usuarios;
using Microsoft.AspNetCore.Authorization;

namespace GT.Api.Reportes;

public static class PoliticasAnalisisReportes
{
    public static void AgregarPoliticasAnalisisReportes(this AuthorizationOptions opciones)
    {
        opciones.AgregarPoliticasDePermisos(CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesFlotaConsultar);
        string[] fuentes = [CodigosPermiso.ViajesConsultar, CodigosPermiso.FacturacionConsultar,
            CodigosPermiso.LiquidacionesConsultar, CodigosPermiso.CajaConsultar, CodigosPermiso.ReportesFlotaConsultar,
            CodigosPermiso.ChoferesVencimientosConsultar, CodigosPermiso.FlotaVencimientosConsultar];
        foreach (var fuente in fuentes)
        {
            opciones.AgregarPoliticaCombinada(CodigosPermiso.ReportesConsultar, fuente);
            opciones.AgregarPoliticaCombinada(CodigosPermiso.ReportesConsultar, fuente, CodigosPermiso.ReportesEmitir);
        }
    }
}
