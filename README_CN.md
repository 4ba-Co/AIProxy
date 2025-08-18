# KestrelAIProxy

[English](README.md) | 中文


基于 .NET 9 和 YARP（Yet Another Reverse Proxy）构建的高性能 AI 服务代理网关。KestrelAIProxy 为多个 AI 服务提供商提供统一的 API 网关，实现 AI API 请求的无缝路由和管理。

## 🚀 功能特性

- **多提供商支持**: 20+ AI 服务提供商的统一网关
  - OpenAI、Anthropic、Google AI Studio、Google Vertex AI
  - Azure OpenAI、AWS Bedrock、Cohere、Groq
  - Mistral、DeepSeek、Perplexity AI、Hugging Face
  - ElevenLabs、Replicate、Vercel AI 等...

- **高性能**: 基于 .NET 9 和 Kestrel 服务器构建，使用 YARP 反向代理
- **基于路径的路由**: 基于 URL 模式的智能请求路由
- **请求头管理**: 自动请求头转换和转发
- **Docker 支持**: 开箱即用的 Docker 容器
- **可扩展架构**: 易于添加新的 AI 提供商
- **生产就绪**: 内置日志记录、错误处理和监控

## 🏗️ 架构

```
客户端请求 → PathPatternMiddleware → ProviderRouter → ProviderStrategy → AiGatewayMiddleware → 目标 AI 服务
```

## 📦 安装

### 前置要求
- .NET 9.0 SDK
- Docker（可选）

### 快速开始

1. **克隆仓库**
   ```bash
   git clone https://github.com/yourusername/KestrelAIProxy.git
   cd KestrelAIProxy
   ```

2. **使用 .NET 运行**
   ```bash
   dotnet run --project KestrelAIProxy
   ```

3. **使用 Docker 运行**
   ```bash
   docker build -t kestrel-ai-proxy .
   docker run -p 5501:5501 kestrel-ai-proxy
   ```

## 🔧 使用方法

### 请求格式
```
/{provider}/{api_path}
```

### 示例

**OpenAI API**
```bash
# 聊天补全
curl -X POST "http://localhost:5501/openai/v1/chat/completions" \
  -H "Authorization: Bearer your-openai-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'

# 模型列表
curl "http://localhost:5501/openai/v1/models" \
  -H "Authorization: Bearer your-openai-key"
```

**Anthropic API**
```bash
# Claude 消息
curl -X POST "http://localhost:5501/anthropic/v1/messages" \
  -H "x-api-key: your-anthropic-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "claude-3-sonnet-20240229", "max_tokens": 1024, "messages": [{"role": "user", "content": "Hello"}]}'
```

**Google AI Studio**
```bash
# 生成内容
curl -X POST "http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent" \
  -H "Authorization: Bearer your-google-key" \
  -H "Content-Type: application/json" \
  -d '{"contents": [{"parts": [{"text": "Hello"}]}]}'
```

## 🛠️ 配置

代理根据 URL 路径中的提供商名称自动路由请求。基本使用无需额外配置。

## 🔌 支持的提供商

| 提供商 | URL 模式 | 目标主机 |
|--------|----------|----------|
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
| 更多... | | |

## 🏗️ 开发

### 项目结构
```
KestrelAIProxy/
├── KestrelAIProxy/                 # 主 Web 应用程序
├── KestrelAIProxy.AIGateway/       # 网关中间件和策略
│   ├── ProviderStrategies/         # AI 提供商实现
│   └── Extensions/                 # 服务扩展
└── KestrelAIProxy.Common/          # 共享工具
```

### 添加新提供商

1. 在 `ProviderStrategies/` 中创建新的策略类
2. 实现 `IProviderStrategy` 接口
3. 在 `AiGatewayExtensions.cs` 中自动注册策略

示例:
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

## 📊 性能

- **延迟**: 最小开销（通常 < 5ms）
- **吞吐量**: 高性能反向代理，支持连接池
- **内存**: 利用 .NET 9 优化实现高效内存使用
- **可扩展性**: 通过负载均衡器支持水平扩展

## 🔒 安全性

- 请求头清理（移除转发头）
- 请求验证和错误处理
- 无敏感数据日志记录
- 生产环境部署支持 HTTPS

## 📝 许可证

本项目采用 GNU General Public License v3.0 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。
