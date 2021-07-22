using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using ResilientHttp.Policies;

namespace ResilientHttp
{
  /// <summary>An <see cref="HttpClient"/> that honors a <see cref="ConnectionPolicy"/> to implement retry and caching support.</summary>
  /// <remarks>
  /// If a <see cref="HttpMessageHandler"/> is specified in the constructor,
  /// it will be wrapped as the inner-most handler (and thus will be retried/cached appropriately).
  /// </remarks>
  public sealed class ResilientHttpClient : HttpClient
  {
    private static readonly ILog Logger = LogManager.GetLogger<ResilientHttpClient>();

    public ResilientHttpClient()
      : this(new HttpClientHandler())
    {
    }

    public ResilientHttpClient(HttpMessageHandler innerHandler)
      : this(new ConnectionPolicy(), innerHandler)
    {
    }

    private ResilientHttpClient(ConnectionPolicy connectionPolicy, HttpMessageHandler innerHandler)
      : base(new PollyDelegatingHandler(connectionPolicy, innerHandler))
    {
      ConnectionPolicy = connectionPolicy;
    }

    /// <summary>An alias for the 'GlobalTimeout' on the policy.</summary>
    public new TimeSpan Timeout
    {
      get => ConnectionPolicy.GlobalTimeout;
      set => ConnectionPolicy.GlobalTimeout = value;
    }

    public ConnectionPolicy ConnectionPolicy { get; }

    /// <summary>The internal <see cref="DelegatingHandler"/> which implements the Polly policy.</summary>
    /// <remarks>
    /// N.B: It's important that this behaviour occurs inside of the <see cref="DelegatingHandler"/> chain, as we potentially re-use
    /// <see cref="HttpRequestMessage"/>s over the course of several HTTP requests. If it occured at the <see cref="HttpClient"/> layer,
    /// this would result in the messages being disposed after the first failure, and exceptions being raised on the 2nd+ retry.
    /// </remarks>
    private sealed class PollyDelegatingHandler : DelegatingHandler
    {
      private readonly ConnectionPolicy connectionPolicy;

      public PollyDelegatingHandler(ConnectionPolicy connectionPolicy, HttpMessageHandler innerHandler)
        : base(innerHandler)
      {
        this.connectionPolicy = connectionPolicy;
      }

      protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      {
        var context = connectionPolicy.ContextFactory(request);
        var policy  = connectionPolicy.ToPollyPolicy(ResolveOverrides(request));

        var response = await policy.ExecuteAsync(
          action: (_, innerToken) => base.SendAsync(request, innerToken),
          context: context,
          cancellationToken: cancellationToken
        );

        if (connectionPolicy.LogFaults && !response.IsSuccessStatusCode)
        {
          // N.B: leave this as 'TraceFormat' and don't use the string interpolation operator $
          Logger.TraceFormat("The request for {0} failed with status code: {1}", request.RequestUri, response.StatusCode);
        }

        return response;
      }

      private static ConnectionPolicyOverrides ResolveOverrides(HttpRequestMessage request)
      {
        if (request is ResilientHttpRequestMessage message)
        {
          return message.ConnectionPolicyOverrides;
        }

        return ConnectionPolicyOverrides.None;
      }
    }
  }
}