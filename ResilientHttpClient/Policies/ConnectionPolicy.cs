using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.Caching;
using ResilientHttp.Utilities;
using static ResilientHttp.Policies.ConnectionPolicyOverrides;

namespace ResilientHttp.Policies
{
  public delegate Context PollyContextFactory(HttpRequestMessage request);

  /// <summary>A policy for the <see cref="ResilientHttpClient"/> using Polly.</summary>
  public sealed record ConnectionPolicy
  {
    private static HttpRequestOptionsKey<string> OperationKey { get; } = new("OperationKey");

    /// <summary>A factory to use for the underlying Polly context; this can be customized for advanced use cases.</summary>
    /// <remarks>The default factory should be reasonable for most services.</remarks>
    public PollyContextFactory ContextFactory { get; set; } = request =>
    {
      // if the user specifies a custom operation key on the request, use that instead
      if (request.Options.TryGetValue(OperationKey, out var operationKey))
      {
        return new Context(operationKey);
      }

      return new Context($"{request.Method}:{request.RequestUri}");
    };

    public RetryOptions   Retry   { get; } = new();
    public CircuitOptions Circuit { get; } = new();
    public CachingOptions Caching { get; } = new();

    /// <summary>True to log HTTP errors automatically to a logger.</summary>
    public bool LogFaults { get; set; } = true;

    /// <summary>How long to wait until considering the entire operation is considered timed out (not an individual retry).</summary>
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromMinutes(1);

    internal AsyncPolicy<HttpResponseMessage> ToPollyPolicy(ConnectionPolicyOverrides overrides)
    {
      var policies = BuildPollyPolicies(overrides);

      return Policy.WrapAsync(policies.ToArray());
    }

    private IEnumerable<IAsyncPolicy<HttpResponseMessage>> BuildPollyPolicies(ConnectionPolicyOverrides overrides)
    {
      // N.B: (the last policy returned is the inner-most; the closest one to the HTTP layer).
      var globalTimeout    = overrides.GlobalTimeout.GetOrDefault(GlobalTimeout);
      var isRetryEnabled   = overrides.Retry.IsEnabled.GetOrDefault(Retry.IsEnabled);
      var isCircuitEnabled = overrides.Circuit.IsEnabled.GetOrDefault(Circuit.IsEnabled);
      var isCachingEnabled = overrides.Caching.IsEnabled.GetOrDefault(Caching.IsEnabled);

      yield return Policy.TimeoutAsync<HttpResponseMessage>(globalTimeout);

      if (isRetryEnabled)
      {
        yield return Retry.ToPollyPolicy(overrides.Retry);
      }

      if (isCircuitEnabled)
      {
        yield return Circuit.ToPollyPolicy(overrides.Circuit);
      }

      if (isCachingEnabled)
      {
        yield return Caching.ToPollyPolicy(overrides.Caching);
      }
    }

    public sealed record RetryOptions
    {
      /// <summary>True if the retry policy should be honored.</summary>
      public bool IsEnabled { get; set; } = true;

      /// <summary>The maximum number of retry attempts.</summary>
      public int MaxRetries { get; set; } = 4;

      /// <summary>How long to wait until an individual retry is considered timed out (not the entire operation).</summary>
      public TimeSpan RetryTimeout { get; set; } = TimeSpan.FromMinutes(2);

      /// <summary>The policy to use for each step of the retry; the default should be reasonable for most services.</summary>
      public BackOffPolicy BackOffPolicy { get; set; } = BackOffPolicies.ExponentialWithJitter(
        duration: TimeSpan.FromSeconds(1),
        baseDelay: TimeSpan.FromMilliseconds(500),
        maxDelay: TimeSpan.FromSeconds(30),
        maxJitter: TimeSpan.FromSeconds(3)
      );

      /// <summary>The set of <see cref="HttpStatusCode"/>s on which to retry method invocations.</summary>
      public ICollection<HttpStatusCode> StatusCodes { get; } = new HashSet<HttpStatusCode>(new[]
      {
        HttpStatusCode.InternalServerError,
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
      });

      /// <summary>A predicate that determines if a particular exception should be retried.</summary>
      public ExceptionPolicy ExceptionsPolicy { get; set; } = ExceptionPolicies.All;

      public AsyncPolicy<HttpResponseMessage> ToPollyPolicy(RetryOptionsOverride overrides)
      {
        var statusCodes       = overrides.StatusCodes.GetOrDefault(StatusCodes);
        var maxRetries        = overrides.MaxRetries.GetOrDefault(MaxRetries);
        var backOffPolicy     = overrides.BackOffPolicy.GetOrDefault(BackOffPolicy);
        var retryTimeout      = overrides.RetryTimeout.GetOrDefault(RetryTimeout);
        var exceptionsToRetry = overrides.ExceptionPolicy.GetOrDefault(ExceptionsPolicy);

        return Policy
          .HandleResult<HttpResponseMessage>(message => statusCodes.Contains(message.StatusCode))
          .Or<Exception>(exception => exceptionsToRetry(exception))
          .WaitAndRetryAsync(maxRetries, attempt => backOffPolicy(attempt))
          .WrapAsync(Policy.TimeoutAsync<HttpResponseMessage>(retryTimeout));
      }
    }

    public sealed record CircuitOptions
    {
      /// <summary>True if the circuit policy should be honored.</summary>
      public bool IsEnabled => Policy != null;

      /// <summary>A policy to use for circuit operation.</summary>
      public CircuitPolicy Policy { get; set; }

