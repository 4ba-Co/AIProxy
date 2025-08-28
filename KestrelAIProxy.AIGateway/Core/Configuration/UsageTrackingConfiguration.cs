using System.ComponentModel.DataAnnotations;
using KestrelAIProxy.AIGateway.Core.Models;

namespace KestrelAIProxy.AIGateway.Core.Configuration;

/// <summary>
/// Configuration options for usage tracking behavior
/// </summary>
public sealed class UsageTrackingConfiguration
{
    public const string ConfigurationSection = "UsageTracking";

    /// <summary>
    /// Global enable/disable switch for usage tracking
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Provider-specific configurations
    /// </summary>
    public Dictionary<string, ProviderTrackingOptions> Providers { get; set; } = new();

    /// <summary>
    /// Batching and performance options
    /// </summary>
    public BatchingOptions Batching { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Sampling configuration
    /// </summary>
    public SamplingOptions Sampling { get; set; } = new();

    /// <summary>
    /// Storage and sink configurations
    /// </summary>
    public StorageOptions Storage { get; set; } = new();
}

/// <summary>
/// Provider-specific tracking options
/// </summary>
public sealed class ProviderTrackingOptions
{
    /// <summary>
    /// Whether tracking is enabled for this provider
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to calculate costs for this provider
    /// </summary>
    public bool EnableCostCalculation { get; set; } = true;

    /// <summary>
    /// Supported endpoints for tracking
    /// </summary>
    public List<string> SupportedEndpoints { get; set; } = new();

    /// <summary>
    /// Sampling rate specific to this provider (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double SamplingRate { get; set; } = 1.0;

    /// <summary>
    /// Model-specific pricing configuration
    /// </summary>
    public Dictionary<string, PricingConfig> ModelPricing { get; set; } = new();

    /// <summary>
    /// Custom headers to include in tracking
    /// </summary>
    public List<string> TrackedHeaders { get; set; } = new();

    /// <summary>
    /// Whether to track detailed metadata
    /// </summary>
    public bool EnableMetadataTracking { get; set; } = false;
}

/// <summary>
/// Batching and performance configuration
/// </summary>
public sealed class BatchingOptions
{
    /// <summary>
    /// Whether batching is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of records in a batch
    /// </summary>
    [Range(1, 10000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time to wait before flushing a batch (milliseconds)
    /// </summary>
    [Range(100, 60000)]
    public int FlushIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum queue size before dropping records
    /// </summary>
    [Range(100, 100000)]
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// Number of background processing threads
    /// </summary>
    [Range(1, 16)]
    public int ProcessingThreads { get; set; } = Math.Max(1, Environment.ProcessorCount / 4);

    /// <summary>
    /// Memory pool configuration
    /// </summary>
    public MemoryPoolOptions MemoryPool { get; set; } = new();
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Whether circuit breaker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    [Range(1, 100)]
    public int FailureThreshold { get; set; } = 10;

    /// <summary>
    /// Time to wait before attempting to close circuit (milliseconds)
    /// </summary>
    [Range(1000, 300000)]
    public int ResetTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Sampling window for failure rate calculation
    /// </summary>
    [Range(10, 1000)]
    public int SamplingDuration { get; set; } = 100;

    /// <summary>
    /// Minimum number of requests before circuit can open
    /// </summary>
    [Range(1, 100)]
    public int MinimumThroughput { get; set; } = 20;
}

/// <summary>
/// Sampling configuration options
/// </summary>
public sealed class SamplingOptions
{
    /// <summary>
    /// Global sampling strategy
    /// </summary>
    public SamplingStrategy Strategy { get; set; } = SamplingStrategy.Uniform;

    /// <summary>
    /// Global sampling rate (0.0 to 1.0)
    /// </summary>
    [Range(0.0, 1.0)]
    public double Rate { get; set; } = 1.0;

    /// <summary>
    /// Model-specific sampling rates
    /// </summary>
    public Dictionary<string, double> ModelRates { get; set; } = new();

    /// <summary>
    /// Request size-based sampling (sample more for larger requests)
    /// </summary>
    public SizeBiasedSamplingOptions SizeBiased { get; set; } = new();
}

/// <summary>
/// Storage and sink configuration
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Primary storage sink type
    /// </summary>
    public StorageSinkType PrimarySink { get; set; } = StorageSinkType.Logging;

    /// <summary>
    /// Secondary storage sinks for backup/redundancy
    /// </summary>
    public List<StorageSinkType> SecondarySinks { get; set; } = new();

    /// <summary>
    /// Database connection configuration
    /// </summary>
    public DatabaseOptions Database { get; set; } = new();

    /// <summary>
    /// File storage configuration
    /// </summary>
    public FileStorageOptions FileStorage { get; set; } = new();

    /// <summary>
    /// Message queue configuration
    /// </summary>
    public MessageQueueOptions MessageQueue { get; set; } = new();

    /// <summary>
    /// Metrics collection configuration
    /// </summary>
    public MetricsOptions Metrics { get; set; } = new();
}

/// <summary>
/// Memory pool configuration
/// </summary>
public sealed class MemoryPoolOptions
{
    /// <summary>
    /// Maximum number of pooled objects per type
    /// </summary>
    [Range(10, 1000)]
    public int MaxPooledObjects { get; set; } = 100;

