using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace ResilientHttp.Policies
{
  /// <summary>A policy for determining in an HTTP request is behind a closed circuit.</summary>
  public delegate bool CircuitPolicy(HttpResponseMessage response, int attempt);

  /// <summary>Standard-purpose <see cref="CircuitPolicy"/>s.</summary>
  public static class CircuitPolicies
  {
    public static CircuitPolicy Never { get; } = (_, _) => false;

    public static CircuitPolicy Standard(int failureCount, ICircuitStateRepository repository)
    {
      return (response, attempt) =>
      {
        if (repository.TryGetState(response.RequestMessage!.RequestUri, out var isCircuitBroken))
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

  /// <summary>A repository for states used in <see cref="CircuitPolicies.Standard"/>.</summary>
  /// <remarks>Implementations of this interface are expected to be thread-safe.</remarks>
  public interface ICircuitStateRepository
  {
    bool TryGetState(Uri uri, out bool isCircuitBroken);
    void ClearState(Uri uri);
  }

  /// <summary>A simple in-memory <see cref="ICircuitStateRepository"/>.</summary>
  public sealed class InMemoryCircuitStateRepository : ICircuitStateRepository
  {
    private readonly ConcurrentDictionary<Uri, bool> statesByUri = new();

    public bool TryGetState(Uri uri, out bool isCircuitBroken)
    {
      return statesByUri.TryGetValue(uri, out isCircuitBroken);
    }

    public void ClearState(Uri uri)
    {
      statesByUri.TryRemove(uri, out _);
    }
  }
}