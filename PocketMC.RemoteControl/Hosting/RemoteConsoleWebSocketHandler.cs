using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PocketMC.Core.Services;

namespace PocketMC.RemoteControl.Hosting;

public class RemoteConsoleWebSocketHandler
{
    private readonly IConsoleLogService _logService;
    private static readonly Regex AnsiStripRegex = new Regex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled);

    public RemoteConsoleWebSocketHandler(IConsoleLogService logService)
    {
        _logService = logService;
    }

    public async Task HandleWebSocketAsync(HttpContext context, WebSocket webSocket, string instanceId)
    {
        var history = _logService.GetLogs(instanceId);
        foreach (var line in history)
        {
            await SendLineAsync(webSocket, line);
        }

        Action<string, string> logHandler = async (slug, line) =>
        {
            if (slug == instanceId && webSocket.State == WebSocketState.Open)
            {
                await SendLineAsync(webSocket, line);
            }
        };

        _logService.LogReceived += logHandler;

        try
        {
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                }
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected unexpectedly
        }
        finally
        {
            _logService.LogReceived -= logHandler;
        }
    }

    private static async Task SendLineAsync(WebSocket webSocket, string line)
    {
        var cleanLine = AnsiStripRegex.Replace(line, string.Empty);
        var bytes = Encoding.UTF8.GetBytes(cleanLine + "\n");
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
