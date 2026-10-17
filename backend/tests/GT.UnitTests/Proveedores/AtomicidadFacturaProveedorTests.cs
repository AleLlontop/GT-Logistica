using GT.Application.Proveedores;
using GT.Application.Choferes.Documentacion;
using GT.Domain.Proveedores;

namespace GT.UnitTests.Proveedores;

public class AtomicidadFacturaProveedorTests
{
    private static readonly DateOnly Hoy = new(2026,10,16);
    private static readonly DatosFacturaProveedor Datos = new(1,"001",Hoy,"Detalle",100m,"CAE",Hoy);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FalloPrevioAlCommitCompensaConTokenNoCanceladoAunqueBorrarFalle(bool fallaBorrado)
    {
        var almacen = new Almacen { FallaBorrado = fallaBorrado };
        var repo = new Repo { FallaRegistro = new ProveedorException("rechazo", "Rechazado") };
        await Assert.ThrowsAsync<ProveedorException>(() => Caso(repo, almacen).EjecutarAsync(Datos, Archivo(), Hoy,new CancellationToken(true)));
        Assert.Equal(1, almacen.Borrados); Assert.False(almacen.TokenBorrado.CanBeCanceled);
    }

    [Fact]
    public async Task EscrituraFallidaNoIntentaSqlYRelecturaFallidaNoBorraConfirmado()
    {
        var repo = new Repo(); var almacen = new Almacen { FallaEscritura = true };
        await Assert.ThrowsAsync<ArchivoNoGuardadoException>(() => Caso(repo, almacen).EjecutarAsync(Datos, Archivo(), Hoy));
        Assert.Equal(0,repo.Registros);
        repo.FallaLectura = true; almacen.FallaEscritura = false;
        await Assert.ThrowsAsync<IOException>(() => Caso(repo, almacen).EjecutarAsync(Datos, Archivo(), Hoy));
        Assert.Equal(0,almacen.Borrados);
    }

    [Theory]
    [InlineData(true,false,0)] [InlineData(false,false,1)] [InlineData(false,true,0)]
    public async Task CommitInciertoCompruebaReferenciaYConservaSiNoPuedeVerificar(bool existe, bool fallaVerificacion, int borrados)
    {
        var repo = new Repo { FallaRegistro = new ConfirmacionFacturaInciertaException(12,new IOException()), Existe = existe, FallaVerificacion = fallaVerificacion, FallaLectura = true };
        var almacen = new Almacen();
        await Assert.ThrowsAnyAsync<Exception>(() => Caso(repo, almacen).EjecutarAsync(Datos, Archivo(), Hoy));
        Assert.Equal(1,repo.Verificaciones); Assert.Equal(borrados,almacen.Borrados);
    }

    [Theory]
    [InlineData("image/jpeg",100,"pdf.pdf")]
    [InlineData("application/pdf",10485761,"grande.pdf")]
    [InlineData("application/pdf",10,"largo")]
    public async Task TipoTamanioYNombreSeValidanAntesDeEscribir(string tipo,long tamanio,string nombre)
    {
        var almacen=new Almacen(); var repo=new Repo();
        if(nombre=="largo") nombre=new string('a',256);
        var caso=new CargarFacturaProveedor(repo,new PrepararPdfFacturaProveedor(almacen,new Validador(tipo),new Registro()),almacen,new Registro());
        await Assert.ThrowsAsync<ProveedorException>(()=>caso.EjecutarAsync(Datos,new(nombre,tamanio,()=>new MemoryStream([1,2,3])),Hoy));
        Assert.Equal(0,almacen.Escrituras); Assert.Equal(0,repo.Registros);
    }

    private static CargarFacturaProveedor Caso(Repo repo,Almacen almacen) => new(repo,
        new PrepararPdfFacturaProveedor(almacen,new Validador("application/pdf"),new Registro()),almacen,new Registro());
    private sealed class Registro : IRegistroErroresFacturaProveedor { public void Error(Exception error,string mensaje) { } }
    private static ArchivoCargado Archivo() => new("factura.pdf",10,()=>new MemoryStream([1,2,3]));

