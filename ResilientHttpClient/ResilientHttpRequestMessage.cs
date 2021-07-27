using System;
using System.Net.Http;
using ResilientHttp.Policies;

namespace ResilientHttp
{
  /// <summary>
  /// A <see cref="HttpRequestMessage"/> with an attached <see cref="ConnectionPolicy"/>
  /// for per-request behaviour that is unique from the default <see cref="ResilientHttpClient.ConnectionPolicy"/>.
  /// </summary>
  public class ResilientHttpRequestMessage : HttpRequestMessage
  {
    public ResilientHttpRequestMessage()
    {
    }

    public ResilientHttpRequestMessage(HttpMethod method, string requestUri)
      : base(method, requestUri)
    {
    }

    public ResilientHttpRequestMessage(HttpMethod method, Uri requestUri)
      : base(method, requestUri)
    {
    }

    public ConnectionPolicyOverrides ConnectionPolicy { get; } = new();
  }
}