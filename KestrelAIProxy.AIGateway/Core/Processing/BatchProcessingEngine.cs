using System.Collections.Concurrent;
using System.Threading.Channels;
using KestrelAIProxy.AIGateway.Core.Configuration;
using KestrelAIProxy.AIGateway.Core.Interfaces;
using KestrelAIProxy.AIGateway.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KestrelAIProxy.AIGateway.Core.Processing;

/// <summary>
/// High-performance batch processing engine for usage records
/// </summary>
public sealed class BatchProcessingEngine : BackgroundService, IUsageProcessor
{
    private readonly ILogger<BatchProcessingEngine> _logger;
    private readonly IOptionsMonitor<UsageTrackingConfiguration> _options;
    private readonly IEnumerable<IUsageSink> _sinks;
    private readonly IUsageTrackingCircuitBreaker _circuitBreaker;
    
    // High-performance channels for batch processing
    private readonly Channel<UsageRecord> _recordQueue;
    private readonly ChannelWriter<UsageRecord> _recordWriter;
    private readonly ChannelReader<UsageRecord> _recordReader;
    
    // Batch management
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private readonly Timer _flushTimer;
    private readonly ConcurrentQueue<UsageRecord> _pendingRecords = new();
    
    // Performance counters
    private long _processedCount;
    private long _droppedCount;
    private long _batchCount;

    public BatchProcessingEngine(
        ILogger<BatchProcessingEngine> logger,
        IOptionsMonitor<UsageTrackingConfiguration> options,
        IEnumerable<IUsageSink> sinks,
        IUsageTrackingCircuitBreaker circuitBreaker)
    {
        _logger = logger;
        _options = options;
        _sinks = sinks;
        _circuitBreaker = circuitBreaker;

        var config = _options.CurrentValue;
        var channelOptions = new BoundedChannelOptions(config.Batching.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        };

        _recordQueue = Channel.CreateBounded<UsageRecord>(channelOptions);
        _recordWriter = _recordQueue.Writer;
        _recordReader = _recordQueue.Reader;

        _flushTimer = new Timer(OnFlushTimer, null, TimeSpan.FromMilliseconds(config.Batching.FlushIntervalMs), 
            TimeSpan.FromMilliseconds(config.Batching.FlushIntervalMs));
    }

    public async ValueTask ProcessAsync(UsageRecord record, CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.IsAllowed)
        {
            record.Dispose();
            Interlocked.Increment(ref _droppedCount);
            return;
        }

