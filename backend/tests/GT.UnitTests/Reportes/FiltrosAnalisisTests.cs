using GT.Application.Reportes.Analisis;

namespace GT.UnitTests.Reportes;

public class FiltrosAnalisisTests
{
    private static PeriodoAnalisis Periodo => new(new(2026, 9, 15), new(2026, 10, 10));

    [Theory]
    [InlineData(0,20)] [InlineData(1,0)] [InlineData(1,201)] [InlineData(int.MaxValue,200)]
    public void PaginaInvalidaNoDesborda(int pagina,int tamanio) => Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Pagina(pagina,tamanio));

    [Theory]
    [InlineData(1,1)] [InlineData(1,20)] [InlineData(2,200)]
    public void PaginaValida(int pagina,int tamanio) => ValidacionAnalisis.Pagina(pagina,tamanio);

    [Fact]
    public void FiltrosValidosPreservanLasPerspectivas()
    {
        FiltroTemporalAnalisis[] filtros = [new FiltroViajesAnalisis(Periodo,Estado:"facturado"),new FiltroFacturacionAnalisis(Periodo,Estado:"vencida"),
            new FiltroCobrosAnalisis(Periodo),new FiltroLiquidacionesAnalisis(Periodo,Estado:"anulada"),new FiltroPagosAnalisis(Periodo,Vigencia:"revertidos"),
            new FiltroCajaAnalisis(Periodo),new FiltroFlotaAnalisis(Periodo,Activo:false,Orden:"cantidadAsc")];
        foreach(var filtro in filtros) ValidacionAnalisis.Filtro(filtro);
        Assert.False(new FiltroFlotaAnalisis(Periodo,Activo:false).Activo);
        Assert.Null(new FiltroFlotaAnalisis(Periodo).Activo);
    }

    [Fact]
    public void CodigosYReferenciasInvalidosTienenCampo()
    {
        var estado=Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Filtro(new FiltroViajesAnalisis(Periodo,Estado:"rendido; DROP")));
        Assert.Equal("estado",estado.Campo);
        var id=Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Filtro(new FiltroCobrosAnalisis(Periodo,ClienteId:0)));
        Assert.Equal("clienteId",id.Campo);
        Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Filtro(new FiltroPagosAnalisis(Periodo,Vigencia:"pendiente")));
        Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Filtro(new FiltroFlotaAnalisis(Periodo,Orden:"importeDesc")));
    }

    [Fact]
    public void DimensionDebeCorresponderALaPerspectiva()
    {
        ValidacionAnalisis.Grupo(new(new FiltroCobrosAnalisis(Periodo),"cliente"));
        Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Grupo(new(new FiltroCobrosAnalisis(Periodo),"estado")));
        Assert.Throws<ReglaAnalisisException>(()=>ValidacionAnalisis.Grupo(new(new FiltroFlotaAnalisis(Periodo),"cliente")));
    }
}
