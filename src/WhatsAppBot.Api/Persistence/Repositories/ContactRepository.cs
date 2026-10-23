using System.Data;
using Microsoft.Data.SqlClient;
using WhatsAppBot.Api.Business.Abstractions;

namespace WhatsAppBot.Api.Persistence.Repositories;

public sealed class ContactRepository(StoredProcedureExecutor executor) : IContactRepository
{
    public async Task<long> UpsertWhatsAppAsync(
        Guid integrationId,
        Guid tenantId,
        string whatsappUserId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var contactId = await executor.QuerySingleAsync(
            "dbo.sp_Contact_UpsertWhatsApp",
            [
                new SqlParameter("@IntegrationId", SqlDbType.UniqueIdentifier) { Value = integrationId },
                new SqlParameter("@TenantId", SqlDbType.UniqueIdentifier) { Value = tenantId },
                new SqlParameter("@WhatsAppUserId", SqlDbType.NVarChar, 64) { Value = whatsappUserId },
                new SqlParameter("@DisplayName", SqlDbType.NVarChar, 256)
                    { Value = (object?)displayName ?? DBNull.Value }
            ],
            reader => reader.GetInt64(reader.GetOrdinal("ContactId")),
            cancellationToken);
        return contactId > 0
            ? contactId
            : throw new InvalidOperationException("The WhatsApp contact could not be persisted.");
    }
}
