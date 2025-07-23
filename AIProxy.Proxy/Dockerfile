FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5501
EXPOSE 5502

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AIProxy.Proxy/AIProxy.Proxy.csproj", "AIProxy.Proxy/"]
RUN dotnet restore "AIProxy.Proxy/AIProxy.Proxy.csproj"
COPY . .
WORKDIR "/src/AIProxy.Proxy"
RUN dotnet build "./AIProxy.Proxy.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AIProxy.Proxy.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AIProxy.Proxy.dll"]