    [Fact]
    public async Task RutaFueraDeCapacidadCompensaAntesDeSqlSinTruncar()
    {
        var almacen=new Almacen { Ruta=new string('a',401) }; var repo=new Repo();
        await Assert.ThrowsAsync<ProveedorException>(()=>Caso(repo,almacen).EjecutarAsync(Datos,Archivo(),Hoy));
        Assert.Equal(1,almacen.Borrados); Assert.Equal(0,repo.Registros);
        almacen.Ruta=new string('a',400);
        var resultado=await Caso(repo,almacen).EjecutarAsync(Datos,new(new string('a',255),10485760,()=>new MemoryStream([1,2,3])),Hoy);
        Assert.Equal(12,resultado.Id); Assert.Equal(1,repo.Registros); Assert.Equal(1,almacen.Borrados);
    }
    private sealed class Validador(string tipo) : IValidadorDeArchivo
    { public Task<ValidacionDeArchivo> ValidarAsync(Stream contenido,long tamanioEnBytes,CancellationToken cancelacion=default)=>Task.FromResult(new ValidacionDeArchivo(true,tipo)); }
    private sealed class Almacen : IAlmacenDeArchivos
    {
        public int Escrituras; public int Borrados; public bool FallaEscritura; public bool FallaBorrado; public CancellationToken TokenBorrado; public string Ruta="archivo.pdf";
        public Task<string> GuardarAsync(Stream contenido,CancellationToken cancelacion=default) { Escrituras++; if(FallaEscritura) throw new ArchivoNoGuardadoException("No guardado"); return Task.FromResult(Ruta); }
        public Task<Stream?> AbrirAsync(string rutaRelativa,CancellationToken cancelacion=default)=>Task.FromResult<Stream?>(null);
        public Task BorrarAsync(string rutaRelativa,CancellationToken cancelacion=default) { Borrados++; TokenBorrado=cancelacion; if(FallaBorrado) throw new IOException(); return Task.CompletedTask; }
    }
    private sealed class Repo : IRepositorioFacturasProveedor
    {
        public Task<GT.Application.Choferes.PaginaDe<FacturaProveedorResumen>> ConsultarAsync(FiltrosFacturaProveedor filtros,CancellationToken ct)=>Task.FromResult(GT.Application.Choferes.PaginaDe<FacturaProveedorResumen>.Vacia(filtros.Pagina));
        public Task<IReadOnlyList<LiquidacionDisponibleProveedor>> ConsultarLiquidacionesDisponiblesAsync(int proveedorId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<LiquidacionDisponibleProveedor>>([]);
        public Exception? FallaRegistro; public bool FallaLectura; public bool Existe; public bool FallaVerificacion; public int Registros; public int Verificaciones;
        public Task<int> RegistrarAsync(FacturaProveedor factura,CancellationToken ct) { Registros++; if(FallaRegistro is not null) throw FallaRegistro; return Task.FromResult(12); }
        public Task<DetalleFacturaProveedor?> ObtenerAsync(int id,CancellationToken ct) { if(FallaLectura) throw new IOException(); return Task.FromResult<DetalleFacturaProveedor?>(new(12,1,"Proveedor","30712345671","proveedorNormal","001",Hoy,"Detalle",100m,21m,121m,"CAE",Hoy,"pendiente",null)); }
        public Task<bool> EstaConfirmadaAsync(int id,string ruta,CancellationToken ct) { Verificaciones++; if(FallaVerificacion) throw new IOException(); return Task.FromResult(Existe); }
        public Task<PdfFacturaPreparado?> ObtenerPdfAsync(int id,CancellationToken ct)=>Task.FromResult<PdfFacturaPreparado?>(null);
    }
}
