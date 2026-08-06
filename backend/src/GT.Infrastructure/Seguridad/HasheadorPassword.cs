using GT.Domain.Usuarios;
using Microsoft.AspNetCore.Identity;

namespace GT.Infrastructure.Seguridad;

public interface IHasheadorPassword
{
    string Hashear(string password);

    bool Verificar(string hashAlmacenado, string passwordIngresada);
}

/// <summary>
/// Hasheo de contraseñas (FR-002).
///
/// Envuelve <see cref="PasswordHasher{TUser}"/>, la única pieza que este proyecto toma de ASP.NET
/// Core Identity. Cumple lo que exige FR-002 sin escribir criptografía propia: usa PBKDF2, guarda
/// el algoritmo y el salt dentro del propio hash, y permite endurecer los parámetros más adelante
/// sin invalidar las contraseñas ya almacenadas.
///
/// El Módulo 2 crea usuarios y genera contraseñas temporales, así que debe usar este mismo
/// hasheador.
/// </summary>
public class HasheadorPassword : IHasheadorPassword
{
    /// <summary>
    /// <see cref="PasswordHasher{TUser}"/> pide una instancia de usuario que no usa para nada: no
    /// mezcla ningún dato de la cuenta en el hash. Por eso los campos van vacíos.
    /// </summary>
    private static readonly Usuario UsuarioIrrelevante = new()
    {
        Username = string.Empty,
        UsernameNormalizado = string.Empty,
        Email = string.Empty,
        EmailNormalizado = string.Empty,
        PasswordHash = string.Empty,
    };

    private readonly PasswordHasher<Usuario> _hasher = new();

    public string Hashear(string password) => _hasher.HashPassword(UsuarioIrrelevante, password);

    public bool Verificar(string hashAlmacenado, string passwordIngresada)
    {
        var resultado = _hasher.VerifyHashedPassword(
            UsuarioIrrelevante,
            hashAlmacenado,
            passwordIngresada);

        return resultado is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
