using ManagementSystem2.Controllers;
using ManagementSystem2.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using Xunit;

namespace ManagementSystem2.Tests
{
    public class HealthMonitorControllerTests
    {
        private readonly HealthMonitorController _controller;

        public HealthMonitorControllerTests()
        {
            _controller = new HealthMonitorController();
        }

        [Fact]
        public void Health_ValidRequest_ReturnsOkWithCorrectResponse()
        {
            // Arrange
            var request = new HealthRequest
            {
                Id = Guid.NewGuid().ToString(),
                SystemTime = DateTime.UtcNow,
                NumberOfConnectedClients = 5
            };

            // Act
            var result = _controller.Health(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<HealthResponse>(okResult.Value);
            Assert.True(response.IsEnabled);
            Assert.InRange(response.NumberOfActiveClients, 0, 5);
            Assert.True(response.ExpirationTime > DateTime.UtcNow && response.ExpirationTime <= DateTime.UtcNow.AddMinutes(10).AddSeconds(1));
        }

        [Fact]
        public void Health_NullRequest_ReturnsBadRequest()
        {
            // Act
            var result = _controller.Health(null);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public void Health_EmptyId_ReturnsValidResponse()
        {
            // Arrange
            var request = new HealthRequest
            {
                Id = "",
                SystemTime = DateTime.UtcNow,
                NumberOfConnectedClients = 5
            };

            // Act
            var result = _controller.Health(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<HealthResponse>(okResult.Value);
            Assert.True(response.IsEnabled);
        }

        [Fact]
        public void Health_NegativeClients_ReturnsValidResponse()
        {
            // Arrange
            var request = new HealthRequest
            {
                Id = Guid.NewGuid().ToString(),
                SystemTime = DateTime.UtcNow,
                NumberOfConnectedClients = -1
            };

            // Act
            var result = _controller.Health(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<HealthResponse>(okResult.Value);
            Assert.True(response.IsEnabled);
        }
    }
}