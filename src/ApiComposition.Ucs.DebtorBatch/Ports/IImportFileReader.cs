using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IImportFileReader
    {
        IAsyncEnumerable<(int RowNumber, DebtorRecord Record)> ReadAsync(
            Stream fileStream,
            string fileName,
            CancellationToken ct = default);
    }
}
