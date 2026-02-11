using UCS.DebtorBatch.Api.Contracts.Shared;

namespace UCS.DebtorBatch.Api.Contracts.Responses
{
    public sealed record JobResponse(Guid JobId, ImportJobStatus Status, string Message);
}
