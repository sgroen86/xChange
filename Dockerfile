# Build the API. Restore is a separate layer so it is cached unless a csproj
# changes; the whole solution is copied because the API references four other
# projects.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY InvoicePlatform.sln ./
COPY src/InvoicePlatform.Domain/*.csproj src/InvoicePlatform.Domain/
COPY src/InvoicePlatform.Application/*.csproj src/InvoicePlatform.Application/
COPY src/InvoicePlatform.Infrastructure/*.csproj src/InvoicePlatform.Infrastructure/
COPY src/InvoicePlatform.Contracts/*.csproj src/InvoicePlatform.Contracts/
COPY src/InvoicePlatform.Api/*.csproj src/InvoicePlatform.Api/
COPY src/InvoicePlatform.Worker/*.csproj src/InvoicePlatform.Worker/
COPY tests/InvoicePlatform.Domain.Tests/*.csproj tests/InvoicePlatform.Domain.Tests/
COPY tests/InvoicePlatform.Integration.Tests/*.csproj tests/InvoicePlatform.Integration.Tests/
RUN dotnet restore src/InvoicePlatform.Api/InvoicePlatform.Api.csproj

COPY src/ src/
RUN dotnet publish src/InvoicePlatform.Api/InvoicePlatform.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Run as the non-root user the base image already provides.
USER $APP_UID

# App Runner routes plain HTTP to the container and terminates TLS itself, so
# the app must not redirect to HTTPS internally - that would loop.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "InvoicePlatform.Api.dll"]
