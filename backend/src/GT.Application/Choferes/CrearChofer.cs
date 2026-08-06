using GT.Application.Choferes.Transportistas;
using GT.Application.Usuarios.Personas;
using GT.Domain.Choferes;
using GT.Domain.Personas;

namespace GT.Application.Choferes;

/// <summary>
/// Alta de un chofer (FR-006, FR-007, FR-008, FR-011).
///
/// Si el DNI ya está en el padrón del Módulo 2, <b>reutiliza esa persona</b> en vez de duplicarla, y
/// no le toca los datos: el padrón es de aquel módulo y se edita desde ahí. Si esa persona ya es
/// chofer, se rechaza como duplicado (research §6).
/// </summary>
public class CrearChofer(
    IRepositorioChoferes choferes,
    IRepositorioTransportistas transportistas,
    IRepositorioPersonas personas)
{
    public async Task<ResultadoChofer> EjecutarAsync(
        ChoferRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorChofer.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoChofer(ErrorChofer.DatosInvalidos, Campo: invalido);
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

        if (await choferes.ExistePorCuilAsync(cuil, cancelacion))
        {
            return new ResultadoChofer(ErrorChofer.CuilDuplicado, Campo: "cuil");
        }

        var persona = await personas.ObtenerPorDniAsync(dni, cancelacion);
        var reutilizoPersona = persona is not null;

        if (persona is not null)
        {
            if (await choferes.ExistePorPersonaAsync(persona.Id, cancelacion))
            {
                return new ResultadoChofer(ErrorChofer.DniDuplicado, Campo: "dni");
            }
        }
        else
        {
            persona = new Persona
            {
                Nombre = peticion.Nombre!.Trim(),
                Apellido = peticion.Apellido!.Trim(),
                Dni = dni,
                Tipo = TipoIntegrante.Chofer,
                Telefono = peticion.Telefono!.Trim(),
                Email = peticion.Email!.Trim(),
                FechaNacimiento = fechaNacimiento,
                Activa = true
            };
        }

        var chofer = new Chofer
        {
            PersonaId = reutilizoPersona ? persona.Id : 0,
            TransportistaId = transportista.Id,
            Cuil = cuil,
            Activo = true
        };

        // Las validaciones de arriba cierran la ventana normal; los índices únicos cierran la
        // carrera entre dos altas simultáneas, que ninguna consulta previa puede evitar (FR-007).
        try
        {
            await choferes.CrearAsync(chofer, reutilizoPersona ? null : persona, cancelacion);
        }
        catch (CuilDuplicadoException)
        {
            return new ResultadoChofer(ErrorChofer.CuilDuplicado, Campo: "cuil");
        }
        catch (PersonaYaEsChoferException)
        {
            return new ResultadoChofer(ErrorChofer.DniDuplicado, Campo: "dni");
        }

        var choferCompleto = await choferes.ObtenerPorIdConRelacionesAsync(chofer.Id, cancelacion);

        var detalle = ChoferDetalle.Desde(choferCompleto!, FechaHoyArgentina.Hoy()) with
        {
            ReutilizoPersona = reutilizoPersona
        };

        return new ResultadoChofer(ErrorChofer.Ninguno, detalle, ReutilizoPersona: reutilizoPersona);
    }
}
