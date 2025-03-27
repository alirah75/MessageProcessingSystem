using Grpc.Net.Client;
using MessageProcessor2.Services;
using System;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grpc.Core;

// Message processor using gRPC
public class Program
{
    // Program entry point
    static async Task Main(string[] args)
    {
        var macAddress = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Select(n => n.GetPhysicalAddress().ToString())
            .FirstOrDefault() ?? "NoMAC";
        var processorId = Guid.NewGuid().ToString() + "-" + macAddress;

        var channel = GrpcChannel.ForAddress("https://localhost:7263", new GrpcChannelOptions
        {
            HttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
            }
        });
        var client = new MessageService.MessageServiceClient(channel);

        var registerResponse = await RegisterWithRetry(client, processorId);
        Console.WriteLine($"Registered: IsActive={registerResponse.IsActive}");

        if (!registerResponse.IsActive)
        {
            Console.WriteLine("Processor not activated. Exiting...");
            return;
        }

        var metadata = new Metadata { { "processor-id", processorId } };
        await ProcessMessages(client, metadata);
    }

    // Registers processor with retry logic
    public static async Task<ProcessorResponse> RegisterWithRetry(MessageService.MessageServiceClient client, string processorId)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                return await client.RegisterProcessorAsync(new ProcessorInfo
                {
                    Id = processorId,
                    Type = "RegexEngine"
                });
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"Registration failed: {ex.Status.Detail}. Retrying in 10s...");
                await Task.Delay(10000);
            }
        }
        throw new Exception("Failed to register after 5 attempts.");
    }

    // Processes incoming messages and applies regex analysis
    public static async Task ProcessMessages(MessageService.MessageServiceClient client, Metadata metadata)
    {
        while (true)
        {
            try
            {
                using var streamingCall = client.StreamMessages(metadata);
                await foreach (var response in streamingCall.ResponseStream.ReadAllAsync())
                {
                    var rawMessage = response.RawMessage;
                    var messageLength = response.MessageLength;
                    var isValid = response.IsValid;

                    var regexResults = new Dictionary<string, bool>();
                    foreach (var pattern in response.RegexPatterns)
                    {
                        bool matches = Regex.IsMatch(rawMessage, pattern.Value);
                        regexResults[pattern.Key] = matches;
                    }

                    Console.WriteLine($"Processed: Id={response.Id}, Length={messageLength}, Valid={isValid}");
                    Console.WriteLine($"Raw Message: {rawMessage}");
                    Console.WriteLine($"Regex Patterns: {string.Join(", ", response.RegexPatterns.Select(r => $"{r.Key}={r.Value}"))}");
                    Console.WriteLine($"Regex Results: {string.Join(", ", regexResults.Select(r => $"{r.Key}={r.Value}"))}");
                    Console.WriteLine("---");

                    // Send processed results back to MessageDispatcher2
                    var processedMessage = new MessageRequest
                    {
                        Id = response.Id,
                        Sender = "Processor",
                        Message = $"Results: {string.Join(", ", regexResults.Select(r => $"{r.Key}={r.Value}"))}"
                    };
                    await streamingCall.RequestStream.WriteAsync(processedMessage);
                }
                await streamingCall.RequestStream.CompleteAsync(); // Signal end of sending
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"Stream failed: {ex.Status.Detail}. Retrying in 10s...");
                await Task.Delay(10000);
            }
            await Task.Delay(200);
        }
    }
}