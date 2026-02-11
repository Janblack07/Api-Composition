using Microsoft.Extensions.Options;
using System.Security.Claims;
using UCS.DebtorBatch.Api.Options;

namespace UCS.DebtorBatch.Api.Infrastructure.Auth
{
    public sealed class MockIdentityMiddleware(RequestDelegate next, IOptions<MockIdentityOptions> opt)
    {
        public async Task InvokeAsync(HttpContext ctx)
        {
            var o = opt.Value;
            if (!o.Enabled)
            {
                await next(ctx);
                return;
            }

            // Si ya viene autenticado real, no lo toques
            if (ctx.User?.Identity?.IsAuthenticated == true)
            {
                await next(ctx);
                return;
            }

            var claims = new List<Claim>
        {
            new("tid", o.TenantId),
            new("did", o.DepartmentId),
            new("sub", o.UserId),
            new(ClaimTypes.NameIdentifier, o.UserId)
        };

            foreach (var p in o.Permissions)
                claims.Add(new("permissions", p));

            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Mock"));
            await next(ctx);
        }
    }
}
