using GT.Domain.Choferes;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Datos de prueba del módulo de viajes, cargados directamente en la base para no depender de la API
/// que el propio test está verificando.
/// </summary>
public static class DatosDePruebaViajes
{
    private static int _contadorCuit = 27000000;
    private static int _contadorRazonSocial;

    public static Task<Cliente> CrearClienteAsync(
        this AplicacionDePrueba app,
        string razonSocial = "Distribuidora del Litoral",
        string? cuit = null,
        bool activo = true) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var cliente = new Cliente
            {
                RazonSocial = $"{razonSocial} {Interlocked.Increment(ref _contadorRazonSocial)}",
                Cuit = cuit ?? CuitUnico(),
                Telefono = "0341-555-5555",
                Email = "compras@litoral.com.ar",
                Activo = activo,
            };

            contexto.Clientes.Add(cliente);
            await contexto.SaveChangesAsync();

            return cliente;
        });

    /// <summary>
    /// Un viaje ya en el estado pedido, saltando el ciclo de vida.
    ///
    /// Sirve para armar el punto de partida de un test —"con un viaje en curso…"— sin tener que
    /// recorrer las transiciones que ese test no está verificando. Los tests del ciclo de vida sí
    /// usan los endpoints.
    /// </summary>
    public static Task<Viaje> CrearViajeAsync(
        this AplicacionDePrueba app,
        int clienteId,
        DateOnly? fecha = null,
        EstadoViaje estado = EstadoViaje.Pendiente,
        string origen = "Rosario",
        string destino = "Córdoba",
        string? numeroRemito = null,
        decimal importe = 0m,
        int? choferId = null,
        int? vehiculoId = null,
        int? transportistaId = null,
        string? motivoAnulacion = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var viaje = new Viaje
            {
                ClienteId = clienteId,
                Fecha = fecha ?? FechaHoyArgentina.Hoy(),
                Origen = origen,
                Destino = destino,
                NumeroRemito = numeroRemito,
                Importe = importe,
                Estado = estado,
                ChoferId = choferId,
                VehiculoId = vehiculoId,
                TransportistaId = transportistaId,
                MotivoAnulacion = motivoAnulacion,
            };

            contexto.Viajes.Add(viaje);
            await contexto.SaveChangesAsync();

            return viaje;
        });

    public static Task<Viaje?> RecargarViajeAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Viajes
            .AsNoTracking()
            .FirstOrDefaultAsync(viaje => viaje.Id == id));

    public static Task<Cliente?> RecargarClienteAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(cliente => cliente.Id == id));

    /// <summary>El historial de un viaje, de la línea más vieja a la más nueva (FR-035).</summary>
    public static Task<List<CambioDeEstadoViaje>> HistorialDeAsync(
        this AplicacionDePrueba app,
        int viajeId) =>
        app.ConAlcanceAsync(contexto => contexto.CambiosDeEstadoViaje
            .Where(cambio => cambio.ViajeId == viajeId)
            .OrderBy(cambio => cambio.OcurridoEn)
            .ThenBy(cambio => cambio.Id)
            .AsNoTracking()
            .ToListAsync());

    /// <summary>Una cuenta con un rol del sistema, para verificar el reparto de los dos permisos.</summary>
    public static Task<Usuario> CrearUsuarioConRolViajesAsync(
        this AplicacionDePrueba app,
        string username,
        string password,
        string codigoRol) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();
            var rol = await contexto.Roles.FirstAsync(r => r.Codigo == codigoRol);

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(password),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });

    private static int _semilla = 80_000_000;

    /// <summary>Una semilla distinta por llamada, para no chocar contra DNI ni CUIL repetidos.</summary>
    public static int SemillaUnica() => Interlocked.Increment(ref _semilla);

    /// <summary>Un CUIT con dígito verificador correcto y distinto en cada llamada (FR-004).</summary>
    public static string CuitUnico() => CuitValidoPara(Interlocked.Increment(ref _contadorCuit));

    public static string CuitValidoPara(int semilla)
    {
        var baseCuit = $"27{semilla:D8}";
        int[] multiplicadores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (baseCuit[i] - '0') * multiplicadores[i];
        }

        var resto = suma % 11;
        var digito = resto == 0 ? 0 : resto == 1 ? 9 : 11 - resto;

        return baseCuit + digito;
    }
}

// ── Lo que devuelve el backend, tal como lo fija contracts/viajes-api.yaml ──────────────────────

public record ResumenLeido(int Id, string Nombre, bool Activo);

public record ClienteLeido(
    int Id,
    string RazonSocial,
    string Cuit,
    string Telefono,
    string Email,
    string? Direccion,
    bool Activo);

public record ViajeLeido(
    int Id,
    int Numero,
    string Fecha,
    ResumenLeido Cliente,
    string Origen,
    string Destino,
    ResumenLeido? Chofer,
    ResumenLeido? Vehiculo,
    ResumenLeido? Transportista,
    string Estado,
    decimal Importe,
    bool Demorado,
    bool EsRetroactivo,
    string? MotivoAnulacion);

public record ViajeDetalleLeido(
    int Id,
    int Numero,
    string Fecha,
    ResumenLeido Cliente,
    string Origen,
    string Destino,
    ResumenLeido? Chofer,
    ResumenLeido? Vehiculo,
    ResumenLeido? Transportista,
    string Estado,
    decimal Importe,
    bool Demorado,
    bool EsRetroactivo,
    string? MotivoAnulacion,
    string? NumeroRemito,
    string? DetalleCarga,
    List<CambioDeEstadoLeido> Historial);

public record CambioDeEstadoLeido(
    string? EstadoAnterior,
    string EstadoNuevo,
    string Usuario,
    DateTime OcurridoEn);

public record AdvertenciaLeida(string Codigo, string Mensaje);

public record RespuestaViajeLeida(ViajeDetalleLeido Viaje, List<AdvertenciaLeida> Advertencias);

public record PaginaDeViajesLeida(List<ViajeLeido> Items, int Total, int Pagina, int TamanioPagina);

public record PaginaDeClientesLeida(
    List<ClienteLeido> Items,
    int Total,
    int Pagina,
    int TamanioPagina);

public record AsignablesLeidos(List<ResumenLeido> Choferes, List<ResumenLeido> Vehiculos);

public record TotalLeido(int Id, string Nombre, int CantidadViajes, decimal ImporteTotal);

public record TotalesLeidos(List<TotalLeido> PorCliente, List<TotalLeido> PorTransportista);

/// <summary>
/// El cuerpo de error del módulo, con los cuatro campos opcionales que llevan los rechazos que
/// necesitan explicarse: cuántos viajes, qué viaje ocupa, qué unidad y qué documento bloquean.
/// </summary>
public record ErrorViajeLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    int? CantidadViajes,
    int? ViajeQueOcupa,
    string? UnidadQueBloquea,
    string? DocumentoQueBloquea);
