using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// La regla del estado vive en un solo lugar conceptual pero se ejecuta en <b>dos</b>: el dominio en
/// C# (<see cref="CalculadorEstadoVehiculo"/>) y la consulta en SQL (<c>RepositorioVehiculos</c>).
///
/// Este test las compara sobre el mismo dato, que es lo que evita que se separen sin que nadie se
/// entere. Es la convención [003] de <c>AGENTS.md</c> —"cuando una regla derivada se ejecuta en dos
/// lados, va un test que compara las dos"— y el riesgo que research §13 declara.
/// </summary>
public class EquivalenciaEstadoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_EstadoDelListado_CoincideConElDelDominio_SobreElMismoDato()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dominio contra SQL en flota");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la equivalencia");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de la equivalencia",
            diasAvisoVencimiento: 15);

        var vehiculos = new List<int>();

        // Los bordes que la regla distingue: lejano, dentro de la ventana, vence hoy, vencido, y una
        // unidad sin ningún papel.
        foreach (var dias in new[] { 400, 5, 0, -3 })
        {
            var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
            await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, dias);
            vehiculos.Add(vehiculo.Id);
        }

        var sinPapeles = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        vehiculos.Add(sinPapeles.Id);

        // Y una unidad con historial: el vigente de su tipo es el de vencimiento más lejano, así que
        // el vencido de abajo no tiene que contar en ninguna de las dos escrituras (FR-024).
        var conHistorial = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(conHistorial.Id, tipoDocumento.Id, -60);
        await app.CrearDocumentoVehiculoAsync(conHistorial.Id, tipoDocumento.Id, 300);
        vehiculos.Add(conHistorial.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        Assert.Equal(vehiculos.Count, pagina!.Total);

        var hoy = FechaHoyArgentina.Hoy();

        foreach (var fila in pagina.Items)
        {
            // El mismo dato, calculado por el dominio directamente sobre las filas de la base.
            var documentos = await app.ConAlcanceAsync(contexto => contexto.DocumentacionesVehiculo
                .Include(documento => documento.Tipo)
                .Where(documento => documento.VehiculoId == fila.Id)
                .AsNoTracking()
                .ToListAsync());

            var estadoDelDominio = CalculadorEstadoVehiculo.Calcular(documentos, hoy);
            var esperado = EnCamelCase(estadoDelDominio.ToString());

            Assert.Equal(esperado, fila.EstadoDocumentacion);
        }
    }

    /// <summary>
    /// La misma comparación para el estado operativo derivado, que también se resuelve en los dos
    /// lados: en la consulta para el listado y en el dominio para la ficha (FR-014).
    /// </summary>
    [Fact]
    public async Task El_EstadoOperativo_DelListadoYDeLaFicha_Coinciden()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Operativo en dos lados");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo operativo en dos lados");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro operativo en dos lados",
            diasAvisoVencimiento: 15);

        foreach (var (guardado, dias) in new[]
                 {
                     (VehiculoEstado.Disponible, 400),
                     (VehiculoEstado.Disponible, -3),
                     (VehiculoEstado.FueraDeServicio, 400),
                     (VehiculoEstado.Disponible, 5),
                 })
        {
            var vehiculo = await app.CrearVehiculoAsync(
                tipoVehiculo.Id,
                transportista.Id,
                estadoOperativo: guardado);

            await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, dias);
        }

        // Y una sin documentación, que el listado tiene que dar fuera de servicio igual.
        await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        foreach (var fila in pagina!.Items)
        {
            var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
                $"/api/flota/vehiculos/{fila.Id}");

            Assert.Equal(ficha!.Estado, fila.Estado);
            Assert.Equal(ficha.EstadoDocumentacion, fila.EstadoDocumentacion);
        }
    }

    private static string EnCamelCase(string nombre) => char.ToLowerInvariant(nombre[0]) + nombre[1..];
}
