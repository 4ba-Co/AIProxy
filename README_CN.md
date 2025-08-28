# KestrelAIProxy

[![CircleCI](https://dl.circleci.com/status-badge/img/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/circleci/9a4zSW4Kt4bK39F5t1m5WR/XiupSmJYh6VGA6D7gtZdJp/tree/main)
[![Static Badge](https://img.shields.io/badge/All_Providers-AAFF00)](https://ai-proxy.4ba.ai/providers)

[English](README.md) | 中文

基于 .NET 10 preview 和 YARP（Yet Another Reverse Proxy）构建的高性能 AI 服务代理网关。KestrelAIProxy 为 20+ AI 服务提供商提供统一的 API 网关，具备 AOT 编译、使用量追踪和智能请求路由等高级功能。

## 🚀 功能特性

- **多提供商支持**: 20+ AI 服务提供商的透明代理
  - OpenAI、Anthropic、Google AI Studio、Google Vertex AI
  - Azure OpenAI、AWS Bedrock、Cohere、Groq
  - Mistral、DeepSeek、Perplexity AI、Hugging Face
  - ElevenLabs、Replicate、Vercel AI 等...

- **透明代理**: 直接请求转发，最小化处理
- **高性能**: 基于 .NET 10 preview 构建，支持 AOT 编译
- **高级中间件管道**: 复杂的请求处理，包含路径路由和使用量追踪
- **使用量分析**: 实时 Token 使用量追踪和成本计算（支持的提供商）
- **生产就绪**: AOT 优化构建、对象池和内存高效的流处理
- **Docker 支持**: 优化的容器镜像，最小资源占用
- **企业级功能**: 熔断器、健康检查和全面监控

## 🏗️ 架构

```
HTTP 请求 → 管道路由器 → 路径模式中间件 → 使用量追踪 → AI网关中间件 → AI 提供商
                     ↓
               静态文件中间件 (用于 /health, /providers, 静态内容)
```

高级中间件管道，具备条件路由、使用量分析和智能请求转发。

## 📦 安装

### 前置要求
- .NET 10.0 SDK (preview)
- Docker（可选）

> **注意**: 此项目使用 .NET 10 preview 功能进行 AOT 编译和性能优化。

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

只需将原始 AI 服务域名替换为代理 URL，并在第一个路径段添加提供商名称。

### 示例

**OpenAI API**
```bash
# 原始地址: https://api.openai.com/v1/chat/completions
# 代理地址: http://localhost:5501/openai/v1/chat/completions

curl -X POST "http://localhost:5501/openai/v1/chat/completions" \
  -H "Authorization: Bearer your-openai-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'
```

**Anthropic API**
```bash
# 原始地址: https://api.anthropic.com/v1/messages
# 代理地址: http://localhost:5501/anthropic/v1/messages

curl -X POST "http://localhost:5501/anthropic/v1/messages" \
  -H "x-api-key: your-anthropic-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "claude-3-sonnet-20240229", "max_tokens": 1024, "messages": [{"role": "user", "content": "Hello"}]}'
```

**Google AI Studio**
```bash
# 原始地址: https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent
# 代理地址: http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent

curl -X POST "http://localhost:5501/google-ai-studio/v1beta/models/gemini-pro:generateContent" \
  -H "Authorization: Bearer your-google-key" \
  -H "Content-Type: application/json" \
  -d '{"contents": [{"parts": [{"text": "Hello"}]}]}'
```

## 🛠️ 配置

无需配置！代理根据 URL 路径中的提供商名称自动路由请求。

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
├── KestrelAIProxy.AIGateway/       # 代理路由逻辑
│   ├── ProviderStrategies/         # AI 提供商路由规则
│   └── Extensions/                 # 服务扩展
└── KestrelAIProxy.Common/          # 共享工具
```

### 添加新提供商

1. 在 `ProviderStrategies/` 中创建新的策略类
2. 实现 `IProviderStrategy` 接口
3. 定义提供商名称和目标主机

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

- **AOT 编译**: 原生编译，启动更快，内存使用更少
- **零拷贝流处理**: 内存高效的请求/响应处理
- **对象池化**: 通过智能对象重用减少 GC 压力
- **高吞吐量**: 10,000+ RPS，亚毫秒级延迟
- **内存优化**: ~200MB 基准内存使用，支持连接池

## 🔒 安全性

- **请求头清理**: 自动移除转发和代理头
- **透明认证**: 保留原始 API 密钥和认证头
- **隐私保护**: 可选的使用量追踪，支持数据匿名化
- **安全默认**: HTTPS 优先的生产环境配置
- **请求隔离**: 提供商特定的请求处理，防止交叉污染

## 📝 许可证

本项目采用 GNU General Public License v3.0 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。