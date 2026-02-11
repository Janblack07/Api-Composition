using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using UCS.DebtorBatch.Api.Application.Abstractions;
using UCS.DebtorBatch.Api.Contracts.Shared;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Infrastructure.Core;

public sealed class CoreDebtorHttpClient(
    HttpClient http,
    IOptions<CoreOptions> opt,
    ILogger<CoreDebtorHttpClient> logger)
    : IDebtorBatchClient
{
    private readonly CoreOptions _o = opt.Value;

    public async Task<CoreBatchImportResponse> SendBatchAsync(
        CoreBatchImportRequest request,
        string userJwt,
        Guid tenantId,
        Guid departmentId,
        string correlationId,
        CancellationToken ct)
    {
        // ✅ Ruta real confirmada
        const string endpoint = "/debtors/batch-import";

        var baseUrl = (_o.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("CoreOptions.BaseUrl is not configured.");

        var url = $"{baseUrl}{endpoint}";

        // --- Logs útiles
        logger.LogInformation(
            "CoreDebtorHttpClient -> URL={Url} TenantId={TenantId} DepartmentId={DepartmentId} CorrelationId={CorrelationId}",
            url, tenantId, departmentId, correlationId);

        logger.LogInformation("JWT length: {Len}", userJwt?.Length ?? 0);

        using var msg = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        

        // Si no tienes JWT real aún, userJwt puede ser "" (eso te dará 401/400 en Core dependiendo)
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userJwt ?? string.Empty);

        msg.Headers.TryAddWithoutValidation("X-Tenant-ID", tenantId.ToString());
        msg.Headers.TryAddWithoutValidation("X-Department-ID", departmentId.ToString());
        msg.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        // ✅ Opcional (muchos proyectos ABP lo usan)
        // Si el Core valida tenant por este header, esto resuelve "Tenant es requerido"
        msg.Headers.TryAddWithoutValidation("__tenant", tenantId.ToString());

        logger.LogInformation(
            "Headers -> Authorization=Bearer(***) X-Tenant-ID={TenantId} X-Department-ID={DepartmentId} X-Correlation-ID={CorrelationId} __tenant={TenantId2}",
            tenantId, departmentId, correlationId, tenantId);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(msg, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HTTP error calling Core -> {Url}", url);
            throw;
        }

        // Log del status para debugging
        logger.LogInformation("Core response -> StatusCode={StatusCode}", (int)resp.StatusCode);

        // Si es error, intenta leer body para ver mensaje real del Core (sin romper el flujo)
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await SafeReadBodyAsync(resp, ct);
            logger.LogWarning("Core error body -> {Body}", errBody);

            // Lanza excepción para que el worker decida si falla el job o marca chunk fallido
            resp.EnsureSuccessStatusCode();
        }

        var body = await resp.Content.ReadFromJsonAsync<CoreBatchImportResponse>(cancellationToken: ct);
        if (body is null)
            throw new HttpRequestException("Core response body was null.");

        return body;
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return "<unreadable>";
        }
    }
}