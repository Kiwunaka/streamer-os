using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        var automation = LoadJsonState("AutomationState");
        var stream = LoadJsonState("StreamState");

        bool current = automation.Value<bool?>("SafeModeEnabled")
            ?? stream["FeatureFlags"]?["SafeModeEnabled"]?.Value<bool>()
            ?? false;

        bool next = ResolveNextValue(current);

        automation["SchemaVersion"] = automation["SchemaVersion"] ?? 1;
        automation["SafeModeEnabled"] = next;
        automation["ViewerModes"] = automation["ViewerModes"] ?? new JObject();

        stream["SchemaVersion"] = stream["SchemaVersion"] ?? 1;
        stream["FeatureFlags"] = stream["FeatureFlags"] ?? new JObject();
        stream["FeatureFlags"]["ChatBotEnabled"] = stream["FeatureFlags"]["ChatBotEnabled"] ?? true;
        stream["FeatureFlags"]["SafeModeEnabled"] = next;
        stream["FeatureFlags"]["ViewerEventModesEnabled"] = stream["FeatureFlags"]["ViewerEventModesEnabled"] ?? true;

        if (next)
        {
            automation["ViewerModes"]["chatbot_auto_enabled"] = false;
            CPH.SetGlobalVar("ChatBotAutoEnabled", "false", true);
        }

        CPH.SetGlobalVar("SafeModeEnabled", next.ToString().ToLowerInvariant(), true);
        CPH.SetGlobalVar("ChatBotStrictSafety", next.ToString().ToLowerInvariant(), true);
        CPH.SetGlobalVar("ImageReactionStrictSafety", next.ToString().ToLowerInvariant(), true);
        CPH.SetGlobalVar("ChatBotSharpnessMode", next ? "safe" : "normal", true);
        CPH.SetGlobalVar("AutomationState", automation.ToString(Formatting.None), true);
        CPH.SetGlobalVar("StreamState", stream.ToString(Formatting.None), true);

        string reason = GetArg("reason", "");
        string message = next ? "Safe Mode enabled" : "Safe Mode disabled";
        if (!string.IsNullOrWhiteSpace(reason))
        {
            message += $": {reason}";
        }

        PublishToast(message, next ? "warn" : "info");
        CPH.LogInfo($"[SafeModeToggle] {message}");
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
            source = "streamerbot.safe-mode",
            target = "system",
            priority = 70,
            ttlMs = 6000,
            payload = new
            {
                message = message,
                level = level
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "safe-mode",
                minDisplayMs = 1000,
                cooldownKey = "safe-mode",
                cooldownMs = 500
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
            CPH.LogDebug($"[SafeModeToggle] Runtime publish failed: {ex.Message}");
        }
    }
}
