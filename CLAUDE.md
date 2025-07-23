# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AIProxy is a .NET 9.0 reverse proxy system that routes AI service requests through different gateways based on reverse modes. The solution consists of three projects:

- **AIProxy.Proxy**: The main reverse proxy service using YARP (Yet Another Reverse Proxy)
- **AIProxy.CCenter**: Configuration center with SignalR hub for token validation and provider management
- **AIProxy.Common**: Shared models and enums

## Development Commands

### Building the Solution
```bash
dotnet build AIProxy.sln
```

### Running Services
```bash
# Run the proxy service
cd AIProxy.Proxy
dotnet run

# Run the configuration center
cd AIProxy.CCenter
dotnet run
```

### Building with Docker
```bash
# Build proxy service
docker build -f AIProxy.Proxy/Dockerfile .

# Build configuration center
docker build -f AIProxy.CCenter/Dockerfile .
```

### Database Operations
The CCenter service uses Entity Framework Core with PostgreSQL. Connection string should be configured in appsettings.json under "Supabase".

## Architecture

### Reverse Proxy Modes
The system supports three reverse modes defined in `ReverseMode` enum:
- **HongKong2Singapore**: Routes Hong Kong traffic to Singapore proxy
- **SingaporeGateway**: Routes through Singapore Cloudflare gateway
- **AmericaGateway**: Routes through North America Cloudflare gateway

### Route Configuration
Routes are dynamically built by `ProxyRoutesFactory` based on the configured reverse mode. All routes follow the pattern `/{provider}/{**else}` and include transforms for header management and path rewriting.

### Token Authentication
The CCenter service validates user request tokens via:
- Database lookup with expiration checking
- Hybrid caching for performance
- SignalR hub for real-time token validation

### Configuration Management
Both services use:
- `appsettings.json` for base configuration
- `appsettings.Development.json` for development overrides
- Environment-specific settings for database connections

### SignalR Communication
The CCenter exposes a SignalR hub at `/configHub` with methods:
- `SearchToken(string token)`: Validates user tokens
- `GetProviders()`: Retrieves available AI providers

### SSL Certificates
The Proxy service includes SSL certificates in the `certs/` directory that are copied to output during build.