using UCS.DebtorBatch.Api.DomainLike;

namespace UCS.DebtorBatch.Api.Application.Abstractions
{
    public interface IExcelParser
    {
        IAsyncEnumerable<DebtorRecord> ParseAsync(Stream fileStream, string fileName, CancellationToken ct);
    }
}
