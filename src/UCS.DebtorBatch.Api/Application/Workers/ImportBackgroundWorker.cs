using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Application.Import;
using UCS.DebtorBatch.Api.Application.Validation;
using UCS.DebtorBatch.Api.Contracts.Shared;
using UCS.DebtorBatch.Api.DomainLike;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Application.Workers;

public sealed class ImportBackgroundWorker(
    ILogger<ImportBackgroundWorker> logger,
    IBackgroundTaskQueue queue,
    IImportJobRepository repo,
    IFileStorage storage,
    IExcelParser parser,
    IValidationRuleProvider ruleProvider,
    DynamicValidatorFactory validatorFactory,
    IDebtorBatchClient coreClient,
    ImportJobTracker tracker,
    IOptions<ImportOptions> opt,
    IOptions<ErrorPresentationOptions> errorOpt)
    : BackgroundService, IImportJobExecutor
{
    private readonly ImportOptions _o = opt.Value;
    private readonly ErrorPresentationOptions _ep = errorOpt.Value;

    private sealed record BatchSendResult(int ProcessedDelta, int FailedDelta);

    private sealed record PresentedError(
        int RowIndex,
        string ExternalKey,
        string Field,
        string Rule,
        string Friendly,
        string Hint,
        string Technical);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var work = await queue.DequeueAsync(stoppingToken);

            try
            {
                await work(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // shutdown normal
            }
            catch (Exception ex)
            {
                // ✅ IMPORTANTÍSIMO: no dejar que una tarea tumbe todo el Host
                logger.LogError(ex, "Background job crashed but host will continue running.");
            }
        }
    }

    public async Task ExecuteAsync(
        Guid jobId,
        string userJwt,
        Guid tenantId,
        Guid departmentId,
        string correlationId,
        CancellationToken ct)
    {
        var job = await repo.GetAsync(jobId, ct);
        if (job is null) return;

        using var _scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = jobId,
            ["TenantId"] = tenantId,
            ["DepartmentId"] = departmentId,
            ["CorrelationId"] = correlationId
        });

        logger.LogInformation("JOB START -> FileUrl={FileUrl}", job.FileUrl);

        // RowIndex -> (NombreCompleto, Identificación)
        var rowMeta = new Dictionary<int, (string name, string id)>();

        try
        {
            await tracker.UpdateAsync(job, ImportJobStatus.VALIDATING, ct);

            var rules = await ruleProvider.GetRulesAsync(tenantId, ct);
            var validate = validatorFactory.Build(rules);

            await using var file = await storage.OpenReadAsync(job.FileUrl, ct);

            int total = 0;
            int processed = 0;
            int failed = 0;

            // (ya no corta el proceso) - solo para métricas
            int inspected = 0;
            int invalidInSample = 0;

            var errors = new List<ValidationError>(capacity: 256);
            var validBuffer = new List<DebtorRecord>(capacity: _o.BatchSize);

            await tracker.UpdateAsync(job, ImportJobStatus.PROCESSING, ct);

            await foreach (var record in parser.ParseAsync(file, job.OriginalFileName ?? job.FileUrl, ct))
            {
                ct.ThrowIfCancellationRequested();
                total++;

                // sample metrics (no fail-fast)
                if (inspected < _o.FailFastInspectRows) inspected++;

                // Guardar metadata (Nombre + Identificación)
                // ⚠️ Ajusta si tus propiedades se llaman distinto.
                var firstName = (record.FirstName ?? "").Trim();
                var lastName = (record.LastName ?? "").Trim();
                var fullName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
                var id = (record.ExternalKey ?? "").Trim();

                if (!rowMeta.ContainsKey(record.RowIndex))
                    rowMeta[record.RowIndex] = (fullName, id);

                // Validación local
                var rowErrors = validate(record).ToList();
                if (rowErrors.Count > 0)
                {
                    failed++;
                    errors.AddRange(rowErrors);

                    if (inspected <= _o.FailFastInspectRows)
                        invalidInSample++;

                    await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
                    continue;
                }

                // Fila válida -> buffer para envío al Core
                validBuffer.Add(record);

                if (validBuffer.Count >= _o.BatchSize)
                {
                    var result = await SendChunkAsync(job, validBuffer, userJwt, tenantId, departmentId, correlationId, errors, ct);
                    processed += result.ProcessedDelta;
                    failed += result.FailedDelta;

                    validBuffer.Clear();
                    await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
                }
            }

            // Remanente
            if (validBuffer.Count > 0)
            {
                var result = await SendChunkAsync(job, validBuffer, userJwt, tenantId, departmentId, correlationId, errors, ct);
                processed += result.ProcessedDelta;
                failed += result.FailedDelta;

                validBuffer.Clear();
                await tracker.UpdateProgressAsync(job, total, processed, failed, ct);
            }

            // Generar reporte completo (si hay errores)
            if (errors.Count > 0)
            {
                var errorFileUrl = await WriteErrorReportAsync(jobId, errors, rowMeta, ct);
                job.ErrorFileUrl = errorFileUrl;

                logger.LogInformation("ERROR REPORT GENERATED -> ErrorFileUrl={ErrorFileUrl} ErrorsCount={ErrorsCount}",
                    job.ErrorFileUrl, errors.Count);
            }

            // ✅ Final: analizamos el 100% y marcamos progreso final
            await tracker.UpdateProgressAsync(job, total, processed, failed, ct);

            // ✅ Decisión FINAL por porcentaje real sobre todo el archivo
            var errorRate = total == 0 ? 0 : (failed * 100d) / total;

            if (errorRate >= _o.FailFastThresholdPercent)
            {
                await tracker.UpdateAsync(job, ImportJobStatus.FAILED, ct, "FAIL_FAST_TRIGGERED");

                logger.LogWarning(
                    "JOB FAILED (END-OF-FILE THRESHOLD) -> total={Total} processed={Processed} failed={Failed} rate={Rate:0.00}% threshold={Threshold}% sampleInvalid={SampleInvalid}/{SampleInspected}",
                    total, processed, failed, errorRate, _o.FailFastThresholdPercent, invalidInSample, inspected);
            }
            else
            {
                await tracker.UpdateAsync(job, ImportJobStatus.COMPLETED, ct);

                logger.LogInformation(
                    "JOB COMPLETED -> total={Total} processed={Processed} failed={Failed} rate={Rate:0.00}% sampleInvalid={SampleInvalid}/{SampleInspected}",
                    total, processed, failed, errorRate, invalidInSample, inspected);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed", jobId);

            var reason = ex is HttpRequestException
                ? "CORE_COMMUNICATION_ERROR"
                : "UNHANDLED_ERROR";

            await tracker.UpdateAsync(job, ImportJobStatus.FAILED, ct, reason);
        }
    }

    private async Task<BatchSendResult> SendChunkAsync(
        ImportJob job,
        IReadOnlyList<DebtorRecord> chunk,
        string userJwt,
        Guid tenantId,
        Guid departmentId,
        string correlationId,
        List<ValidationError> errors,
        CancellationToken ct)
    {
        var req = BatchBuilder.Build(job.JobId, chunk);

        try
        {
            var resp = await coreClient.SendBatchAsync(req, userJwt, tenantId, departmentId, correlationId, ct);

            // Errores devueltos por el Core (no tumba el job)
            if (resp?.Data?.Errors is not null)
            {
                foreach (var e in resp.Data.Errors)
                    errors.Add(new ValidationError(e.RowIndex, e.ExternalKey, e.Message));
            }

            return new BatchSendResult(
                ProcessedDelta: resp?.Data?.ProcessedCount ?? 0,
                FailedDelta: resp?.Data?.FailedCount ?? 0
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "CORE COMMUNICATION ERROR -> JobId={JobId} Items={Items} CorrelationId={CorrelationId}",
                job.JobId, chunk.Count, correlationId);

            throw;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex,
                "CORE TIMEOUT -> JobId={JobId} Items={Items} CorrelationId={CorrelationId}",
                job.JobId, chunk.Count, correlationId);

            throw new HttpRequestException("Core request timeout.", ex);
        }
    }

    // =========================================================
    // PRESENTACIÓN AMIGABLE (Campo/Regla + Friendly/Hint)
    // =========================================================
    private PresentedError Present(ValidationError e)
    {
        var tech = (e.Message ?? "").Trim();

        var (field, rule) = InferFieldAndRule(tech);

        // ✅ ahora ApplyMappingsOrFallback también devuelve field/rule si hay mapping
        var (finalField, finalRule, friendly, hint) = ApplyMappingsOrFallback(tech, field, rule);

        return new PresentedError(
            RowIndex: e.RowIndex,
            ExternalKey: e.ExternalKey ?? "",
            Field: finalField,
            Rule: finalRule,
            Friendly: friendly,
            Hint: hint,
            Technical: tech
        );
    }

    private (string field, string rule) InferFieldAndRule(string tech)
    {
        // Caso genérico: "ExternalKey/Identificación is required"
        if (tech.Contains(" is required", StringComparison.OrdinalIgnoreCase) && tech.Contains('/'))
        {
            var parts = tech.Split('/', 2);
            var afterSlash = parts.Length == 2 ? parts[1] : tech;
            var name = afterSlash.Replace("is required", "", StringComparison.OrdinalIgnoreCase).Trim();
            return (string.IsNullOrWhiteSpace(name) ? "Campo" : name, "Requerido");
        }

        return ("Dato", "Validación");
    }

    private (string field, string rule, string friendly, string hint) ApplyMappingsOrFallback(string tech, string field, string rule)
    {
        var map = _ep.Mappings
            .Where(m => !string.IsNullOrWhiteSpace(m.Contains))
            .OrderByDescending(m => m.Priority)
            .FirstOrDefault(m => tech.Contains(m.Contains, StringComparison.OrdinalIgnoreCase));

        if (map is not null)
        {
            // ✅ Si hay mapping, también fija Field/Rule para que no salga "Dato"
            var f = string.IsNullOrWhiteSpace(map.Field) ? field : map.Field!;
            var r = string.IsNullOrWhiteSpace(map.Rule) ? rule : map.Rule!;
            return (f, r, map.Friendly, map.Hint);
        }

        // fallback mínimo
        if (rule.Equals("Requerido", StringComparison.OrdinalIgnoreCase))
            return (field, rule, $"{field} es obligatorio.", $"Completa la columna \"{field}\" y vuelve a intentar.");

        return (field, rule, tech, "Revisa el valor ingresado y vuelve a intentar.");
    }

    // =========================================================
    // REPORTE XLSX ESTÉTICO (ClosedXML)
    // - Resumen (cliente): 1 fila por registro con error
    // - Detalle (soporte): 1 fila por error
    // =========================================================
    private async Task<string> WriteErrorReportAsync(
        Guid jobId,
        List<ValidationError> errors,
        Dictionary<int, (string name, string id)> rowMeta,
        CancellationToken ct)
    {
        var presented = errors.Select(Present).ToList();

        var resumen = presented
            .GroupBy(x => new { x.RowIndex, x.ExternalKey })
            .OrderBy(g => g.Key.RowIndex)
            .Select((g, i) =>
            {
                rowMeta.TryGetValue(g.Key.RowIndex, out var meta);

                var campos = g.Select(x => x.Field).Distinct().ToList();
                var erroresTxt = g.Select(x => $"• {x.Field}: {x.Friendly}").Distinct().ToList();
                var hints = g.Select(x => $"• {x.Hint}").Distinct().ToList();

                var ident = string.IsNullOrWhiteSpace(g.Key.ExternalKey) ? meta.id : g.Key.ExternalKey;

                return new
                {
                    NroRegistro = i + 1,
                    Nombre = meta.name,
                    Identificacion = ident,
                    Campos = string.Join(", ", campos),
                    QuePaso = string.Join("\n", erroresTxt),
                    ComoCorregir = string.Join("\n", hints),

                    // Opcional: fila real del archivo
                    FilaEnArchivo = g.Key.RowIndex
                };
            })
            .ToList();

        var detalle = presented
            .OrderBy(x => x.RowIndex)
            .ThenBy(x => x.Field)
            .Select(x =>
            {
                rowMeta.TryGetValue(x.RowIndex, out var meta);
                var ident = string.IsNullOrWhiteSpace(x.ExternalKey) ? meta.id : x.ExternalKey;

                return new
                {
                    FilaEnArchivo = x.RowIndex,
                    Nombre = meta.name,
                    Identificacion = ident,
                    Campo = x.Field,
                    Regla = x.Rule,
                    Error = x.Friendly,
                    Sugerencia = x.Hint,
                    DetalleTecnico = x.Technical
                };
            })
            .ToList();

        using var wb = new XLWorkbook();

        // RESUMEN
        var ws1 = wb.Worksheets.Add("Resumen");
        ws1.Cell(1, 1).InsertTable(resumen, "ResumenTable", true);
        StyleSheet(ws1, wrapColumns: new[] { 5, 6 }); // QuePaso, ComoCorregir
        SetWidthsResumen(ws1);

        // DETALLE
        var ws2 = wb.Worksheets.Add("Detalle");
        ws2.Cell(1, 1).InsertTable(detalle, "DetalleTable", true);
        StyleSheet(ws2, wrapColumns: new[] { 6, 7, 8 }); // Error, Sugerencia, DetalleTecnico
        SetWidthsDetalle(ws2);

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        return await storage.SaveErrorReportAsync(
            ms,
            $"job-{jobId:N}-errors.xlsx",
            TimeSpan.FromDays(7),
            ct
        );

        static void StyleSheet(IXLWorksheet ws, int[] wrapColumns)
        {
            ws.SheetView.FreezeRows(1);
            var table = ws.Tables.First();
            table.ShowAutoFilter = true;
            table.Theme = XLTableTheme.TableStyleMedium9;

            var header = ws.Row(1);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Height = 22;

            ws.RangeUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            ws.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            foreach (var c in wrapColumns)
                ws.Column(c).Style.Alignment.WrapText = true;

            ws.RowsUsed().AdjustToContents();
        }

        static void SetWidthsResumen(IXLWorksheet ws)
        {
            // 1 NroRegistro, 2 Nombre, 3 Identificación, 4 Campos, 5 Qué pasó, 6 Cómo corregir, 7 FilaEnArchivo
            ws.Column(1).Width = 12;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 28;
            ws.Column(5).Width = 45;
            ws.Column(6).Width = 60;
            ws.Column(7).Width = 14;

            ws.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        static void SetWidthsDetalle(IXLWorksheet ws)
        {
            // 1 FilaEnArchivo, 2 Nombre, 3 Identificación, 4 Campo, 5 Regla, 6 Error, 7 Sugerencia, 8 DetalleTecnico
            ws.Column(1).Width = 14;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 18;
            ws.Column(4).Width = 20;
            ws.Column(5).Width = 18;
            ws.Column(6).Width = 35;
            ws.Column(7).Width = 40;
            ws.Column(8).Width = 55;

            ws.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }
}