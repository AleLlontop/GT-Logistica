using GT.Application.Usuarios;
using Microsoft.Extensions.Logging;

namespace GT.Infrastructure.Correo;

/// <summary>
/// Enviador para desarrollo y CI: en vez de mandar el correo, lo registra en el log.
///
/// No es un atajo ni un doble de prueba olvidado. Es lo que permite que <c>podman compose up</c>
/// alcance para recorrer el <c>quickstart</c> completo sin un servidor SMTP real ni un contenedor
/// extra, y el Principio IV pide justamente que los criterios se comprueben operando la aplicación
/// (research §1).
///
/// Registra el destinatario y el asunto. <b>Nunca el cuerpo</b>: ahí viaja la contraseña temporal en
/// texto plano, y FR-009 y SC-004 exigen que no quede expuesta en ningún lado.
/// </summary>
public class EnviadorCorreoRegistrado(ILogger<EnviadorCorreoRegistrado> registro) : IEnviadorCorreo
{
    public Task<bool> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken cancelacion = default)
    {
        registro.LogInformation(
            "Correo no enviado (no hay SMTP configurado). Destinatario: {Destinatario}. Asunto: {Asunto}.",
            destinatario,
            asunto);

        return Task.FromResult(true);
    }
}
