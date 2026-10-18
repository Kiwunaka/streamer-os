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
        string title = GetArg("title", "");
        string artist = GetArg("artist", "");
        string source = GetArg("source", "manual");
        string status = GetArg("status", string.IsNullOrWhiteSpace(title) ? "stopped" : "playing");
        string nextTitle = GetArg("nextTitle", "");
        string nextArtist = GetArg("nextArtist", "");
        string cover = GetArg("cover", "");
        int progress = ParseInt("progressMs", 0);
        int duration = ParseInt("durationMs", 0);

        UpdateStreamState(title, artist, status == "playing");

        var stats = new List<string>
        {
            $"Source: {source}",
            $"Status: {status}"
        };
        if (duration > 0)
        {
            stats.Add($"Progress: {Math.Max(0, progress / 1000)}s / {Math.Max(0, duration / 1000)}s");
        }
        if (!string.IsNullOrWhiteSpace(nextTitle))
        {
            stats.Add($"Next: {nextArtist} {nextTitle}".Trim());
        }

        var payload = new JObject
        {
            ["title"] = string.IsNullOrWhiteSpace(title) ? "Playback update" : title,
            ["subtitle"] = artist,
            ["body"] = string.IsNullOrWhiteSpace(title)
                ? $"Playback is now {status}"
                : $"Now playing from {source}",
            ["source"] = source,
            ["status"] = status,
            ["cover"] = cover,
            ["progress"] = progress,
            ["duration"] = duration,
            ["stats"] = new JArray(stats)
        };

        if (!string.IsNullOrWhiteSpace(nextTitle))
        {
            payload["nextTrack"] = new JObject
            {
                ["title"] = nextTitle,
                ["artist"] = nextArtist
            };
        }

        PublishRuntimeEvent(payload);
        CPH.LogInfo($"[RuntimeNowPlayingPush] {title} / {artist} ({status})");
        return true;
    }

    private void UpdateStreamState(string title, string artist, bool isPlaying)
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
        state["CurrentGame"] = state["CurrentGame"] ?? "Unknown";
        state["StreamMode"] = state["StreamMode"] ?? "live";
        state["ActiveScene"] = state["ActiveScene"] ?? "";
        state["Music"] = state["Music"] ?? new JObject();
        state["Music"]["TrackTitle"] = title;
        state["Music"]["Artist"] = artist;
        state["Music"]["IsPlaying"] = isPlaying;
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
    }

    private void PublishRuntimeEvent(JObject payload)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var body = new
        {
            type = "music.track.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.now-playing",
            target = "runtime",
            priority = 20,
            ttlMs = 7000,
            payload = payload,
            queue = new
            {
                interruptible = true,
                replaceByKey = "music-track",
                minDisplayMs = 900,
                cooldownKey = "music-track",
                cooldownMs = 250
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
            CPH.LogDebug($"[RuntimeNowPlayingPush] Runtime publish failed: {ex.Message}");
        }
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
}
