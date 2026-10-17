using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GT.Application.Proveedores;
using GT.Domain.Choferes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Proveedores;

public class CargarFacturaProveedorTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData("100000000000000.01")] [InlineData("100000000000000.02")]
    public async Task MultipartRealDevuelveDetalleCompletoEImportesExactos(string neto)
    {
        using var cliente=app.CrearCliente();
        await EscenariosPermisosProveedor.IngresarAsync(cliente,app.EnLaBaseAsync,null,[GT.Domain.Usuarios.CodigosPermiso.ProveedoresFacturasCargar]);
        var id=await NormalAsync(neto.EndsWith("01")?800:801);
        using var form=Formulario(id," 0001-000002 ",neto,Pdf());
        var respuesta=await cliente.PostAsync("/api/proveedores/facturas",form);
        Assert.Equal(HttpStatusCode.Created,respuesta.StatusCode);
        using var documento=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var d=documento.RootElement;
        Assert.Equal(neto,d.GetProperty("neto").GetString());
        Assert.Equal("pendiente",d.GetProperty("estado").GetString());
        Assert.Equal("0001-000002",d.GetProperty("numero").GetString());
        Assert.Equal("Detalle",d.GetProperty("detalle").GetString());
        Assert.Equal("CAE",d.GetProperty("cae").GetString());
        Assert.Equal(JsonValueKind.Null,d.GetProperty("liquidacion").ValueKind);
        // Cargar no otorga consultar: el DTO del POST permite ver el resultado, pero no abrir el PDF.
        Assert.Equal(HttpStatusCode.Forbidden,(await cliente.GetAsync(d.GetProperty("archivo").GetProperty("url").GetString())).StatusCode);
        Assert.False(d.GetProperty("archivo").TryGetProperty("ruta",out _));
    }

    [Fact]
    public async Task NumeroRepetidoSimultaneoSoloDejaUnaFilaYMismoNumeroEnOtroProveedorEsValido()
    {
        using var cliente=await app.CrearClienteAutenticadoAsync();
        var id=await NormalAsync(802); var otro=await NormalAsync(803);
        var archivosAntes=ContarArchivos();
        using var uno=Formulario(id,"Repetida","100.00",Pdf()); using var dos=Formulario(id,"Repetida","100.00",Pdf());
        var respuestas=await Task.WhenAll(cliente.PostAsync("/api/proveedores/facturas",uno),cliente.PostAsync("/api/proveedores/facturas",dos));
        Assert.Single(respuestas,r=>r.StatusCode==HttpStatusCode.Created); Assert.Single(respuestas,r=>r.StatusCode==HttpStatusCode.BadRequest);
        Assert.Equal(archivosAntes+1,ContarArchivos());
        using var tercero=Formulario(otro,"Repetida","100.00");
        Assert.Equal(HttpStatusCode.Created,(await cliente.PostAsync("/api/proveedores/facturas",tercero)).StatusCode);
        await app.EnLaBaseAsync(async db=>Assert.Equal(1,await db.FacturasProveedor.CountAsync(f=>f.ProveedorId==id)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RegistroSeSerializaConEdicionOBajaDelProveedor(bool baja)
    {
        using var cliente=await app.CrearClienteAutenticadoAsync();var id=await NormalAsync(baja?810:811);
        using var form=Formulario(id,"Concurrente","100.00",Pdf());var antes=ContarArchivos();
        var registrar=cliente.PostAsync("/api/proveedores/facturas",form);
        var cambiar=baja?cliente.PostAsJsonAsync($"/api/proveedores/{id}/baja",new{version=1}):cliente.PutAsJsonAsync($"/api/proveedores/{id}",new ProveedorPeticion("proveedorNormal","Nombre concurrente",PadronProveedorTests.Cuit(811),Version:1));
        var respuestas=await Task.WhenAll(registrar,cambiar);Assert.Equal(HttpStatusCode.OK,respuestas[1].StatusCode);
        Assert.Contains(respuestas[0].StatusCode,new[]{HttpStatusCode.Created,HttpStatusCode.Conflict});
        var creada=respuestas[0].StatusCode==HttpStatusCode.Created;
        Assert.Equal(antes+(creada?1:0),ContarArchivos());
        await app.EnLaBaseAsync(async db=>Assert.Equal(creada?1:0,await db.FacturasProveedor.CountAsync(f=>f.ProveedorId==id)));
    }

    [Fact]
    public async Task BajaYEdicionConcurrentesNoDejanFacturaParcialYCongelanIdentidad()
    {
        using var cliente=await app.CrearClienteAutenticadoAsync(); var id=await NormalAsync(804);
        using var form=Formulario(id,"Snapshot","100.00");
        var respuesta=await cliente.PostAsync("/api/proveedores/facturas",form); Assert.Equal(HttpStatusCode.Created,respuesta.StatusCode);
        await app.EnLaBaseAsync(async db=>
        {
            var repo=new RepositorioProveedores(db); var p=(await repo.ObtenerAsync(id,default))!;
            await repo.GuardarAsync(id,new("proveedorNormal","Nombre nuevo",p.Cuit,Version:p.Version),default);
            Assert.NotEqual("Nombre nuevo",(await db.FacturasProveedor.SingleAsync(f=>f.ProveedorId==id)).ProveedorRazonSocial);
            p=(await repo.ObtenerAsync(id,default))!; await repo.CambiarEstadoAsync(id,p.Version,false,default);
        });
        using var baja=Formulario(id,"Tras baja","100.00",Pdf());
        Assert.Equal(HttpStatusCode.Conflict,(await cliente.PostAsync("/api/proveedores/facturas",baja)).StatusCode);
        await app.EnLaBaseAsync(async db=>Assert.Equal(1,await db.FacturasProveedor.CountAsync(f=>f.ProveedorId==id)));
    }

    [Theory]
    [InlineData("image/jpeg",10)] [InlineData("application/pdf",10485761)]
    public async Task ArchivoInvalidoNoDejaFila(string tipo,int largo)
    {
        using var cliente=await app.CrearClienteAutenticadoAsync(); var id=await NormalAsync(tipo=="image/jpeg"?805:806);
        var bytes=tipo=="image/jpeg"?new byte[]{0xff,0xd8,0xff,0xe0,0,0,0,0,0,0}:new byte[largo];
        using var form=Formulario(id,"Inválida","100.00",bytes);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/api/proveedores/facturas",form)).StatusCode);
        await app.EnLaBaseAsync(async db=>Assert.False(await db.FacturasProveedor.AnyAsync(f=>f.ProveedorId==id)));
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("image/jpeg")]
    public async Task CheckArchivoNoAceptaMetadataParcial(string? contenido)
    {
        var id=await NormalAsync(contenido is null?807:contenido.Length==0?808:809);
        await app.EnLaBaseAsync(async db=>
        {
            db.FacturasProveedor.Add(new() { ProveedorId=id,TipoProveedorAlRegistrar=GT.Domain.Proveedores.TipoProveedor.ProveedorNormal,
                ProveedorRazonSocial="Proveedor",ProveedorCuit=PadronProveedorTests.Cuit(807),Numero="Check",Fecha=new(2026,10,16),Detalle="Detalle",Neto=100m,Iva=21m,ImporteTotal=121m,Cae="CAE",FechaVencimientoCae=new(2026,10,16),ArchivoRuta="archivo",ArchivoNombre="a.pdf",ArchivoTipoContenido=contenido });
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            Assert.Contains("CK_FacturasProveedor_Archivo",error.InnerException!.Message);
        });
    }

    [Theory]
    [InlineData(255,true)] [InlineData(256,false)]
    public async Task MetadataDeNombreSeValidaSinTruncar(int largo,bool admitido)
    {
        using var cliente=await app.CrearClienteAutenticadoAsync();var id=await NormalAsync(admitido?812:813);
        using var form=Formulario(id,"Metadata","100.00");
        form.Add(new ByteArrayContent(Pdf()),"archivo",new string('a',largo-4)+".pdf");
        Assert.Equal(admitido?HttpStatusCode.Created:HttpStatusCode.BadRequest,(await cliente.PostAsync("/api/proveedores/facturas",form)).StatusCode);
        await app.EnLaBaseAsync(async db=>
        {
            var factura=await db.FacturasProveedor.AsNoTracking().SingleOrDefaultAsync(f=>f.ProveedorId==id);
            if(admitido) Assert.Equal(255,factura!.ArchivoNombre!.Length);else Assert.Null(factura);
        });
    }

    [Theory]
    [InlineData("Importes",814)] [InlineData("Estado",815)] [InlineData("Fechas",816)]
    [InlineData("Textos",817)] [InlineData("Liquidacion",818)]
    public async Task RestriccionesSqlRechazanUnaViolacionPorCaso(string restriccion,int cuit)
    {
        var id=await NormalAsync(cuit);
        await app.EnLaBaseAsync(async db=>
        {
            var factura=new GT.Domain.Proveedores.FacturaProveedor{ProveedorId=id,TipoProveedorAlRegistrar=GT.Domain.Proveedores.TipoProveedor.ProveedorNormal,
                ProveedorRazonSocial="Proveedor",ProveedorCuit=PadronProveedorTests.Cuit(cuit),Numero="Check",Fecha=new(2026,10,16),Detalle="Detalle",Neto=100,Iva=21,ImporteTotal=121,Cae="CAE",FechaVencimientoCae=new(2026,10,16)};
            switch(restriccion)
            {
                case "Importes":factura.Iva=20;factura.ImporteTotal=120;break;
                case "Estado":factura.Estado=(GT.Domain.Proveedores.EstadoFacturaProveedor)99;break;
                case "Fechas":factura.FechaVencimientoCae=factura.Fecha.AddDays(-1);break;
                case "Textos":factura.Detalle=" ";break;
                case "Liquidacion":factura.TipoProveedorAlRegistrar=GT.Domain.Proveedores.TipoProveedor.Fletero;break;
            }
            db.FacturasProveedor.Add(factura);
            var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            Assert.Contains("CK_FacturasProveedor_"+restriccion,error.InnerException!.Message);
        });
    }

    private async Task<int> NormalAsync(int cuit)
    {
        int id=0; await app.EnLaBaseAsync(async db=>id=(await new RepositorioProveedores(db).GuardarAsync(null,new("proveedorNormal","Proveedor US2",PadronProveedorTests.Cuit(cuit)),default)).Id); return id;
    }
    private int ContarArchivos()=>Directory.Exists(app.RutaDeArchivos)?Directory.EnumerateFiles(app.RutaDeArchivos,"*",SearchOption.AllDirectories).Count():0;
    internal static byte[] Pdf()=>Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF");
    internal static MultipartFormDataContent Formulario(int id,string numero,string neto,byte[]? bytes=null)
    {
        var form=new MultipartFormDataContent();
        foreach(var (k,v) in new Dictionary<string,string> { ["proveedorId"]=id.ToString(),["numero"]=numero,["fecha"]=FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"),["detalle"]="Detalle",["neto"]=neto,["cae"]="CAE",["fechaVencimientoCae"]=FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd") }) form.Add(new StringContent(v),k);
        if(bytes is not null) form.Add(new ByteArrayContent(bytes),"archivo","factura.pdf"); return form;
    }
}
