namespace ApiComposition.Ucs.DebtorBatch.Domain
{
    public sealed record DebtorRecord(
        string Identification,
        string FirstName,
        string LastName,
        string? Email,
        string? Phone,
        decimal DebtAmount,
        int DaysOverdue,
        int DebtorType 
    );
}
