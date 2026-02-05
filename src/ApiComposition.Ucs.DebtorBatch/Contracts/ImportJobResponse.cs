using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Contracts
{
    public sealed record ImportJobResponse(
       Guid JobId,
       ImportJobStatus Status,
       DateTime CreatedAtUtc,
       DateTime UpdatedAtUtc,
       int TotalRecords,
       int ProcessedRecords,
       int FailedRecords,
       int ProgressPercentage,
       string? FailureReason
   );
}