      public AsyncPolicy<HttpResponseMessage> ToPollyPolicy(CircuitOptionsOverride overrides)
      {
        var policy = overrides.Policy.GetOrDefault(Policy);

        return new ExternalCircuitBreakerPolicy(policy);
      }

      /// <summary>A <see cref="AsyncPolicy"/> that implements a circuit breaker pattern by deferring to some external implementation.</summary>
      private sealed class ExternalCircuitBreakerPolicy : AsyncPolicy<HttpResponseMessage>
      {
        private readonly CircuitPolicy circuitPolicy;

        public ExternalCircuitBreakerPolicy(CircuitPolicy circuitPolicy)
        {
          this.circuitPolicy = circuitPolicy;
        }

        protected override async Task<HttpResponseMessage> ImplementationAsync(
          Func<Context, CancellationToken, Task<HttpResponseMessage>> action,
          Context context,
          CancellationToken cancellationToken,
          bool continueOnCapturedContext)
        {
          var response = await action(context, cancellationToken).ConfigureAwait(continueOnCapturedContext);

          throw new NotImplementedException();
        }
      }
    }

    public sealed record CachingOptions
    {
      /// <summary>True if the caching policy should be honored.</summary>
      public bool IsEnabled => CacheProvider != null;

      /// <summary>The <see cref="IAsyncCacheProvider"/> to use for caching results.</summary>
      /// <remarks>This must be specified in order to enable the cache support.</remarks>
      public IAsyncCacheProvider<HttpResponseMessage> CacheProvider { get; set; }

      /// <summary>A string prefix to automatically apply to cache keys.</summary>
      public string CachePrefix { get; set; } = string.Empty;

      /// <summary>A set of <see cref="HttpStatusCode"/> for which to permit caching results.</summary>
      public ICollection<HttpStatusCode> StatusCodes { get; } = new HashSet<HttpStatusCode>(new[]
      {
        HttpStatusCode.OK,
        HttpStatusCode.NotFound
      });

      /// <summary>The default amount of time each cache entry persists for.</summary>
      public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(5);

      /// <summary>True to enable a sliding expiration on the resultant cache entries.</summary>
      /// <remarks>If this is true, the <see cref="TimeToLive"/> will be renewed upon each successful cache hit.</remarks>
      public bool SlidingExpiration { get; set; }

      /// <summary>The <see cref="TimeToLivePolicy"/> to be used. By default HTTP response headers are honored.</summary>
      /// <remarks>N.B: The response must still pass the <see cref="StatusCodes"/> check in order for it to be cached.</remarks>
      public TimeToLivePolicy TimeToLivePolicy { get; set; } = TimeToLivePolicies.Default;

      public AsyncPolicy<HttpResponseMessage> ToPollyPolicy(CachingOptionsOverride overrides)
      {
        var cacheProvider     = overrides.CacheProvider.GetOrDefault(CacheProvider);
        var cachePrefix       = overrides.CachePrefix.GetOrDefault(CachePrefix);
        var statusCodes       = overrides.StatusCodes.GetOrDefault(StatusCodes);
        var timeToLivePolicy  = overrides.TimeToLivePolicy.GetOrDefault(TimeToLivePolicy);
        var defaultTimeToLive = overrides.TimeToLive.GetOrDefault(TimeToLive);
        var slidingExpiration = overrides.SlidingExpiration.GetOrDefault(SlidingExpiration);

        return Policy.CacheAsync(
          cacheProvider: cacheProvider,
          cacheKeyStrategy: context => $"{cachePrefix ?? string.Empty}{context.OperationKey}",
          ttlStrategy: new ResultTtl<HttpResponseMessage>(response =>
          {
            if (statusCodes.Contains(response.StatusCode))
            {
              var timeToLive = timeToLivePolicy(response, defaultTimeToLive: defaultTimeToLive);

              return new Ttl(timeToLive, slidingExpiration);
            }

            return new Ttl(TimeSpan.Zero);
          })
        );
      }
    }
  }

  /// <summary>Allows overriding individual properties on a <see cref="ConnectionPolicy"/>.</summary>
  public sealed record ConnectionPolicyOverrides
  {
    public static ConnectionPolicyOverrides None { get; } = new();

    public RetryOptionsOverride   Retry   { get; } = new();
    public CircuitOptionsOverride Circuit { get; } = new();
    public CachingOptionsOverride Caching { get; } = new();

    public Optional<TimeSpan> GlobalTimeout { get; set; }

    public sealed record RetryOptionsOverride
    {
      public Optional<bool>                        IsEnabled       { get; set; }
      public Optional<int>                         MaxRetries      { get; set; }
      public Optional<TimeSpan>                    RetryTimeout    { get; set; }
      public Optional<BackOffPolicy>               BackOffPolicy   { get; set; }
      public Optional<ICollection<HttpStatusCode>> StatusCodes     { get; set; }
      public Optional<ExceptionPolicy>             ExceptionPolicy { get; set; }
    }

    public sealed record CircuitOptionsOverride
    {
      public Optional<bool>          IsEnabled { get; set; }
      public Optional<CircuitPolicy> Policy    { get; set; }
    }

    public sealed record CachingOptionsOverride
    {
      public Optional<bool>             IsEnabled         { get; set; }
      public Optional<string>           CachePrefix       { get; set; }
      public Optional<TimeSpan>         TimeToLive        { get; set; }
      public Optional<TimeToLivePolicy> TimeToLivePolicy  { get; set; }
      public Optional<bool>             SlidingExpiration { get; set; }

      public Optional<ICollection<HttpStatusCode>>              StatusCodes   { get; set; }
      public Optional<IAsyncCacheProvider<HttpResponseMessage>> CacheProvider { get; set; }
    }
  }
}