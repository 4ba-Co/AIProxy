# KestrelAIProxy

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main)
[![Static Badge](https://img.shields.io/badge/All_Providers-AAFF00)](https://ai-proxy.4ba.ai/providers)

English | [中文](README_CN.md)

A simple and transparent AI service proxy built with .NET 9 and YARP (Yet Another Reverse Proxy). KestrelAIProxy provides transparent proxying for multiple AI service providers with minimal overhead and configuration.

## 🚀 Features

- **Multi-Provider Support**: Transparent proxy for 20+ AI service providers
  - OpenAI, Anthropic, Google AI Studio, Google Vertex AI
  - Azure OpenAI, AWS Bedrock, Cohere, Groq
  - Mistral, DeepSeek, Perplexity AI, Hugging Face
  - ElevenLabs, Replicate, Vercel AI, and more...

- **Transparent Proxying**: Direct request forwarding with minimal processing
- **High Performance**: Built on .NET 9 with Kestrel server and YARP reverse proxy
- **Path-Based Routing**: Simple URL pattern-based request routing
- **Zero Configuration**: Works out of the box with no setup required
- **Docker Support**: Ready-to-deploy Docker container
- **Lightweight**: Minimal resource usage and fast startup

## 🏗️ Architecture

```
Client Request → Path Router → Target AI Service
```

Simple and transparent - requests are routed based on URL patterns and forwarded directly to the target AI service.

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

- **Low Latency**: Minimal proxy overhead
- **High Throughput**: Efficient request forwarding via YARP
- **Lightweight**: Small memory footprint
- **Fast Startup**: Quick application initialization

## 🔒 Security

- **Transparent Headers**: Preserves original authentication headers
- **Direct Forwarding**: No request/response modification or logging
- **Secure Defaults**: HTTPS support for production deployments

## 📝 License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
