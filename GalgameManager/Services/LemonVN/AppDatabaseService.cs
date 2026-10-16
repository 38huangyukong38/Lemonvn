using LemonVN.Contracts.Services;
using LiteDB;
using LemonVN.Infrastructure.Anime;

namespace LemonVN.Services;

/// <summary>Owns the LemonVN database lifetime. No game collections, migrations,
/// automatic repair, or schema reset are performed here.</summary>
public sealed class AppDatabaseService(IAppDataPathService paths) : IDisposable
{
    private LiteDatabase? _database;
    private readonly object _gate = new();
    public bool IsOpen => _database is not null;

    public void Initialize()
    {
        if (_database is not null) return;
        if (File.Exists(paths.DatabasePath))
        {
            using var existing = new LiteDatabase(new ConnectionString
                { Filename = paths.DatabasePath, ReadOnly = true });
            BsonDocument? identity = existing.GetCollection("app_info").FindById("identity");
            if (identity is null || identity["application"].AsString != AppIdentity.PackageName ||
                identity["formatVersion"].AsInt32 != 1)
                throw new InvalidDataException("Unsupported database identity or format. Original data was preserved.");
        }
        Directory.CreateDirectory(paths.RootPath);
        LiteDatabase database = new(paths.DatabasePath, AnimeBsonMapping.Create());
        database.UtcDate = true;
        try
        {
            if (!File.Exists(paths.DatabasePath)) throw new IOException("Database creation failed.");
            database.GetCollection("app_info").Upsert(new BsonDocument
            {
                ["_id"] = "identity",
                ["application"] = AppIdentity.PackageName,
                ["formatVersion"] = 1
            });
            _database = database;
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    // All repositories share this connection/lifetime. Transactions are synchronous
    // under the same gate; callers must not await or retain collections outside it.
    public T Read<T>(Func<LiteDatabase, T> operation)
    {
        lock (_gate)
            return operation(_database ?? throw new InvalidOperationException("The application database is not open."));
    }

    public T Write<T>(Func<LiteDatabase, T> operation)
    {
        lock (_gate)
        {
            LiteDatabase database = _database ?? throw new InvalidOperationException("The application database is not open.");
            if (!database.BeginTrans()) throw new InvalidOperationException("A database transaction is already active.");
            try
            {
                T result = operation(database);
                if (!database.Commit()) throw new IOException("The database transaction was not committed.");
                return result;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _database?.Dispose();
            _database = null;
        }
    }
}
