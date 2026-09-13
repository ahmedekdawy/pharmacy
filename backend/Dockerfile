# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Pharmacy.sln ./
COPY src/Pharmacy.Api/Pharmacy.Api.csproj src/Pharmacy.Api/
COPY src/Pharmacy.Application/Pharmacy.Application.csproj src/Pharmacy.Application/
COPY src/Pharmacy.Infrastructure/Pharmacy.Infrastructure.csproj src/Pharmacy.Infrastructure/
COPY src/Pharmacy.Domain/Pharmacy.Domain.csproj src/Pharmacy.Domain/
COPY src/Pharmacy.Contracts/Pharmacy.Contracts.csproj src/Pharmacy.Contracts/
COPY src/Pharmacy.Shared/Pharmacy.Shared.csproj src/Pharmacy.Shared/
COPY tests/Pharmacy.UnitTests/Pharmacy.UnitTests.csproj tests/Pharmacy.UnitTests/
COPY tests/Pharmacy.IntegrationTests/Pharmacy.IntegrationTests.csproj tests/Pharmacy.IntegrationTests/

RUN dotnet restore src/Pharmacy.Api/Pharmacy.Api.csproj

COPY . .
RUN dotnet publish src/Pharmacy.Api/Pharmacy.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Pharmacy.Api.dll"]
