using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> tabla)
    {
        tabla.ToTable("Clientes");
        tabla.HasKey(cliente => cliente.Id);

        tabla.Property(cliente => cliente.RazonSocial).HasMaxLength(100).IsRequired();
        tabla.Property(cliente => cliente.Cuit).HasMaxLength(11).IsRequired();
        tabla.Property(cliente => cliente.Telefono).HasMaxLength(30).IsRequired();
        tabla.Property(cliente => cliente.Email).HasMaxLength(254).IsRequired();
        tabla.Property(cliente => cliente.Direccion).HasMaxLength(200);
        tabla.Property(cliente => cliente.Activo).IsRequired();

        // Único y **sin filtro por Activo**, igual que la patente del Módulo 4: el CUIT de un cliente
        // dado de baja sigue ocupado, y registrarlo de nuevo se rechaza con `cuit_de_cliente_dado_de_baja`
        // —distinto de `cuit_duplicado`— para que quien opera sepa que tiene que darlo de alta de
        // nuevo en vez de buscarlo sin encontrarlo (FR-003, FR-007).
        tabla.HasIndex(cliente => cliente.Cuit).IsUnique();
    }
}
