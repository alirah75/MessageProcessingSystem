using MessageDispatcher2.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MessageDispatcher2.Services
{
    // Background service for periodic health checks
    public class HealthCheckService : BackgroundService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HealthCheckService> _logger;
        private bool _isEnabled = true;
        private int _maxActiveClients = 5;
        private DateTime _expirationTime = DateTime.MaxValue;

        // Constructor with dependency injection
        public HealthCheckService(HttpClient httpClient, ILogger<HealthCheckService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("http://localhost:5172");
        }

        // Runs the health check loop
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CheckHealthAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        // Performs a single health check
        private async Task CheckHealthAsync()
        {
            try
            {
                var request = new HealthRequest
                {
                    Id = Guid.NewGuid().ToString(),
                    SystemTime = DateTime.UtcNow,
                    NumberOfConnectedClients = 5
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/module/health", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Raw response: {responseJson}");

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var healthResponse = JsonSerializer.Deserialize<HealthResponse>(responseJson, options);

                    _isEnabled = healthResponse.IsEnabled;
                    _maxActiveClients = healthResponse.NumberOfActiveClients;
                    _expirationTime = healthResponse.ExpirationTime;

                    _logger.LogInformation($"Healthcheck: Enabled={_isEnabled}, ActiveClients={_maxActiveClients}, Expires={_expirationTime}");
                }
                else
                {
                    await RetryHealthCheckAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Healthcheck failed: {ex.Message}");
                await RetryHealthCheckAsync();
            }
        }

        // Retries health check on failure
        private async Task RetryHealthCheckAsync()
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var response = await _httpClient.PostAsync("/api/module/health", null);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Healthcheck retry succeeded.");
                        return;
                    }
                }
                catch { }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            _isEnabled = false;
            _logger.LogWarning("Healthcheck failed after 5 retries. Disabling service.");
        }

        // Indicates if service is enabled and not expired
        public bool IsEnabled => _isEnabled && DateTime.UtcNow < _expirationTime;

        // Maximum number of active clients
        public int MaxActiveClients => _maxActiveClients;
    }
}