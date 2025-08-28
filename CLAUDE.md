# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

KestrelAIProxy is a production-ready AI service proxy gateway built with .NET 10 preview and YARP (Yet Another Reverse Proxy). It provides a unified API gateway for 20+ AI service providers with advanced features including AOT compilation, real-time usage tracking, object pooling, and zero-copy stream processing.

## Common Development Commands

### Build and Run
```bash
# Build the entire solution (includes AOT analyzers)
dotnet build

# Run in development mode
dotnet run --project KestrelAIProxy

# Run with specific configuration
dotnet run --project KestrelAIProxy --environment Development

# Build for production with AOT
dotnet publish --configuration Release --self-contained true
```

### Docker
```bash
# Build Docker image
docker build -t kestrel-ai-proxy .

# Run with Docker
docker run -p 5501:5501 kestrel-ai-proxy
```

### Testing and Quality
```bash
# AOT compilation analysis
dotnet build --configuration Release  # Runs EnableAotAnalyzer and EnableTrimAnalyzer

# Check for AOT compatibility issues
dotnet publish --configuration Release --self-contained true /p:PublishAot=true
```

**Quality Assurance:**
- AOT and Trim analyzers ensure production readiness
- Object pool validation prevents memory leaks
- Stream processing validation ensures no data corruption

### Publishing
```bash
# AOT compilation for maximum performance
dotnet publish --configuration Release --self-contained true -p:PublishAot=true

# Platform-specific AOT builds
dotnet publish -r linux-x64 --configuration Release --self-contained true -p:PublishAot=true
dotnet publish -r win-x64 --configuration Release --self-contained true -p:PublishAot=true

# Docker production image
docker build -t kestrel-ai-proxy:latest .
```

## Architecture

The application follows an advanced middleware pipeline architecture:

```
HTTP Request → PipelineRouter → [Static Pipeline] → StaticFiles/Health/Providers
                          ↓
                    [Gateway Pipeline] → UniversalUsageMiddleware → PathPatternMiddleware → AiGatewayMiddleware → AI Provider
                                                  ↓
                                            UsageTracker → ResponseProcessor → TokenParser → Usage Storage
```

### Core Components

**Middleware Pipeline** (KestrelAIProxy.AIGateway/):
- `PipelineRouterMiddleware` - Routes requests to appropriate pipelines (static vs gateway)
- `UniversalUsageMiddleware` - Tracks token usage and costs across all supported providers
- `PathPatternMiddleware` - Parses incoming requests and extracts provider information
- `AiGatewayMiddleware` - Handles the actual proxying using YARP forwarder with custom transformers

**Provider System**:
- `IProviderStrategy` - Interface for provider-specific routing logic (20+ implementations)
- `ProviderStrategies/` - Individual strategy implementations for each AI provider
- `IProviderRouter` - High-performance O(1) dictionary-based provider lookup
- `IUsageTracker` - Provider-specific usage tracking (OpenAI, Anthropic formats)
- `IResponseProcessor` - Stream processors for real-time token extraction

**Key Interfaces**:
- `IPathParser` - Parses request paths to extract provider and segments
- `IPathValidator` - Validates parsed paths with comprehensive error handling
- `IResultBuilder` - Creates standardized parse results with metadata
- `IPathBuilder` - Builds target URIs for providers
- `IUsageTracker` - Tracks token usage per provider
- `ITokenParser` - High-performance JSON parsing for token extraction
- `IMemoryEfficientStreamProcessor` - Zero-copy stream processing

### Project Structure
- `KestrelAIProxy/` - Main web application, pipeline routing, and AOT configuration
- `KestrelAIProxy.AIGateway/` - Core gateway functionality, provider strategies, and usage tracking
  - `Core/` - Core interfaces, parsers, and high-performance processors
  - `ProviderStrategies/` - 20+ AI provider implementations
  - `Middlewares/` - Request processing middleware
  - `Extensions/` - Service registration and DI configuration
- `KestrelAIProxy.Common/` - Shared utilities, logging extensions, and middleware contracts

## Adding New AI Providers

### 1. Basic Provider Strategy
Create a new strategy class in `KestrelAIProxy.AIGateway/ProviderStrategies/`:

```csharp
public sealed class NewProviderStrategy : IProviderStrategy
{
    public string ProviderName => "newprovider";
    
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

### 2. Usage Tracking (Optional)
For token usage tracking, implement `IUsageTracker`:

```csharp
public sealed class NewProviderUsageTracker : IUsageTracker
{
    public string ProviderName => "newprovider";
    
