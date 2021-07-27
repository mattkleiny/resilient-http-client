using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Polly.Caching;
using ResilientHttp.Policies;

namespace ResilientHttp
{
  public class ResilientHttpClientTests
  {
    [Test]
    public async Task it_should_retry_basic_requests()
    {
      var client = new ResilientHttpClient
      {
        BaseAddress = new Uri("http://www.google.com.au"),
        ConnectionPolicy =
        {
          Retry =
          {
            MaxRetries       = 3,
            ExceptionsPolicy = ExceptionPolicies.OfType<ValidationException>()
          },
          Circuit =
          {
            Policy = CircuitPolicies.Standard(3, new InMemoryCircuitStateRepository())
          },
          Caching =
          {
            CacheProvider = new InMemoryCacheProvider(),
          }
        }
      };

      await client.SendAsync(new ResilientHttpRequestMessage(HttpMethod.Get, "/")
      {
        ConnectionPolicy =
        {
          Retry =
          {
            MaxRetries = 5
          }
        }
      });

      await client.SendAsync(new ResilientHttpRequestMessage(HttpMethod.Get, "/"));
      await client.SendAsync(new ResilientHttpRequestMessage(HttpMethod.Get, "/"));
      await client.SendAsync(new ResilientHttpRequestMessage(HttpMethod.Get, "/"));
    }

    private sealed class InMemoryCacheProvider : IAsyncCacheProvider<HttpResponseMessage>
    {
      private static readonly ConcurrentDictionary<string, HttpResponseMessage> entries = new();

      public Task<(bool, HttpResponseMessage)> TryGetAsync(string key, CancellationToken cancellationToken, bool continueOnCapturedContext)
      {
        if (entries.TryGetValue(key, out var response))
        {
          return Task.FromResult<(bool, HttpResponseMessage)>((true, response));
        }

        return Task.FromResult<(bool, HttpResponseMessage)>((false, null));
      }

      public Task PutAsync(string key, HttpResponseMessage value, Ttl ttl, CancellationToken cancellationToken, bool continueOnCapturedContext)
      {
        entries.TryAdd(key, value);

        return Task.CompletedTask;
      }
    }
  }
}