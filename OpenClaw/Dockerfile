FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/OpenClaw.Api/OpenClaw.Api.csproj", "OpenClaw.Api/"]
COPY ["src/OpenClaw.Application/OpenClaw.Application.csproj", "OpenClaw.Application/"]
COPY ["src/OpenClaw.Domain/OpenClaw.Domain.csproj", "OpenClaw.Domain/"]
COPY ["src/OpenClaw.Contracts/OpenClaw.Contracts.csproj", "OpenClaw.Contracts/"]
COPY ["src/OpenClaw.Infrastructure/OpenClaw.Infrastructure.csproj", "OpenClaw.Infrastructure/"]
COPY ["src/OpenClaw.Infrastructure.Llm.Ollama/OpenClaw.Infrastructure.Llm.Ollama.csproj", "OpenClaw.Infrastructure.Llm.Ollama/"]
COPY ["src/Weda.Core/Weda.Core.csproj", "Weda.Core/"]
COPY ["src/Weda.Protocol/Weda.Protocol.csproj", "Weda.Protocol/"]

# Copy WikiGenerator project for build-time wiki generation
COPY ["tools/WikiGenerator/WikiGenerator.csproj", "tools/WikiGenerator/"]

# Restore dependencies
RUN dotnet restore "OpenClaw.Api/OpenClaw.Api.csproj"
RUN dotnet restore "tools/WikiGenerator/WikiGenerator.csproj"

# Copy source code and docs
COPY src/ ./
COPY tools/ /src/../tools/
COPY docs/ /src/../docs/

# Build
WORKDIR /src/OpenClaw.Api
RUN dotnet build "OpenClaw.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "OpenClaw.Api.csproj" --no-restore -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
ENV ASPNETCORE_HTTP_PORTS=5001
EXPOSE 5001
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenClaw.Api.dll"]