using UCS.DebtorBatch.Api.Contracts.Shared;

namespace UCS.DebtorBatch.Api.Contracts.Responses
{
    public sealed record JobStatusDto(
    Guid JobId,
    ImportJobStatus Status,
    int ProgressPercentage,
    int TotalRecords,
    int ProcessedRecords,
    int FailedRecords,
    string? DownloadErrorLogUrl,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
}
