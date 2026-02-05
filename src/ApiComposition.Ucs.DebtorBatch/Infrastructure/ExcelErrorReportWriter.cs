using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using MiniExcelLibs;

namespace ApiComposition.Ucs.DebtorBatch.Infrastructure
{
    public sealed class ExcelErrorReportWriter(IObjectStorage storage) : IErrorReportWriter
    {
        public async Task<string> WriteAsync(Guid jobId, IReadOnlyList<ValidationError> errors, CancellationToken ct = default)
        {
            // dataset para excel
            var rows = errors.Select(e => new
            {
                RowNumber = e.RowNumber,
                Field = e.Field,
                Message = e.Message
            }).ToList();

            // archivo temporal
            var tmp = Path.Combine(Path.GetTempPath(), $"import-errors-{jobId:N}.xlsx");
            MiniExcel.SaveAs(tmp, rows);

            await using var fs = File.OpenRead(tmp);

            // lo guardamos en tu storage (local disk / s3 futuro)
            var objectKey = await storage.PutAsync(
                fs,
                fileName: $"errors-{jobId:N}.xlsx",
                contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ct: ct);

            try { File.Delete(tmp); } catch { /* ignore */ }

            return objectKey;
        }
    }
}
