using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.Domain.Choferes;
using GT.Domain.Personas;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Usuarios;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Datos de prueba del Módulo 10, cargados directamente en la base para no depender de la API que el
/// propio test está verificando.
///
/// Cada test crea <b>sus propias</b> personas, con DNI y CUIL únicos: los tests de una misma clase comparten
/// la base, así que las aserciones sobre listas miran sólo las filas que el test creó.
/// </summary>
public static class DatosDePruebaAdelantos
{
    public static DateOnly HoyEnArgentina => FechaHoyArgentina.Hoy();

    public static string Hoy => HoyEnArgentina.ToString("yyyy-MM-dd");

    public static Task<Persona> EmpleadoAsync(
        this AplicacionDePrueba app,
        string apellido = "Torres",
        string nombre = "Ana",
        bool activa = true) =>
        app.CrearPersonaAsync(
            dni: DatosDePruebaViajes.SemillaUnica().ToString(),
            nombre: nombre,
            apellido: apellido,
            tipo: TipoIntegrante.Empleado,
            activa: activa);

    /// <summary>Persona con ficha de chofer del transportista indicado.</summary>
    /// <param name="tipo">El dato informativo del padrón: la ficha es lo que decide quién es chofer.</param>
    public static async Task<Persona> ChoferAsync(
        this AplicacionDePrueba app,
        int transportistaId,
        string apellido = "Pérez",
        string nombre = "Juan",
        bool personaActiva = true,
        bool fichaActiva = true,
        TipoIntegrante tipo = TipoIntegrante.Chofer)
    {
        var semilla = DatosDePruebaViajes.SemillaUnica();

        var persona = await app.CrearPersonaAsync(
            dni: semilla.ToString(),
            nombre: nombre,
            apellido: apellido,
            tipo: tipo,
            activa: personaActiva);

        await app.CrearChoferAsync(persona.Id, transportistaId, Choferes.DatosDePrueba.CuilValidoPara(semilla), fichaActiva);

        return persona;
    }

    /// <summary>Chofer de G&amp;T Logística: configura la empresa emisora y el transportista con su CUIT.</summary>
    public static async Task<Persona> ChoferPropioAsync(
        this AplicacionDePrueba app,
        string apellido = "Pérez",
        string nombre = "Juan",
        bool fichaActiva = true)
    {
        await app.ConfigurarEmpresaEmisoraAsync();
        var propio = await app.TransportistaPropioAsync();

        return await app.ChoferAsync(propio.Id, apellido, nombre, fichaActiva: fichaActiva);
    }

    public static async Task<Persona> ChoferExternoAsync(
        this AplicacionDePrueba app,
        string apellido = "Díaz",
        string nombre = "Luis")
    {
        var externo = await app.TransportistaExternoAsync();

        return await app.ChoferAsync(externo.Id, apellido, nombre);
    }

