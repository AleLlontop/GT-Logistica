using GT.Domain.Choferes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Choferes;

public static class DatosDePrueba
{
    public static Task<Transportista> CrearTransportistaAsync(
        this AplicacionDePrueba app,
        string nombre = "G&T Logística S.A.",
        string? cuit = null,
        TipoPersona tipo = TipoPersona.Juridica,
        bool activo = true) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var transportista = new Transportista
            {
                Nombre = nombre,
                Cuit = cuit ?? GenerarCuitValido(),
                Tipo = tipo,
                Telefono = "11-5555-5555",
                Email = "info@gt.com.ar",
                Activo = activo
            };

            contexto.Transportistas.Add(transportista);
            await contexto.SaveChangesAsync();

            return transportista;
        });

    public static Task<Chofer> CrearChoferAsync(
        this AplicacionDePrueba app,
        int personaId,
        int transportistaId,
        string cuil = "20123456781",
        bool activo = true) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var chofer = new Chofer
            {
                PersonaId = personaId,
                TransportistaId = transportistaId,
                Cuil = cuil,
                Activo = activo
            };

            contexto.Choferes.Add(chofer);
            await contexto.SaveChangesAsync();

            return chofer;
        });

    private static int _contadorCuit = 10000000;
    private static string GenerarCuitValido()
    {
        var baseCuit = "30" + Interlocked.Increment(ref _contadorCuit).ToString();
        var multiplicadores = new[] { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (baseCuit[i] - '0') * multiplicadores[i];
        }
        var resto = suma % 11;
        var digito = resto == 0 ? 0 : resto == 1 ? 9 : 11 - resto;
        return baseCuit + digito.ToString();
    }
}
