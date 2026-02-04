namespace ApiComposition.Ucs.DebtorBatch.Domain
{
    public sealed class ImportJob
    {
        public Guid JobId { get; init; }
        public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

        public Guid TenantId { get; init; }
        public Guid? DepartmentId { get; init; }
        public string UserId { get; init; } = default!;

        public ImportJobStatus Status { get; set; } = ImportJobStatus.Queued;

        public int TotalRecords { get; set; }
        public int ProcessedRecords { get; set; }
        public int FailedRecords { get; set; }

        public string? FailureReason { get; set; }

        public string? SourceObjectKey { get; set; }
        public string? ErrorsReportObjectKey { get; set; }
    }
}
