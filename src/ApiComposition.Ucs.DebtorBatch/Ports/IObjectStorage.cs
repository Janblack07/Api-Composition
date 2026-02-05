namespace ApiComposition.Ucs.DebtorBatch.Ports
{
    public interface IObjectStorage
    {
        Task<string> PutAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
        Task<string> GetDownloadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken ct = default);
        Task<Stream> OpenReadAsync(string objectKey, CancellationToken ct = default);
    }
}
