namespace UCS.DebtorBatch.Api.Contracts.Shared
{
    public sealed record ErrorResponse(ErrorBody Error);

    public sealed record ErrorBody(string Code, string Message, object? Details = null);
}
