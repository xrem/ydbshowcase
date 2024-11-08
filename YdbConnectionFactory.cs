using System.Data.Common;
using Ydb.Sdk.Ado;

namespace Ydb.Showcase;

public class YdbConnectionFactory
{
    private readonly string _connectionString;

    public YdbConnectionFactory(YdbConnectionStringBuilder builder)
    {
        _connectionString = builder.ConnectionString;
    }

    // Provide abstraction
    public DbConnection Create() => new YdbConnection(_connectionString);
}