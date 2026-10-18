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
        var okItems = new List<string>();
        var warnings = new List<string>();

        CheckRequiredKey("VisualRuntimeEndpoint", okItems, warnings);
        string llm = CPH.GetGlobalVar<string>("LlmProvider", true) ?? "openrouter";
        var keys = new HashSet<string>();
        foreach (string setting in new[] { "ChatBotProvider", "ModProvider", "OracleProvider", "DonationProvider", "StreamRecapProvider" })
        {
            string provider = CPH.GetGlobalVar<string>(setting, true);
            if (string.IsNullOrWhiteSpace(provider)) provider = llm;
            keys.Add(provider == "fireworks" ? "FireworksKey" : "OpenRouterKey");
        }
        foreach (string setting in new[] { "TtsProviderPrimary", "TtsProviderSecondary", "TtsProviderTertiary" })
        {
            string provider = CPH.GetGlobalVar<string>(setting, true);
            switch (provider)
            {
                case "inworld": keys.Add("InworldTTSAuth"); keys.Add("InworldVoiceId"); break;
                case "fish": keys.Add("FishAudioApiKey"); keys.Add("FishAudioVoiceRefId"); break;
                case "elevenlabs": keys.Add("ElevenLabsKey"); keys.Add("ElevenLabsVoiceId"); break;
                case "ntts": keys.Add("NttsApiKey"); break;
            }
        }
        string imageProvider = CPH.GetGlobalVar<string>("ImageGenerationProvider", true);
        keys.Add(imageProvider == "fal" ? "FalApiKey" : "FireworksKey");
        if (CPH.GetGlobalVar<string>("DonationImageProvider", true) == "fal") keys.Add("FalApiKey");
        foreach (string key in keys) CheckRequiredKey(key, okItems, warnings);

        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            CheckRuntimeEndpoint(endpoint, okItems, warnings);
        }

        UpdateAutomationState(warnings);

        string summary = $"Startup check: {okItems.Count} ok, {warnings.Count} warning(s)";
        PublishToast(summary, warnings.Count == 0 ? "info" : "warn");

        CPH.LogInfo("[StartupHealthCheck] " + summary);
        foreach (string item in okItems)
        {
            CPH.LogInfo("[StartupHealthCheck][OK] " + item);
        }
        foreach (string item in warnings)
        {
            CPH.LogWarn("[StartupHealthCheck][WARN] " + item);
        }

        return true;
    }

    private void CheckRequiredKey(string key, List<string> okItems, List<string> warnings)
    {
        string value = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(value) || value.Contains("<REPLACE_ME>") || value.EndsWith("..."))
        {
            warnings.Add($"{key} is missing or still has a placeholder.");
        }
        else
        {
            okItems.Add($"{key} is set");
        }
    }

    private void CheckOptionalKey(string key, List<string> okItems, List<string> warnings, string warningMessage)
    {
        string value = CPH.GetGlobalVar<string>(key, true);
        if (string.IsNullOrWhiteSpace(value))
        {
            warnings.Add(warningMessage);
        }
        else
        {
            okItems.Add($"{key} is set");
        }
    }

    private bool GetBoolGlobal(string key, bool fallback)
    {
        string value = CPH.GetGlobalVar<string>(key, true);
        bool parsed;
        return bool.TryParse(value, out parsed) ? parsed : fallback;
    }

    private void CheckRuntimeEndpoint(string endpoint, List<string> okItems, List<string> warnings)
    {
        try
        {
            var url = new Uri(endpoint);
            var builder = new UriBuilder(url.Scheme, url.Host, url.Port, "/runtime/state");

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = client.GetAsync(builder.Uri).Result;
                if (response.IsSuccessStatusCode && JObject.Parse(response.Content.ReadAsStringAsync().Result)["pendingCount"] != null)
                {
                    okItems.Add("OBS runtime endpoint responded");
                }
                else
                {
                    warnings.Add($"OBS runtime endpoint returned {response.StatusCode}.");
                }
            }
        }
        catch (Exception ex)
        {
            warnings.Add("OBS runtime endpoint check failed: " + ex.Message);
        }
    }

    private void UpdateAutomationState(List<string> warnings)
    {
        string raw = CPH.GetGlobalVar<string>("AutomationState", true);
        JObject state;
        try
        {
            state = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
        }
        catch
        {
            state = new JObject();
        }

        state["SchemaVersion"] = state["SchemaVersion"] ?? 1;
        state["LastFailureReason"] = warnings.Count == 0 ? "" : string.Join(" | ", warnings);
        CPH.SetGlobalVar("AutomationState", state.ToString(Formatting.None), true);
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
            source = "streamerbot.health-check",
            target = "system",
            priority = 35,
            ttlMs = 7000,
            payload = new
            {
                message = message,
                level = level
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "startup-health",
                minDisplayMs = 1200,
                cooldownKey = "startup-health",
                cooldownMs = 1000
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
            CPH.LogDebug($"[StartupHealthCheck] Runtime publish failed: {ex.Message}");
        }
    }
}
