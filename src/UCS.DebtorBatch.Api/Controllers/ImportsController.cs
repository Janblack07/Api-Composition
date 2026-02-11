// File: src/UCS.DebtorBatch.Api/Controllers/ImportsController.cs

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
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
    // ✅ 1) POST /imports/debtors  (principal)
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

            // ✅ NUEVO: para descargar el original correctamente
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
    // ✅ 2) GET /imports/jobs/{jobId}  (principal)
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

            // OJO: si quieres, aquí puedes devolver el link público “bonito”
            // pero tú pediste que salga en /errors, así que aquí lo dejo igual:
            DownloadErrorLogUrl: job.ErrorFileUrl,

            FailureReason: job.FailureReason,
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt
        );

        return Ok(dto);
    }

    // =========================================================
    // ✅ 3) GET /imports/jobs/{jobId}/errors  (principal)
    //     AQUÍ debe aparecer el link de descarga
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

        // ✅ CAMBIO CLAVE:
        // Ya NO devolvemos presigned (Drive/local), devolvemos un link interno del API
        var expiresIn = TimeSpan.FromMinutes(_o.PresignedUrlExpirationMinutes);

        var url = Url.Action(nameof(DownloadErrorLog), values: new { jobId })
                  ?? $"/imports/jobs/{jobId}/errors/download";

        return Ok(new ErrorLogResponse(
            DownloadUrl: url,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiresIn),
            RecordCount: job.FailedRecords
        ));
    }

    // =========================================================
    // 🔒 Endpoint interno: descarga CSV de errores
    // NO aparece en Swagger (para que “solo existan 3 endpoints” visibles)
    // GET /imports/jobs/{jobId}/errors/download
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

        // ✅ FIX del error “Cannot access a closed file”:
        // NO usar "using var stream". Retornamos el stream vivo y ASP.NET lo dispone al final.
        var stream = await storage.OpenReadAsync(job.ErrorFileUrl!, ct);

        return File(stream, "text/csv", $"job-{jobId:N}-errors.csv");
    }

    // =========================================================
    // 🔒 Endpoint interno: descarga archivo original
    // NO aparece en Swagger
    // GET /imports/jobs/{jobId}/file
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