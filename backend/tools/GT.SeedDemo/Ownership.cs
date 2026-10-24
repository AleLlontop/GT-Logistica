using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GT.SeedDemo;

// Metadata de SQL Server; no es una entidad ni sale por API/UI. Sólo contiene PK,
// versión y completitud. Se guarda en la MISMA transacción que cada INSERT.
public sealed class Ownership : SaveChangesInterceptor
{
    const string Property = "GT.SeedDemo.DEMO015";
    const int ChunkSize = 7000; // sql_variant de una extended property admite hasta 7500 bytes.
    static readonly HashSet<string> Shared = ["Roles", "Permisos", "RolPermisos", "EmpresaEmisora"];
    public sealed class Manifest
    {
        public int Version { get; set; } = Dataset.Version;
        public bool Complete { get; set; }
        public HashSet<string> Keys { get; set; } = [];
    }
    sealed record Header(int Format, int Chunks, string Hash);
    public Manifest? Current { get; private set; }
    public bool Recording { get; set; }
    List<EntityEntry> pending = [];
    IDbContextTransaction? ownTransaction;
    bool failAfterMetadata;

    public static string Key(EntityEntry row) => row.Metadata.GetTableName() + "|" + string.Join("|",
        row.Metadata.FindPrimaryKey()!.Properties.Select(p => Convert.ToString(row.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture)));

