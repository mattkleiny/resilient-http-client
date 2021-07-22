using System;
using System.Net.Http;

namespace ResilientHttp.Policies
{
  public delegate bool CircuitBreakerPolicy(HttpRequestMessage request, HttpResponseMessage response, int attempt);

  /// <remarks>Implementations of this interface are expected to be thread-safe.</remarks>
  public interface ICircuitBreakerRepository
  {
    bool TryGetState(Uri uri, out bool isCircuitBroken);
    void ClearState(Uri uri);
  }

  public static class CircuitBreakerPolicies
  {
    public static CircuitBreakerPolicy Never()
    {
      return (_, _, _) => false;
    }

    public static CircuitBreakerPolicy Standard(int failureCount, ICircuitBreakerRepository repository)
    {
      return (request, response, attempt) =>
      {
        if (repository.TryGetState(request.RequestUri, out var isCircuitBroken))
        {
          return isCircuitBroken;
        }

        if (attempt >= failureCount)
        {
          return true;
        }

        return false;
      };
    }
  }
}