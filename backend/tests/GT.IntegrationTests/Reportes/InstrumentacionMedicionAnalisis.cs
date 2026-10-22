using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public sealed record ComandoMedicion(string Sql, SqlParameter[] Parametros, IsolationLevel? Aislamiento)
{
    public string Clave => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Sql+string.Join('|',Parametros.Select(p=>p.ParameterName+"="+Convert.ToString(p.Value,CultureInfo.InvariantCulture))))))[..16];
}

public sealed class CapturaMedicionAnalisis : DbCommandInterceptor
{
    public bool Activa;
    public ConcurrentQueue<ComandoMedicion> Comandos { get; } = new();
    public void Iniciar() { Comandos.Clear();Activa=true; }
    private static bool Negocio(string sql)=>new[]{"[Viajes]","[Facturas]","[Liquidaciones]","[OrdenesDePago]","[MovimientosDeCaja]","[Vehiculos]","[Documentaciones]","[DocumentacionesVehiculo]"}.Any(sql.Contains);
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default)
    {
        if(Activa && Negocio(command.CommandText))
            Comandos.Enqueue(new(command.CommandText,command.Parameters.Cast<SqlParameter>().Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray(),eventData.Context?.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel));
        return ValueTask.FromResult(result);
    }
}

public sealed class AplicacionMedicionAnalisis : AplicacionAnalisisDePrueba
{
    public CapturaMedicionAnalisis Captura { get; }=new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s=>s.AddDbContext<GtDbContext>(o=>o.AddInterceptors(Captura)));
    }
}

internal sealed record PlanMedido(string Recurso,string Clave,long LecturasLogicas,string[] Mensajes,string Plan,string Sql);
internal static partial class LecturasYPlanesAnalisis
{
    public static async Task<PlanMedido> MedirAsync(string baseEfimera,string recurso,ComandoMedicion original,string carpeta,CancellationToken ct)
    {
        if(!baseEfimera.StartsWith("GtLogistica_Test_",StringComparison.Ordinal) || !original.Sql.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La medición admite sólo SELECT de la base efímera.");
        var mensajes=new ConcurrentQueue<string>();
        await using var conexion=new SqlConnection(ConfiguracionEntorno.CadenaDeConexion(baseEfimera));
        conexion.InfoMessage+=(_,e)=>mensajes.Enqueue(e.Message);
        await conexion.OpenAsync(ct);
        await using var transaccion=(SqlTransaction)await conexion.BeginTransactionAsync(IsolationLevel.Snapshot,ct);
        await using(var opciones=conexion.CreateCommand())
        { opciones.Transaction=transaccion;opciones.CommandText="SET STATISTICS IO ON; SET STATISTICS XML ON;";await opciones.ExecuteNonQueryAsync(ct); }
        string? xml=null;
        await using(var comando=conexion.CreateCommand())
        {
            comando.Transaction=transaccion;comando.CommandTimeout=60;comando.CommandText=original.Sql;
            comando.Parameters.AddRange(original.Parametros.Select(p=>(SqlParameter)((ICloneable)p).Clone()).ToArray());
            await using var lector=await comando.ExecuteReaderAsync(ct);
            do
            {
                while(await lector.ReadAsync(ct))
                    if(lector.FieldCount==1 && (string.Equals(lector.GetDataTypeName(0),"xml",StringComparison.OrdinalIgnoreCase) || lector.GetName(0).Contains("Showplan",StringComparison.OrdinalIgnoreCase)))xml=lector.GetString(0);
            } while(await lector.NextResultAsync(ct));
        }
        await using(var opciones=conexion.CreateCommand())
        { opciones.Transaction=transaccion;opciones.CommandText="SET STATISTICS XML OFF; SET STATISTICS IO OFF;";await opciones.ExecuteNonQueryAsync(ct); }
        await transaccion.CommitAsync(ct);
        var textos=mensajes.ToArray();
        var lecturas=textos.SelectMany(t=>Lecturas().Matches(t).Cast<Match>()).Sum(m=>long.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture));
        if(xml is null || textos.Length==0)throw new InvalidOperationException($"No se recuperaron el plan real y STATISTICS IO: XML={xml is not null}, mensajes={textos.Length}.");
        Directory.CreateDirectory(carpeta);
        var plan=Path.Combine(carpeta,original.Clave+".sqlplan");var sql=Path.Combine(carpeta,original.Clave+".sql");
        await File.WriteAllTextAsync(plan,xml,ct);await File.WriteAllTextAsync(sql,original.Sql,ct);
        return new(recurso,original.Clave,lecturas,textos,plan,sql);
    }
    [GeneratedRegex(@"(?:logical reads|lecturas lógicas)\s+(\d+)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]
    private static partial Regex Lecturas();
}
