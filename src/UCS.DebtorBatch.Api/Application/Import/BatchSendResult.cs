namespace UCS.DebtorBatch.Api.Application.Import
{
    public sealed class BatchSendResult
    {
        public int ProcessedDelta { get; init; }
        public int FailedDelta { get; init; }
    }
}
