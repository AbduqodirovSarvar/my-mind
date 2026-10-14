# Build context: repository root (needs global.json and Directory.*.props).
# Targets: `runtime` (default, the API) and `migrator` (EF Core migration bundle, plan D10).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/MyMind.Api/MyMind.Api.csproj src/MyMind.Api/
COPY src/MyMind.Application/MyMind.Application.csproj src/MyMind.Application/
COPY src/MyMind.Domain/MyMind.Domain.csproj src/MyMind.Domain/
COPY src/MyMind.Infrastructure/MyMind.Infrastructure.csproj src/MyMind.Infrastructure/
# Optional extra root CA (e.g. a TLS-inspecting corporate proxy); empty in CI. See ci-cd/.env.example.
RUN --mount=type=secret,id=extra_ca \
    if [ -s /run/secrets/extra_ca ]; then \
      cp /run/secrets/extra_ca /usr/local/share/ca-certificates/extra-ca.crt && update-ca-certificates; \
    fi && \
    dotnet restore src/MyMind.Api/MyMind.Api.csproj

COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/MyMind.Api/MyMind.Api.csproj -c Release -o /app --no-restore

FROM build AS bundle
RUN dotnet tool install --global dotnet-ef --version 10.0.13
RUN /root/.dotnet/tools/dotnet-ef migrations bundle \
      --project src/MyMind.Infrastructure --startup-project src/MyMind.Api \
      --configuration Release --output /efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /efbundle .
USER $APP_UID
ENTRYPOINT ["sh", "-c", "exec ./efbundle --connection \"$ConnectionStrings__Default\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Volume mount points owned by the non-root app user, so named volumes inherit the ownership.
RUN mkdir -p /keys /uploads && chown "$APP_UID" /keys /uploads

COPY --from=build /app .

USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyMind.Api.dll"]
