using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IImportJobStore
    {
        Task<ImportJob?> GetAsync(Guid jobId, CancellationToken ct = default);
        Task SetAsync(ImportJob job, TimeSpan ttl, CancellationToken ct = default);
        Task UpdateAsync(Guid jobId, Action<ImportJob> mutate, CancellationToken ct = default);
    }
}
