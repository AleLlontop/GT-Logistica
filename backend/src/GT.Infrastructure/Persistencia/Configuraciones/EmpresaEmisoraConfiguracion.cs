using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class EmpresaEmisoraConfiguracion : IEntityTypeConfiguration<EmpresaEmisora>
{
    /// <summary>
    /// El <c>CHECK</c> que hace de la unicidad una garantía de la base y no una convención del código
    /// (research §12). Los tests lo nombran para verificar que rechaza la segunda fila.
    /// </summary>
    public const string CheckFilaUnica = "CK_EmpresaEmisora_FilaUnica";

    public void Configure(EntityTypeBuilder<EmpresaEmisora> tabla)
    {
        // FR-001: única para todo el sistema, se edita y nunca se crea una segunda ni se borra. Una
        // garantía escrita en la base cuesta una línea y no depende de que nadie escriba un `Add` de
        // más; la disciplina del código sí depende de eso (research §12).
        tabla.ToTable(
            "EmpresaEmisora",
            constructor => constructor.HasCheckConstraint(CheckFilaUnica, "[Id] = 1"));

        tabla.HasKey(empresa => empresa.Id);

        // Sin identidad: el `1` lo pone el código y el CHECK lo verifica. Con IDENTITY, la segunda
        // inserción pediría el 2 y el rechazo llegaría por el CHECK en vez de por la intención.
        tabla.Property(empresa => empresa.Id).ValueGeneratedNever();

        tabla.Property(empresa => empresa.RazonSocial).HasMaxLength(200).IsRequired();

        // Once dígitos ya normalizados: se guarda `30712345678`, nunca `30-71234567-8`.
        tabla.Property(empresa => empresa.Cuit).HasMaxLength(11).IsRequired();

        tabla.Property(empresa => empresa.Domicilio).HasMaxLength(200).IsRequired();
        tabla.Property(empresa => empresa.CondicionIva).HasMaxLength(100).IsRequired();

        tabla.Property(empresa => empresa.IngresosBrutos).HasMaxLength(50);
        tabla.Property(empresa => empresa.PuntoDeVenta).HasMaxLength(4);
        tabla.Property(empresa => empresa.Cbu).HasMaxLength(22);
        tabla.Property(empresa => empresa.Telefono).HasMaxLength(50);
        tabla.Property(empresa => empresa.Email).HasMaxLength(254);

        tabla.Property(empresa => empresa.LogoRuta).HasMaxLength(260);
        tabla.Property(empresa => empresa.LogoTipoContenido).HasMaxLength(100);
        tabla.Property(empresa => empresa.LogoNombreOriginal).HasMaxLength(255);

        // `Alicuota` no existe acá y tampoco en `Facturas`: se deriva del tipo de comprobante
        // (research §5). Se ignora por si alguna vez alguien agrega una propiedad calculada.
    }
}
