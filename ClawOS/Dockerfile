FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["src/ClawOS.Api/ClawOS.Api.csproj", "src/ClawOS.Api/"]
COPY ["src/ClawOS.Application/ClawOS.Application.csproj", "src/ClawOS.Application/"]
COPY ["src/ClawOS.Domain/ClawOS.Domain.csproj", "src/ClawOS.Domain/"]
COPY ["src/ClawOS.Contracts/ClawOS.Contracts.csproj", "src/ClawOS.Contracts/"]
COPY ["src/ClawOS.Infrastructure/ClawOS.Infrastructure.csproj", "src/ClawOS.Infrastructure/"]
COPY ["src/ClawOS.Infrastructure.Llm.Ollama/ClawOS.Infrastructure.Llm.Ollama.csproj", "src/ClawOS.Infrastructure.Llm.Ollama/"]
COPY ["src/ClawOS.Infrastructure.Llm.OpenAI/ClawOS.Infrastructure.Llm.OpenAI.csproj", "src/ClawOS.Infrastructure.Llm.OpenAI/"]
COPY ["src/ClawOS.Hosting/ClawOS.Hosting.csproj", "src/ClawOS.Hosting/"]
COPY ["src/Weda.Core/Weda.Core.csproj", "src/Weda.Core/"]
COPY ["src/Weda.Protocol/Weda.Protocol.csproj", "src/Weda.Protocol/"]
COPY ["src/ClawOS.Channels.Telegram/ClawOS.Channels.Telegram.csproj", "src/ClawOS.Channels.Telegram/"]

# Copy tool project files (C# tools, formerly skills/)
COPY ["src/tools/ClawOS.Tools.FileSystem/ClawOS.Tools.FileSystem.csproj", "src/tools/ClawOS.Tools.FileSystem/"]
COPY ["src/tools/ClawOS.Tools.Http/ClawOS.Tools.Http.csproj", "src/tools/ClawOS.Tools.Http/"]
COPY ["src/tools/ClawOS.Tools.Shell/ClawOS.Tools.Shell.csproj", "src/tools/ClawOS.Tools.Shell/"]
COPY ["src/tools/ClawOS.Tools.Git/ClawOS.Tools.Git.csproj", "src/tools/ClawOS.Tools.Git/"]
COPY ["src/tools/ClawOS.Tools.GitHub/ClawOS.Tools.GitHub.csproj", "src/tools/ClawOS.Tools.GitHub/"]
COPY ["src/tools/ClawOS.Tools.AzureDevOps/ClawOS.Tools.AzureDevOps.csproj", "src/tools/ClawOS.Tools.AzureDevOps/"]
COPY ["src/tools/ClawOS.Tools.WebSearch/ClawOS.Tools.WebSearch.csproj", "src/tools/ClawOS.Tools.WebSearch/"]
COPY ["src/tools/ClawOS.Tools.ImageGen/ClawOS.Tools.ImageGen.csproj", "src/tools/ClawOS.Tools.ImageGen/"]
COPY ["src/tools/ClawOS.Tools.Pdf/ClawOS.Tools.Pdf.csproj", "src/tools/ClawOS.Tools.Pdf/"]
COPY ["src/tools/ClawOS.Tools.Tmux/ClawOS.Tools.Tmux.csproj", "src/tools/ClawOS.Tools.Tmux/"]
COPY ["src/tools/ClawOS.Tools.Preference/ClawOS.Tools.Preference.csproj", "src/tools/ClawOS.Tools.Preference/"]
COPY ["src/tools/ClawOS.Tools.Notion/ClawOS.Tools.Notion.csproj", "src/tools/ClawOS.Tools.Notion/"]

# Copy WikiGenerator project
COPY ["tools/WikiGenerator/WikiGenerator.csproj", "tools/WikiGenerator/"]

# Restore dependencies
RUN dotnet restore "src/ClawOS.Api/ClawOS.Api.csproj"
RUN dotnet restore "tools/WikiGenerator/WikiGenerator.csproj"

# Copy source code, tools, skills and docs
COPY src/ ./src/
COPY tools/ ./tools/
COPY skills/ ./skills/
COPY docs/ ./docs/

# Build
WORKDIR /src/src/ClawOS.Api
RUN dotnet build "ClawOS.Api.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
WORKDIR /src/src/ClawOS.Api
RUN dotnet publish "ClawOS.Api.csproj" --no-restore -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install GitHub CLI and Python3 (for skill scripts)
RUN apt-get update && apt-get install -y curl python3 \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg | dd of=/usr/share/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /usr/share/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" | tee /etc/apt/sources.list.d/github-cli.list > /dev/null \
    && apt-get update \
    && apt-get install -y gh \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=5001
ENV Skills__Directory=/app/skills
EXPOSE 5001
WORKDIR /app
COPY --from=publish /app/publish .
# Copy markdown skills to runtime
COPY --from=build /src/skills/ /app/skills/
ENTRYPOINT ["dotnet", "ClawOS.Api.dll"]
