using Grpc.Core;
using MessageDispatcher2.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MessageDispatcher2.Tests
{
    public class MessageServiceImplTests
    {
        private readonly Mock<ILogger<MessageServiceImpl>> _loggerMock;
        private readonly Mock<HealthCheckService> _healthCheckMock;
        private readonly MessageServiceImpl _service;

        public MessageServiceImplTests()
        {
            _loggerMock = new Mock<ILogger<MessageServiceImpl>>();
            _healthCheckMock = new Mock<HealthCheckService>();
            _service = new MessageServiceImpl(_loggerMock.Object, _healthCheckMock.Object);
        }

        [Fact]
        public async Task RegisterProcessor_HealthDisabled_ReturnsInactive()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(false);
            var request = new ProcessorInfo { Id = "proc1", Type = "RegexEngine" };

            // Act
            var response = await _service.RegisterProcessor(request, null);

            // Assert
            Assert.False(response.IsActive);
        }

        [Fact]
        public async Task RegisterProcessor_LimitExceeded_ReturnsInactive()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(true);
            _healthCheckMock.Setup(h => h.MaxActiveClients).Returns(1);
            await _service.RegisterProcessor(new ProcessorInfo { Id = "proc1", Type = "RegexEngine" }, null);
            var request = new ProcessorInfo { Id = "proc2", Type = "RegexEngine" };

            // Act
            var response = await _service.RegisterProcessor(request, null);

            // Assert
            Assert.False(response.IsActive);
        }

        [Fact]
        public async Task RegisterProcessor_ValidRequest_ReturnsActive()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(true);
            _healthCheckMock.Setup(h => h.MaxActiveClients).Returns(2);
            var request = new ProcessorInfo { Id = "proc1", Type = "RegexEngine" };

            // Act
            var response = await _service.RegisterProcessor(request, null);

            // Assert
            Assert.True(response.IsActive);
        }

        [Fact]
        public async Task StreamMessages_UnregisteredProcessor_ThrowsRpcException()
        {
            // Arrange
            var requestStreamMock = new Mock<IAsyncStreamReader<MessageRequest>>();
            var responseStreamMock = new Mock<IServerStreamWriter<MessageResponse>>();
            var context = new Mock<ServerCallContext>();
            context.Setup(c => c.RequestHeaders).Returns(new Metadata { new Metadata.Entry("processor-id", "unknown") });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                _service.StreamMessages(requestStreamMock.Object, responseStreamMock.Object, context.Object));
            Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        }

        [Fact]
        public async Task StreamMessages_GeneratesMessages()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(true);
            _healthCheckMock.Setup(h => h.MaxActiveClients).Returns(2);
            await _service.RegisterProcessor(new ProcessorInfo { Id = "proc1", Type = "RegexEngine" }, null);

            var requestStreamMock = new Mock<IAsyncStreamReader<MessageRequest>>();
            requestStreamMock.Setup(r => r.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var responseStreamMock = new Mock<IServerStreamWriter<MessageResponse>>();
            var messagesSent = new ConcurrentBag<MessageResponse>();
            responseStreamMock.Setup(w => w.WriteAsync(It.IsAny<MessageResponse>())).Callback<MessageResponse>(m => messagesSent.Add(m));

            var context = new Mock<ServerCallContext>();
            context.Setup(c => c.RequestHeaders).Returns(new Metadata { new Metadata.Entry("processor-id", "proc1") });
            var cts = new CancellationTokenSource(1000);
            context.Setup(c => c.CancellationToken).Returns(cts.Token);

            // Act
            await _service.StreamMessages(requestStreamMock.Object, responseStreamMock.Object, context.Object);

            // Assert
            Assert.NotEmpty(messagesSent);
            foreach (var msg in messagesSent)
            {
                Assert.InRange(msg.Id, 1000, 9999);
                Assert.Equal("RegexEngine", msg.Engine);
                Assert.True(msg.MessageLength > 0);
                Assert.Equal(msg.IsValid, msg.MessageLength > 5);
                Assert.NotEmpty(msg.RawMessage);
                Assert.Equal(3, msg.RegexPatterns.Count);
            }
        }

        [Fact]
        public async Task StreamMessages_ClientSendsMessage_ProcessesCorrectly()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(true);
            _healthCheckMock.Setup(h => h.MaxActiveClients).Returns(2);
            await _service.RegisterProcessor(new ProcessorInfo { Id = "proc1", Type = "RegexEngine" }, null);

            var requestStreamMock = new Mock<IAsyncStreamReader<MessageRequest>>();
            requestStreamMock.SetupSequence(r => r.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(false);
            requestStreamMock.Setup(r => r.Current).Returns(new MessageRequest
            {
                Id = 9999,
                Sender = "TestClient",
                Message = "abc123"
            });

            var responseStreamMock = new Mock<IServerStreamWriter<MessageResponse>>();
            var messagesSent = new ConcurrentBag<MessageResponse>();
            responseStreamMock.Setup(w => w.WriteAsync(It.IsAny<MessageResponse>())).Callback<MessageResponse>(m => messagesSent.Add(m));

            var context = new Mock<ServerCallContext>();
            context.Setup(c => c.RequestHeaders).Returns(new Metadata { new Metadata.Entry("processor-id", "proc1") });
            var cts = new CancellationTokenSource(500);
            context.Setup(c => c.CancellationToken).Returns(cts.Token);

            // Act
            await _service.StreamMessages(requestStreamMock.Object, responseStreamMock.Object, context.Object);

            // Assert
            var clientResponse = messagesSent.FirstOrDefault(m => m.Id == 9999);
            Assert.NotNull(clientResponse);
            Assert.Equal("abc123", clientResponse.RawMessage);
            Assert.Equal(6, clientResponse.MessageLength);
            Assert.True(clientResponse.IsValid);
            Assert.Equal(3, clientResponse.RegexPatterns.Count);
        }

        [Fact]
        public async Task StreamMessages_InactiveProcessor_RemovesAfter5Minutes()
        {
            // Arrange
            _healthCheckMock.Setup(h => h.IsEnabled).Returns(true);
            _healthCheckMock.Setup(h => h.MaxActiveClients).Returns(2);
            await _service.RegisterProcessor(new ProcessorInfo { Id = "proc1", Type = "RegexEngine" }, null);

            var field = typeof(MessageServiceImpl).GetField("_activeProcessors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var processors = (ConcurrentDictionary<string, DateTime>)field.GetValue(_service);
            processors["proc1"] = DateTime.UtcNow.AddMinutes(-6);

            var requestStreamMock = new Mock<IAsyncStreamReader<MessageRequest>>();
            requestStreamMock.Setup(r => r.MoveNext(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var responseStreamMock = new Mock<IServerStreamWriter<MessageResponse>>();

            var context = new Mock<ServerCallContext>();
            context.Setup(c => c.RequestHeaders).Returns(new Metadata { new Metadata.Entry("processor-id", "proc1") });
            var cts = new CancellationTokenSource(200);
            context.Setup(c => c.CancellationToken).Returns(cts.Token);

            // Act
            await _service.StreamMessages(requestStreamMock.Object, responseStreamMock.Object, context.Object);

            // Assert
            Assert.False(processors.ContainsKey("proc1"));
        }
    }
}