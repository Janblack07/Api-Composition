using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IImportJobRepository
    {
        Task CreateAsync(ImportJob job, TimeSpan ttl, CancellationToken ct);
        Task<ImportJob?> GetAsync(Guid jobId, CancellationToken ct);
        Task UpdateAsync(ImportJob job, TimeSpan ttl, CancellationToken ct);
    }

}
