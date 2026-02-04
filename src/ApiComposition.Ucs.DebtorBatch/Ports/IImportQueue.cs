namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IImportQueue
    {
        Task EnqueueAsync(Guid jobId, CancellationToken ct = default);
        ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
    }
}
