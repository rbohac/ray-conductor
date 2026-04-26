using System.Text.Json;
using System.Threading.Channels;
using Maestro.Api.Services;
using Maestro.Api.Sse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
namespace Maestro.Api.Controllers;

[ApiController]
[Route("api/events")]
public sealed class EventsController(
    EventBus bus,
    WorkspaceStore store,
    IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOpts,
    ILogger<EventsController> logger) : ControllerBase
{
    [HttpGet("workspace/{id}")]
    public async Task GetWorkspaceStream(string id, CancellationToken ct)
    {
        if (store.GetWorkspace(id) is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        SseWriter.ApplyHeaders(Response);
        await Response.Body.FlushAsync(ct);

        var json = jsonOpts.Value.JsonSerializerOptions;
        var outbound = Channel.CreateUnbounded<(string Kind, string Body)>(new()
        {
            SingleReader = true,
            SingleWriter = false,
        });

        // Heartbeat: keep the connection alive through proxies/idle detection.
        var heartbeat = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                    await outbound.Writer.WriteAsync(("comment", "heartbeat"), ct);
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex) { logger.LogDebug(ex, "Heartbeat ended"); }
        }, ct);

        // Producer: pump events from the bus into the outbound channel.
        var producer = Task.Run(async () =>
        {
            try
            {
                await foreach (var ev in bus.Subscribe(id, ct))
                {
                    var data = JsonSerializer.Serialize(ev.Payload, json);
                    await outbound.Writer.WriteAsync(("event:" + ev.Type, data), ct);
                }
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex) { logger.LogDebug(ex, "Subscriber ended"); }
            finally { outbound.Writer.TryComplete(); }
        }, ct);

        // Initial hello so the client knows it's connected.
        await SseWriter.WriteCommentAsync(Response, "connected", ct);

        try
        {
            await foreach (var (kind, body) in outbound.Reader.ReadAllAsync(ct))
            {
                if (kind == "comment")
                {
                    await SseWriter.WriteCommentAsync(Response, body, ct);
                }
                else
                {
                    var name = kind["event:".Length..];
                    var bytes = System.Text.Encoding.UTF8.GetBytes($"event: {name}\ndata: {body}\n\n");
                    await Response.Body.WriteAsync(bytes, ct);
                    await Response.Body.FlushAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally
        {
            outbound.Writer.TryComplete();
            try { await heartbeat; } catch { /* ignore */ }
            try { await producer; } catch { /* ignore */ }
        }
    }
}
