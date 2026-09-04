# escape=`

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

LABEL org.opencontainers.image.source="https://github.com/stmarcelo/finort"
LABEL org.opencontainers.image.description="Finort - Finanças Norteadas"

WORKDIR /src

COPY Version.props ./
COPY src/aspnet/Finort.csproj src/aspnet/
RUN dotnet restore src/aspnet/Finort.csproj

COPY src/ src/
RUN dotnet publish src/aspnet/Finort.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN addgroup --system --gid 1001 appgroup `
    && adduser --system --uid 1001 --ingroup appgroup appuser

COPY --from=build --chown=appuser:appgroup /app/publish .

RUN mkdir -p /app/data && chown appuser:appgroup /app/data

USER appuser

ENV ASPNETCORE_URLS=http://+:5298 `
    DOTNET_RUNNING_IN_CONTAINER=true `
    DOTNET_gcServer=1 `
    FINORT_DATA_DIR=/app/data

EXPOSE 5298

ENTRYPOINT ["dotnet", "Finort.dll"]
