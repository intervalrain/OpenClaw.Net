FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/OpenClaw.Api/OpenClaw.Api.csproj", "src/OpenClaw.Api/"]
COPY ["src/OpenClaw.Application/OpenClaw.Application.csproj", "src/OpenClaw.Application/"]
COPY ["src/OpenClaw.Domain/OpenClaw.Domain.csproj", "src/OpenClaw.Domain/"]
COPY ["src/OpenClaw.Contracts/OpenClaw.Contracts.csproj", "src/OpenClaw.Contracts/"]
COPY ["src/OpenClaw.Infrastructure/OpenClaw.Infrastructure.csproj", "src/OpenClaw.Infrastructure/"]
COPY ["src/OpenClaw.Infrastructure.Llm.Ollama/OpenClaw.Infrastructure.Llm.Ollama.csproj", "src/OpenClaw.Infrastructure.Llm.Ollama/"]
COPY ["src/OpenClaw.Infrastructure.Llm.OpenAI/OpenClaw.Infrastructure.Llm.OpenAI.csproj", "src/OpenClaw.Infrastructure.Llm.OpenAI/"]
COPY ["src/OpenClaw.Hosting/OpenClaw.Hosting.csproj", "src/OpenClaw.Hosting/"]
COPY ["src/Weda.Core/Weda.Core.csproj", "src/Weda.Core/"]
COPY ["src/Weda.Protocol/Weda.Protocol.csproj", "src/Weda.Protocol/"]

# Copy skills project files
COPY ["skills/OpenClaw.Skills.FileSystem/OpenClaw.Skills.FileSystem.csproj", "skills/OpenClaw.Skills.FileSystem/"]
COPY ["skills/OpenClaw.Skills.Http/OpenClaw.Skills.Http.csproj", "skills/OpenClaw.Skills.Http/"]
COPY ["skills/OpenClaw.Skills.Shell/OpenClaw.Skills.Shell.csproj", "skills/OpenClaw.Skills.Shell/"]

# Copy WikiGenerator project for build-time wiki generation
COPY ["tools/WikiGenerator/WikiGenerator.csproj", "tools/WikiGenerator/"]

# Restore dependencies
RUN dotnet restore "src/OpenClaw.Api/OpenClaw.Api.csproj"
RUN dotnet restore "tools/WikiGenerator/WikiGenerator.csproj"

# Copy source code, skills and docs
COPY src/ ./src/
COPY skills/ ./skills/
COPY tools/ ./tools/
COPY docs/ ./docs/

# Build
WORKDIR /src/src/OpenClaw.Api
RUN dotnet build "OpenClaw.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
WORKDIR /src/src/OpenClaw.Api
RUN dotnet publish "OpenClaw.Api.csproj" --no-restore -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime

# Install GitHub CLI
RUN apt-get update && apt-get install -y curl \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
    && apt-get update \
    && apt-get install -y gh \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=5001
EXPOSE 5001
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenClaw.Api.dll"]