using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using ClosedXML.Excel;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GT.Infrastructure.Persistencia;

namespace GT.IntegrationTests.Reportes;

/// <summary>Objetivos NFR-003/004 finales, con el dataset y planes reales del harness aprobado.</summary>
public class RendimientoFinalAnalisisTests
{
    [Fact]
    public async Task DatasetAnual_20Muestras_ConsultasAcotadas_Exportaciones1000ConReapertura()
    {
        const string carpeta = "/tmp/gt015-medicion/final";
        var app = new AplicacionMedicionAnalisis();
        string? nombre = null;
        try
        {
            await app.InitializeAsync();
            nombre = await app.ConAlcanceAsync(db => Task.FromResult(db.Database.GetDbConnection().Database));
            await ArnesMedicionAnalisis.EjecutarAsync(app, carpeta, repeticiones: 20);
            using (var tiempos = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(carpeta, "tiempos.json"))))
            {
                var resultados = tiempos.RootElement.GetProperty("Resultados");
                Assert.Equal(11, resultados.GetArrayLength());
                foreach (var recurso in resultados.EnumerateArray())
                {
                    var muestras = recurso.GetProperty("MuestrasMs").EnumerateArray().Select(x => x.GetInt64()).ToArray();
                    Assert.Equal(20, muestras.Length);
                    Assert.True(muestras.Count(ms => ms <= 5000) >= 19,
                        $"{recurso.GetProperty("Recurso").GetString()}: menos de 19/20 respuestas dentro de 5 segundos.");
                }
            }
            using var http = await app.CrearClienteAutenticadoAsync();
            // 200 pendientes vencidas por mes × enero–mayo = 1000 documentos reales del dataset.
            const string filtro = "desde=2026-01-01&hasta=2026-05-31&estado=vencida";
            using (var consulta = await http.GetAsync("/api/reportes/facturacion?" + filtro))
            {
                Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
                using var json = JsonDocument.Parse(await consulta.Content.ReadAsByteArrayAsync());
                Assert.Equal(1000, json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
            }
            var exportaciones = new List<object>();
            foreach (var formato in new[] { "pdf", "excel" })
            {
                var reloj = Stopwatch.StartNew();
                using var respuesta = await http.GetAsync($"/api/reportes/facturacion/exportacion?{filtro}&formato={formato}");
                var bytes = await respuesta.Content.ReadAsByteArrayAsync();
                reloj.Stop();
                Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
                Assert.True(reloj.ElapsedMilliseconds < 15000, $"{formato}: {reloj.ElapsedMilliseconds} ms excede 15 segundos.");
                if (formato == "pdf") Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
                else
                {
                    // El tiempo HTTP ya incluye la reapertura/verificación del armador de producción.
                    using var libro = new XLWorkbook(new MemoryStream(bytes));
                    Assert.Equal(1000, libro.Worksheet(1).RowsUsed().Count(f => f.Cell(2).DataType == XLDataType.DateTime));
                }
                await File.WriteAllBytesAsync(Path.Combine(carpeta, "1000-facturas." + (formato == "pdf" ? "pdf" : "xlsx")), bytes);
                exportaciones.Add(new { Formato = formato, Filas = 1000, Milisegundos = reloj.ElapsedMilliseconds, Bytes = bytes.Length, IncluyeReaperturaMonetariaEnXlsx = formato == "excel" });
            }
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
            var conexion = db.Database.GetDbConnection(); await conexion.OpenAsync();
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT snapshot_isolation_state_desc, is_read_committed_snapshot_on, CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) FROM sys.databases WHERE database_id=DB_ID()";
            await using var lector = await comando.ExecuteReaderAsync(); await lector.ReadAsync();
            var entorno = new { Framework = RuntimeInformation.FrameworkDescription, Sistema = RuntimeInformation.OSDescription, Procesadores = Environment.ProcessorCount,
                Snapshot = lector.GetString(0), Rcsi = lector.GetBoolean(1), SqlServer = lector.GetString(2), UnaSesionActiva = true, CalentamientoConsultas = 2, Repeticiones = 20,
                IndicesNuevos = false, Exportaciones = exportaciones };
            await File.WriteAllTextAsync(Path.Combine(carpeta, "aceptacion-nfr.json"), JsonSerializer.Serialize(entorno, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            await app.DisposeAsync();
            if (nombre is not null)
            {
                await using var conexion = new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra()); await conexion.OpenAsync();
                await using var comando = conexion.CreateCommand(); comando.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name=@nombre"; comando.Parameters.AddWithValue("@nombre", nombre);
                Assert.Equal(0, Convert.ToInt32(await comando.ExecuteScalarAsync()));
                Directory.CreateDirectory(carpeta); await File.WriteAllTextAsync(Path.Combine(carpeta, "limpieza.json"), "{\"BaseEfimeraEliminada\":true}");
            }
        }
    }
}
