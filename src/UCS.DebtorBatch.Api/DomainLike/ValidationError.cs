namespace UCS.DebtorBatch.Api.DomainLike
{
    public sealed record ValidationError(int RowIndex, string ExternalKey, string Message);
}
