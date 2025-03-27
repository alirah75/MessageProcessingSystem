# MessageDispatcher2

A gRPC-based service for generating and dispatching messages to processors.

## Prerequisites
- .NET 8 SDK
- gRPC tools (`dotnet tool install -g dotnet-grpc`)
- ManagementSystem2 running on `http://localhost:5172`

## Running the Service
- dotnet run --launch-profile https

## Configuration
- Ports: Edit Properties/launchSettings.json.
- Health check URL: Hardcoded to http://localhost:5172/api/module/health.

