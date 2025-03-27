using Grpc.Core;
using MessageDispatcher2.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MessageDispatcher2.Services
{
    // Implements gRPC message service
    public class MessageServiceImpl : MessageService.MessageServiceBase
    {
        private readonly ILogger<MessageServiceImpl> _logger;
        private readonly HealthCheckService _healthCheckService;
        private readonly Random _random = new Random();
        private readonly ConcurrentDictionary<string, DateTime> _activeProcessors = new ConcurrentDictionary<string, DateTime>();
        private readonly Dictionary<string, string> _regexPatterns = new Dictionary<string, string>
        {
            { "isAlpha", "^[a-zA-Z]+$" },
            { "hasNumber", "\\d+" },
            { "isShort", "^.{1,5}$" }
        };
        // New: Queue for storing processed message results
        private readonly ConcurrentQueue<MessageRequest> _resultsQueue = new ConcurrentQueue<MessageRequest>();

        // Constructor with dependency injection
        public MessageServiceImpl(ILogger<MessageServiceImpl> logger, HealthCheckService healthCheckService)
        {
            _logger = logger;
            _healthCheckService = healthCheckService;
        }

        // Registers a processor if conditions allow
        public override Task<ProcessorResponse> RegisterProcessor(ProcessorInfo request, ServerCallContext context)
        {
            if (!_healthCheckService.IsEnabled || _activeProcessors.Count >= _healthCheckService.MaxActiveClients)
            {
                _logger.LogWarning($"Processor registration rejected: Id={request.Id}, Enabled={_healthCheckService.IsEnabled}, ActiveClients={_activeProcessors.Count}/{_healthCheckService.Max... (rest unchanged)
                return Task.FromResult(new ProcessorResponse { IsActive = false });
            }

            _activeProcessors[request.Id] = DateTime.UtcNow;
                _logger.LogInformation($"Processor registered: Id={request.Id}, Type={request.Type}");
                return Task.FromResult(new ProcessorResponse { IsActive = true });
            }

        // Streams messages to/from processors
        public override async Task StreamMessages(IAsyncStreamReader<MessageRequest> requestStream, IServerStreamWriter<MessageResponse> responseStream, ServerCallContext context)
        {
            var processorId = context.RequestHeaders.GetValue("processor-id") ?? "unknown";

            if (!_activeProcessors.ContainsKey(processorId))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Processor not registered."));
            }

            var clientMessagesTask = Task.Run(async () =>
            {
                await foreach (var request in requestStream.ReadAllAsync())
                {
                    _activeProcessors[processorId] = DateTime.UtcNow;
                    _logger.LogInformation($"Received processed message from client: Id={request.Id}, Sender={request.Sender}, Message={request.Message}");
                    // New: Add processed message to results queue
                    _resultsQueue.Enqueue(request);
                    _logger.LogInformation($"Message added to results queue: Id={request.Id}");
                }
            });

            while (!context.CancellationToken.IsCancellationRequested)
            {
                foreach (var processor in _activeProcessors.ToArray())
                {
                    if ((DateTime.UtcNow - processor.Value).TotalMinutes > 5)
                    {
                        _activeProcessors.TryRemove(processor.Key, out _);
                        _logger.LogInformation($"Processor deactivated due to inactivity: Id={processor.Key}");
                    }
                }

                var message = GenerateRandomMessage();
                _logger.LogInformation($"Generated message: Id={message.Id}, Sender={message.Sender}, Message={message.Message}");

                var response = new MessageResponse
                {
                    Id = message.Id,
                    Engine = "RegexEngine",
                    MessageLength = message.Message.Length,
                    IsValid = message.Message.Length > 5,
                    RawMessage = message.Message
                };
                response.RegexPatterns.Add(_regexPatterns);
                await responseStream.WriteAsync(response);
                _activeProcessors[processorId] = DateTime.UtcNow;
                await Task.Delay(200, context.CancellationToken);
            }

            await clientMessagesTask;
        }

        // Generates a random message
        private MessageRequest GenerateRandomMessage()
        {
            var senders = new[] { "Legal", "Support", "Admin", "Guest" };
            var messages = new[] { "lorem ipsum", "hello123", "test", "random text" };

            return new MessageRequest
            {
                Id = _random.Next(1000, 9999),
                Sender = senders[_random.Next(senders.Length)],
                Message = messages[_random.Next(messages.Length)]
            };
        }
    }
}