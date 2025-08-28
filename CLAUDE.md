# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KestrelAIProxy is a high-performance AI service proxy gateway built with .NET 10 preview and YARP (Yet Another Reverse Proxy). It provides a unified API gateway for 20+ AI service providers with path-based routing. The project is configured for AOT compilation with speed optimization.

## Common Development Commands

### Build and Run
```bash
# Build the entire solution
dotnet build

# Run the main application
dotnet run --project KestrelAIProxy

# Run with specific configuration
dotnet run --project KestrelAIProxy --environment Development
```

### Docker
```bash
# Build Docker image
docker build -t kestrel-ai-proxy .

# Run with Docker
docker run -p 5501:5501 kestrel-ai-proxy
```

### Testing and Quality
- No test projects are currently present in the solution
- No explicit lint commands configured in project files
- Project uses AOT compilation analyzers and trim analyzers for code quality

### Publishing
```bash
# Publish with AOT compilation (optimized for production)
dotnet publish --configuration Release --self-contained true

# Publish for specific runtime
dotnet publish -r linux-x64 --configuration Release --self-contained true
```

## Architecture

The application follows a middleware-based architecture with the following flow:
```
Client Request → PathPatternMiddleware → ProviderRouter → ProviderStrategy → AiGatewayMiddleware → Target AI Service
```

### Core Components

**Middleware Pipeline** (KestrelAIProxy.AIGateway/):
- `PathPatternMiddleware` - Parses incoming requests and extracts provider information
- `AiGatewayMiddleware` - Handles the actual proxying using YARP forwarder

**Provider System**:
- `IProviderStrategy` - Interface for provider-specific logic
- `ProviderStrategies/` - Individual strategy implementations for each AI provider (20+ providers)
- `IProviderRouter` - Routes requests to appropriate provider strategies

**Key Interfaces**:
- `IPathParser` - Parses request paths to extract provider and segments
- `IPathValidator` - Validates parsed paths
- `IResultBuilder` - Creates standardized parse results
- `IPathBuilder` - Builds target URIs for providers

### Project Structure
- `KestrelAIProxy/` - Main web application and entry point
- `KestrelAIProxy.AIGateway/` - Core gateway functionality and provider strategies
- `KestrelAIProxy.Common/` - Shared utilities and extensions

## Adding New AI Providers

1. Create a new strategy class in `KestrelAIProxy.AIGateway/ProviderStrategies/`
2. Implement `IProviderStrategy` interface with:
   - `ProviderName` property (used in URL routing as `/{provider-name}/...`)
   - `ParseAsync` method that returns `ParseResult` with target host and path mapping
3. The strategy is automatically registered via reflection in `AiGatewayExtensions.cs`

Example provider strategy:
```csharp
public sealed class NewProviderStrategy : IProviderStrategy
{
    public string ProviderName => "new-provider";
    
    public Task<ParseResult> ParseAsync(HttpContext context, ParsedPath parsedPath)
    {
        return Task.FromResult(resultBuilder.CreateSuccessResult(
            providerName: ProviderName,
            targetHost: "api.newprovider.com",
            pathSegments: parsedPath.ProviderSegments,
            queryString: parsedPath.QueryString,
            additionalHeaders: [],
            additionalMetadata: []));
    }
}
```

## Key Configuration

- **Framework**: .NET 10.0 preview (with AOT compilation support)
- **Main Dependencies**: YARP.ReverseProxy (2.3.0), Serilog
- **Optimization**: Configured for speed optimization with trim analysis
- **Port**: 5501 (default)
- **Request Format**: `/{provider}/{api_path}`
- **Health Check**: `/health`
- **Provider List**: `/providers` (returns all registered provider names)

## Request Flow

1. Request comes in with format `/{provider}/{api_path}`
2. `PathPatternMiddleware` parses the path to extract provider name and remaining segments
3. `IProviderRouter` finds matching `IProviderStrategy` by provider name
4. Strategy creates `ParseResult` with target host and transformed path
5. `AiGatewayMiddleware` forwards request to target AI service using YARP
6. Response is proxied back to client

## Special Features

- Automatic header sanitization (removes forwarding headers like X-Forwarded-*)
- Custom header injection per provider via strategies
- Connection pooling and HTTP/2 support
- Configurable timeouts (300s activity timeout, 5s connect timeout)
- Comprehensive logging with Serilog