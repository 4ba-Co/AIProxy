# KestrelAIProxy

English | [中文](README_CN.md)

A high-performance AI service proxy gateway built with .NET 9 and YARP (Yet Another Reverse Proxy). KestrelAIProxy provides a unified API gateway for multiple AI service providers, enabling seamless routing and management of AI API requests.

## 🚀 Features

- **Multi-Provider Support**: Unified gateway for 20+ AI service providers
  - OpenAI, Anthropic, Google AI Studio, Google Vertex AI
  - Azure OpenAI, AWS Bedrock, Cohere, Groq
  - Mistral, DeepSeek, Perplexity AI, Hugging Face
  - ElevenLabs, Replicate, Vercel AI, and more...

- **High Performance**: Built on .NET 9 with Kestrel server and YARP reverse proxy
- **Path-Based Routing**: Intelligent request routing based on URL patterns
- **Header Management**: Automatic header transformation and forwarding
- **Docker Support**: Ready-to-deploy Docker container
- **Extensible Architecture**: Easy to add new AI providers
- **Production Ready**: Built-in logging, error handling, and monitoring

## 🏗️ Architecture

```
Client Request → PathPatternMiddleware → ProviderRouter → ProviderStrategy → AiGatewayMiddleware → Target AI Service
```

## 📦 Installation

### Prerequisites
- .NET 9.0 SDK
- Docker (optional)

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

### Examples

**OpenAI API**
```bash
# Chat completions
curl -X POST "http://localhost:5501/openai/v1/chat/completions" \
  -H "Authorization: Bearer your-openai-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'

# Models list
curl "http://localhost:5501/openai/v1/models" \
  -H "Authorization: Bearer your-openai-key"
```

**Anthropic API**
```bash
# Claude messages
curl -X POST "http://localhost:5501/anthropic/v1/messages" \
  -H "x-api-key: your-anthropic-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "claude-3-sonnet-20240229", "max_tokens": 1024, "messages": [{"role": "user", "content": "Hello"}]}'
```

**Google AI Studio**
```bash
# Generate content
curl -X POST "http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent" \
  -H "Authorization: Bearer your-google-key" \
  -H "Content-Type: application/json" \
  -d '{"contents": [{"parts": [{"text": "Hello"}]}]}'
```

## 🛠️ Configuration

The proxy automatically routes requests based on the provider name in the URL path. No additional configuration is required for basic usage.

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
├── KestrelAIProxy.AIGateway/       # Gateway middleware and strategies
│   ├── ProviderStrategies/         # AI provider implementations
│   └── Extensions/                 # Service extensions
└── KestrelAIProxy.Common/          # Shared utilities
```

### Adding a New Provider

1. Create a new strategy class in `ProviderStrategies/`
2. Implement `IProviderStrategy` interface
3. Automatically register the strategy in `AiGatewayExtensions.cs`

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

- **Latency**: Minimal overhead (< 5ms typical)
- **Throughput**: High-performance reverse proxy with connection pooling
- **Memory**: Efficient memory usage with .NET 9 optimizations
- **Scalability**: Horizontal scaling support via load balancers

## 🔒 Security

- Header sanitization (removes forwarding headers)
- Request validation and error handling
- No sensitive data logging
- HTTPS support for production deployments

## 📝 License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
