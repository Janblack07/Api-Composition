using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ApiComposition.Ucs.DebtorBatch.Controllers
{
    [ApiController]
    [Route("dev")]
    public sealed class DevController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IHostEnvironment _env;

        public DevController(IConfiguration cfg, IHostEnvironment env)
        {
            _cfg = cfg;
            _env = env;
        }

        public sealed record DevTokenRequest(
            string? Tid,
            string? Did,
            string? Sub,
            int ExpiresMinutes = 360
        );

        /// <summary>
        /// Genera un JWT REAL (HS256) SOLO para Development, para poder probar endpoints protegidos en Swagger.
        /// </summary>
        [HttpPost("token")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult Token([FromBody] DevTokenRequest? body)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var jwt = _cfg.GetSection("Jwt");
            var issuer = jwt["Issuer"];
            var audience = jwt["Audience"];
            var key = jwt["Key"];

            if (string.IsNullOrWhiteSpace(issuer) ||
                string.IsNullOrWhiteSpace(audience) ||
                string.IsNullOrWhiteSpace(key) ||
                key.Length < 32)
            {
                return Problem("Falta configuración JWT en appsettings.json: Jwt:Issuer, Jwt:Audience, Jwt:Key (Key >= 32 chars).");
            }

            var tid = body?.Tid ?? "11111111-1111-1111-1111-111111111111";
            var did = body?.Did ?? "22222222-2222-2222-2222-222222222222";
            var sub = body?.Sub ?? "dev-user";

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
        {
            new("tid", tid),
            new("sub", sub)
        };

            // did es opcional
            if (!string.IsNullOrWhiteSpace(did))
                claims.Add(new("did", did));

            var expiresMinutes = body?.ExpiresMinutes ?? 360;
            if (expiresMinutes is < 5 or > 24 * 60) expiresMinutes = 360;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            var jwtString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                access_token = jwtString,
                token_type = "Bearer",
                expires_in = expiresMinutes * 60
            });
        }
    }
}