    /// <summary>
    /// Un adelanto ya en el estado pedido y con cualquier fecha, saltando el registro: la regla de la fecha
    /// no deja cargar por la API los escenarios de listado. Respeta lo que exige la base —el motivo de
    /// cierre en rechazado y anulado— y arma el historial que ese estado implica.
    /// </summary>
    public static Task<Adelanto> CrearAdelantoAsync(
        this AplicacionDePrueba app,
        int personaId,
        EstadoAdelanto estado = EstadoAdelanto.Pendiente,
        decimal importe = 150_000m,
        DateOnly? fecha = null,
        TipoBeneficiario tipo = TipoBeneficiario.Empleado,
        string motivo = "Gastos médicos",
        string? motivoDeCierre = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var administrador = await DatosDePruebaLiquidaciones.AdministradorAsync(contexto);
            var registradoEn = DateTime.UtcNow.AddMinutes(-10);

            var adelanto = new Adelanto
            {
                PersonaId = personaId,
                TipoBeneficiario = tipo,
                Fecha = fecha ?? HoyEnArgentina,
                Motivo = motivo,
                Importe = importe,
                Estado = estado,
                MotivoRechazo = estado is EstadoAdelanto.Rechazado ? motivoDeCierre ?? "Rechazado en la prueba." : null,
                MotivoAnulacion = estado is EstadoAdelanto.Anulado ? motivoDeCierre ?? "Anulado en la prueba." : null,
            };

            void Entrada(OperacionDeAdelanto operacion, int minutosDespues) =>
                adelanto.Cambios.Add(new CambioDeAdelanto
                {
                    AdelantoId = 0,
                    Operacion = operacion,
                    UsuarioId = administrador.Id,
                    OcurridoEn = registradoEn.AddMinutes(minutosDespues),
                });

            Entrada(OperacionDeAdelanto.Registro, 0);

            if (estado is EstadoAdelanto.Aprobado or EstadoAdelanto.Anulado)
            {
                Entrada(OperacionDeAdelanto.Aprobacion, 1);
            }

            if (estado is EstadoAdelanto.Rechazado)
            {
                Entrada(OperacionDeAdelanto.Rechazo, 1);
            }

            if (estado is EstadoAdelanto.Anulado)
            {
                Entrada(OperacionDeAdelanto.Anulacion, 2);
            }

            contexto.Adelantos.Add(adelanto);
            await contexto.SaveChangesAsync();

            return adelanto;
        });

    public static Task<Adelanto?> RecargarAdelantoAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Adelantos
            .Include(adelanto => adelanto.Cambios)
            .AsNoTracking()
            .FirstOrDefaultAsync(adelanto => adelanto.Id == id));

    public static Task<int> ContarAdelantosDeAsync(this AplicacionDePrueba app, int personaId) =>
        app.ConAlcanceAsync(contexto => contexto.Adelantos.CountAsync(adelanto => adelanto.PersonaId == personaId));

    public static Task<Chofer> RecargarFichaAsync(this AplicacionDePrueba app, int personaId) =>
        app.ConAlcanceAsync(contexto => contexto.Choferes
            .AsNoTracking()
            .FirstAsync(chofer => chofer.PersonaId == personaId));

    public static Task DarDeBajaPersonaAsync(this AplicacionDePrueba app, int personaId) =>
        app.EnLaBaseAsync(contexto => contexto.Personas
            .Where(persona => persona.Id == personaId)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(persona => persona.Activa, false)));

    public static Task DarDeBajaFichaAsync(this AplicacionDePrueba app, int personaId) =>
        app.EnLaBaseAsync(contexto => contexto.Choferes
            .Where(chofer => chofer.PersonaId == personaId)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(chofer => chofer.Activo, false)));

    public static Task CorregirApellidoAsync(this AplicacionDePrueba app, int personaId, string apellido) =>
        app.EnLaBaseAsync(contexto => contexto.Personas
            .Where(persona => persona.Id == personaId)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(persona => persona.Apellido, apellido)));

    // ── Peticiones ──────────────────────────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> RegistrarAsync(
        this HttpClient cliente,
        string tipo,
        int personaId,
        DateOnly? fecha = null,
        string motivo = "gastos médicos",
        decimal importe = 150_000m) =>
        cliente.PostAsJsonAsync(
            "/api/adelantos",
            new { tipo, personaId, fecha = (fecha ?? HoyEnArgentina).ToString("yyyy-MM-dd"), motivo, importe });

    public static Task<HttpResponseMessage> AprobarAsync(this HttpClient cliente, int id) =>
        cliente.PostAsync($"/api/adelantos/{id}/aprobacion", null);

    public static Task<HttpResponseMessage> RechazarAsync(this HttpClient cliente, int id, string motivo, bool? confirmado) =>
        cliente.PostAsJsonAsync($"/api/adelantos/{id}/rechazo", new { motivo, confirmado });

    public static Task<HttpResponseMessage> AnularAsync(this HttpClient cliente, int id, string motivo, bool? confirmado) =>
        cliente.PostAsJsonAsync($"/api/adelantos/{id}/anulacion", new { motivo, confirmado });

    public static async Task<BeneficiariosLeidos> BeneficiariosAsync(this HttpClient cliente, string tipo) =>
        (await cliente.GetFromJsonAsync<BeneficiariosLeidos>($"/api/adelantos/beneficiarios?tipo={tipo}"))!;

    /// <summary>Recorre todas las páginas de una consulta: las filas de otros tests pueden empujar las propias.</summary>
    public static async Task<List<AdelantoListadoLeido>> TodasLasPaginasAsync(this HttpClient cliente, string consulta)
    {
        var filas = new List<AdelantoListadoLeido>();
        var separador = consulta.Contains('?') ? '&' : '?';

        for (var pagina = 1; ; pagina++)
        {
            var leida = (await cliente.GetFromJsonAsync<PaginaDeAdelantosLeida>($"{consulta}{separador}pagina={pagina}"))!;
            filas.AddRange(leida.Items);

            if (pagina * leida.TamanioPagina >= leida.Total)
            {
                return filas;
            }
        }
    }
}

// ── Lo que devuelve el backend, tal como lo fija contracts/adelantos-api.yaml ───────────────────

public record PersonaResumenLeida(int Id, string Apellido, string Nombre, string Dni);

public record BeneficiariosLeidos(bool EmpresaEmisoraConfigurada, List<PersonaResumenLeida> Personas);

public record AdelantoListadoLeido(
    int Id,
    string Fecha,
    PersonaResumenLeida Persona,
    string Tipo,
    string Motivo,
    decimal Importe,
    string Estado);

public record PaginaDeAdelantosLeida(
    List<AdelantoListadoLeido> Items,
    int Total,
    int Pagina,
    int TamanioPagina,
    decimal TotalAdelantado);

public record CambioDeAdelantoLeido(string Operacion, string Usuario, DateTime OcurridoEn, string? Motivo);

public record AdelantoDetalleLeido(
    int Id,
    string Fecha,
    PersonaResumenLeida Persona,
    string Tipo,
    string Motivo,
    decimal Importe,
    string Estado,
    string? MotivoRechazo,
    string? MotivoAnulacion,
    List<CambioDeAdelantoLeido> Historial,
    bool PuedeResolverse,
    bool PuedeAnularse);

/// <summary>Los cuatro cuerpos de error del contrato en uno: los campos que un rechazo no lleva llegan nulos.</summary>
public record ErrorAdelantoLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    string? Desde,
    string? Hasta,
    string? Motivo,
    string? Estado);
