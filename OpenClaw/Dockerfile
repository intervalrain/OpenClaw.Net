FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/WedaCleanArch.Api/WedaCleanArch.Api.csproj", "WedaCleanArch.Api/"]
COPY ["src/WedaCleanArch.Application/WedaCleanArch.Application.csproj", "WedaCleanArch.Application/"]
COPY ["src/WedaCleanArch.Domain/WedaCleanArch.Domain.csproj", "WedaCleanArch.Domain/"]
COPY ["src/WedaCleanArch.Contracts/WedaCleanArch.Contracts.csproj", "WedaCleanArch.Contracts/"]
COPY ["src/WedaCleanArch.Infrastructure/WedaCleanArch.Infrastructure.csproj", "WedaCleanArch.Infrastructure/"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]
RUN dotnet restore "WedaCleanArch.Api/WedaCleanArch.Api.csproj"
COPY . ../
WORKDIR /src/WedaCleanArch.Api
RUN dotnet build "WedaCleanArch.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish --no-restore -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
ENV ASPNETCORE_HTTP_PORTS=5001
EXPOSE 5001
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WedaCleanArch.Api.dll"]