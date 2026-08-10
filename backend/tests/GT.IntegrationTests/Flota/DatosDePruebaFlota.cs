using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Datos de prueba del módulo de flota, cargados directamente en la base para no depender de la API
/// que el propio test está verificando.
/// </summary>
public static class DatosDePruebaFlota
{
    private static int _contadorTipo;
    private static int _contadorPatente;

    public static Task<TipoVehiculo> CrearTipoVehiculoAsync(
        this AplicacionDePrueba app,
        string nombre = "Tractor",
        bool activo = true) =>
        app.ConAlcanceAsync(async contexto =>
        {
            // El nombre lleva sufijo único porque el catálogo tiene índice único y varios tests
            // comparten la misma base.
            var tipo = new TipoVehiculo
            {
                Nombre = $"{nombre} {Interlocked.Increment(ref _contadorTipo)}",
                Activo = activo,
            };

            contexto.TiposVehiculo.Add(tipo);
            await contexto.SaveChangesAsync();

            return tipo;
        });

    /// <summary>
    /// Una patente Mercosur válida y distinta en cada llamada, para que dos tests de la misma base no
    /// choquen contra el índice único (FR-002).
    /// </summary>
    public static string PatenteUnica()
    {
        var numero = Interlocked.Increment(ref _contadorPatente);

        var primeraLetra = (char)('A' + numero / 26 % 26);
        var segundaLetra = (char)('A' + numero % 26);

        return $"{primeraLetra}{segundaLetra}{numero % 1000:D3}ZZ";
    }

    public static Task<Vehiculo> CrearVehiculoAsync(
        this AplicacionDePrueba app,
        int tipoVehiculoId,
        int transportistaId,
        string? patente = null,
        VehiculoEstado estadoOperativo = VehiculoEstado.FueraDeServicio,
        bool activo = true,
        string marca = "Scania",
        string modelo = "R450") =>
        app.ConAlcanceAsync(async contexto =>
        {
            var vehiculo = new Vehiculo
            {
                Patente = NormalizadorPatente.Normalizar(patente ?? PatenteUnica()),
                Marca = marca,
                Modelo = modelo,
                TipoVehiculoId = tipoVehiculoId,
                TransportistaId = transportistaId,
                EstadoOperativo = estadoOperativo,
                Activo = activo,
            };

            contexto.Vehiculos.Add(vehiculo);
            await contexto.SaveChangesAsync();

            return vehiculo;
        });

    /// <summary>
    /// Un documento del vehículo. <paramref name="diasHastaVencimiento"/> se cuenta desde hoy en hora
    /// de Argentina, que es contra lo que se calcula el estado (FR-020).
    /// </summary>
    public static Task<DocumentacionVehiculo> CrearDocumentoVehiculoAsync(
        this AplicacionDePrueba app,
        int vehiculoId,
        int tipoId,
        int diasHastaVencimiento,
        string numero = "POL-123",
        string? archivoRuta = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hoy = FechaHoyArgentina.Hoy();

            var documento = new DocumentacionVehiculo
            {
                VehiculoId = vehiculoId,
                DocumentacionTipoId = tipoId,
                Numero = numero,
                FechaEmision = hoy.AddYears(-1),
                FechaVencimiento = hoy.AddDays(diasHastaVencimiento),
                ArchivoRuta = archivoRuta,
                ArchivoNombre = archivoRuta is null ? null : "escaneo.pdf",
                ArchivoTipoContenido = archivoRuta is null ? null : "application/pdf",
            };

            contexto.DocumentacionesVehiculo.Add(documento);
            await contexto.SaveChangesAsync();

            return documento;
        });

    /// <summary>Un tipo de documentación de <b>ámbito vehículo</b>, que es el único que la flota acepta.</summary>
    public static Task<DocumentacionTipo> CrearTipoDocumentacionDeVehiculoAsync(
        this AplicacionDePrueba app,
        string nombre = "Seguro del vehículo",
        int diasAvisoVencimiento = 30,
        bool activo = true) =>
        Choferes.DatosDePrueba.CrearTipoDocumentacionAsync(
            app,
            nombre,
            diasAvisoVencimiento,
            activo,
            DocumentacionAmbito.Vehiculo);

    public static Task<DocumentacionVehiculo?> RecargarDocumentoVehiculoAsync(
        this AplicacionDePrueba app,
        int id) =>
        app.ConAlcanceAsync(contexto => contexto.DocumentacionesVehiculo
            .AsNoTracking()
            .FirstOrDefaultAsync(documento => documento.Id == id));

    public static Task<Vehiculo?> RecargarVehiculoAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Vehiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(vehiculo => vehiculo.Id == id));

    /// <summary>
    /// Una cuenta con un rol del sistema, para verificar el reparto de los dos permisos del módulo
    /// (FR-039).
    /// </summary>
    public static Task<Usuario> CrearUsuarioConRolAsync(
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
}

/// <summary>Lo que devuelve el backend por cada vehículo del listado (<c>contracts/flota-api.yaml</c>).</summary>
public record VehiculoLeido(
    int Id,
    string Patente,
    string Marca,
    string Modelo,
    ResumenLeido Tipo,
    ResumenLeido Transportista,
    bool Activo,
    string Estado,
    string EstadoDocumentacion);

/// <summary>La ficha: suma el estado guardado y los documentos (FR-038).</summary>
public record VehiculoDetalleLeido(
    int Id,
    string Patente,
    string Marca,
    string Modelo,
    ResumenLeido Tipo,
    ResumenLeido Transportista,
    bool Activo,
    string Estado,
    string EstadoDocumentacion,
    string EstadoOperativoGuardado,
    List<DocumentoVehiculoLeido> Documentos);

public record DocumentoVehiculoLeido(
    int Id,
    int VehiculoId,
    ResumenLeido Tipo,
    string Numero,
    string FechaEmision,
    string FechaVencimiento,
    string Estado,
    bool EsVigenteDelTipo,
    int DiasHastaVencimiento,
    bool TieneArchivo,
    string? ArchivoNombre);

public record AlertaFlotaLeida(
    int VehiculoId,
    string Patente,
    ResumenLeido Transportista,
    DocumentoVehiculoLeido Documento);

public record ResumenLeido(int Id, string Nombre);

public record TipoVehiculoLeido(int Id, string Nombre, bool Activo, int CantidadVehiculos);

public record PaginaDeVehiculos(List<VehiculoLeido> Items, int Total, int Pagina, int TamanioPagina);

public record ErrorFlotaLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    int? CantidadVehiculos,
    int? CantidadChoferes,
    int? CantidadDocumentos);
