using System.Security.Cryptography;
using GT.Application.Usuarios;

namespace GT.Infrastructure.Seguridad;

/// <summary>
/// Genera la contraseña temporal de un restablecimiento (FR-009, research §2).
///
/// Tres decisiones, cada una con su motivo:
///
/// - <b>12 caracteres</b>: superan con holgura el mínimo de 8 de FR-004 y compensan que la
///   contraseña viaje por correo en texto plano.
/// - <b><see cref="RandomNumberGenerator"/> y no <see cref="Random"/></b>: es una credencial, y
///   <c>Random</c> es predecible a partir de sus salidas.
/// - <b>Alfabeto sin caracteres ambiguos</b>: alguien va a tener que tipear esto leyéndolo de un
///   mail, así que no están ni <c>l</c>, ni <c>1</c>, ni <c>O</c>, ni <c>0</c>.
/// </summary>
public class GeneradorPasswordTemporal : IGeneradorPasswordTemporal
{
    public const int Largo = 12;

    /// <summary>Sin <c>l</c>, <c>1</c>, <c>O</c> ni <c>0</c>, que se confunden entre sí al leerlos.</summary>
    private const string Alfabeto = "ABCDEFGHIJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public string Generar() => RandomNumberGenerator.GetString(Alfabeto, Largo);
}
