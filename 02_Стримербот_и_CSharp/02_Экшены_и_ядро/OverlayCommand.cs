using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string type = GetArg("type", "overlay.show.v1");
        string target = GetArg("target", "runtime");
        string payloadJson = GetArg("payloadJson", "{}");
        string replaceByKey = GetArg("replaceByKey", type);
        string cooldownKey = GetArg("cooldownKey", replaceByKey);

        JObject payload;
        try
        {
            payload = string.IsNullOrWhiteSpace(payloadJson) ? new JObject() : JObject.Parse(payloadJson);
        }
        catch
        {
            payload = new JObject
            {
                ["message"] = payloadJson
            };
        }

        var body = new
        {
            type = type,
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.overlay-command",
            target = target,
            priority = ParseInt("priority", 50),
            ttlMs = ParseInt("ttlMs", 8000),
            payload = payload,
            queue = new
            {
                interruptible = ParseBool("interruptible", true),
                replaceByKey = replaceByKey,
                minDisplayMs = ParseInt("minDisplayMs", 1200),
                cooldownKey = cooldownKey,
                cooldownMs = ParseInt("cooldownMs", 500)
            }
        };

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                var endpoint = GetArg("endpoint", CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true));
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    endpoint = "http://127.0.0.1:1608/events";
                }

                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var response = client.PostAsync(endpoint, content).Result;
                if (!response.IsSuccessStatusCode)
                {
                    CPH.LogWarn($"[OverlayCommand] Runtime event returned {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"[OverlayCommand] Runtime publish failed: {ex.Message}");
            return false;
        }

        return true;
    }

    private string GetArg(string key, string fallback)
    {
        return args.ContainsKey(key) ? args[key]?.ToString() : fallback;
    }

    private int ParseInt(string key, int fallback)
    {
        int parsed;
        return int.TryParse(GetArg(key, ""), out parsed) ? parsed : fallback;
    }

    private bool ParseBool(string key, bool fallback)
    {
        bool parsed;
        return bool.TryParse(GetArg(key, ""), out parsed) ? parsed : fallback;
    }
}
