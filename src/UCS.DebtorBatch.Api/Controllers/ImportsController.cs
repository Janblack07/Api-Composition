// File: src/UCS.DebtorBatch.Api/Controllers/ImportsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Import;
using UCS.DebtorBatch.Api.Contracts.Requests;
using UCS.DebtorBatch.Api.Contracts.Responses;
using UCS.DebtorBatch.Api.Contracts.Shared;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Infrastructure.Auth;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Controllers;

[ApiController]
[Route("imports")]
public sealed class ImportsController(
    IImportJobRepository repo,
    ImportJobTracker tracker,
    IFileStorage storage,
    ImportJobDispatcher dispatcher,
    IOptions<ImportOptions> opt)
    : ControllerBase
{
    private readonly ImportOptions _o = opt.Value;

    // =========================================================
    // POST /imports/debtors
    // =========================================================
    [HttpPost("debtors")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadDebtorFile(
        [FromForm] UploadDebtorsRequest request,
        [FromHeader(Name = "X-Correlation-ID")] string? correlationId,
        CancellationToken ct)
    {
        if (!User.HasPermission("debtor:batch:create"))
        {
            return StatusCode(403, new ErrorResponse(
                new ErrorBody("INSUFFICIENT_PERMISSIONS", "User does not have permission to perform this action",
                    new { requiredPermission = "debtor:batch:create" })));
        }

        var file = request.File;
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponse(
                new ErrorBody("INVALID_FILE_FORMAT", "File is required")));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".xlsx" and not ".csv")
        {
            return BadRequest(new ErrorResponse(
                new ErrorBody("INVALID_FILE_FORMAT", "The uploaded file is not a valid Excel or CSV file",
                    new { allowedExtensions = new[] { ".xlsx", ".csv" }, receivedExtension = ext })));
        }

        var maxBytes = _o.MaxFileSizeMB * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            return StatusCode(413, new ErrorResponse(
                new ErrorBody("FILE_TOO_LARGE", $"File size exceeds maximum allowed size of {_o.MaxFileSizeMB} MB",
                    new
                    {
                        maxSizeMB = _o.MaxFileSizeMB,
                        receivedSizeMB = Math.Round(file.Length / 1024d / 1024d, 2)
                    })));
        }

        correlationId ??= Guid.NewGuid().ToString();

        var tenantId = User.GetTenantId();
        var departmentId = User.GetDepartmentId();
        var userId = User.GetUserId();

        var jobId = Guid.NewGuid();

        await using var stream = file.OpenReadStream();
        var fileUrl = await storage.SaveAsync(stream, file.FileName, ct);

        var job = new ImportJob
        {
            JobId = jobId,
            TenantId = tenantId,
            DepartmentId = departmentId,
            UserId = userId,
            Status = ImportJobStatus.QUEUED,

            FileUrl = fileUrl,

            OriginalFileName = file.FileName,
            OriginalContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,

            TotalRecords = 0,
            ProcessedRecords = 0,
            FailedRecords = 0,
            ProgressPercentage = 0
        };

        await tracker.CreateAsync(job, ct);

        var auth = Request.Headers.Authorization.ToString();
        var jwt = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? auth["Bearer ".Length..].Trim()
            : "";

        dispatcher.Enqueue(jobId, jwt, tenantId, departmentId, correlationId);

        return Accepted(new JobResponse(jobId, ImportJobStatus.QUEUED, "File uploaded successfully. Processing started."));
    }

    // =========================================================
    // GET /imports/jobs/{jobId}
    // =========================================================
    [HttpGet("jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(JobStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus(Guid jobId, CancellationToken ct)
    {
        var tenantId = User.GetTenantId();

        var job = await repo.GetAsync(jobId, ct);
        if (job is null)
        {
            return NotFound(new ErrorResponse(
                new ErrorBody("JOB_NOT_FOUND", "The specified Job ID does not exist", new { jobId })));
        }

        if (job.TenantId != tenantId)
        {
            return StatusCode(403, new ErrorResponse(
                new ErrorBody("TENANT_MISMATCH", "Access denied. This resource belongs to a different tenant",
                    new { userTenantId = tenantId, resourceTenantId = job.TenantId })));
        }

        var dto = new JobStatusDto(
            JobId: job.JobId,
            Status: job.Status,
            ProgressPercentage: job.ProgressPercentage,
            TotalRecords: job.TotalRecords,
            ProcessedRecords: job.ProcessedRecords,
            FailedRecords: job.FailedRecords,
            DownloadErrorLogUrl: job.ErrorFileUrl,
            FailureReason: job.FailureReason,
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt
        );

        return Ok(dto);
    }

    // =========================================================
    // GET /imports/jobs/{jobId}/errors
    // Devuelve link ABSOLUTO de descarga
    // =========================================================
    [HttpGet("jobs/{jobId:guid}/errors")]
    [ProducesResponseType(typeof(ErrorLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetErrorLog(Guid jobId, CancellationToken ct)
    {
        var tenantId = User.GetTenantId();

        var job = await repo.GetAsync(jobId, ct);
        if (job is null)
        {
            return NotFound(new ErrorResponse(
                new ErrorBody("JOB_NOT_FOUND", "The specified Job ID does not exist", new { jobId })));
        }

        if (job.TenantId != tenantId)
        {
            return StatusCode(403, new ErrorResponse(
                new ErrorBody("TENANT_MISMATCH", "Access denied. This resource belongs to a different tenant",
                    new { userTenantId = tenantId, resourceTenantId = job.TenantId })));
        }

        if (job.Status is not ImportJobStatus.COMPLETED and not ImportJobStatus.FAILED)
        {
            return NotFound(new ErrorResponse(
                new ErrorBody("JOB_NOT_COMPLETED", "Cannot download error log. Job is still processing.",
                    new { currentStatus = job.Status.ToString() })));
        }

        if (job.FailedRecords <= 0 || string.IsNullOrWhiteSpace(job.ErrorFileUrl))
            return NoContent();

        var expiresIn = TimeSpan.FromMinutes(_o.PresignedUrlExpirationMinutes);

        // ✅ URL ABSOLUTA al endpoint de descarga
        var absolute = Url.Action(
            action: nameof(DownloadErrorLog),
            controller: null,
            values: new { jobId },
            protocol: Request.Scheme,
            host: Request.Host.ToString()
        );

        var downloadUrl = absolute ?? $"{Request.Scheme}://{Request.Host}/imports/jobs/{jobId}/errors/download";

        return Ok(new ErrorLogResponse(
            DownloadUrl: downloadUrl,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiresIn),
            RecordCount: job.FailedRecords
        ));
    }

    // =========================================================
    // 🔒 GET /imports/jobs/{jobId}/errors/download
    // Descarga XLSX
    // =========================================================
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("jobs/{jobId:guid}/errors/download")]
    public async Task<IActionResult> DownloadErrorLog(Guid jobId, CancellationToken ct)
    {
        var tenantId = User.GetTenantId();

        var job = await repo.GetAsync(jobId, ct);
        if (job is null)
            return NotFound();

        if (job.TenantId != tenantId)
            return Forbid();

        if (job.FailedRecords <= 0 || string.IsNullOrWhiteSpace(job.ErrorFileUrl))
            return NotFound();

        var stream = await storage.OpenReadAsync(job.ErrorFileUrl!, ct);

        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"job-{jobId:N}-errors.xlsx"
        );
    }

    // =========================================================
    // 🔒 GET /imports/jobs/{jobId}/file
    // Descarga el archivo original subido
    // =========================================================
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("jobs/{jobId:guid}/file")]
    public async Task<IActionResult> DownloadOriginalFile(Guid jobId, CancellationToken ct)
    {
        var tenantId = User.GetTenantId();

        var job = await repo.GetAsync(jobId, ct);
        if (job is null)
            return NotFound();

        if (job.TenantId != tenantId)
            return Forbid();

        var stream = await storage.OpenReadAsync(job.FileUrl, ct);

        var fileName = job.OriginalFileName ?? $"job-{jobId:N}-source";
        var contentType = job.OriginalContentType ?? "application/octet-stream";

        return File(stream, contentType, fileName);
    }
}