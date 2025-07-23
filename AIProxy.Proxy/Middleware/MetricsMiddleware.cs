using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AIProxy.Proxy.Middleware;

public sealed class MetricsMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly Meter _meter;
    private readonly Counter<int> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<int> _errorCounter;

    public MetricsMiddleware(RequestDelegate next)
    {
        _next = next;
        _meter = new Meter("AIProxy.Proxy", "1.0.0");
        
        _requestCounter = _meter.CreateCounter<int>(
            "aiproxy_requests_total",
            description: "Total number of proxy requests");
            
        _requestDuration = _meter.CreateHistogram<double>(
            "aiproxy_request_duration_seconds",
            description: "Request duration in seconds");
            
        _errorCounter = _meter.CreateCounter<int>(
            "aiproxy_errors_total", 
            description: "Total number of errors");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        _requestCounter.Add(1, 
            new KeyValuePair<string, object?>("method", context.Request.Method),
            new KeyValuePair<string, object?>("path", context.Request.Path.ToString()));

        try
        {
            await _next(context);
        }
        catch (Exception)
        {
            _errorCounter.Add(1,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("error", "exception"));
            throw;
        }
        finally
        {
            stopwatch.Stop();
            
            _requestDuration.Record(stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("method", context.Request.Method),
                new KeyValuePair<string, object?>("status_code", context.Response.StatusCode));
            
            if (context.Response.StatusCode >= 400)
            {
                _errorCounter.Add(1,
                    new KeyValuePair<string, object?>("method", context.Request.Method),
                    new KeyValuePair<string, object?>("status_code", context.Response.StatusCode));
            }
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}