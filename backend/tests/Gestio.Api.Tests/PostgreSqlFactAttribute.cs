using Xunit;

namespace Gestio.Api.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GESTIO_TEST_POSTGRES")))
            Skip = "Configura GESTIO_TEST_POSTGRES para ejecutar pruebas de concurrencia PostgreSQL.";
    }
}
