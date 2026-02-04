using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Contracts
{
    public sealed record ImportJobResponse(
    Guid JobId,
    ImportJobStatus Status,
    DateTime CreatedAtUtc,
    int TotalRecords,
    int ProcessedRecords,
    int FailedRecords,
    string? FailureReason
);
}
