FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0-preview AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5501

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY --link . .
RUN dotnet restore "./KestrelAIProxy/KestrelAIProxy.csproj"
WORKDIR "/src/KestrelAIProxy"
RUN dotnet build "./KestrelAIProxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./KestrelAIProxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --link --from=publish /app/publish .
ENTRYPOINT ["dotnet", "KestrelAIProxy.dll"]