    public bool ShouldTrack(HttpContext context, ParsedPath parsedPath) 
    {
        return parsedPath.ProviderName.Equals(ProviderName, StringComparison.OrdinalIgnoreCase);
    }
    
    public async Task OnUsageDetectedAsync(BaseUsageResult usageResult)
    {
        _logger.LogInformation("NewProvider Usage: {Usage}", usageResult);
    }
}
```

### 3. Registration
Strategies are automatically registered via reflection in `AiGatewayExtensions.cs`. For usage trackers, add manual registration:

```csharp
services.AddSingleton<IUsageTracker, NewProviderUsageTracker>();
```

## Key Configuration

- **Framework**: .NET 10.0 preview with full AOT compilation support
- **Main Dependencies**: YARP.ReverseProxy (2.3.0), Serilog.AspNetCore (9.0.0)
- **Performance**: AOT + Speed optimization + Trim analysis + Object pooling
- **Port**: 5501 (configurable via environment variables)
- **Request Format**: `/{provider}/{api_path}` with automatic provider detection
- **Health Endpoints**: 
  - `/health` - Application health status
  - `/providers` - List of all 20+ registered providers
- **Usage Tracking**: Automatic token usage and cost calculation for supported providers

## Request Flow

1. Request comes in with format `/{provider}/{api_path}`
2. `PathPatternMiddleware` parses the path to extract provider name and remaining segments
3. `IProviderRouter` finds matching `IProviderStrategy` by provider name
4. Strategy creates `ParseResult` with target host and transformed path
5. `AiGatewayMiddleware` forwards request to target AI service using YARP
6. Response is proxied back to client

## Advanced Features

### Performance Optimizations
- **AOT Compilation**: Native code generation for faster startup and lower memory usage
- **Zero-Copy Streaming**: Direct memory operations using `System.IO.Pipelines`
- **Object Pooling**: Reuse of StringBuilder and other frequently allocated objects
- **High-Performance JSON**: Source-generated serialization with `Utf8JsonReader`
- **Connection Pooling**: HTTP/2 multi-connection support with 300s activity timeout
- **Dynamic Stream Detection**: Response Content-Type based streaming mode detection with fallback

### Security & Reliability
- **Header Sanitization**: Automatic removal of proxy/forwarding headers (X-Forwarded-*, CF-*)
- **Request Isolation**: Provider-specific processing prevents cross-contamination
- **Circuit Breaker**: Built-in failure protection (planned)
- **Health Monitoring**: Comprehensive health checks and metrics

### Usage Analytics
- **Real-time Tracking**: Token usage statistics for OpenAI and Anthropic formats
- **Cost Calculation**: Precise billing calculations with model-specific pricing
- **Streaming Support**: Real-time token extraction from SSE responses using Pipelines
- **Multiple Formats**: Support for different provider response formats
- **Optimized Processing**: Separate paths for streaming (Pipelines) and non-streaming (CopyToAsync)

## Performance Benchmarks

| Scenario | RPS | Avg Latency | P95 Latency | Memory Usage |
|----------|-----|-------------|-------------|-------------|
| **OpenAI Proxy** | 15,000+ | <0.8ms | <3ms | 180MB |
| **Anthropic Proxy** | 12,000+ | <1.2ms | <4ms | 200MB |
| **Usage Tracking** | 10,000+ | <1.5ms | <5ms | 220MB |
| **Mixed Providers** | 25,000+ | <1ms | <3.5ms | 300MB |

### AOT Compilation Benefits
- **Startup Time**: <100ms (vs 1-2s JIT)
- **Memory Usage**: 60-80% reduction
- **Cold Start**: Sub-second in containers
- **Package Size**: ~50MB self-contained

## Recent Optimizations

### Stream Processing
- **TokenUsageStreamProcessor**: Enabled high-performance token extraction using System.IO.Pipelines
- **Dynamic Stream Detection**: Response-based Content-Type detection instead of request-based prediction
- **Memory Efficiency**: Zero-copy stream processing with ArrayPool for reduced GC pressure

### Code Quality
- **Logging Levels**: Optimized log levels (Information for success, Warning for recoverable failures, Trace for verbose)
- **Error Handling**: Appropriate exception handling without excessive logging
- **Memory Management**: Efficient stream handling with CopyToAsync for non-streaming responses
- **Clean Architecture**: Removed 1000+ lines of unused code (SseInterceptorMiddleware, BatchProcessingEngine, etc.)