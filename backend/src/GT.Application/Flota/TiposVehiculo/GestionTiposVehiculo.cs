using EntidadTipoVehiculo = GT.Domain.Flota.TipoVehiculo;

namespace GT.Application.Flota.TiposVehiculo;

public enum ErrorTipoVehiculo
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    NombreDuplicado,

    /// <summary>El tipo tiene vehículos asociados y por eso no se puede dar de baja (FR-010).</summary>
    ConVehiculos,
}

public record ResultadoTipoVehiculo(
    ErrorTipoVehiculo Error,
    TipoVehiculoDto? Tipo = null,
    string? Campo = null)
{
    public bool Exitoso => Error is ErrorTipoVehiculo.Ninguno;

    /// <summary>Cuántos vehículos usan el tipo, para poder decirlo en el mensaje (FR-010, SC-008).</summary>
    public int? CantidadVehiculos { get; init; }
}

public record TipoVehiculoRequest(string? Nombre);

/// <param name="CantidadVehiculos">
/// Vehículos que usan este tipo, <b>activos e inactivos</b>. Es lo que impide la baja y lo que el
/// listado muestra para explicar por qué algunos no se pueden dar de baja (FR-010).
/// </param>
public record TipoVehiculoDto(int Id, string Nombre, bool Activo, int CantidadVehiculos)
{
    public static TipoVehiculoDto Desde(EntidadTipoVehiculo tipo, int cantidadVehiculos = 0) =>
        new(tipo.Id, tipo.Nombre, tipo.Activo, cantidadVehiculos);

    public static TipoVehiculoDto Desde(TipoConVehiculos fila) => Desde(fila.Tipo, fila.Vehiculos);
}

/// <summary>
/// Catálogo de tipos de vehículo (FR-009 a FR-011).
///
/// Arranca vacío y se completa desde la pantalla: sin al menos un tipo activo no se puede registrar
/// ninguna unidad (FR-005). El ABM exige <c>flota.tipos.gestionar</c>, que sólo tiene el
/// Administrador del sistema; leer el catálogo alcanza con <c>flota.gestionar</c>, porque el
/// formulario de vehículo lo consume (FR-039).
/// </summary>
public class GestionTiposVehiculo(IRepositorioTiposVehiculo repositorio)
{
    public async Task<List<TipoVehiculoDto>> ConsultarAsync(
        bool soloActivos = false,
        CancellationToken cancelacion = default)
    {
        var tipos = await repositorio.ConsultarAsync(soloActivos, cancelacion);

        return tipos.Select(TipoVehiculoDto.Desde).ToList();
    }

    public async Task<ResultadoTipoVehiculo> CrearAsync(
        TipoVehiculoRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.DatosInvalidos, Campo: invalido);
        }

        var nombre = peticion.Nombre!.Trim();

        if (await repositorio.ExisteNombreAsync(nombre, null, cancelacion))
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NombreDuplicado, Campo: "nombre");
        }

        var tipo = new EntidadTipoVehiculo { Nombre = nombre, Activo = true };

        await repositorio.AgregarAsync(tipo, cancelacion);

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (NombreDeTipoVehiculoDuplicadoException)
        {
            // La consulta previa cierra la ventana normal; el índice cierra la carrera.
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NombreDuplicado, Campo: "nombre");
        }

        // Recién creado: todavía no lo usa ningún vehículo.
        return new ResultadoTipoVehiculo(
            ErrorTipoVehiculo.Ninguno,
            TipoVehiculoDto.Desde(tipo, cantidadVehiculos: 0));
    }

    public async Task<ResultadoTipoVehiculo> ModificarAsync(
        int id,
        TipoVehiculoRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.DatosInvalidos, Campo: invalido);
        }

        var tipo = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (tipo is null)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NoEncontrado);
        }

        var nombre = peticion.Nombre!.Trim();

        // Conservar el propio nombre no es un duplicado (FR-009).
        if (await repositorio.ExisteNombreAsync(nombre, id, cancelacion))
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NombreDuplicado, Campo: "nombre");
        }

        tipo.Nombre = nombre;

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (NombreDeTipoVehiculoDuplicadoException)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NombreDuplicado, Campo: "nombre");
        }

        var fila = await repositorio.ObtenerConVehiculosAsync(id, cancelacion);

        return new ResultadoTipoVehiculo(ErrorTipoVehiculo.Ninguno, TipoVehiculoDto.Desde(fila!));
    }

    /// <summary>
    /// Baja lógica: el registro <b>no se borra</b>, queda inactivo y deja de ofrecerse al registrar
    /// vehículos (FR-009, FR-028).
    ///
    /// Se rechaza si el tipo tiene <b>cualquier</b> vehículo asociado, activo o dado de baja,
    /// informando cuántos son (FR-010). La asimetría con la baja de transportista —que sólo mira
    /// dependientes activos— es deliberada: un vehículo dado de baja sigue mostrando su tipo
    /// (FR-011), así que el tipo tiene que seguir existiendo (research §8).
    /// </summary>
    public async Task<ResultadoTipoVehiculo> DarDeBajaAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var fila = await repositorio.ObtenerConVehiculosAsync(id, cancelacion);
        if (fila is null)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NoEncontrado);
        }

        if (fila.Vehiculos > 0)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.ConVehiculos)
            {
                CantidadVehiculos = fila.Vehiculos,
            };
        }

        var tipo = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        tipo!.Activo = false;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoTipoVehiculo(
            ErrorTipoVehiculo.Ninguno,
            TipoVehiculoDto.Desde(tipo, cantidadVehiculos: 0));
    }

    /// <summary>
    /// Vuelve a poner activo un tipo dado de baja (FR-009).
    ///
    /// No tiene nada que validar —un tipo no depende de nadie— y por eso no lleva confirmación
    /// aparte: la acción vive dentro del formulario de edición, que ya exige haber elegido el tipo.
    /// Es idempotente: reactivar uno que ya está activo lo deja como está en vez de fallar.
    /// </summary>
    public async Task<ResultadoTipoVehiculo> ReactivarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var tipo = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (tipo is null)
        {
            return new ResultadoTipoVehiculo(ErrorTipoVehiculo.NoEncontrado);
        }

        tipo.Activo = true;

        await repositorio.GuardarCambiosAsync(cancelacion);

        var fila = await repositorio.ObtenerConVehiculosAsync(id, cancelacion);

        return new ResultadoTipoVehiculo(ErrorTipoVehiculo.Ninguno, TipoVehiculoDto.Desde(fila!));
    }

    private static string? PrimerCampoInvalido(TipoVehiculoRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre)) return "nombre";
        if (peticion.Nombre.Trim().Length > 100) return "nombre";

        return null;
    }
}
