using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Polly.Caching;
using ResilientHttp.Policies;

namespace ResilientHttp
{
  public static class Program
  {
    public static async Task Main(string[] args)
    {
      // build a client and set-up it's connection policy once
      var client = new ResilientHttpClient
      {
        BaseAddress = new Uri("http://www.google.com.au"),
        ConnectionPolicy =
        {
          GlobalTimeout = TimeSpan.FromSeconds(10),
          Retry =
          {
            IsEnabled  = true,
            MaxRetries = 3,
          },
          Circuit =
          {
            Policy = CircuitBreakerPolicies.Standard(failureCount: 10, new ExampleCircuitBreakerRepository())
          },
          Caching =
          {
            CacheProvider     = new ExampleCacheProvider(),
            TimeToLive        = TimeSpan.FromMinutes(5),
            SlidingExpiration = true
          }
        },
      };

      // for specific requests, specify properties on a policy override if necessary
      var request = new ResilientHttpRequestMessage(HttpMethod.Get, "/")
      {
        ConnectionPolicyOverrides =
        {
          Retry =
          {
            MaxRetries = 10
          }
        }
      };

      // just make requests, behind the scenes it's implementing the policy
      var response = await client.SendAsync(request);

      // N.B: the client extends HttpClient. Use HttpClient as a base type
      // for extension methods (such as GetJsonAsync<T>()) to simplify common
      // HTTP-like interactions.
      //
      // you could also create a similar sort of policy for the WCF mechanisms,
      // though WCF internally already has retry/caching behaviours.
    }

    private sealed class ExampleCacheProvider : IAsyncCacheProvider<HttpResponseMessage>
    {
      public Task<(bool, HttpResponseMessage)> TryGetAsync(string key, CancellationToken cancellationToken, bool continueOnCapturedContext)
      {
        // TODO: implement me
        return Task.FromResult<(bool, HttpResponseMessage)>((false, null));
      }

      public Task PutAsync(string key, HttpResponseMessage value, Ttl ttl, CancellationToken cancellationToken, bool continueOnCapturedContext)
      {
        // TODO: implement me
        return Task.CompletedTask;
      }
    }

    private sealed class ExampleCircuitBreakerRepository : ICircuitBreakerRepository
    {
      private readonly ConcurrentDictionary<string, bool> statusByHost = new();

      public bool TryGetState(Uri uri, out bool isCircuitBroken)
      {
        if (statusByHost.TryGetValue(uri.Host, out var status))
        {
          isCircuitBroken = status;
          return true;
        }

        isCircuitBroken = false;
        return false;
      }

      public void ClearState(Uri uri)
      {
        statusByHost.TryRemove(uri.Host, out _);
      }
    }
  }
}