    public static byte[] Encode(Manifest manifest)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true))
            JsonSerializer.Serialize(gzip, manifest);
        return output.ToArray();
    }
    public static Manifest Decode(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<Manifest>(gzip) ?? throw new InvalidOperationException("Manifiesto vacío.");
    }
    public async Task Read(DbContext db)
    {
        var bytes = await ReadProperty(db, Property);
        if (bytes is null) { Current = null; return; }
        var header = JsonSerializer.Deserialize<Header>(bytes) ?? throw new InvalidOperationException("Metadata DEMO015 inválida.");
        if (header.Format != 1 || header.Chunks is < 1 or > 1000) throw new InvalidOperationException("Formato de metadata DEMO015 desconocido.");
        using var buffer = new MemoryStream();
        for (int i = 0; i < header.Chunks; i++)
            buffer.Write(await ReadProperty(db, Property + "." + i) ?? throw new InvalidOperationException("Metadata DEMO015 incompleta; no se infiere pertenencia por nombres."));
        var payload = buffer.ToArray();
        if (Convert.ToHexString(SHA256.HashData(payload)) != header.Hash) throw new InvalidOperationException("Metadata DEMO015 corrupta; no se permite reset ni generación.");
        Current = Decode(payload);
    }
    public async Task Start(DbContext db)
    {
        if (Current is not null) throw new InvalidOperationException("Ya existe metadata DEMO015.");
        Current = new();
        await using var tx = await db.Database.BeginTransactionAsync();
        await Write(db);
        await tx.CommitAsync();
        Recording = true;
    }
    public async Task Complete(DbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        Current!.Complete = true;
        await Write(db);
        await tx.CommitAsync();
        Recording = false;
    }
    public async Task Delete(DbContext db)
    {
        // Participa en la transacción del reset; nunca borra metadata ajena.
        var headerBytes = await ReadProperty(db, Property);
        if (headerBytes is not null)
        {
            var header = JsonSerializer.Deserialize<Header>(headerBytes)!;
            for (int i = 0; i < header.Chunks; i++)
                await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_dropextendedproperty @name={Property + "." + i}");
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_dropextendedproperty @name={Property}");
        }
    }
    public void ResetMemory() { Current = null; Recording = false; }

    async Task Write(DbContext db)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("La metadata exige una transacción activa.");
        var payload = Encode(Current!);
        int chunks = (payload.Length + ChunkSize - 1) / ChunkSize;
        var previous = await ReadProperty(db, Property);
        int oldChunks = previous is null ? 0 : JsonSerializer.Deserialize<Header>(previous)!.Chunks;
        for (int i = 0; i < chunks; i++)
            await WriteProperty(db, Property + "." + i, payload.Skip(i * ChunkSize).Take(ChunkSize).ToArray());
        for (int i = chunks; i < oldChunks; i++)
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_dropextendedproperty @name={Property + "." + i}");
        await WriteProperty(db, Property, JsonSerializer.SerializeToUtf8Bytes(new Header(1, chunks, Convert.ToHexString(SHA256.HashData(payload)))));
    }
    static async Task<byte[]?> ReadProperty(DbContext db, string name)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT CONVERT(varbinary(max),value) FROM sys.extended_properties WHERE class=0 AND major_id=0 AND minor_id=0 AND name=@name";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name; command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync() as byte[];
    }
    static Task WriteProperty(DbContext db, string name, byte[] bytes) => db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @payload varbinary(7500)=CONVERT(varbinary(7500),{bytes});
        IF EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND major_id=0 AND minor_id=0 AND name={name})
            EXEC sys.sp_updateextendedproperty @name={name}, @value=@payload;
        ELSE EXEC sys.sp_addextendedproperty @name={name}, @value=@payload;
        """);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken token = default)
    {
        if (!Recording) return result;
        var db = data.Context!;
        pending = db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && !Shared.Contains(e.Metadata.GetTableName()!)).ToList();
        if (pending.Count > 0 && db.Database.CurrentTransaction is null)
            ownTransaction = await db.Database.BeginTransactionAsync(token);
        return result;
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken token = default)
    {
        if (!Recording || pending.Count == 0) return result;
        try
        {
            foreach (var row in pending) Current!.Keys.Add(Key(row));
            await Write(data.Context!);
            if(failAfterMetadata)throw new InvalidOperationException("Fallo de prueba posterior a metadata.");
            if (ownTransaction is not null) await ownTransaction.CommitAsync(token);
            return result;
        }
        finally { await Release(); }
    }
    public override async Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken token = default) => await Release();
    public override async Task SaveChangesCanceledAsync(DbContextEventData data, CancellationToken token = default) => await Release();
    async Task Release()
    {
        pending = [];
        if (ownTransaction is not null) { await ownTransaction.DisposeAsync(); ownTransaction = null; }
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result)
    {
        if (Recording) throw new InvalidOperationException("El seeder exige SaveChangesAsync para registrar pertenencia atómica.");
        return result;
    }

    public async Task VerifySql(DbContext db)
    {
        var original=await ReadProperty(db,Property);
        var name="Banco de verificación interna "+Guid.NewGuid().ToString("N");
        // Transacción del coordinador: la metadata debe volver atrás junto con las filas.
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
            Current=new(); Recording=true;
            var row=new GT.Domain.Pagos.Banco {Nombre=name};
            db.Add(row); await db.SaveChangesAsync();
            await Read(db);
            if(!Current!.Keys.Contains("Bancos|"+row.Id))throw new InvalidOperationException("Metadata no participa de la transacción.");
            await tx.RollbackAsync();
        }
        Recording=false; db.ChangeTracker.Clear(); await Read(db);
        if(!Equal(original,await ReadProperty(db,Property)))throw new InvalidOperationException("Rollback no conservó metadata original.");
        // SaveChanges sin transacción exterior: fallo entre metadata y commit también revierte ambos.
        Current=new(); Recording=true; failAfterMetadata=true;
        try
        {
            db.Add(new GT.Domain.Pagos.Banco {Nombre=name});
            await db.SaveChangesAsync();
            throw new InvalidOperationException("No se inyectó el fallo esperado.");
        }
        catch(InvalidOperationException e) when(e.Message=="Fallo de prueba posterior a metadata.") { }
        finally { Recording=false; failAfterMetadata=false; db.ChangeTracker.Clear(); await Read(db); }
        if(!Equal(original,await ReadProperty(db,Property)) || await db.Set<GT.Domain.Pagos.Banco>().AnyAsync(b=>b.Nombre==name))
            throw new InvalidOperationException("Fallo atómico: quedaron filas o metadata tras rollback.");
        Console.WriteLine("Tests SQL propios: rollback del coordinador y fallo entre metadata/commit conservan filas y metadata originales.");
    }
    static bool Equal(byte[]? a,byte[]? b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
}
