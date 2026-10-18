using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string mode = GetArg("mode", "viewer_modes_enabled").Trim();
        if (string.IsNullOrWhiteSpace(mode))
        {
            mode = "viewer_modes_enabled";
        }

        var automation = LoadJsonState("AutomationState");
        var stream = LoadJsonState("StreamState");

        automation["SchemaVersion"] = automation["SchemaVersion"] ?? 1;
        automation["ViewerModes"] = automation["ViewerModes"] ?? new JObject();
        stream["SchemaVersion"] = stream["SchemaVersion"] ?? 1;
        stream["FeatureFlags"] = stream["FeatureFlags"] ?? new JObject();

        var viewerModes = (JObject)automation["ViewerModes"];
        bool current = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase)
            ? viewerModes.Value<bool?>("viewer_modes_enabled") ?? false
            : viewerModes.Value<bool?>(mode) ?? false;
        bool next = ResolveNextValue(current);

        if (string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in new[] { "viewer_modes_enabled", "chatbot_enabled", "chatbot_auto_enabled", "polls_enabled", "combo_alerts_enabled", "milestones_enabled" })
            {
                viewerModes[key] = next;
            }
        }
        else
        {
            viewerModes[mode] = next;
        }

        if (string.Equals(mode, "viewer_modes_enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            stream["FeatureFlags"]["ViewerEventModesEnabled"] = next;
        }

        if (string.Equals(mode, "chatbot_enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            CPH.SetGlobalVar("ChatBotEnabled", next.ToString().ToLowerInvariant(), true);
            stream["FeatureFlags"]["ChatBotEnabled"] = next;
        }

        if (string.Equals(mode, "chatbot_auto_enabled", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase))
        {
            CPH.SetGlobalVar("ChatBotAutoEnabled", next.ToString().ToLowerInvariant(), true);
        }

        CPH.SetGlobalVar("AutomationState", automation.ToString(Formatting.None), true);
        CPH.SetGlobalVar("StreamState", stream.ToString(Formatting.None), true);

        string label = string.Equals(mode, "all", StringComparison.OrdinalIgnoreCase) ? "Viewer modes" : mode;
        string message = $"{label} {(next ? "enabled" : "disabled")}";

        PublishToast(message, next ? "info" : "warn");
        CPH.LogInfo($"[ViewerModesToggle] {message}");
        return true;
    }

    private JObject LoadJsonState(string key)
    {
        string raw = CPH.GetGlobalVar<string>(key, true);
        try
        {
            return string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        }
        catch
        {
            return new JObject();
        }
    }

    private bool ResolveNextValue(bool current)
    {
        bool parsed;
        string enabledArg = GetArg("enabled", "");
        if (bool.TryParse(enabledArg, out parsed))
        {
            return parsed;
        }

        return !current;
    }

    private string GetArg(string key, string fallback)
    {
        return args.ContainsKey(key) ? args[key]?.ToString() : fallback;
    }

    private void PublishToast(string message, string level)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var body = new
        {
            type = "system.toast.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.viewer-modes",
            target = "system",
            priority = 45,
            ttlMs = 5000,
            payload = new
            {
                message = message,
                level = level
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "viewer-modes",
                minDisplayMs = 1000,
                cooldownKey = "viewer-modes",
                cooldownMs = 350
            }
        };

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                client.PostAsync(endpoint, content).Wait();
            }
        }
        catch (Exception ex)
        {
            CPH.LogDebug($"[ViewerModesToggle] Runtime publish failed: {ex.Message}");
        }
    }
}
