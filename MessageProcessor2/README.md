# MessageDispatcher2

A gRPC client that processes messages from MessageDispatcher2 using regex analysis.

## Prerequisites
- .NET 8 SDK
- MessageDispatcher2 running on `https://localhost:7263`

## Run the processor
- dotnet run
- Connects to: https://localhost:7263
- Requests messages every 200ms and analyzes them with received regex patterns.


## Notes
- Uses MAC Address + GUID for unique processor ID.
- Retries connection on failure (5 attempts, 10s delay).

