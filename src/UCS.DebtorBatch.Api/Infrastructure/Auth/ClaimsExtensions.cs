using System.Security.Claims;

namespace UCS.DebtorBatch.Api.Infrastructure.Auth
{
    public static class ClaimsExtensions
    {
        public static Guid GetTenantId(this ClaimsPrincipal user)
            => Guid.Parse(user.FindFirstValue("tid") ?? user.FindFirstValue("custom:tenant_id") ?? throw new UnauthorizedAccessException("Missing tenant claim"));

        public static Guid GetDepartmentId(this ClaimsPrincipal user)
            => Guid.Parse(user.FindFirstValue("did") ?? user.FindFirstValue("custom:department_id") ?? throw new UnauthorizedAccessException("Missing department claim"));

        public static Guid GetUserId(this ClaimsPrincipal user)
            => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? throw new UnauthorizedAccessException("Missing user claim"));

        public static bool HasPermission(this ClaimsPrincipal user, string permission)
            => user.Claims.Where(c => c.Type == "permissions").Any(c => string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    }
}
