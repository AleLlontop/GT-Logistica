using GT.Domain.Personas;

namespace GT.Application.Usuarios.Personas;

/// <summary>Motivo por el que un alta o una edición de persona no se pudo guardar.</summary>
public enum ErrorPersona
{
    Ninguno,
    NoEncontrada,
    DatosInvalidos,
    DniDuplicado,
    /// <summary>El DNI es de una persona dada de baja: se la reactiva, no se la carga de nuevo (FR-022b).</summary>
    DniDePersonaDadaDeBaja,
    /// <summary>Se pidió reactivar una persona que ya estaba activa (FR-022b).</summary>
    YaEstaActiva,
    /// <summary>Está vinculada a un usuario, así que no se puede dar de baja (FR-028).</summary>
    Vinculada,
    /// <summary>
    /// Está registrada como chofer (Módulo 3), así que tampoco se puede dar de baja: quedaría un
    /// chofer apuntando a una persona inactiva.
    /// </summary>
    EsChofer,
}

public record ResultadoPersona(ErrorPersona Error, PersonaDto? Persona, string? Campo = null)
{
    public bool Exitoso => Error is ErrorPersona.Ninguno;

    /// <summary>Username del usuario que la tiene, para poder nombrarlo en el mensaje (FR-028).</summary>
    public string? UsernameQueLaTiene { get; init; }
}

/// <summary>
/// Validación de los siete datos de una persona (FR-026).
///
/// Vive aparte porque el alta y la edición la comparten completa: son el mismo conjunto de campos y
/// las mismas reglas.
/// </summary>
public static class ValidadorPersona
{
    public const int LargoMinimoDni = 7;
    public const int LargoMaximoDni = 15;

    private static bool NombreValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var nombre = valor.Trim();

        return nombre.Length <= 100
            && nombre.Any(char.IsLetter)
            && nombre.All(caracter =>
                char.IsLetter(caracter)
                || caracter is ' ' or '-' or '\'' or '’' or '.');
    }

    private static bool TelefonoValido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return false;

        var telefono = valor.Trim();

        if (telefono.Length > 30)
            return false;

        var cantidadDigitos = telefono.Count(char.IsAsciiDigit);

        return cantidadDigitos is >= 6 and <= 20
            && telefono.All(caracter =>
                char.IsAsciiDigit(caracter)
                || caracter is '+' or ' ' or '-' or '(' or ')' or '.');
    }

    /// <returns>El nombre del campo que falla, o <c>null</c> si está todo bien.</returns>
    public static string? PrimerCampoInvalido(PersonaRequest peticion)
    {
        if (!NombreValido(peticion.Nombre))
        {
            return "nombre";
        }

        if (!NombreValido(peticion.Apellido))
        {
            return "apellido";
        }

        var dni = (peticion.Dni ?? string.Empty).Trim();

        // Sólo dígitos: un DNI con puntos o letras se guarda distinto según quién lo escriba, y la
        // unicidad de FR-027 dejaría de detectar duplicados reales.
        if (dni.Length is < LargoMinimoDni or > LargoMaximoDni || !dni.All(char.IsAsciiDigit))
        {
            return "dni";
        }

        if (TipoIntegranteTexto.Interpretar(peticion.Tipo) is null)
        {
            return "tipo";
        }

        if (!TelefonoValido(peticion.Telefono))
        {
            return "telefono";
        }

        if (!ValidadorEmail.EsValido(peticion.Email))
        {
            return "email";
        }

        if (peticion.FechaNacimiento is null)
        {
            return "fechaNacimiento";
        }

        return null;
    }
}

/// <summary>Alta de una persona en el padrón (User Story 6).</summary>
public class CrearPersona(IRepositorioPersonas repositorio)
{
    public async Task<ResultadoPersona> EjecutarAsync(
        PersonaRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorPersona.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoPersona(ErrorPersona.DatosInvalidos, null, invalido);
        }

        var dni = peticion.Dni!.Trim();

        // FR-022b: el alta nunca reactiva. Si el DNI es de una persona dada de baja, se la manda a
        // reactivarla, que es un recurso propio y no un efecto lateral del alta ([004]).
        if (await repositorio.ObtenerPorDniAsync(dni, cancelacion) is { } existente)
        {
            return new ResultadoPersona(
                existente.Activa ? ErrorPersona.DniDuplicado : ErrorPersona.DniDePersonaDadaDeBaja,
                null,
                "dni");
        }

        var persona = new Persona
        {
            Nombre = peticion.Nombre!.Trim(),
            Apellido = peticion.Apellido!.Trim(),
            Dni = dni,
            Tipo = TipoIntegranteTexto.Interpretar(peticion.Tipo)!.Value,
            Telefono = peticion.Telefono!.Trim(),
            Email = peticion.Email!.Trim(),
            FechaNacimiento = peticion.FechaNacimiento!.Value,
            Activa = true,
        };

