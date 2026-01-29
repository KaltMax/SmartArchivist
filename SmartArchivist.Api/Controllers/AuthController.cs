using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SmartArchivist.Contract.Logger;

namespace SmartArchivist.Api.Controllers
{
    /// <summary>
    /// Provides API endpoints for issuing JSON Web Tokens (JWT) to clients for authentication purposes.
    /// </summary>
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILoggerWrapper<AuthController> _logger;

        public AuthController(IConfiguration configuration, ILoggerWrapper<AuthController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
        public IActionResult GetToken()
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (clientIp == null)
            {
                _logger.LogWarning("Token request denied: Unable to determine client IP");
                return Forbid();
            }

            _logger.LogInformation("Token request from IP: {ClientIp}", clientIp);

            var token = GenerateToken();

            _logger.LogInformation("JWT-token successfully issued");
            return Ok(new { token });
        }

        private string GenerateToken()
        {
            var secret = _configuration["Jwt:Secret"]
                         ?? throw new InvalidOperationException("JWT secret not configured");
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, "anonymous-user"), // Subject: Generic user for demo purposes (no authentication)
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // JWT ID: Unique token Id
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"], // Who created the token
                audience: _configuration["Jwt:Audience"], // Who should accept the token
                claims: claims,
                expires: null, // Token never expires -> valid until the server restarts
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
