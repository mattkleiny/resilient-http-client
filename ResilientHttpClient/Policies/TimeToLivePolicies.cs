using System;
using System.Net.Http;

namespace ResilientHttp.Policies
{
  /// <summary>A provider for the duration in which to cache an <see cref="HttpResponseMessage"/>.</summary>
  public delegate TimeSpan TimeToLivePolicy(HttpResponseMessage response, TimeSpan defaultTimeToLive);

  public static class TimeToLivePolicies
  {
    /// <summary>Uses the default <see cref="ConnectionPolicy.CachingOptions.TimeToLive"/>.</summary>
    public static TimeToLivePolicy Default => (_, defaultTimeToLive) => defaultTimeToLive;

    /// <summary>A constant time to live, regardless of response.</summary>
    public static TimeToLivePolicy Constant(TimeSpan duration) => (_, _) => duration;

    /// <summary>Provides a time to live based on the incoming HTTP headers (Cache-Control, etc).</summary>
    public static TimeToLivePolicy ByCacheControlHeaders { get; } = (response, defaultTimeToLive) =>
    {
      var header = response.Headers.CacheControl;

      if (header is {NoCache: false, NoStore: false, MaxAge: var maxAge})
      {
        return maxAge.GetValueOrDefault(defaultTimeToLive);
      }

      return defaultTimeToLive;
    };
  }
}