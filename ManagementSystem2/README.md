# ManagementSystem2

A simple ASP.NET Core service providing health check API for other services.

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or any compatible IDE

## Run the service using the HTTPS profile
- dotnet run --launch-profile https

## Configuration
- Ports can be modified in Properties/launchSettings.json.

## Notes
- Always returns IsEnabled: true.
- NumberOfActiveClients is randomly generated between 0 and 5.
- ExpirationTime is set to current time + 10 minutes.