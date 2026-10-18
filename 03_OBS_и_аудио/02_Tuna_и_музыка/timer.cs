using System;
using Newtonsoft.Json;

// Stream start timer control via global variables.
public class CPHInline
{
    public bool Execute()
    {
        string mode = args.ContainsKey("mode") ? args["mode"]?.ToString()?.ToLowerInvariant() : "start";
        if (mode == "stop" || mode == "clear")
        {
            CPH.SetGlobalVar("StreamStartAt", "0", true);
            CPH.SetGlobalVar("StreamStartTitle", "", true);
            CPH.SetGlobalVar("StreamStartSubtitle", "", true);
            CPH.SetGlobalVar("StreamStartLabel", "", true);
            var clearPayload = new {
                action = "stream_start",
                start_at = 0,
                title = "",
                subtitle = "",
                label = ""
            };
            CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(clearPayload));
            CPH.LogInfo("[StreamStart] Cleared.");
            return true;
        }

        int minutes = 10;
        int seconds = 0;
        if (args.ContainsKey("minutes")) int.TryParse(args["minutes"]?.ToString(), out minutes);
        if (args.ContainsKey("seconds")) int.TryParse(args["seconds"]?.ToString(), out seconds);
        if (minutes < 0) minutes = 0;
        if (seconds < 0) seconds = 0;

        string title = args.ContainsKey("title") ? args["title"]?.ToString() : "СТАРТ СТРИМА";
        string subtitle = args.ContainsKey("subtitle") ? args["subtitle"]?.ToString() : "Готовим сцену, звук и магию. Врываемся красиво.";
        string label = args.ContainsKey("label") ? args["label"]?.ToString() : "Запуск через";

        long startMs = DateTimeOffset.UtcNow.AddMinutes(minutes).AddSeconds(seconds).ToUnixTimeMilliseconds();

        CPH.SetGlobalVar("StreamStartAt", startMs.ToString(), true);
        CPH.SetGlobalVar("StreamStartTitle", title, true);
        CPH.SetGlobalVar("StreamStartSubtitle", subtitle, true);
        CPH.SetGlobalVar("StreamStartLabel", label, true);
        var payload = new {
            action = "stream_start",
            start_at = startMs,
            title = title,
            subtitle = subtitle,
            label = label
        };
        // Broadcasting to default WebSocket server
        CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(payload));
        CPH.LogInfo($"[StreamStart] Scheduled: {startMs} (in {minutes}m {seconds}s).");

        return true;
    }
}
