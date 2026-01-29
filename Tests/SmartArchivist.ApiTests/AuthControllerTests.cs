using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SmartArchivist.Contract.Logger;
using SmartArchivist.Api.Controllers;
using System.Net;

namespace Tests.SmartArchivist.ApiTests
{
    public class AuthControllerTests
    {
        private readonly IConfiguration _config;
        private readonly ILoggerWrapper<AuthController> _logger;
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _config = Substitute.For<IConfiguration>();
            _logger = Substitute.For<ILoggerWrapper<AuthController>>();
            _controller = new AuthController(_config, _logger);

            _config["Jwt:Secret"].Returns("this-is-a-very-long-secret-key-for-jwt-token-signing");
            _config["Jwt:Issuer"].Returns("SmartArchivistAPI");
            _config["Jwt:Audience"].Returns("SmartArchivistClient");
        }

        [Fact]
        public void GetToken_ValidRequest_ReturnsOkWithToken()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.GetToken();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public void GetToken_MissingClientIp_ReturnsForbid()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = null;
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = _controller.GetToken();

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public void GetToken_MissingJwtSecret_ThrowsException()
        {
            // Arrange
            _config["Jwt:Secret"].Returns((string?)null);
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
            _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _controller.GetToken());
        }
    }
}
