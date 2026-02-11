namespace UCS.DebtorBatch.Api.Infrastructure.Core
{
    public sealed class CoreBatchImportRequest
    {
        public required string BatchId { get; init; }
        public required List<CoreBatchImportItem> Items { get; init; }
    }

    public sealed class CoreBatchImportItem
    {
        public required CoreDebtor Debtor { get; init; }
    }

    public sealed class CoreDebtor
    {
        public required string ExternalKey { get; init; }
        public required string Type { get; init; } // Person/Company
        public required string FullName { get; init; }
        public required string FirstName { get; init; }
        public string? LastName { get; init; }
        public required string DocumentNumber { get; init; }
        public string? PrimaryPhone { get; init; }
        public string? PrimaryEmail { get; init; }
        public required string Status { get; init; } // Active
        public List<CoreContact> Contacts { get; init; } = [];
        public List<CoreDebt> Debts { get; init; } = [];
    }

    public sealed class CoreContact
    {
        public required string Type { get; init; } // Email / Phone
        public required string Value { get; init; }
        public bool IsPrimary { get; init; }
    }

    public sealed class CoreDebt
    {
        public required string ExternalId { get; init; }
        public required decimal Amount { get; init; }
        public required string CurrencyCode { get; init; }
        public required DateTime DueDate { get; init; }
        public required string OriginSystem { get; init; }
        public required string Status { get; init; } // Pending
        public string? Description { get; init; }
    }

    public sealed class CoreBatchImportResponse
    {
        public bool Success { get; init; }
        public required CoreBatchImportResponseData Data { get; init; }
    }

    public sealed class CoreBatchImportResponseData
    {
        public int ProcessedCount { get; init; }
        public int FailedCount { get; init; }
        public List<CoreBatchError> Errors { get; init; } = [];
    }

    public sealed class CoreBatchError
    {
        public int RowIndex { get; init; }
        public string ExternalKey { get; init; } = "";
        public string Message { get; init; } = "";
    }
}
