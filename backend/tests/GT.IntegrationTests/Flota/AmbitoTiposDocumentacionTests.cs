using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// El ámbito del catálogo compartido, que es el primero de los dos cambios que este módulo hace sobre
/// el Módulo 3 (FR-017, FR-017a, FR-017b, FR-017d).
/// </summary>
public class AmbitoTiposDocumentacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// FR-017a: cada módulo ofrece únicamente los tipos de su ámbito. El formulario de documento de
    /// vehículo consume <c>?ambito=vehiculo&amp;soloActivos=true</c> y no ve los de chofer.
    /// </summary>
    [Fact]
    public async Task El_ListadoFiltradoPorVehiculo_NoDevuelveLosDeChofer()
    {
        var deChofer = await app.CrearTipoDocumentacionAsync(nombre: "Licencia sólo de chofer");
        var deVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "VTV sólo de vehículo");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var deVehiculos = await cliente.GetFromJsonAsync<List<TipoLeido>>(
            "/api/tipos-documentacion?ambito=vehiculo");

        Assert.Contains(deVehiculos!, t => t.Id == deVehiculo.Id);
        Assert.DoesNotContain(deVehiculos!, t => t.Id == deChofer.Id);

        var deChoferes = await cliente.GetFromJsonAsync<List<TipoLeido>>(
            "/api/tipos-documentacion?ambito=chofer");

        Assert.Contains(deChoferes!, t => t.Id == deChofer.Id);
        Assert.DoesNotContain(deChoferes!, t => t.Id == deVehiculo.Id);
    }

    /// <summary>Sin el parámetro se devuelven los dos ámbitos: es la pantalla de mantenimiento.</summary>
    [Fact]
    public async Task Sin_FiltroDeAmbito_DevuelveLosDosAmbitos()
    {
        var deChofer = await app.CrearTipoDocumentacionAsync(nombre: "ART de los dos ámbitos");
        var deVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "RUTA de los dos ámbitos");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var todos = await cliente.GetFromJsonAsync<List<TipoLeido>>("/api/tipos-documentacion");

        Assert.Contains(todos!, t => t.Id == deChofer.Id);
        Assert.Contains(todos!, t => t.Id == deVehiculo.Id);
    }

    /// <summary>
    /// FR-017b: la baja de un tipo cuenta los documentos de <b>las dos</b> tablas. Cambia hacia el
    /// lado seguro: bloquea más bajas que antes, nunca menos.
    /// </summary>
    [Fact]
    public async Task La_Baja_CuentaLosDocumentosDeLosDosLados()
    {
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Seguro con documentos");

        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del conteo cruzado");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del conteo cruzado");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipo.Id, diasHastaVencimiento: 200);

        var cliente = await app.CrearClienteAutenticadoAsync();

        // El documento es de un vehículo y el tipo no tiene ninguno de chofer: antes del Módulo 4
        // esta baja habría procedido.
        var respuesta = await cliente.DeleteAsync($"/api/tipos-documentacion/{tipo.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_con_documentos", error!.Codigo);
        Assert.Contains("1 documento(s)", error.Mensaje);
    }

    /// <summary>Y el conteo del listado también suma las dos tablas (FR-017b).</summary>
    [Fact]
    public async Task El_ConteoDelCatalogo_SumaLasDosTablas()
    {
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Cédula verde contada");

        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del conteo del catálogo");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del conteo del catálogo");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipo.Id, diasHastaVencimiento: 100);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipo.Id, diasHastaVencimiento: 300);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var catalogo = await cliente.GetFromJsonAsync<List<TipoLeido>>("/api/tipos-documentacion");

        var fila = Assert.Single(catalogo!, t => t.Id == tipo.Id);
        Assert.Equal(2, fila.DocumentosAsociados);
    }

    /// <summary>
    /// FR-017d: el ámbito se corrige mientras el tipo no tenga ningún documento.
    /// </summary>
    [Fact]
    public async Task Acepta_CambiarElAmbito_SinDocumentos()
    {
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo que cambia de ámbito");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo.Id}",
            new { nombre = tipo.Nombre, diasAvisoVencimiento = 30, ambito = "vehiculo" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<TipoLeido>();
        Assert.Equal("vehiculo", modificado!.Ambito);
    }

    /// <summary>
    /// Con documentos asociados se rechaza informando cuántos son: si el cambio pasara, esos
    /// documentos quedarían colgando de un tipo que su propio módulo ya no ofrece (FR-017d).
    /// </summary>
    [Fact]
    public async Task Rechaza_CambiarElAmbito_ConDocumentos_YDiceCuantos()
    {
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Tipo con ámbito trabado");

        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del ámbito trabado");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del ámbito trabado");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipo.Id, diasHastaVencimiento: 150);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo.Id}",
            new { nombre = tipo.Nombre, diasAvisoVencimiento = 30, ambito = "chofer" });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("ambito_no_modificable", error!.Codigo);
        Assert.Equal("No se puede cambiar el ámbito: 1 documento(s) ya usan este tipo.", error.Mensaje);
        Assert.Equal(1, error.CantidadDocumentos);
    }

    /// <summary>
    /// El nombre y los días de aviso se modifican igual, tengan documentos o no: lo único que traba
    /// el cambio es el ámbito (FR-017d).
    /// </summary>
    [Fact]
    public async Task Acepta_CambiarNombreYDiasDeAviso_ConDocumentos_SiElAmbitoNoCambia()
    {
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Tipo que se renombra");

        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del renombre");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del renombre");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipo.Id, diasHastaVencimiento: 150);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo.Id}",
            new { nombre = $"{tipo.Nombre} corregido", diasAvisoVencimiento = 60, ambito = "vehiculo" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<TipoLeido>();
        Assert.Equal(60, modificado!.DiasAvisoVencimiento);
    }

    private record TipoLeido(
        int Id,
        string Nombre,
        int DiasAvisoVencimiento,
        string Ambito,
        bool Activo,
        int DocumentosAsociados);
}
