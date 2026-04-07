FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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
COPY ["src/OpenClaw.Channels.Telegram/OpenClaw.Channels.Telegram.csproj", "src/OpenClaw.Channels.Telegram/"]

# Copy tool project files (C# tools, formerly skills/)
COPY ["src/tools/OpenClaw.Tools.FileSystem/OpenClaw.Tools.FileSystem.csproj", "src/tools/OpenClaw.Tools.FileSystem/"]
COPY ["src/tools/OpenClaw.Tools.Http/OpenClaw.Tools.Http.csproj", "src/tools/OpenClaw.Tools.Http/"]
COPY ["src/tools/OpenClaw.Tools.Shell/OpenClaw.Tools.Shell.csproj", "src/tools/OpenClaw.Tools.Shell/"]
COPY ["src/tools/OpenClaw.Tools.Git/OpenClaw.Tools.Git.csproj", "src/tools/OpenClaw.Tools.Git/"]
COPY ["src/tools/OpenClaw.Tools.GitHub/OpenClaw.Tools.GitHub.csproj", "src/tools/OpenClaw.Tools.GitHub/"]
COPY ["src/tools/OpenClaw.Tools.AzureDevOps/OpenClaw.Tools.AzureDevOps.csproj", "src/tools/OpenClaw.Tools.AzureDevOps/"]
COPY ["src/tools/OpenClaw.Tools.WebSearch/OpenClaw.Tools.WebSearch.csproj", "src/tools/OpenClaw.Tools.WebSearch/"]
COPY ["src/tools/OpenClaw.Tools.ImageGen/OpenClaw.Tools.ImageGen.csproj", "src/tools/OpenClaw.Tools.ImageGen/"]
COPY ["src/tools/OpenClaw.Tools.Pdf/OpenClaw.Tools.Pdf.csproj", "src/tools/OpenClaw.Tools.Pdf/"]
COPY ["src/tools/OpenClaw.Tools.Tmux/OpenClaw.Tools.Tmux.csproj", "src/tools/OpenClaw.Tools.Tmux/"]
COPY ["src/tools/OpenClaw.Tools.Preference/OpenClaw.Tools.Preference.csproj", "src/tools/OpenClaw.Tools.Preference/"]
COPY ["src/tools/OpenClaw.Tools.Notion/OpenClaw.Tools.Notion.csproj", "src/tools/OpenClaw.Tools.Notion/"]

# Copy WikiGenerator project
COPY ["tools/WikiGenerator/WikiGenerator.csproj", "tools/WikiGenerator/"]

# Restore dependencies
RUN dotnet restore "src/OpenClaw.Api/OpenClaw.Api.csproj"
RUN dotnet restore "tools/WikiGenerator/WikiGenerator.csproj"

# Copy source code, tools, skills and docs
COPY src/ ./src/
COPY tools/ ./tools/
COPY skills/ ./skills/
COPY docs/ ./docs/

# Build
WORKDIR /src/src/OpenClaw.Api
RUN dotnet build "OpenClaw.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
WORKDIR /src/src/OpenClaw.Api
RUN dotnet publish "OpenClaw.Api.csproj" --no-restore -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install GitHub CLI, Docker CLI, and Python3 (for skill scripts)
RUN apt-get update && apt-get install -y curl python3 \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
    && curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo $VERSION_CODENAME) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null \
    && apt-get update \
    && apt-get install -y gh docker-ce-cli docker-compose-plugin \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=5001
ENV Skills__Directory=/app/skills
EXPOSE 5001
WORKDIR /app
COPY --from=publish /app/publish .
# Copy markdown skills to runtime
COPY --from=build /src/skills/ /app/skills/
ENTRYPOINT ["dotnet", "OpenClaw.Api.dll"]
