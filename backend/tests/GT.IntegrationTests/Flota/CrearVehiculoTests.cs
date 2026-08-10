using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Alta de una unidad: lo que se exige y lo que se rechaza (FR-005, FR-006, FR-008a, FR-014a,
/// US2 esc. 3 a 8).
/// </summary>
public class CrearVehiculoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Crea_UnaUnidad_ConSuTipoYSuTransportista()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tractor del alta feliz");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del alta feliz");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal(tipo.Id, creado!.Tipo.Id);
        Assert.Equal(transportista.Id, creado.Transportista.Id);
        Assert.True(creado.Activo);

        // Sin documentación cargada: sinDocumentacion, que no es lo mismo que estar en regla (FR-033).
        Assert.Equal("sinDocumentacion", creado.EstadoDocumentacion);
        Assert.Equal("fueraDeServicio", creado.Estado);
        Assert.Empty(creado.Documentos);
    }

    /// <summary>
    /// US2 esc. 8: <b>el alta sólo admite <c>fueraDeServicio</c></b>. Una unidad recién registrada no
    /// tiene documentos, así que su estado general es <c>sinDocumentacion</c> y <c>disponible</c>
    /// queda rechazado (FR-013, FR-014a).
    /// </summary>
    [Fact]
    public async Task Rechaza_Disponible_PorqueLaUnidadNuevaNoTieneDocumentacion()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del alta disponible");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del alta disponible");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id,
                estadoOperativo: "disponible"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("disponible_sin_documentacion", error!.Codigo);
        Assert.Equal(
            "No podés dejar la unidad disponible: todavía no tiene documentación cargada.",
            error.Mensaje);
        Assert.Equal("estadoOperativo", error.Campo);
    }

    /// <summary>US2 esc. 3 y 5: el tipo tiene que existir y estar activo (FR-005).</summary>
    [Fact]
    public async Task Rechaza_UnTipoInactivo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo dado de baja", activo: false);
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del tipo inactivo");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_vehiculo_inexistente", error!.Codigo);
        Assert.Equal("Elegí un tipo de vehículo activo.", error.Mensaje);
        Assert.Equal("tipoVehiculoId", error.Campo);
    }

    [Fact]
    public async Task Rechaza_UnTipoInexistente()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del tipo inexistente");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(DatosDePruebaFlota.PatenteUnica(), 999999, transportista.Id));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_vehiculo_inexistente", error!.Codigo);
    }

    /// <summary>US2 esc. 4: el transportista tiene que existir y estar activo (FR-008a).</summary>
    [Fact]
    public async Task Rechaza_UnTransportistaInactivo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del transportista inactivo");
        var transportista = await app.CrearTransportistaAsync(
            nombre: "Transportista dado de baja",
            activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
        Assert.Equal("Elegí un transportista activo.", error.Mensaje);
        Assert.Equal("transportistaId", error.Campo);
    }

    [Fact]
    public async Task Rechaza_UnTransportistaInexistente()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del transportista inexistente");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(DatosDePruebaFlota.PatenteUnica(), tipo.Id, 999999));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
    }

    /// <summary>FR-006: marca y modelo son obligatorios y se guardan con <c>Trim</c>.</summary>
    [Fact]
    public async Task Rechaza_MarcaVacia_YRecortaLosEspacios()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de marca y modelo");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de marca y modelo");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var sinMarca = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id,
                marca: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, sinMarca.StatusCode);

        var error = await sinMarca.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("marca", error.Campo);

        var conEspacios = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id,
                marca: "  Volvo  ",
                modelo: "  FH  "));

        var creado = await conEspacios.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal("Volvo", creado!.Marca);
        Assert.Equal("FH", creado.Modelo);
    }

    /// <summary>El estado operativo tiene que ser uno de los dos valores del contrato (FR-012).</summary>
    [Fact]
    public async Task Rechaza_UnEstadoOperativoDesconocido()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del estado desconocido");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del estado desconocido");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta(
                DatosDePruebaFlota.PatenteUnica(),
                tipo.Id,
                transportista.Id,
                estadoOperativo: "enTaller"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("estadoOperativo", error.Campo);
    }
}
