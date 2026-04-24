# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for layer caching
COPY AsistOff.MES.Gateway/AsistOff.MES.Gateway.csproj AsistOff.MES.Gateway/
COPY AsistOff.MES.Configuration.Api/AsistOff.MES.Configuration.Api.csproj AsistOff.MES.Configuration.Api/
COPY AsistOff.MES.Configuration.Application/AsistOff.MES.Configuration.Application.csproj AsistOff.MES.Configuration.Application/
COPY AsistOff.MES.Configuration.Core/AsistOff.MES.Configuration.Core.csproj AsistOff.MES.Configuration.Core/
COPY AsistOff.MES.Configuration.Infrastructure/AsistOff.MES.Configuration.Infrastructure.csproj AsistOff.MES.Configuration.Infrastructure/
COPY AsistOff.MES.Multitenancy/AsistOff.MES.Multitenancy.csproj AsistOff.MES.Multitenancy/
COPY AsistOff.MES.Multitenancy.Contracts/AsistOff.MES.Multitenancy.Contracts.csproj AsistOff.MES.Multitenancy.Contracts/
COPY AsistOff.MES.Shared.Abstractions/AsistOff.MES.Shared.Abstractions.csproj AsistOff.MES.Shared.Abstractions/
COPY AsistOff.MES.Shared.Infrastructure/AsistOff.MES.Shared.Infrastructure.csproj AsistOff.MES.Shared.Infrastructure/
COPY AsistOff.MES.Users.Api/AsistOff.MES.Users.Api.csproj AsistOff.MES.Users.Api/
COPY AsistOff.MES.Users.Application/AsistOff.MES.Users.Application.csproj AsistOff.MES.Users.Application/
COPY AsistOff.MES.Users.Core/AsistOff.MES.Users.Core.csproj AsistOff.MES.Users.Core/
COPY AsistOff.MES.Users.Infrastructure/AsistOff.MES.Users.Infrastructure.csproj AsistOff.MES.Users.Infrastructure/
COPY AsistOff.MES.Attachments.Api/AsistOff.MES.Attachments.Api.csproj AsistOff.MES.Attachments.Api/
COPY AsistOff.MES.Attachments.Application/AsistOff.MES.Attachments.Application.csproj AsistOff.MES.Attachments.Application/
COPY AsistOff.MES.Attachments.Core/AsistOff.MES.Attachments.Core.csproj AsistOff.MES.Attachments.Core/
COPY AsistOff.MES.Attachments.Infrastructure/AsistOff.MES.Attachments.Infrastructure.csproj AsistOff.MES.Attachments.Infrastructure/
COPY AsistOff.MES.Production.Api/AsistOff.MES.Production.Api.csproj AsistOff.MES.Production.Api/
COPY AsistOff.MES.Production.Application/AsistOff.MES.Production.Application.csproj AsistOff.MES.Production.Application/
COPY AsistOff.MES.Production.Core/AsistOff.MES.Production.Core.csproj AsistOff.MES.Production.Core/
COPY AsistOff.MES.Production.Infrastructure/AsistOff.MES.Production.Infrastructure.csproj AsistOff.MES.Production.Infrastructure/

RUN dotnet restore AsistOff.MES.Gateway/AsistOff.MES.Gateway.csproj

# Copy all source and publish
COPY . .
RUN dotnet publish AsistOff.MES.Gateway/AsistOff.MES.Gateway.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AsistOff.MES.Gateway.dll"]
