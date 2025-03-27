using Grpc.Core;
using Grpc.Net.Client;
using MessageProcessor2.Services;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MessageProcessor2.Tests
{
    public class ProgramTests
    {
        private readonly Mock<MessageService.MessageServiceClient> _clientMock;

        public ProgramTests()
        {
            _clientMock = new Mock<MessageService.MessageServiceClient>();
        }

        [Fact]
        public async Task RegisterWithRetry_Success_ReturnsResponse()
        {
            // Arrange
            var response = new ProcessorResponse { IsActive = true };
            _clientMock.Setup(c => c.RegisterProcessorAsync(It.IsAny<ProcessorInfo>(), null, null, CancellationToken.None))
                .Returns(() => new AsyncUnaryCall<ProcessorResponse>(
                    Task.FromResult(response),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => new Metadata(),
                    () => { }
                ));

            // Act
            var result = await Program.RegisterWithRetry(_clientMock.Object, "test-proc");

            // Assert
            Assert.True(result.IsActive);
        }

        [Fact]
        public async Task RegisterWithRetry_FailsAfterRetries_ThrowsException()
        {
            // Arrange
            _clientMock.Setup(c => c.RegisterProcessorAsync(It.IsAny<ProcessorInfo>(), null, null, CancellationToken.None))
                .Throws(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable")));

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => Program.RegisterWithRetry(_clientMock.Object, "test-proc"));
            Assert.Equal("Failed to register after 5 attempts.", ex.Message);
        }

        [Fact]
        public async Task ProcessMessages_ValidMessage_AnalyzesCorrectly()
        {
            // Arrange
            var metadata = new Metadata { { "processor-id", "test-proc" } };
            var responseStreamMock = new Mock<IAsyncStreamReader<MessageResponse>>();
            responseStreamMock.SetupSequence(r => r.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            var messageResponse = new MessageResponse
            {
                Id = 1234,
                Engine = "RegexEngine",
                MessageLength = 8,
                IsValid = true,
                RawMessage = "hello123"
            };
            messageResponse.RegexPatterns.Add(new Dictionary<string, string>
            {
                { "isAlpha", "^[a-zA-Z]+$" },
                { "hasNumber", "\\d+" },
                { "isShort", "^.{1,5}$" }
            });
            responseStreamMock.Setup(r => r.Current).Returns(messageResponse);

            var callMock = new Mock<AsyncDuplexStreamingCall<MessageRequest, MessageResponse>>();
            callMock.Setup(c => c.ResponseStream).Returns(responseStreamMock.Object);
            _clientMock.Setup(c => c.StreamMessages(metadata, null, CancellationToken.None)).Returns(callMock.Object);

            var consoleOutput = new System.IO.StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            var task = Program.ProcessMessages(_clientMock.Object, metadata);
            await Task.Delay(300);
            callMock.Setup(c => c.Dispose());

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("Processed: Id=1234, Length=8, Valid=True", output);
            Assert.Contains("Raw Message: hello123", output);
            Assert.Contains("Regex Results: isAlpha=False, hasNumber=True, isShort=False", output);
        }

        [Fact]
        public async Task ProcessMessages_RpcException_Retries()
        {
            // Arrange
            var metadata = new Metadata { { "processor-id", "test-proc" } };
            _clientMock.SetupSequence(c => c.StreamMessages(metadata, null, CancellationToken.None))
                .Throws(new RpcException(new Status(StatusCode.Unavailable, "Service unavailable")))
                .Returns(new Mock<AsyncDuplexStreamingCall<MessageRequest, MessageResponse>>().Object);

            var consoleOutput = new System.IO.StringWriter();
            Console.SetOut(consoleOutput);

            // Act
            var cts = new CancellationTokenSource(15000);
            var task = Program.ProcessMessages(_clientMock.Object, metadata);
            await Task.Delay(11000, cts.Token);
            cts.Cancel();

            // Assert
            var output = consoleOutput.ToString();
            Assert.Contains("Stream failed: Service unavailable. Retrying in 10s...", output);
        }
    }
}