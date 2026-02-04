using System.Security.Claims;

namespace ApiComposition.Ucs.DebtorBatch.Security
{
    public sealed class HttpTenantContextAccessor(IHttpContextAccessor http) : ITenantContextAccessor
    {
        public TenantContext GetOrThrow()
        {
            var user = http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
                throw new UnauthorizedAccessException("User is not authenticated.");

            // Ajusta a tus claims reales si difieren
            var tidRaw =
                user.FindFirstValue("tid") ??
                user.FindFirstValue("tenant_id") ??
                user.FindFirstValue("tenantId") ??
                user.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid"); // ✅ fallback

            if (!Guid.TryParse(tidRaw, out var tenantId) || tenantId == Guid.Empty)
                throw new UnauthorizedAccessException("Missing/invalid tenantId claim (tid).");

            var didRaw =
                user.FindFirstValue("did") ??
                user.FindFirstValue("department_id") ??
                user.FindFirstValue("departmentId");

            Guid? departmentId = Guid.TryParse(didRaw, out var did) && did != Guid.Empty ? did : null;

            var userId =
                user.FindFirstValue(ClaimTypes.NameIdentifier) ??
                user.FindFirstValue("sub") ??
                throw new UnauthorizedAccessException("Missing user id claim (sub).");

            return new TenantContext(tenantId, departmentId, userId);
        }
    }
}
