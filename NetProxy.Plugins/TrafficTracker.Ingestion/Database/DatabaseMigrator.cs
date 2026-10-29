using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.RegularExpressions;

namespace NetProxy.TrafficTracker.Ingestion.Database
{
    /// <summary>
    /// Puts the database in the state that the service expects. The SQL scripts that are embedded in the service (Database/Scripts, in the order of
    /// their names) are run once each, and what has been run is kept in traffic.SchemaVersion, so a newer version of the service brings an older
    /// database up to date by itself. Several services that start together do not run a script twice: they take turns.
    /// </summary>
    public sealed partial class DatabaseMigrator(IOptions<IngestionOptions> options, ILogger<DatabaseMigrator> logger)
    {
        private readonly IngestionOptions _options = options.Value;

        [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
        private static partial Regex BatchSeparator();

        public async Task MigrateAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                throw new InvalidOperationException("The connection string ConnectionStrings:Traffic is not set.");
            }

            if (_options.CreateDatabaseIfMissing)
            {
                await CreateDatabaseAsync(cancellationToken);
            }

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            //One at a time, whatever instance of the service this is.
            await ExecuteAsync(connection, null, """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = 'NetProxy.TrafficTracker.Migration', @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 60000;
                IF @result < 0 THROW 50000, 'The database is being migrated by another service, and it took too long.', 1;
                """, cancellationToken);

            try
            {
                await ExecuteAsync(connection, null, """
                    IF SCHEMA_ID('traffic') IS NULL EXEC('CREATE SCHEMA traffic');
                    IF OBJECT_ID('traffic.SchemaVersion') IS NULL
                        CREATE TABLE traffic.SchemaVersion (ScriptName nvarchar(200) NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY, AppliedUtc datetime2(3) NOT NULL);
                    """, cancellationToken);

                var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var command = new SqlCommand("SELECT ScriptName FROM traffic.SchemaVersion", connection))
                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        applied.Add(reader.GetString(0));
                    }
                }

                var assembly = typeof(DatabaseMigrator).Assembly;
                var scripts = assembly.GetManifestResourceNames()
                    .Where(o => o.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(o => o, StringComparer.Ordinal)
                    .ToList();

                foreach (var resource in scripts)
                {
                    var name = ScriptName(resource);
                    if (applied.Contains(name))
                    {
                        continue;
                    }

                    logger.LogInformation("Updating the database: running {Script}.", name);
                    await RunScriptAsync(connection, assembly, resource, name, cancellationToken);
                }
            }
            finally
            {
                await ExecuteAsync(connection, null, "EXEC sp_releaseapplock @Resource = 'NetProxy.TrafficTracker.Migration', @LockOwner = 'Session';", CancellationToken.None);
            }
        }

        /// <summary>
        /// "NetProxy.TrafficTracker.Ingestion.Database.Scripts.001_Schema.sql" is "001_Schema.sql".
        /// </summary>
        internal static string ScriptName(string resource)
        {
            const string marker = ".Scripts.";
            var index = resource.LastIndexOf(marker, StringComparison.Ordinal);
            return index < 0 ? resource : resource[(index + marker.Length)..];
        }

        private static async Task RunScriptAsync(SqlConnection connection, Assembly assembly, string resource, string name, CancellationToken cancellationToken)
        {
            string text;
            await using (var stream = assembly.GetManifestResourceStream(resource)!)
            using (var reader = new StreamReader(stream))
            {
                text = await reader.ReadToEndAsync(cancellationToken);
            }

            //A script is run in a transaction, so one that fails leaves nothing half done.
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var batch in BatchSeparator().Split(text).Where(o => string.IsNullOrWhiteSpace(o) == false))
                {
                    await ExecuteAsync(connection, transaction, batch, cancellationToken);
                }

                await using var record = new SqlCommand("INSERT traffic.SchemaVersion (ScriptName, AppliedUtc) VALUES (@name, SYSUTCDATETIME())", connection, transaction);
                record.Parameters.AddWithValue("@name", name);
                await record.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw new InvalidOperationException($"The database script {name} failed: {ex.GetBaseException().Message}", ex);
            }
        }

        private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
        {
            var builder = new SqlConnectionStringBuilder(_options.ConnectionString);
            var database = builder.InitialCatalog;

            if (string.IsNullOrWhiteSpace(database))
            {
                throw new InvalidOperationException("The connection string needs a Database to make.");
            }

            builder.InitialCatalog = "master";
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand("""
                DECLARE @created bit = 0;
                IF DB_ID(@name) IS NULL
                BEGIN
                    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name);
                    EXEC (@sql);
                    SET @created = 1;
                END
                SELECT @created;
                """, connection);
            command.Parameters.AddWithValue("@name", database);

            if ((bool)(await command.ExecuteScalarAsync(cancellationToken))!)
            {
                logger.LogInformation("Made the database {Database}.", database);
            }
        }

        private static async Task ExecuteAsync(SqlConnection connection, SqlTransaction? transaction, string sql, CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
