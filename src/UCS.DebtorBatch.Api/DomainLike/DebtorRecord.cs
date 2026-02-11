namespace UCS.DebtorBatch.Api.DomainLike
{
    public sealed class DebtorRecord
    {
        public int RowIndex { get; init; } // fila en el Excel
        public string ExternalKey { get; init; } = default!;
        public string FirstName { get; init; } = default!;
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
        public decimal Amount { get; init; }
        public DateOnly DueDate { get; init; }
        public string? Concept { get; init; }
    }
}