        try
        {
            if (!_recordWriter.TryWrite(record))
            {
                // Queue is full, drop the record
                record.Dispose();
                Interlocked.Increment(ref _droppedCount);
                _logger.LogWarning("Usage record dropped due to queue overflow");
            }
        }
        catch (Exception ex)
        {
            record.Dispose();
            _logger.LogError(ex, "Failed to enqueue usage record");
            _circuitBreaker.RecordFailure();
            Interlocked.Increment(ref _droppedCount);
        }
    }

    public async ValueTask ProcessBatchAsync(UsageBatch batch, CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.IsAllowed)
        {
            batch.Dispose();
            Interlocked.Add(ref _droppedCount, batch.Count);
            return;
        }

        try
        {
            await ProcessBatchInternal(batch.Records, cancellationToken);
            _circuitBreaker.RecordSuccess();
            Interlocked.Add(ref _processedCount, batch.Count);
            Interlocked.Increment(ref _batchCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process usage batch with {Count} records", batch.Count);
            _circuitBreaker.RecordFailure();
            Interlocked.Add(ref _droppedCount, batch.Count);
        }
        finally
        {
            batch.Dispose();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        await _flushSemaphore.WaitAsync(cancellationToken);
        try
        {
            await FlushPendingRecords(cancellationToken);
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _options.CurrentValue;
        var processingTasks = new Task[config.Batching.ProcessingThreads];

        // Start processing threads
        for (int i = 0; i < processingTasks.Length; i++)
        {
            processingTasks[i] = ProcessingLoop(stoppingToken);
        }

        // Wait for all processing to complete
        await Task.WhenAll(processingTasks);
    }

    private async Task ProcessingLoop(CancellationToken cancellationToken)
    {
        var config = _options.CurrentValue;
        var batchSize = config.Batching.BatchSize;
        var records = new List<UsageRecord>(batchSize);

        try
        {
            await foreach (var record in _recordReader.ReadAllAsync(cancellationToken))
            {
                records.Add(record);

                // Process batch when full or when requested
                if (records.Count >= batchSize)
                {
                    await ProcessRecordsBatch(records, cancellationToken);
                    records.Clear();
                }
            }

            // Process remaining records
            if (records.Count > 0)
            {
                await ProcessRecordsBatch(records, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing loop encountered an error");
        }
        finally
        {
            // Clean up remaining records
            foreach (var record in records)
            {
                record.Dispose();
            }
        }
    }

    private async Task ProcessRecordsBatch(List<UsageRecord> records, CancellationToken cancellationToken)
    {
        if (!_circuitBreaker.IsAllowed)
        {
            foreach (var record in records)
            {
                record.Dispose();
            }
            Interlocked.Add(ref _droppedCount, records.Count);
            return;
        }

        try
        {
            await ProcessBatchInternal(records, cancellationToken);
            _circuitBreaker.RecordSuccess();
            Interlocked.Add(ref _processedCount, records.Count);
            Interlocked.Increment(ref _batchCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process batch of {Count} usage records", records.Count);
            _circuitBreaker.RecordFailure();
            Interlocked.Add(ref _droppedCount, records.Count);
        }
        finally
        {
            foreach (var record in records)
            {
                record.Dispose();
            }
        }
    }

    private async Task ProcessBatchInternal(IReadOnlyList<UsageRecord> records, CancellationToken cancellationToken)
    {
        var recordsMemory = records.ToArray().AsMemory();
        var sinkTasks = _sinks.Select(sink => sink.WriteAsync(recordsMemory, cancellationToken)).ToArray();
        
        await Task.WhenAll(sinkTasks.Select(t => t.AsTask()));
    }

    private async void OnFlushTimer(object? state)
    {
        try
        {
            await FlushAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled flush");
        }
    }

    private async Task FlushPendingRecords(CancellationToken cancellationToken)
    {
        var records = new List<UsageRecord>();
        
        while (_pendingRecords.TryDequeue(out var record))
        {
            records.Add(record);
        }

        if (records.Count > 0)
        {
            await ProcessRecordsBatch(records, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping batch processing engine...");
        
        // Stop accepting new records
        _recordWriter.Complete();
        
        // Wait for processing to complete
        await base.StopAsync(cancellationToken);
        
        // Flush any remaining data
        await FlushAsync(cancellationToken);
        
        // Flush sinks
        var flushTasks = _sinks.Select(sink => sink.FlushAsync(cancellationToken));
        await Task.WhenAll(flushTasks.Select(t => t.AsTask()));
        
        _logger.LogInformation("Batch processing engine stopped. Processed: {Processed}, Dropped: {Dropped}, Batches: {Batches}", 
            _processedCount, _droppedCount, _batchCount);
    }

    public override void Dispose()
    {
        _flushTimer?.Dispose();
        _flushSemaphore?.Dispose();
        base.Dispose();
    }
}

/// <summary>
/// Circuit breaker implementation for usage tracking
/// </summary>
public sealed class UsageTrackingCircuitBreaker : IUsageTrackingCircuitBreaker
{
    private readonly ILogger<UsageTrackingCircuitBreaker> _logger;
    private readonly IOptionsMonitor<UsageTrackingConfiguration> _options;
    private readonly object _lock = new();
    
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTime _lastFailureTime;
    private DateTime _nextAttemptTime;

    public CircuitBreakerState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public bool IsAllowed
    {
        get
        {
            lock (_lock)
            {
                return _state switch
                {
                    CircuitBreakerState.Closed => true,
                    CircuitBreakerState.Open => DateTime.UtcNow >= _nextAttemptTime && TryTransitionToHalfOpen(),
                    CircuitBreakerState.HalfOpen => true,
                    _ => false
                };
            }
        }
    }

    public UsageTrackingCircuitBreaker(
        ILogger<UsageTrackingCircuitBreaker> logger,
        IOptionsMonitor<UsageTrackingConfiguration> options)
    {
        _logger = logger;
        _options = options;
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Closed;
                _failureCount = 0;
                _logger.LogInformation("Circuit breaker closed after successful operation");
            }
            else if (_state == CircuitBreakerState.Closed && _failureCount > 0)
            {
                _failureCount = Math.Max(0, _failureCount - 1);
            }
        }
    }

    public void RecordFailure()
    {
        var config = _options.CurrentValue.CircuitBreaker;
        if (!config.Enabled) return;

        lock (_lock)
        {
            _failureCount++;
            _lastFailureTime = DateTime.UtcNow;

            if (_state == CircuitBreakerState.Closed && _failureCount >= config.FailureThreshold)
            {
                _state = CircuitBreakerState.Open;
                _nextAttemptTime = DateTime.UtcNow.AddMilliseconds(config.ResetTimeoutMs);
                _logger.LogWarning("Circuit breaker opened after {FailureCount} failures", _failureCount);
            }
            else if (_state == CircuitBreakerState.HalfOpen)
            {
                _state = CircuitBreakerState.Open;
                _nextAttemptTime = DateTime.UtcNow.AddMilliseconds(config.ResetTimeoutMs);
                _logger.LogWarning("Circuit breaker returned to open state after failure in half-open state");
            }
        }
    }

    private bool TryTransitionToHalfOpen()
    {
        _state = CircuitBreakerState.HalfOpen;
        _logger.LogInformation("Circuit breaker transitioned to half-open state");
        return true;
    }
}

/// <summary>
/// Intelligent sampling strategy implementation
/// </summary>
public sealed class AdaptiveSamplingStrategy : ISamplingStrategy
{
    private readonly ILogger<AdaptiveSamplingStrategy> _logger;
    private readonly IOptionsMonitor<UsageTrackingConfiguration> _options;
    private readonly ConcurrentDictionary<string, ModelSamplingState> _modelStates = new();
    
    private double _globalSamplingRate = 1.0;
    private long _requestCount;

    public AdaptiveSamplingStrategy(
        ILogger<AdaptiveSamplingStrategy> logger,
        IOptionsMonitor<UsageTrackingConfiguration> options)
    {
        _logger = logger;
        _options = options;
        _globalSamplingRate = options.CurrentValue.Sampling.Rate;
    }

    public bool ShouldTrack(string requestId, string provider, string model)
    {
        var config = _options.CurrentValue;
        var providerConfig = config.Providers.GetValueOrDefault(provider);
        
        if (providerConfig?.Enabled != true)
            return false;

        var samplingRate = GetEffectiveSamplingRate(provider, model, providerConfig);
        return Random.Shared.NextDouble() < samplingRate;
    }

    private double GetEffectiveSamplingRate(string provider, string model, ProviderTrackingOptions providerConfig)
    {
        var config = _options.CurrentValue.Sampling;
        
        // Get provider-specific rate
        var baseRate = providerConfig.SamplingRate;
        
        // Get model-specific rate if configured
        if (config.ModelRates.TryGetValue(model, out var modelRate))
        {
            baseRate = Math.Min(baseRate, modelRate);
        }

        // Apply adaptive adjustment based on load
        if (config.Strategy == SamplingStrategy.Adaptive)
        {
            var loadFactor = CalculateLoadFactor();
            baseRate *= loadFactor;
        }

        return Math.Max(0.0, Math.Min(1.0, baseRate));
    }

    private double CalculateLoadFactor()
    {
        var requestCount = Interlocked.Read(ref _requestCount);
        
        // Simple load-based adjustment: reduce sampling as load increases
        return requestCount switch
        {
            < 100 => 1.0,
            < 1000 => 0.8,
            < 10000 => 0.5,
            < 100000 => 0.2,
            _ => 0.1
        };
    }

    public void UpdateSamplingRate(double rate)
    {
        _globalSamplingRate = Math.Max(0.0, Math.Min(1.0, rate));
        _logger.LogInformation("Updated global sampling rate to {Rate:P2}", _globalSamplingRate);
    }

    private sealed class ModelSamplingState
    {
        public double CurrentRate { get; set; } = 1.0;
        public long RequestCount { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}

/// <summary>
/// High-performance in-memory cache for usage data
/// </summary>
public sealed class UsageDataCache : IDisposable
{
    private readonly ConcurrentDictionary<string, CachedUsageData> _cache = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<UsageDataCache> _logger;
    private readonly TimeSpan _ttl = TimeSpan.FromMinutes(15);

    public UsageDataCache(ILogger<UsageDataCache> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, _ttl, _ttl);
    }

    public bool TryGetUsage(string key, out TokenMetrics tokens, out CostMetrics? costs)
    {
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            tokens = cached.Tokens;
            costs = cached.Costs;
            return true;
        }

        tokens = default;
        costs = null;
        return false;
    }

    public void CacheUsage(string key, in TokenMetrics tokens, in CostMetrics? costs)
    {
        var cached = new CachedUsageData
        {
            Tokens = tokens,
            Costs = costs,
            ExpiresAt = DateTime.UtcNow.Add(_ttl)
        };

        _cache.AddOrUpdate(key, cached, (_, _) => cached);
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cache.Clear();
    }

    private sealed class CachedUsageData
    {
        public TokenMetrics Tokens { get; set; }
        public CostMetrics? Costs { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}