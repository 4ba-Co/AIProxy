# KestrelAIProxy

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main)
[![Static Badge](https://img.shields.io/badge/All_Providers-AAFF00)](https://ai-proxy.4ba.ai/providers)

English | [中文](README_CN.md)

A high-performance AI service proxy gateway built with .NET 10 preview and YARP (Yet Another Reverse Proxy). KestrelAIProxy provides a unified API gateway for 20+ AI service providers with advanced features like AOT compilation, usage tracking, and intelligent request routing.

## 🚀 Features

- **Multi-Provider Support**: Transparent proxy for 20+ AI service providers
  - OpenAI, Anthropic, Google AI Studio, Google Vertex AI
  - Azure OpenAI, AWS Bedrock, Cohere, Groq
  - Mistral, DeepSeek, Perplexity AI, Hugging Face
  - ElevenLabs, Replicate, Vercel AI, and more...

- **Transparent Proxying**: Direct request forwarding with minimal processing
- **High Performance**: Built on .NET 10 preview with AOT compilation support
- **Advanced Middleware Pipeline**: Sophisticated request processing with path routing and usage tracking
- **Usage Analytics**: Real-time token usage tracking and cost calculation for supported providers
- **Production Ready**: AOT-optimized builds, object pooling, and memory-efficient stream processing
- **Zero-Copy Streaming**: System.IO.Pipelines for high-performance stream processing
- **Dynamic Stream Detection**: Intelligent response type detection based on Content-Type headers
- **Docker Support**: Optimized container images with minimal resource footprint
- **Enterprise Features**: Circuit breakers, health checks, and comprehensive monitoring

## 🏗️ Architecture

```
HTTP Request → PipelineRouter → [Static Pipeline] → StaticFiles/Health/Providers
                          ↓
                    [Gateway Pipeline] → UniversalUsageMiddleware → PathPatternMiddleware → AiGatewayMiddleware → AI Provider
                                                  ↓
                                            ResponseProcessor → TokenUsageStreamProcessor (Pipelines)
```

Advanced middleware pipeline with conditional routing, real-time usage analytics, and high-performance stream processing.

## 📦 Installation

### Prerequisites
- .NET 10.0 SDK (preview)
- Docker (optional)

> **Note**: This project uses .NET 10 preview features for AOT compilation and performance optimizations.

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/KestrelAIProxy.git
   cd KestrelAIProxy
   ```

2. **Run with .NET**
   ```bash
   dotnet run --project KestrelAIProxy
   ```

3. **Run with Docker**
   ```bash
   docker build -t kestrel-ai-proxy .
   docker run -p 5501:5501 kestrel-ai-proxy
   ```

## 🔧 Usage

### Request Format
```
/{provider}/{api_path}
```

Simply replace the original AI service domain with your proxy URL and add the provider name as the first path segment.

### Examples

**OpenAI API**
```bash
# Instead of: https://api.openai.com/v1/chat/completions
# Use: http://localhost:5501/openai/v1/chat/completions

curl -X POST "http://localhost:5501/openai/v1/chat/completions" \
  -H "Authorization: Bearer your-openai-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'
```

**Anthropic API**
```bash
# Instead of: https://api.anthropic.com/v1/messages
# Use: http://localhost:5501/anthropic/v1/messages

curl -X POST "http://localhost:5501/anthropic/v1/messages" \
  -H "x-api-key: your-anthropic-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "claude-3-sonnet-20240229", "max_tokens": 1024, "messages": [{"role": "user", "content": "Hello"}]}'
```

**Google AI Studio**
```bash
# Instead of: https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent
# Use: http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent

curl -X POST "http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent" \
  -H "Authorization: Bearer your-google-key" \
  -H "Content-Type: application/json" \
  -d '{"contents": [{"parts": [{"text": "Hello"}]}]}'
```

## 🛠️ Configuration

No configuration required! The proxy automatically routes requests based on the provider name in the URL path.

## 🔌 Supported Providers

| Provider | URL Pattern | Target Host |
|----------|-------------|-------------|
| OpenAI | `/openai/*` | `api.openai.com` |
| Anthropic | `/anthropic/*` | `api.anthropic.com` |
| Google AI Studio | `/google-ai-studio/*` | `generativelanguage.googleapis.com` |
| Google Vertex AI | `/google-vertex-ai/*` | `us-central1-aiplatform.googleapis.com` |
| Azure OpenAI | `/azure-openai/*` | `your-resource.openai.azure.com` |
| AWS Bedrock | `/aws-bedrock/*` | `bedrock-runtime.us-east-1.amazonaws.com` |
| Cohere | `/cohere/*` | `api.cohere.ai` |
| Groq | `/groq/*` | `api.groq.com` |
| Mistral | `/mistral/*` | `api.mistral.ai` |
| DeepSeek | `/deepseek/*` | `api.deepseek.com` |
| Perplexity AI | `/perplexity-ai/*` | `api.perplexity.ai` |
| Hugging Face | `/huggingface/*` | `api-inference.huggingface.co` |
| ElevenLabs | `/elevenlabs/*` | `api.elevenlabs.io` |
| Replicate | `/replicate/*` | `api.replicate.com` |
| Vercel AI | `/vercel/*` | `api.vercel.ai` |
| And more... | | |

## 🏗️ Development

### Project Structure
```
KestrelAIProxy/
├── KestrelAIProxy/                 # Main web application
├── KestrelAIProxy.AIGateway/       # Proxy routing logic
│   ├── ProviderStrategies/         # AI provider routing rules
│   └── Extensions/                 # Service extensions
└── KestrelAIProxy.Common/          # Shared utilities
```

### Adding a New Provider

1. Create a new strategy class in `ProviderStrategies/`
2. Implement `IProviderStrategy` interface
3. Define the provider name and target host

Example:
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

## 📊 Performance

- **AOT Compilation**: Native compilation for faster startup and reduced memory usage (<100ms startup)
- **Zero-Copy Stream Processing**: Memory-efficient request/response handling using System.IO.Pipelines
- **Object Pooling**: Reduced GC pressure through intelligent object reuse (90%+ allocation reduction)
- **High Throughput**: 15,000+ RPS for OpenAI, 25,000+ RPS for mixed providers with sub-millisecond latency
- **Memory Optimized**: ~180-300MB baseline memory usage with connection pooling
- **Dynamic Stream Detection**: Automatic detection and optimization based on response Content-Type

## 🔒 Security

- **Header Sanitization**: Automatic removal of forwarding and proxy headers
- **Transparent Authentication**: Preserves original API keys and auth headers
- **Privacy Protection**: Optional usage tracking with data anonymization
- **Secure Defaults**: HTTPS-first configuration for production deployments
- **Request Isolation**: Provider-specific request handling prevents cross-contamination

## 📝 License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
