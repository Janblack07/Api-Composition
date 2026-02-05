using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IErrorReportWriter
    {
        Task<string> WriteAsync(Guid jobId, IReadOnlyList<ValidationError> errors, CancellationToken ct = default);
    }
}
