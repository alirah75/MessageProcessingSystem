using Microsoft.AspNetCore.Mvc;
using ManagementSystem2.Models;

namespace ManagementSystem2.Controllers
{
    // Handles health check requests
    [ApiController]
    [Route("api/module")]
    public class HealthMonitorController : ControllerBase
    {
        private readonly Random _random = new Random();

        // Returns health status of the system
        [HttpPost("health")]
        public ActionResult<HealthResponse> Health([FromBody] HealthRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body is null.");
                }

                var response = new HealthResponse
                {
                    IsEnabled = true,
                    NumberOfActiveClients = _random.Next(0, 6),
                    ExpirationTime = DateTime.UtcNow.AddMinutes(10)
                };
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}