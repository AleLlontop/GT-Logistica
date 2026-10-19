using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace GT.UnitTests.Pagos;

public class ModeloPagosTests
{
    [Fact]
    public void Modelo_ConstruyeTphYReferenciaDeHistorialAlChequeSinConectarSql()
    {
        using var contexto = new GtDbContext(new DbContextOptionsBuilder<GtDbContext>()
            .UseSqlServer("Server=127.0.0.1;Database=SoloModelo;Integrated Security=true;TrustServerCertificate=true")
            .Options);
        var modelo = contexto.Model;
        var medio = modelo.FindEntityType(typeof(MedioPagoProveedor))!;
        Assert.Equal("MediosPagoProveedor", medio.GetTableName());
        Assert.Equal("Tipo", medio.FindDiscriminatorProperty()!.Name);
        Assert.Equal(3, medio.GetDerivedTypes().Count());
        var historial = modelo.FindEntityType(typeof(CambioEstadoCheque))!;
        var referencia = Assert.Single(historial.GetForeignKeys(), f => f.PrincipalEntityType == medio);
        Assert.Equal(new[] { "ChequeId", "Tipo" }, referencia.Properties.Select(p => p.Name));
        var vinculo = modelo.FindEntityType(typeof(OrdenPagoProveedorFactura))!;
        Assert.Contains(vinculo.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[Vigente]=1");
        var resultado = modelo.FindEntityType(typeof(ResultadoOperacionPago))!;
        Assert.Contains(resultado.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Operacion", "Clave" }));
    }
}
