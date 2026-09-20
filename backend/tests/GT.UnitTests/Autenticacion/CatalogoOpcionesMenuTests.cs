using GT.Application.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.UnitTests.Autenticacion;

/// <summary>
/// Cubre FR-013 del Módulo 7: los dos paneles de vencimientos que existían desde los Módulos 3 y 4
/// se alcanzan desde la navegación.
///
/// Lo que importa verificar no es sólo que aparezcan, sino que **aparezcan bajo el permiso que la
/// pantalla exige**: una entrada con el permiso equivocado le abriría una pantalla a quien no
/// corresponde y nadie se enteraría hasta operarlo.
///
/// Los dos paneles pasaron a tener permiso de lectura propio —<c>choferes.vencimientos.consultar</c> y
/// <c>flota.vencimientos.consultar</c>— para que Gerencia los vea sin quedarse con el resto de cada
/// módulo. Tráfico y el administrador tienen los cuatro permisos, así que su menú no cambió.
/// </summary>
public class CatalogoOpcionesMenuTests
{
    [Fact]
    public void Autorizadas_IncluyeVencimientosDeChoferes_ConSuPermisoDeLectura()
    {
        var opciones = CatalogoOpcionesMenu
            .Autorizadas([CodigosPermiso.ChoferesVencimientosConsultar])
            .ToList();

        var vencimientos = Assert.Single(opciones, o => o.Codigo == "vencimientos-choferes");
        Assert.Equal("Vencimientos de choferes", vencimientos.Etiqueta);
        Assert.Equal("/choferes/vencimientos", vencimientos.Ruta);
    }

    [Fact]
    public void Autorizadas_IncluyeVencimientosDeFlota_ConSuPermisoDeLectura()
    {
        var opciones = CatalogoOpcionesMenu
            .Autorizadas([CodigosPermiso.FlotaVencimientosConsultar])
            .ToList();

        var vencimientos = Assert.Single(opciones, o => o.Codigo == "vencimientos-flota");
        Assert.Equal("Vencimientos de flota", vencimientos.Etiqueta);
        Assert.Equal("/flota/vencimientos", vencimientos.Ruta);
    }

    /// <summary>
    /// El permiso de lectura del panel abre el panel y **nada más**: quien lo tiene solo no ve
    /// *Choferes*, *Transportistas*, *Tipos de documentación* ni *Flota*. Es lo que le permite a
    /// Gerencia mirar los vencimientos sin quedarse con los dos módulos enteros.
    /// </summary>
    [Fact]
    public void Autorizadas_NoAbreElRestoDelModulo_ConSoloElPermisoDelPanel()
    {
        var opciones = CatalogoOpcionesMenu
            .Autorizadas([
                CodigosPermiso.ChoferesVencimientosConsultar,
                CodigosPermiso.FlotaVencimientosConsultar,
            ])
            .Select(o => o.Codigo)
            .ToList();

        Assert.Equal(["vencimientos-choferes", "vencimientos-flota"], opciones);
    }

    /// <summary>
    /// Y al revés: el permiso de gestión del módulo ya no arrastra la entrada del panel. Tráfico y el
    /// administrador siguen viéndola porque el sembrador les da los dos permisos, no porque uno
    /// implique al otro.
    /// </summary>
    [Fact]
    public void Autorizadas_NoIncluyeLosVencimientos_ConSoloLosPermisosDeGestion()
    {
        var opciones = CatalogoOpcionesMenu
            .Autorizadas([CodigosPermiso.ChoferesGestionar, CodigosPermiso.FlotaGestionar])
            .ToList();

        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-choferes");
        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-flota");
    }

    [Fact]
    public void Autorizadas_NoIncluyeLosVencimientos_SinElPermisoDeSuModulo()
    {
        // Quien sólo gestiona usuarios no ve ni uno ni otro.
        var opciones = CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.UsuariosGestionar]).ToList();

        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-choferes");
        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-flota");
    }

    [Fact]
    public void Autorizadas_NoDaVencimientosDeFlota_ConSoloElPermisoDeTiposDeVehiculo()
    {
        // El catálogo de tipos es sólo del administrador y va por un permiso propio (Módulo 4). El
        // panel de vencimientos no: va con `flota.vencimientos.consultar`, que también tiene Tráfico.
        var opciones = CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.FlotaTiposGestionar]).ToList();

        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-flota");
    }

    [Fact]
    public void Autorizadas_NoDevuelveNada_SinPermisos()
    {
        Assert.Empty(CatalogoOpcionesMenu.Autorizadas([]));
    }
}
