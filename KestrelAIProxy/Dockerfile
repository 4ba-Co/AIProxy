FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-preview-noble-chiseled AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5501

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-noble-aot AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY --link . .
RUN dotnet restore "./KestrelAIProxy/KestrelAIProxy.csproj"
WORKDIR "/src/KestrelAIProxy"
RUN dotnet build "./KestrelAIProxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./KestrelAIProxy.csproj" -c $BUILD_CONFIGURATION -r linux-x64 -o /app/publish /p:UseAppHost=false && rm /app/publish/*.dbg /app/publish/*.Development.json

FROM base AS final
WORKDIR /app
COPY --link --from=publish /app/publish .
ENTRYPOINT ["./KestrelAIProxy"]
