FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS build
WORKDIR /src
COPY global.json NuGet.Config ./
COPY backend/Directory.Build.props backend/
COPY backend/LanguageLearning.Core backend/LanguageLearning.Core
COPY backend/LanguageLearning.Infrastructure backend/LanguageLearning.Infrastructure
COPY backend/LanguageLearning.Api backend/LanguageLearning.Api
RUN dotnet restore backend/LanguageLearning.Api/LanguageLearning.Api.csproj --locked-mode
RUN dotnet publish backend/LanguageLearning.Api/LanguageLearning.Api.csproj \
    --configuration Release --no-restore --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e
WORKDIR /app
COPY --from=build /publish ./
RUN mkdir -p /var/lib/lexigo/keys && chown "$APP_UID" /var/lib/lexigo/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "LanguageLearning.Api.dll"]
