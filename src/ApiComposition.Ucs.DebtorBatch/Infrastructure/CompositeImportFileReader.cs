using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class CompositeImportFileReader(
       ExcelImportFileReader excel,
       CsvImportFileReader csv) : IImportFileReader
    {
        public IAsyncEnumerable<(int RowNumber, DebtorRecord Record)> ReadAsync(
            Stream fileStream,
            string fileName,
            CancellationToken ct = default)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            return ext switch
            {
                ".xlsx" => excel.ReadAsync(fileStream, fileName, ct),
                ".csv" => csv.ReadAsync(fileStream, fileName, ct),
                _ => throw new NotSupportedException($"Unsupported file extension: {ext}")
            };
        }
    }
}
