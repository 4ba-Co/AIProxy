# Token 使用量追踪系统

## 系统概述

KestrelAIProxy 集成了先进的 Token 使用量追踪系统，支持多个 AI 提供商的实时统计和成本计算。该系统采用高性能的零拷贝流处理技术，确保对主要代理性能无影响。

## 支持的提供商

### OpenAI 格式 (Token 统计)
支持所有兼容 OpenAI API 格式的提供商：
- OpenAI、Azure OpenAI、Together、Groq 等

**统计内容:**
- `prompt_tokens` - 输入 Token 数量
- `completion_tokens` - 输出 Token 数量  
- `total_tokens` - 总 Token 数量
- `cached_tokens` - 缓存 Token 数量（如可用）
- `audio_tokens` - 音频 Token 数量（如可用）
- `reasoning_tokens` - 推理 Token 数量（如可用）

### Anthropic 格式 (Token 统计 + 精确计费)
专门针对 Anthropic Claude 模型的完整追踪：

**统计内容:**
- `input_tokens` - 输入 Token 数量
- `output_tokens` - 输出 Token 数量
- `cache_creation_input_tokens` - 缓存创建 Token
- `cache_read_input_tokens` - 缓存读取 Token
- 完整的成本分解（输入/输出/缓存创建/缓存读取成本）

**支持的模型及价格 (USD/百万Token):**

| 模型 | 输入价格 | 输出价格 | 缓存写入价格 | 缓存读取价格 |
|------|----------|----------|--------------|--------------|
| Claude 3.5 Sonnet | $3.00 | $15.00 | $3.75 | $0.30 |
| Claude 3.5 Haiku | $1.00 | $5.00 | $1.25 | $0.10 |
| Claude 3 Opus | $15.00 | $75.00 | $18.75 | $1.50 |

## 核心架构

### 流程图
```
HTTP Request → UniversalUsageMiddleware → IUsageTracker → IResponseProcessor → Usage Storage
```

### 关键组件

1. **UniversalUsageMiddleware** - 统一入口，识别提供商并选择相应处理器
2. **IUsageTracker** - 提供商特定的使用量追踪器接口
3. **IResponseProcessor** - 响应解析器，支持流式和非流式解析
4. **UniversalResponseStream** - 高性能的零拷贝流处理器

## 性能特性

- **零拷贝处理**: 使用 `System.IO.Pipelines` 直接在内存上操作
- **对象池化**: 重用 StringBuilder 等频繁分配的对象
- **异步处理**: 使用 `Channel<T>` 进行后台数据处理
- **AOT 优化**: 源生成的 JSON 序列化，支持原生编译

## 使用示例

### OpenAI API 调用
```bash
curl -X POST "http://localhost:5501/openai/v1/chat/completions" \
  -H "Authorization: Bearer your-key" \
  -H "Content-Type: application/json" \
  -d '{"model": "gpt-4", "messages": [{"role": "user", "content": "Hello"}]}'
```

### Anthropic API 调用
```bash
curl -X POST "http://localhost:5501/anthropic/v1/messages" \
  -H "x-api-key: your-key" \
  -H "Content-Type: application/json" \
  -H "anthropic-version: 2023-06-01" \
  -d '{
    "model": "claude-3-5-sonnet-20241022",
    "max_tokens": 1024,
    "messages": [{"role": "user", "content": "Hello"}]
  }'
```

## 日志输出

### OpenAI 使用统计
```log
[12:34:56 INF] OpenAI API Usage - Request: req_abc123, Model: gpt-4, 
Tokens: Prompt=15/Completion=87/Total=102, Streaming: false
```

### Anthropic 使用统计（含计费）
```log
[12:34:56 INF] Anthropic API Usage - Request: req_def456, Model: claude-3-5-sonnet-20241022, 
Tokens: I=15/O=87/CC=0/CR=0, Cost: I=$0.000045/O=$0.001305, Total: $0.001350, Streaming: false
```

## 扩展新提供商

### 1. 创建 UsageTracker
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
        // 实现使用统计处理逻辑
        _logger.LogInformation("New Provider Usage: {Usage}", usageResult);
    }
}
```

### 2. 创建 ResponseProcessor
```csharp
public sealed class NewProviderResponseProcessor : IResponseProcessor
{
    public async Task ProcessAsync(
        Stream responseStream, string requestId, bool isStreaming, 
        string provider, Func<BaseUsageResult, Task> onUsageDetected, 
        CancellationToken cancellationToken = default)
    {
        // 实现响应解析逻辑
    }
}
```

### 3. 注册服务
```csharp
services.AddSingleton<IUsageTracker, NewProviderUsageTracker>();
services.AddSingleton<NewProviderResponseProcessor>();
```

## 生产环境建议

1. **存储后端**: 集成数据库或时序数据库存储使用量数据
2. **监控告警**: 基于使用量设置成本告警
3. **配额管理**: 实现用户/API密钥级别的配额控制
4. **数据导出**: 支持CSV/JSON格式的使用量报告导出

这个统一的追踪系统为 AI 应用提供了完整的可观测性和成本控制能力。