using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ydb.Showcase;

public class SchemaInitializer
{
    private const string MigrationSql =
        """
        DROP TABLE IF EXISTS ScheduleTask;
        CREATE TABLE IF NOT EXISTS ScheduleTask
        (
            Id                   Utf8      NOT NULL,
            Type                 Utf8,
            LastStartUtc         Timestamp,
            LastNonSuccessEndUtc Timestamp,
            LastSuccessUtc       Timestamp,
            Error                Utf8,
        
            INDEX idx_type GLOBAL SYNC ON (`Type`),
        
            PRIMARY KEY (Id)
        );
        """;

    private readonly YdbConnectionFactory _connectionFactory;
    private readonly ILogger<SchemaInitializer> _logger;

    public SchemaInitializer(YdbConnectionFactory factory, ILogger<SchemaInitializer> logger)
    {
        _connectionFactory = factory;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Creating ScheduleTask table");

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var ydbCommand = connection.CreateCommand();
        ydbCommand.CommandText = MigrationSql;
        await ydbCommand.ExecuteNonQueryAsync();

        _logger.LogInformation("Created ScheduleTask table");
    }
}