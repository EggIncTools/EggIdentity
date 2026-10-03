using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EggIdentity.Testing;

public static class StubResponses {
    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Json<T>(HttpStatusCode status, T value, JsonSerializerOptions? options = null) =>
        new(status) { Content = JsonContent.Create(value, options: options ?? JsonSerializerOptions.Web) };

    public static HttpResponseMessage Bytes(HttpStatusCode status, byte[] body, string contentType = "application/octet-stream") {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(status) { Content = content };
    }
}