    /// <summary>
    /// Maximum size of pooled buffers (bytes)
    /// </summary>
    [Range(1024, 1048576)]
    public int MaxBufferSize { get; set; } = 65536;
}

/// <summary>
/// Size-biased sampling configuration
/// </summary>
public sealed class SizeBiasedSamplingOptions
{
    /// <summary>
    /// Whether size-biased sampling is enabled
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Token count thresholds and their sampling rates
    /// </summary>
    public List<TokenThreshold> Thresholds { get; set; } = new()
    {
        new() { TokenCount = 1000, SamplingRate = 0.1 },
        new() { TokenCount = 10000, SamplingRate = 0.5 },
        new() { TokenCount = 50000, SamplingRate = 1.0 }
    };
}

/// <summary>
/// Database storage configuration
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>
    /// Database connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database provider (SqlServer, PostgreSQL, MySQL, SQLite)
    /// </summary>
    public string Provider { get; set; } = "SQLite";

    /// <summary>
    /// Table name for usage records
    /// </summary>
    public string TableName { get; set; } = "UsageRecords";

    /// <summary>
    /// Connection timeout (seconds)
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// File storage configuration
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Base directory for storing usage files
    /// </summary>
    public string BaseDirectory { get; set; } = "./usage_data";

    /// <summary>
    /// File format (JSON, CSV, Parquet)
    /// </summary>
    public FileFormat Format { get; set; } = FileFormat.JSON;

    /// <summary>
    /// File rotation settings
    /// </summary>
    public FileRotationOptions Rotation { get; set; } = new();
}

/// <summary>
/// Message queue configuration
/// </summary>
public sealed class MessageQueueOptions
{
    /// <summary>
    /// Queue provider (RabbitMQ, Kafka, Redis, ServiceBus)
    /// </summary>
    public string Provider { get; set; } = "Redis";

    /// <summary>
    /// Connection string for the message queue
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Topic/queue name for usage events
    /// </summary>
    public string Topic { get; set; } = "usage.tracking";

    /// <summary>
    /// Message serialization format
    /// </summary>
    public SerializationFormat SerializationFormat { get; set; } = SerializationFormat.JSON;
}

/// <summary>
/// Metrics collection configuration
/// </summary>
public sealed class MetricsOptions
{
    /// <summary>
    /// Whether metrics collection is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Metrics provider (Prometheus, Application Insights, StatsD)
    /// </summary>
    public string Provider { get; set; } = "Prometheus";

    /// <summary>
    /// Custom metric tags
    /// </summary>
    public Dictionary<string, string> CustomTags { get; set; } = new();
}

/// <summary>
/// File rotation configuration
/// </summary>
public sealed class FileRotationOptions
{
    /// <summary>
    /// Whether file rotation is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum file size before rotation (MB)
    /// </summary>
    [Range(1, 1000)]
    public int MaxFileSizeMB { get; set; } = 100;

    /// <summary>
    /// Maximum number of files to keep
    /// </summary>
    [Range(1, 100)]
    public int MaxFileCount { get; set; } = 10;

    /// <summary>
    /// Rotation schedule
    /// </summary>
    public RotationSchedule Schedule { get; set; } = RotationSchedule.Daily;
}

/// <summary>
/// Token count threshold for sampling
/// </summary>
public sealed class TokenThreshold
{
    /// <summary>
    /// Token count threshold
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Sampling rate for requests above this threshold
    /// </summary>
    [Range(0.0, 1.0)]
    public double SamplingRate { get; set; }
}

/// <summary>
/// Sampling strategy enumeration
/// </summary>
public enum SamplingStrategy
{
    /// <summary>
    /// Uniform random sampling
    /// </summary>
    Uniform,

    /// <summary>
    /// Sample every Nth request
    /// </summary>
    Systematic,

    /// <summary>
    /// Adaptive sampling based on load
    /// </summary>
    Adaptive,

    /// <summary>
    /// Size-biased sampling (sample larger requests more frequently)
    /// </summary>
    SizeBiased
}

/// <summary>
/// Storage sink type enumeration
/// </summary>
public enum StorageSinkType
{
    /// <summary>
    /// Log to configured logging provider
    /// </summary>
    Logging,

    /// <summary>
    /// Store in database
    /// </summary>
    Database,

    /// <summary>
    /// Store in files
    /// </summary>
    File,

    /// <summary>
    /// Send to message queue
    /// </summary>
    MessageQueue,

    /// <summary>
    /// Send to metrics system
    /// </summary>
    Metrics,

    /// <summary>
    /// In-memory storage (for testing)
    /// </summary>
    Memory
}

/// <summary>
/// File format enumeration
/// </summary>
public enum FileFormat
{
    /// <summary>
    /// JSON format
    /// </summary>
    JSON,

    /// <summary>
    /// CSV format
    /// </summary>
    CSV,

    /// <summary>
    /// Apache Parquet format
    /// </summary>
    Parquet
}

/// <summary>
/// Serialization format enumeration
/// </summary>
public enum SerializationFormat
{
    /// <summary>
    /// JSON serialization
    /// </summary>
    JSON,

    /// <summary>
    /// MessagePack serialization
    /// </summary>
    MessagePack,

    /// <summary>
    /// Protocol Buffers serialization
    /// </summary>
    Protobuf
}

/// <summary>
/// File rotation schedule
/// </summary>
public enum RotationSchedule
{
    /// <summary>
    /// Rotate files hourly
    /// </summary>
    Hourly,

    /// <summary>
    /// Rotate files daily
    /// </summary>
    Daily,

    /// <summary>
    /// Rotate files weekly
    /// </summary>
    Weekly,

    /// <summary>
    /// Rotate files monthly
    /// </summary>
    Monthly
}