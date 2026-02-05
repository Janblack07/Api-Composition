using ApiComposition.Ucs.DebtorBatch.Contracts;
using ApiComposition.Ucs.DebtorBatch.Domain;
using ApiComposition.Ucs.DebtorBatch.Ports;
using ApiComposition.Ucs.DebtorBatch.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace ApiComposition.Ucs.DebtorBatch.Controllers
{
    [ApiController]
    [Route("import")]
    [Authorize]
    public sealed class ImportController : ControllerBase
    {
        private readonly IImportJobStore _store;
        private readonly IObjectStorage _storage;
        private readonly IImportQueue _queue;
        private readonly ITenantContextAccessor _tenantCtx;
        private readonly long _maxFileSize;
        private readonly TimeSpan _jobTtl;

        public ImportController(
            IImportJobStore store,
            IObjectStorage storage,
            IImportQueue queue,
            ITenantContextAccessor tenantCtx,
            IConfiguration cfg)
        {
            _store = store;
            _storage = storage;
            _queue = queue;
            _tenantCtx = tenantCtx;

            _maxFileSize = cfg.GetValue<long>("Upload:MaxFileSizeBytes", 10 * 1024 * 1024);
            _jobTtl = TimeSpan.FromHours(cfg.GetValue<int>("Upload:JobTtlHours", 24));
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(UploadImportResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
        public async Task<IActionResult> Upload([FromForm] UploadImportRequest request, CancellationToken ct)
        {
            var file = request.File;

            if (file is null || file.Length == 0)
                return BadRequest("File is required.");

            if (file.Length > _maxFileSize)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, $"File exceeds max size {_maxFileSize} bytes.");

            var ctx = _tenantCtx.GetOrThrow();
            var jobId = Guid.NewGuid();

            var job = new ImportJob
            {
                JobId = jobId,
                TenantId = ctx.TenantId,
                DepartmentId = ctx.DepartmentId,
                UserId = ctx.UserId,
                Status = ImportJobStatus.Uploading
            };

            await _store.SetAsync(job, _jobTtl, ct);

            await using var stream = file.OpenReadStream();
            var objectKey = await _storage.PutAsync(
                stream,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                ct);

            await _store.UpdateAsync(jobId, j =>
            {
                j.SourceObjectKey = objectKey;
                j.Status = ImportJobStatus.Queued;
            }, ct);

            await _queue.EnqueueAsync(jobId, ct);

            return Accepted(new UploadImportResponse(jobId, ImportJobStatus.Queued));
        }

        [HttpGet("jobs/{jobId:guid}")]
        [ProducesResponseType(typeof(ImportJobResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetJob([FromRoute] Guid jobId, CancellationToken ct)
        {
            var ctx = _tenantCtx.GetOrThrow();
            var job = await _store.GetAsync(jobId, ct);

            if (job is null || job.TenantId != ctx.TenantId || job.UserId != ctx.UserId)
                return NotFound();

            return Ok(new ImportJobResponse(
                job.JobId,
                job.Status,
                job.CreatedAtUtc,
                job.UpdatedAtUtc,
                job.TotalRecords,
                job.ProcessedRecords,
                job.FailedRecords,
                job.ProgressPercentage,
                job.FailureReason
            ));
        }

        [HttpGet("jobs/{jobId:guid}/errors")]
        [ProducesResponseType(typeof(ErrorsDownloadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetErrors([FromRoute] Guid jobId, CancellationToken ct)
        {
            var ctx = _tenantCtx.GetOrThrow();
            var job = await _store.GetAsync(jobId, ct);

            if (job is null || job.TenantId != ctx.TenantId || job.UserId != ctx.UserId)
                return NotFound();

            if (string.IsNullOrWhiteSpace(job.ErrorsReportObjectKey))
                return NotFound();

            var ttl = TimeSpan.FromMinutes(15);
            var url = await _storage.GetDownloadUrlAsync(job.ErrorsReportObjectKey, ttl, ct);

            return Ok(new ErrorsDownloadResponse(url, DateTime.UtcNow.Add(ttl)));
        }
        [HttpGet("dev-download/{objectKey}")]
        [AllowAnonymous] // o [Authorize] si quieres proteger
        public async Task<IActionResult> DevDownload([FromRoute] string objectKey, CancellationToken ct)
        {
            await using var stream = await _storage.OpenReadAsync(objectKey, ct);

            // ContentType por extensión (mejor UX para excel)
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(objectKey, out var contentType))
                contentType = "application/octet-stream";

            return File(stream, contentType, fileDownloadName: objectKey);
        }
    }
}
