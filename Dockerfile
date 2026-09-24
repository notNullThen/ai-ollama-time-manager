FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source/ai-ollama-time-manager
COPY ai-orchestrator-dotnet/AIOrchestrator/ /source/ai-orchestrator-dotnet/AIOrchestrator/
COPY ai-ollama-time-manager/ /source/ai-ollama-time-manager/
RUN dotnet publish "TimeCalculator/TimeCalculator.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENTRYPOINT ["dotnet", "TimeCalculator.dll"]
