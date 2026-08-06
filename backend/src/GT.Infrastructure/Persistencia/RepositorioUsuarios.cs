using GT.Application.Autenticacion;
using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioUsuarios(GtDbContext contexto) : IRepositorioUsuarios
{
    public Task<Usuario?> BuscarPorUsernameNormalizadoAsync(
        string usernameNormalizado,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Include(usuario => usuario.Roles)
            .ThenInclude(rol => rol.Permisos)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                usuario => usuario.UsernameNormalizado == usernameNormalizado,
                cancelacion);

    /// <summary>
    /// Actualiza sólo <c>UltimoAcceso</c> (FR-005). Es el único campo que este módulo escribe sobre
    /// un usuario; el resto es responsabilidad del Módulo 2.
    /// </summary>
    public Task RegistrarUltimoAccesoAsync(
        int idUsuario,
        DateTime momento,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Where(usuario => usuario.Id == idUsuario)
            .ExecuteUpdateAsync(
                actualizacion => actualizacion.SetProperty(u => u.UltimoAcceso, momento),
                cancelacion);
}

/// <summary>Adapta el hasheador de infraestructura a la interfaz que consume la capa de aplicación.</summary>
public class VerificadorPassword(Seguridad.IHasheadorPassword hasheador) : IVerificadorPassword
{
    /// <summary>
    /// Hash de una contraseña que nadie conoce, calculado una sola vez. Verificar contra él cuesta
    /// lo mismo que verificar contra el de un usuario real, que es justamente el punto.
    /// </summary>
    private static readonly Lazy<string> HashFicticio = new(() =>
        new Seguridad.HasheadorPassword().Hashear(Guid.NewGuid().ToString()));

    public bool Verificar(string hashAlmacenado, string passwordIngresada) =>
        hasheador.Verificar(hashAlmacenado, passwordIngresada);

    public void VerificarEnVano(string passwordIngresada) =>
        hasheador.Verificar(HashFicticio.Value, passwordIngresada);
}
