using System.Text;
using System.Text.Json;

namespace Maestro.Api.Sse;

public static class SseWriter
{
    public static void ApplyHeaders(HttpResponse response)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");
    }

    public static async Task WriteEventAsync(
        HttpResponse response,
        string eventName,
        object payload,
        JsonSerializerOptions json,
        CancellationToken ct)
    {
        var data = JsonSerializer.Serialize(payload, json);
        var body = $"event: {eventName}\ndata: {data}\n\n";
        var bytes = Encoding.UTF8.GetBytes(body);
        await response.Body.WriteAsync(bytes, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task WriteCommentAsync(
        HttpResponse response,
        string comment,
        CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes($": {comment}\n\n");
        await response.Body.WriteAsync(bytes, ct).ConfigureAwait(false);
        await response.Body.FlushAsync(ct).ConfigureAwait(false);
    }
}
