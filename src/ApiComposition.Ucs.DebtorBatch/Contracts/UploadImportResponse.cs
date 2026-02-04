using ApiComposition.Ucs.DebtorBatch.Domain;

namespace ApiComposition.Ucs.DebtorBatch.Contracts
{
    public sealed record UploadImportResponse(Guid JobId, ImportJobStatus Status);
}
