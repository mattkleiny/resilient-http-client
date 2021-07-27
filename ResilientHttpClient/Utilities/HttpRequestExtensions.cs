using System.IO;
using System.Net.Http;
using System.Threading;

namespace ResilientHttp.Utilities
{
  internal static class HttpRequestExtensions
  {
    public static HttpRequestMessage Clone(this HttpRequestMessage request)
    {
      var clone = new HttpRequestMessage(request.Method, request.RequestUri)
      {
        Content = request.Content.Clone(),
        Version = request.Version
      };

      // copy headers
      foreach (var (key, value) in request.Headers)
      {
        clone.Headers.TryAddWithoutValidation(key, value);
      }

      return clone;
    }

    private static HttpContent? Clone(this HttpContent? content)
    {
      if (content == null)
      {
        return null;
      }

      using var buffer = new MemoryStream();

      content.CopyTo(buffer, context: null, CancellationToken.None);

      buffer.Position = 0;

      var clone = new StreamContent(buffer);

      foreach (var (key, value) in content.Headers)
      {
        clone.Headers.Add(key, value);
      }

      return clone;
    }
  }
}