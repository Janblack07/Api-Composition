using UCS.DebtorBatch.Api.Infrastructure.Core;

namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IDebtorBatchClient
    {
        Task<CoreBatchImportResponse> SendBatchAsync(
            CoreBatchImportRequest request,
            string userJwt,
            Guid tenantId,
            Guid departmentId,
            string correlationId,
            CancellationToken ct);
    }
}
