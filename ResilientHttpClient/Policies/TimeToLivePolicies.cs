using System;
using System.Net.Http;

namespace ResilientHttp.Policies
{
  /// <summary>A provider for the duration in which to cache an <see cref="HttpResponseMessage"/>.</summary>
  public delegate TimeSpan TimeToLivePolicy(HttpResponseMessage response, TimeSpan defaultTimeToLive);

  public static class TimeToLivePolicies
  {
    /// <summary>Provides a time to live based on the incoming HTTP headers (Cache-Control, etc).</summary>
    public static TimeToLivePolicy ByCacheControlHeaders { get; } = (response, defaultTimeToLive) =>
    {
      var header = response.Headers.CacheControl;

      if (header is {NoCache: false, NoStore: false})
      {
        return header.MaxAge.GetValueOrDefault(defaultTimeToLive);
      }

      return defaultTimeToLive;
    };
  }
}