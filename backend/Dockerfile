# Çok aşamalı build, derleme araçlarını son imaja taşımayarak çalışma imajını küçük ve daha az yüzeyli tutar.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FieldOps.sln ./
COPY src/FieldOps.Api/FieldOps.Api.csproj src/FieldOps.Api/
COPY src/FieldOps.Application/FieldOps.Application.csproj src/FieldOps.Application/
COPY src/FieldOps.Domain/FieldOps.Domain.csproj src/FieldOps.Domain/
COPY src/FieldOps.Infrastructure/FieldOps.Infrastructure.csproj src/FieldOps.Infrastructure/
COPY tests/FieldOps.UnitTests/FieldOps.UnitTests.csproj tests/FieldOps.UnitTests/
COPY tests/FieldOps.IntegrationTests/FieldOps.IntegrationTests.csproj tests/FieldOps.IntegrationTests/
RUN dotnet restore FieldOps.sln

COPY . ./
RUN dotnet publish src/FieldOps.Api/FieldOps.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "FieldOps.Api.dll"]
