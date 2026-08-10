using GT.Domain.Choferes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
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

    /// <summary>
    /// Un tipo del catálogo, saltando la API. El nombre lleva sufijo único porque el catálogo tiene
    /// índice único de nombre y varios tests comparten la misma base.
    /// </summary>
    /// <param name="ambito">
    /// Chofer por defecto, que es lo que el Módulo 3 siempre necesita. Los tests de flota piden
    /// explícitamente <c>Vehiculo</c> (Módulo 4, FR-017).
    /// </param>
    public static Task<DocumentacionTipo> CrearTipoDocumentacionAsync(
        this AplicacionDePrueba app,
        string nombre = "Licencia de conducir",
        int diasAvisoVencimiento = 30,
        bool activo = true,
        DocumentacionAmbito ambito = DocumentacionAmbito.Chofer) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var tipo = new DocumentacionTipo
            {
                Nombre = $"{nombre} {Interlocked.Increment(ref _contadorTipo)}",
                DiasAvisoVencimiento = diasAvisoVencimiento,
                Ambito = ambito,
                Activo = activo,
            };

            contexto.DocumentacionTipos.Add(tipo);
            await contexto.SaveChangesAsync();

            return tipo;
        });

    /// <summary>
    /// Un documento del chofer, saltando la API. <paramref name="diasHastaVencimiento"/> se cuenta
    /// desde hoy en hora de Argentina, que es contra lo que se calcula el estado (FR-017a).
    /// </summary>
    public static Task<Documentacion> CrearDocumentoAsync(
        this AplicacionDePrueba app,
        int choferId,
        int tipoId,
        int diasHastaVencimiento,
        string numero = "ABC-123",
        string? archivoRuta = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hoy = FechaHoyArgentina.Hoy();

            var documento = new Documentacion
            {
                ChoferId = choferId,
                DocumentacionTipoId = tipoId,
                Numero = numero,
                FechaEmision = hoy.AddYears(-1),
                FechaVencimiento = hoy.AddDays(diasHastaVencimiento),
                ArchivoRuta = archivoRuta,
                ArchivoNombre = archivoRuta is null ? null : "escaneo.pdf",
                ArchivoTipoContenido = archivoRuta is null ? null : "application/pdf",
            };

            contexto.Documentaciones.Add(documento);
            await contexto.SaveChangesAsync();

            return documento;
        });

    /// <summary>
    /// Un chofer completo —transportista, persona y chofer— a partir de una semilla que sale del
    /// nombre del test. Evita que dos tests de la misma base choquen por DNI o CUIL repetidos.
    /// </summary>
    public static async Task<Chofer> CrearChoferCompletoAsync(
        this AplicacionDePrueba app,
        int semilla,
        bool activo = true,
        int? transportistaId = null)
    {
        var idTransportista = transportistaId
            ?? (await app.CrearTransportistaAsync()).Id;

        var persona = await app.CrearPersonaAsync(dni: $"{semilla:D8}");

        return await app.CrearChoferAsync(
            persona.Id,
            idTransportista,
            cuil: CuilValidoPara(semilla),
            activo: activo);
    }

    public static Task<int> ContarDocumentosDelChoferAsync(this AplicacionDePrueba app, int choferId) =>
        app.ConAlcanceAsync(contexto =>
            contexto.Documentaciones.CountAsync(documento => documento.ChoferId == choferId));

    /// <summary>El documento tal como quedó en la base, o <c>null</c> si ya no está.</summary>
    public static Task<Documentacion?> RecargarDocumentoAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Documentaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(documento => documento.Id == id));

    /// <summary>Un CUIL con dígito verificador correcto, derivado de la semilla (FR-007).</summary>
    public static string CuilValidoPara(int semilla)
    {
        var baseCuil = $"20{semilla:D8}";
        int[] multiplicadores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (baseCuil[i] - '0') * multiplicadores[i];
        }

        var resto = suma % 11;
        var digito = resto == 0 ? 0 : resto == 1 ? 9 : 11 - resto;

        return baseCuil + digito;
    }

    private static int _contadorTipo;
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
