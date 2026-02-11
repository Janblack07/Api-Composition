namespace UCS.DebtorBatch.Api.Contracts.Requests
{
    public class UploadDebtorsRequest
    {
        public IFormFile File { get; init; } = default!;
    }
}
