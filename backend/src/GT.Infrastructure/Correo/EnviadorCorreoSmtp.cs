using GT.Application.Usuarios;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace GT.Infrastructure.Correo;

/// <summary>Configuración del servidor de correo saliente, leída de <c>Correo:*</c>.</summary>
public class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>Vacío significa que no hay SMTP configurado: en ese caso se usa el enviador que registra al log.</summary>
    public string? Host { get; set; }

    public int Puerto { get; set; } = 587;

    public string? Usuario { get; set; }

    public string? Password { get; set; }

    public string Remitente { get; set; } = "sistema@gtlogistica.com.ar";

    public bool HaySmtpConfigurado => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>
/// Envío por SMTP con MailKit (research §1).
///
/// MailKit y no <c>System.Net.Mail.SmtpClient</c> porque la propia documentación de .NET marca a esa
/// última como obsoleta para desarrollo nuevo y recomienda MailKit en su lugar.
///
/// Nunca registra el cuerpo del mensaje: ahí viaja la contraseña temporal en texto plano (FR-009,
/// SC-004).
/// </summary>
public class EnviadorCorreoSmtp(OpcionesCorreo opciones, ILogger<EnviadorCorreoSmtp> registro)
    : IEnviadorCorreo
{
    public async Task<bool> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken cancelacion = default)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(MailboxAddress.Parse(opciones.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(destinatario));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpo };

        try
        {
            using var clienteSmtp = new SmtpClient();

            await clienteSmtp.ConnectAsync(
                opciones.Host,
                opciones.Puerto,
                SecureSocketOptions.StartTlsWhenAvailable,
                cancelacion);

            if (!string.IsNullOrWhiteSpace(opciones.Usuario))
            {
                await clienteSmtp.AuthenticateAsync(opciones.Usuario, opciones.Password, cancelacion);
            }

            await clienteSmtp.SendAsync(mensaje, cancelacion);
            await clienteSmtp.DisconnectAsync(quit: true, cancelacion);

            return true;
        }
        catch (Exception excepcion)
        {
            // Un fallo de envío no revierte la operación que lo generó (FR-021): se informa y sigue.
            // Se registra el destinatario para poder diagnosticar, nunca el cuerpo.
            registro.LogWarning(
                excepcion,
                "No se pudo enviar el correo a {Destinatario}. La operación que lo generó quedó registrada igual.",
                destinatario);

            return false;
        }
    }
}
