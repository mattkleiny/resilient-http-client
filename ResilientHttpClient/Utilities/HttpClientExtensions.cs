using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ResilientHttp.Utilities
{
  /// <summary>Static extensions for <see cref="HttpClient"/>s.</summary>
  public static class HttpClientExtensions
  {
    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string requestUri, CancellationToken cancellationToken = default)
    {
      var response = await client.GetAsync(requestUri, cancellationToken);

      if (response.StatusCode == HttpStatusCode.NotFound)
      {
        return default;
      }

      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

      return await JsonSerializer.DeserializeAsync<T>(stream, null, cancellationToken);
    }

    public static async Task PutJsonAsync<T>(this HttpClient client, string requestUri, T body, CancellationToken cancellationToken = default)
    {
      var json    = JsonSerializer.Serialize(body);
      var content = new StringContent(json, Encoding.UTF8);

      var response = await client.PutAsync(requestUri, content, cancellationToken);

      response.EnsureSuccessStatusCode();
    }

    public static async Task PostJsonAsync<T>(this HttpClient client, string requestUri, T body, CancellationToken cancellationToken = default)
    {
      var json    = JsonSerializer.Serialize(body);
      var content = new StringContent(json, Encoding.UTF8);

      var response = await client.PostAsync(requestUri, content, cancellationToken);

      response.EnsureSuccessStatusCode();
    }
  }
}