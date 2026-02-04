namespace ApiComposition.Ucs.DebtorBatch.Contracts
{

    public sealed class UploadImportRequest
    {
        public IFormFile File { get; set; } = default!;
    }
}
