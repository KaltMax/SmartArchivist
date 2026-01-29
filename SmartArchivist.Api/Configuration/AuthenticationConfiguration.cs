using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SmartArchivist.Api.Configuration
{
    /// <summary>
    /// Provides extension methods to configure JWT Bearer authentication for both REST API endpoints and SignalR hubs
    /// within an ASP.NET Core application.
    /// </summary>
    public static class AuthenticationConfiguration
    {
        public static IServiceCollection ConfigureJwtAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = false, // Should be set true when using an expiration date
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = configuration["Jwt:Issuer"],
                        ValidAudience = configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]
                            ?? throw new InvalidOperationException("JWT secret not configured")))
                    };

                    // Configure JWT authentication for SignalR
                    // WebSocket connections cannot send Authorization headers during handshake,
                    // so we need to read the token from the query string instead
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            // Read the token from query string for SignalR connections
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            // If the request is for our SignalR hub and has a token
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            services.AddAuthorization();

            return services;
        }
    }
}
