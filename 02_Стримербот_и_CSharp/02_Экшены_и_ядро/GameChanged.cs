using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

// =======================================================
// Game Changed Handler
// Updates game info when category changes mid-stream
// Trigger: Twitch → Channel Update (if available) or manual
// =======================================================

public class CPHInline
{
    public bool Execute()
    {
        // Get game info from args or fetch current
        string game = args.ContainsKey("game") ? args["game"].ToString() : "";
        
        if (string.IsNullOrEmpty(game))
        {
            // If not from trigger, maybe from manual update
            game = args.ContainsKey("rawInput") ? args["rawInput"].ToString() : "";
        }
        
        if (!string.IsNullOrEmpty(game))
        {
            CPH.SetGlobalVar("CurrentGame", game, true);
            UpdateStreamState(game);
            CPH.LogInfo($"[GameChanged] Now playing: {game}");
            
            // Notify
            var data = new { action = "game_changed", game = game };
            CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(data));
        }
        
        return true;
    }

    private void UpdateStreamState(string game)
    {
        string raw = CPH.GetGlobalVar<string>("StreamState", true);
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
        state["CurrentGame"] = game;
        state["StreamMode"] = state["StreamMode"] ?? "live";
        state["ActiveScene"] = state["ActiveScene"] ?? "";
        state["Music"] = state["Music"] ?? new JObject
        {
            ["TrackTitle"] = "",
            ["Artist"] = "",
            ["IsPlaying"] = false
        };
        state["ModerationMode"] = state["ModerationMode"] ?? "standard";
        state["FeatureFlags"] = state["FeatureFlags"] ?? new JObject
        {
            ["ChatBotEnabled"] = true,
            ["SafeModeEnabled"] = false,
            ["ViewerEventModesEnabled"] = true
        };
        state["ProviderRouting"] = state["ProviderRouting"] ?? new JObject
        {
            ["PrimaryLlmProvider"] = "openrouter",
            ["PrimaryVisionProvider"] = "fireworks",
            ["PrimaryTtsProvider"] = "fish",
            ["PrimaryImageProvider"] = "fireworks",
            ["FallbackLlmProvider"] = "fireworks",
            ["FallbackTtsProvider"] = "ntts",
            ["TextModel"] = "",
            ["VisionModel"] = "",
            ["MaxLlmPriceUsd"] = null
        };
        CPH.SetGlobalVar("StreamState", state.ToString(Formatting.None), true);

        PublishRuntimeEvent("system.toast.v1", new
        {
            message = $"Game updated: {game}",
            level = "info"
        }, "system", 5000);
    }

    private void PublishRuntimeEvent(string type, object payload, string target, int ttlMs)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var body = new
        {
            type = type,
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.game",
            target = target,
            priority = 30,
            ttlMs = ttlMs,
            payload = payload,
            queue = new
            {
                interruptible = true,
                replaceByKey = type,
                minDisplayMs = 1200,
                cooldownKey = type,
                cooldownMs = 1500
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
            CPH.LogDebug($"[GameChanged] Runtime event failed: {ex.Message}");
        }
    }
}
