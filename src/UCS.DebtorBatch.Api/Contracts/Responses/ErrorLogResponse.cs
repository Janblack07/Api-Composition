namespace UCS.DebtorBatch.Api.Contracts.Responses
{
    public sealed record ErrorLogResponse(string DownloadUrl, DateTimeOffset ExpiresAt, int RecordCount);
}
