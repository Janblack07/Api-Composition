using UCS.DebtorBatch.Api.Contracts.Shared;

namespace UCS.DebtorBatch.Api.DomainLike
{
    public sealed class ImportJob
    {
        public Guid JobId { get; init; }
        public Guid TenantId { get; init; }
        public Guid DepartmentId { get; init; }
        public Guid UserId { get; init; }

        public ImportJobStatus Status { get; set; } = ImportJobStatus.QUEUED;
        public string? OriginalFileName { get; set; }
        public string? OriginalContentType { get; set; }

        public string FileUrl { get; set; } = default!;
        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int FailedRecords { get; set; }
        public int ProgressPercentage { get; set; }
        public string? ErrorFileUrl { get; set; }
        public string? FailureReason { get; set; }

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
    }
}
