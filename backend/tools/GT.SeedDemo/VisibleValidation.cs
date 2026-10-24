using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace GT.SeedDemo;

public sealed partial class Dataset
{
    internal static readonly string[] Forbidden = ["DEMO", "TEST", "PRUEBA", "FAKE", "SAMPLE"];
    internal static bool ContainsForbidden(string value) => Forbidden.Any(word => value.Contains(word, StringComparison.OrdinalIgnoreCase));
    internal static bool Visible(IEntityType entity, IProperty property) => property.ClrType == typeof(string)
        && entity.GetTableName() != "ResultadosOperacionPago" // Canon y replay internos, no se vuelcan como texto a la UI.
        && property.Name is not ("PasswordHash" or "DocumentoRuta" or "ArchivoRuta");
    static string Q(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

    // Se ejecuta sobre los valores persistidos en SQL, no sobre los textos de los generadores.
    // Devuelve sólo tabla/campo/PK. Nunca imprime contenido, hashes ni credenciales.
    public async Task CheckVisibleSql()
    {
        if(ownership.Current?.Version != Version)throw new InvalidOperationException("La validación visible exige metadata de la versión actual.");
        var rows=await Load();
        var own=Owned(rows);
        var scope=own.ToHashSet();
        // También se muestran configuración y catálogos compartidos referidos por las filas.
        bool changed;
        do
        {
            changed=false;
            foreach(var row in scope.ToArray())
            foreach(var fk in row.Metadata.GetForeignKeys())
            foreach(var parent in rows.Where(p=>Refers(row,fk,p)))changed |= scope.Add(parent);
        }while(changed);
        foreach(var issuer in rows.Where(r=>r.Metadata.GetTableName()=="EmpresaEmisora"))scope.Add(issuer);
        int fields=0, records=0;
        var failures=new List<string>();
        foreach(var group in scope.GroupBy(r=>r.Metadata.GetTableName()!))
        {
            var entity=group.First().Metadata;
            var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
            var properties=group.SelectMany(r=>r.Metadata.GetProperties().Where(p=>Visible(r.Metadata,p)))
                .DistinctBy(p=>p.Name).ToArray();
            if(properties.Length==0)continue;
            var pk=entity.FindPrimaryKey()!.Properties;
            // Las PK son enteras: int o bigint; ambas uniones implícitas tienen PK compuesta.
            if(pk.Any(p=>p.ClrType!=typeof(int) && p.ClrType!=typeof(long)))throw new InvalidOperationException("PK nueva no reconocida: ampliar la validación SQL antes de generar.");
            var keys=group.Select(r=>pk.Select(p=>Convert.ToInt64(r.Property(p.Name).CurrentValue)).ToArray()).DistinctBy(k=>string.Join("|",k)).ToArray();
            await using var command=db.Database.GetDbConnection().CreateCommand();
            command.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
            var keySchema=string.Join(",",pk.Select((p,i)=>$"[k{i}] bigint '$[{i}]'"));
            var keyFilter=string.Join(" AND ",pk.Select((p,i)=>$"j.[k{i}]=t.{Q(p.GetColumnName(store)!)}"));
            var values=string.Join(",",properties.Select(p=>$"(N'{p.Name}',CONVERT(nvarchar(max),t.{Q(p.GetColumnName(store)!)}))"));
            var displayedKey=string.Join(" + N'|' + ",pk.Select(p=>$"CONVERT(nvarchar(30),t.{Q(p.GetColumnName(store)!)})"));
            var table=(entity.GetSchema() is {} schema?Q(schema)+".":"")+Q(group.Key);
            command.CommandText=$"""
                SELECT v.campo, {displayedKey} AS pk
                FROM {table} t CROSS APPLY (VALUES {values}) v(campo,texto)
                WHERE EXISTS (SELECT 1 FROM OPENJSON(@keys) WITH ({keySchema}) j WHERE {keyFilter})
                AND EXISTS (SELECT 1 FROM OPENJSON(@words) w WHERE CHARINDEX(w.value,UPPER(v.texto COLLATE Latin1_General_100_CI_AS))>0)
                """;
            command.Parameters.Add(new SqlParameter("@keys",SqlDbType.NVarChar,-1){Value=JsonSerializer.Serialize(keys)});
            command.Parameters.Add(new SqlParameter("@words",SqlDbType.NVarChar,-1){Value=JsonSerializer.Serialize(Forbidden)});
            await using var reader=await command.ExecuteReaderAsync();
            while(await reader.ReadAsync())failures.Add($"{group.Key} | {reader.GetString(0)} | {reader.GetString(1)}");
            records+=keys.Length;fields+=properties.Length;
        }
        if(failures.Count>0)throw new InvalidOperationException("Texto visible no permitido (tabla | campo | ID):\n"+string.Join("\n",failures));
        Console.WriteLine($"Anti-DEMO SQL: 0 coincidencias visibles; {records} filas, {fields} columnas textuales. Excluidos sólo secretos, rutas físicas y replay técnico.");
    }
}
