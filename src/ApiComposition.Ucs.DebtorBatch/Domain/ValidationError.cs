namespace ApiComposition.Ucs.DebtorBatch.Domain
{
    public sealed record ValidationError(
       int RowNumber,
       string Field,
       string Message
   );
}