        await repositorio.AgregarAsync(persona, cancelacion);

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (Exception excepcion) when (EsDniDuplicado(excepcion))
        {
            // El índice único tiene la última palabra: entre la validación previa y el INSERT hay
            // una ventana en la que dos altas simultáneas creen las dos que el DNI está libre
            // (FR-027, research §3).
            return new ResultadoPersona(ErrorPersona.DniDuplicado, null, "dni");
        }

        return new ResultadoPersona(ErrorPersona.Ninguno, PersonaDto.Desde(persona));
    }

    /// <summary>
    /// La infraestructura marca la violación del índice único del DNI con esta excepción, para no
    /// filtrar tipos de EF Core hacia la capa de aplicación.
    /// </summary>
    private static bool EsDniDuplicado(Exception excepcion) => excepcion is DniDuplicadoException;
}

/// <summary>Violación del índice único de <c>Personas.Dni</c>, traducida por la infraestructura.</summary>
public class DniDuplicadoException(Exception interna)
    : Exception("El DNI ya está registrado en el padrón.", interna);

/// <summary>Edición de una persona (User Story 6).</summary>
public class ModificarPersona(IRepositorioPersonas repositorio)
{
    public async Task<ResultadoPersona> EjecutarAsync(
        int id,
        PersonaRequest peticion,
        CancellationToken cancelacion = default)
    {
        var persona = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (persona is null)
        {
            return new ResultadoPersona(ErrorPersona.NoEncontrada, null);
        }

        if (ValidadorPersona.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoPersona(ErrorPersona.DatosInvalidos, null, invalido);
        }

        var dni = peticion.Dni!.Trim();

        // FR-027: la comparación excluye a la propia persona, así que conservar su DNI no es
        // conflicto.
        if (await repositorio.ExisteDniAsync(dni, id, cancelacion))
        {
            return new ResultadoPersona(ErrorPersona.DniDuplicado, null, "dni");
        }

        persona.Nombre = peticion.Nombre!.Trim();
        persona.Apellido = peticion.Apellido!.Trim();
        persona.Dni = dni;
        persona.Tipo = TipoIntegranteTexto.Interpretar(peticion.Tipo)!.Value;
        persona.Telefono = peticion.Telefono!.Trim();
        persona.Email = peticion.Email!.Trim();
        persona.FechaNacimiento = peticion.FechaNacimiento!.Value;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoPersona(ErrorPersona.Ninguno, PersonaDto.Desde(persona));
    }
}

/// <summary>
/// Baja lógica de una persona (User Story 6).
///
/// Se rechaza si está vinculada a un usuario, <b>sin importar el estado de ese usuario</b> (FR-028).
/// Es el mismo criterio que sostiene FR-008: una persona sigue ocupada aunque el usuario que la
/// tiene esté inactivo, y la única forma de liberarla es desasociarla desde la edición de esa cuenta.
/// </summary>
public class DarDeBajaPersona(IRepositorioPersonas repositorio)
{
    public async Task<ResultadoPersona> EjecutarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var persona = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (persona is null)
        {
            return new ResultadoPersona(ErrorPersona.NoEncontrada, null);
        }

        var usuarioQueLaTiene = await repositorio.UsernameDelUsuarioVinculadoAsync(id, cancelacion);

        if (usuarioQueLaTiene is not null)
        {
            return new ResultadoPersona(ErrorPersona.Vinculada, null)
            {
                UsernameQueLaTiene = usuarioQueLaTiene,
            };
        }

        // Desde el Módulo 3 una persona también puede estar vinculada a un chofer. Darla de baja
        // dejaría un chofer activo apuntando a una persona inactiva, así que se rechaza igual.
        if (await repositorio.EsChoferAsync(id, cancelacion))
        {
            return new ResultadoPersona(ErrorPersona.EsChofer, null);
        }

        // FR-022: baja lógica. El registro no se borra nunca.
        persona.Activa = false;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoPersona(ErrorPersona.Ninguno, PersonaDto.Desde(persona));
    }
}

/// <summary>
/// Reactivación de una persona dada de baja (FR-022b).
///
/// Vuelve a ponerla activa con los datos que tenía; desde ese momento se ofrece otra vez para asociar
/// a un usuario (FR-023). No hay otra regla que comprobar: lo que la baja protege —no dejar a un
/// usuario o a un chofer apuntando a una persona inactiva— no puede romperse al reactivarla.
///
/// Pedirla sobre una persona activa se rechaza: la pantalla ofrece <i>Reactivar</i> sólo sobre una
/// dada de baja, así que significa que la vista quedó vieja.
/// </summary>
public class ReactivarPersona(IRepositorioPersonas repositorio)
{
    public async Task<ResultadoPersona> EjecutarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var persona = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        if (persona is null)
        {
            return new ResultadoPersona(ErrorPersona.NoEncontrada, null);
        }

        if (persona.Activa)
        {
            return new ResultadoPersona(ErrorPersona.YaEstaActiva, null);
        }

        persona.Activa = true;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoPersona(ErrorPersona.Ninguno, PersonaDto.Desde(persona));
    }
}
