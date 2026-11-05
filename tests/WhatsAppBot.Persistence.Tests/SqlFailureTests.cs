using System.Data;
using Microsoft.Data.SqlClient;

namespace WhatsAppBot.Persistence.Tests;

[Collection(LocalDbCollection.Name)]
public sealed class SqlFailureTests(LocalDbFixture fixture)
{
    [Fact]
    public async Task StoredProcedureExecutor_CancellationDoesNotReturnSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Executor.QueryAsync(
            "dbo.sp_Conversation_List",
            [
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = fixture.TenantId },
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() },
                new SqlParameter("@Page", SqlDbType.Int) { Value = 1 },
                new SqlParameter("@PageSize", SqlDbType.Int) { Value = 1 }
            ],
            reader => reader.GetInt64(reader.GetOrdinal("TotalCount")),
            cancellation.Token));
    }
}
