using GT.Application.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.UnitTests.Autenticacion;

/// <summary>
/// Cubre FR-013 del Módulo 7: los dos paneles de vencimientos que existían desde los Módulos 3 y 4
/// se alcanzan desde la navegación.
///
/// Lo que importa verificar no es sólo que aparezcan, sino que **aparezcan bajo el permiso que la
/// pantalla ya exigía**: el rediseño no cambia quién puede ver qué (FR-002). Sin este test, agregar
/// una entrada con el permiso equivocado le abriría una pantalla a quien no corresponde y nadie se
/// enteraría hasta operarlo.
/// </summary>
public class CatalogoOpcionesMenuTests
{
    [Fact]
    public void Autorizadas_IncluyeVencimientosDeChoferes_CuandoTienePermisoDeChoferes()
    {
        var opciones = CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.ChoferesGestionar]).ToList();

        var vencimientos = Assert.Single(opciones, o => o.Codigo == "vencimientos-choferes");
        Assert.Equal("Vencimientos de choferes", vencimientos.Etiqueta);
        Assert.Equal("/choferes/vencimientos", vencimientos.Ruta);
    }

    [Fact]
    public void Autorizadas_IncluyeVencimientosDeFlota_CuandoTienePermisoDeFlota()
    {
        var opciones = CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.FlotaGestionar]).ToList();

        var vencimientos = Assert.Single(opciones, o => o.Codigo == "vencimientos-flota");
        Assert.Equal("Vencimientos de flota", vencimientos.Etiqueta);
        Assert.Equal("/flota/vencimientos", vencimientos.Ruta);
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
        // panel de vencimientos no: va con `flota.gestionar`, que también tiene Tráfico.
        var opciones = CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.FlotaTiposGestionar]).ToList();

        Assert.DoesNotContain(opciones, o => o.Codigo == "vencimientos-flota");
    }

    [Fact]
    public void Autorizadas_NoDevuelveNada_SinPermisos()
    {
        Assert.Empty(CatalogoOpcionesMenu.Autorizadas([]));
    }
}
