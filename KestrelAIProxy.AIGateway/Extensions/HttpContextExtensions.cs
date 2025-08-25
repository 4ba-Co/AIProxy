using KestrelAIProxy.AIGateway.Core.Models;

using Microsoft.AspNetCore.Http;

namespace KestrelAIProxy.AIGateway.Extensions;

public static class HttpContextExtensions
{
    private const string
        ParsedPathKey = "AIGateway:ParsedPath";

    private const string ParseResultKey = "AIGateway:ParseResult";

    public static void SetParsedPath(this HttpContext context, ParsedPath parsedPath)
    {
        context.Items[ParsedPathKey] = parsedPath;
    }

    public static ParsedPath? GetParsedPath(this HttpContext context)
    {
        return context.Items.TryGetValue(ParsedPathKey, out var result)
            ? result as ParsedPath
            : null;
    }

    public static void SetParseResult(this HttpContext context, ParseResult result)
    {
        context.Items[ParseResultKey] = result;
    }

    public static ParseResult? GetParseResult(this HttpContext context)
    {
        return context.Items.TryGetValue(ParseResultKey, out var result)
            ? result as ParseResult
            : null;
    }

    public static bool HasParseResult(this HttpContext context)
    {
        return context.Items.ContainsKey(ParseResultKey);
    }
}