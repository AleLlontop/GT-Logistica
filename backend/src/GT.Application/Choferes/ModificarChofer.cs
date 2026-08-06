using GT.Application.Choferes.Transportistas;
using GT.Application.Usuarios.Personas;
using GT.Domain.Choferes;

namespace GT.Application.Choferes;

/// <summary>
/// Modificación de un chofer y reasignación de transportista (FR-009, SC-009).
///
/// Los datos personales se guardan en su <c>Persona</c> del padrón del Módulo 2, que es la misma
/// fila que ve aquel módulo (FR-006): acá no hay una copia que pueda quedar desincronizada.
///
/// <b>Reasignar el transportista no toca la documentación ya cargada.</b> No hace falta hacer nada
/// para lograrlo —los documentos cuelgan del chofer, no del transportista— y por eso está escrito:
/// para que quede claro que es una propiedad del diseño y no un olvido.
/// </summary>
public class ModificarChofer(
    IRepositorioChoferes choferes,
    IRepositorioTransportistas transportistas,
    IRepositorioPersonas personas)
{
    public async Task<ResultadoChofer> EjecutarAsync(
        int id,
        ChoferRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorChofer.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoChofer(ErrorChofer.DatosInvalidos, Campo: invalido);
        }

        var chofer = await choferes.ObtenerParaModificarAsync(id, cancelacion);
        if (chofer is null)
        {
            return new ResultadoChofer(ErrorChofer.NoEncontrado);
        }

        var dni = NormalizadorDocumentoNumerico.Normalizar(peticion.Dni!);
        var cuil = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuil!);

        if (dni.Length == 0)
        {
            return new ResultadoChofer(ErrorChofer.DatosInvalidos, Campo: "dni");
        }

        if (!ValidadorCuit.EsValido(cuil))
        {
            return new ResultadoChofer(ErrorChofer.DatosInvalidos, Campo: "cuil");
        }

        var fechaNacimiento = DateOnly.Parse(peticion.FechaNacimiento!);
        if (!MayoriaDeEdad.EsMayor(fechaNacimiento, FechaHoyArgentina.Hoy()))
        {
            return new ResultadoChofer(ErrorChofer.MenorDeEdad, Campo: "fechaNacimiento");
        }

        var transportista = await transportistas.ObtenerPorIdAsync(peticion.TransportistaId!.Value, cancelacion);
        if (transportista is null || !transportista.Activo)
        {
            return new ResultadoChofer(ErrorChofer.TransportistaInexistente, Campo: "transportistaId");
        }

        // La unicidad excluye al propio registro: conservar el propio CUIL no es un duplicado.
        if (await choferes.ExistePorCuilAsync(cuil, idAExcluir: id, cancelacion))
        {
            return new ResultadoChofer(ErrorChofer.CuilDuplicado, Campo: "cuil");
        }

        if (await personas.ExisteDniAsync(dni, chofer.PersonaId, cancelacion))
        {
            return new ResultadoChofer(ErrorChofer.DniDuplicado, Campo: "dni");
        }

        var persona = chofer.Persona!;
        persona.Nombre = peticion.Nombre!.Trim();
        persona.Apellido = peticion.Apellido!.Trim();
        persona.Dni = dni;
        persona.Telefono = peticion.Telefono!.Trim();
        persona.Email = peticion.Email!.Trim();
        persona.FechaNacimiento = fechaNacimiento;

        chofer.Cuil = cuil;
        chofer.TransportistaId = transportista.Id;

        try
        {
            await choferes.GuardarCambiosAsync(cancelacion);
        }
        catch (CuilDuplicadoException)
        {
            return new ResultadoChofer(ErrorChofer.CuilDuplicado, Campo: "cuil");
        }
        catch (DniDuplicadoException)
        {
            return new ResultadoChofer(ErrorChofer.DniDuplicado, Campo: "dni");
        }

        var completo = await choferes.ObtenerPorIdConRelacionesAsync(id, cancelacion);

        return new ResultadoChofer(
            ErrorChofer.Ninguno,
            ChoferDetalle.Desde(completo!, FechaHoyArgentina.Hoy()));
    }
}
