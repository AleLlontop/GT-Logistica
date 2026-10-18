using GT.Domain.Choferes;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Proveedores;

/// <summary>Datos mínimos del módulo; cada prueba los usa en su base aislada.</summary>
public static class EscenarioProveedores
{
    public static EmpresaEmisora Emisor() => new()
    {
        RazonSocial = "G&T de prueba", Cuit = "30712345671",
        Domicilio = "Domicilio de prueba", CondicionIva = "Responsable inscripto",
    };

    public static Transportista Externo(string cuit = "20123456786") => new()
    {
        Nombre = "Transportista de prueba", Cuit = cuit, Tipo = TipoPersona.Fisica,
        Telefono = "3411234567", Email = "externo@example.com",
    };

    public static Liquidacion Pendiente(Transportista transportista, decimal total = 121000m) => new()
    {
        TransportistaId = transportista.Id, Transportista = transportista,
        PeriodoMes = 10, PeriodoAnio = 2026, ImporteTotal = total,
    };

    public static Proveedor Normal(string cuit = "30712345671", string razonSocial = "Proveedor de prueba") => new()
    {
        Tipo = TipoProveedor.ProveedorNormal,
        RazonSocial = razonSocial,
        Cuit = cuit,
    };

    public static Proveedor Fletero(Transportista transportista) => new()
    {
        Tipo = TipoProveedor.Fletero,
        Transportista = transportista,
    };

    /// <summary>Dos conexiones de la misma base aislada, abiertas en alcances independientes.</summary>
    public static async Task JuntasAsync(AplicacionDePrueba aplicacion,
        Func<GtDbContext, Task> primera, Func<GtDbContext, Task> segunda)
    {
        using var alcancePrimero = aplicacion.Services.CreateScope();
        using var alcanceSegundo = aplicacion.Services.CreateScope();
        var contextoPrimero = alcancePrimero.ServiceProvider.GetRequiredService<GtDbContext>();
        var contextoSegundo = alcanceSegundo.ServiceProvider.GetRequiredService<GtDbContext>();
        await EjecutarJuntasAsync(() => primera(contextoPrimero), () => segunda(contextoSegundo));
    }

    private static async Task EjecutarJuntasAsync(Func<Task> primera, Func<Task> segunda)
    {
        var listas = 0;
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Ejecutar(Func<Task> operacion)
        {
            if (Interlocked.Increment(ref listas) == 2) salida.SetResult();
            await salida.Task;
            await operacion();
        }
        await Task.WhenAll(Ejecutar(primera), Ejecutar(segunda));
    }
}
