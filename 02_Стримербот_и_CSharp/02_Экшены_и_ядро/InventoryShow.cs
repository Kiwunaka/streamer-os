using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string user = NormalizeUser(GetArg("targetUser", GetArg("userName", GetArg("user", "зритель"))));

        JObject state = ReadViewerGameState();
        JObject players = state["Players"] as JObject ?? new JObject();
        JObject player = players[user] as JObject;

        if (player == null)
        {
            CPH.SendMessage($"@{user} инвентарь пока пуст. Сначала нужен хотя бы один лут.");
            return true;
        }

        JArray inventory = player["inventory"] as JArray ?? new JArray();
        var items = inventory.OfType<JObject>().ToList();
        if (items.Count == 0)
        {
            CPH.SendMessage($"@{user} инвентарь пока пуст. Лут ещё не падал.");
            return true;
        }

        var top = items
            .OrderByDescending(x => RarityScore(x.Value<string>("rarity")))
            .ThenByDescending(x => x.Value<long?>("obtainedAt") ?? 0)
            .Take(3)
            .Select(x => $"{x.Value<string>("name")} [{RarityLabel(x.Value<string>("rarity"))}]")
            .ToList();

        int wins = player.Value<int?>("wins") ?? 0;
        int losses = player.Value<int?>("losses") ?? 0;
        string chat = $"@{user} инвентарь: {items.Count} предмет(ов). Топ: {string.Join(", ", top)}. Дуэли {wins}/{losses}.";
        CPH.SendMessage(chat);

        if (ParseBoolArg("runtime", false))
        {
            PublishRuntimeSummary(user, items.Count, wins, losses, top);
        }

        return true;
    }

    private JObject ReadViewerGameState()
    {
        string raw = CPH.GetGlobalVar<string>("ViewerGameState", true);
        try
        {
            JObject state = string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
            state["SchemaVersion"] = state["SchemaVersion"] ?? 1;
            state["Players"] = state["Players"] ?? new JObject();
            return state;
        }
        catch
        {
            return new JObject
            {
                ["SchemaVersion"] = 1,
                ["Players"] = new JObject()
            };
        }
    }

    private void PublishRuntimeSummary(string user, int totalItems, int wins, int losses, IReadOnlyList<string> top)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var envelope = new
        {
            type = "loot.drop.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.inventory",
            target = "runtime",
            priority = 35,
            ttlMs = 6000,
            payload = new
            {
                title = $"Инвентарь: {user}",
                subtitle = $"Предметов: {totalItems}",
                body = string.Join(" | ", top),
                rarity = "inventory",
                user = user,
                stats = new[]
                {
                    $"Предметов: {totalItems}",
                    $"Дуэли: {wins}/{losses}"
                }
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "inventory-show",
                minDisplayMs = 1500,
                cooldownKey = "inventory-show",
                cooldownMs = 1200
            }
        };

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(2);
                var content = new StringContent(JsonConvert.SerializeObject(envelope), Encoding.UTF8, "application/json");
                client.PostAsync(endpoint, content).Wait();
            }
        }
        catch (Exception ex)
        {
            CPH.LogDebug($"[InventoryShow] Runtime publish failed: {ex.Message}");
        }
    }

    private int RarityScore(string rarity)
    {
        switch ((rarity ?? string.Empty).ToLowerInvariant())
        {
            case "legendary": return 5;
            case "epic": return 4;
            case "rare": return 3;
            case "uncommon": return 2;
            default: return 1;
        }
    }

    private string RarityLabel(string rarity)
    {
        switch ((rarity ?? string.Empty).ToLowerInvariant())
        {
            case "legendary": return "легендарный";
            case "epic": return "эпический";
            case "rare": return "редкий";
            case "uncommon": return "необычный";
            default: return "обычный";
        }
    }

    private string NormalizeUser(string value)
    {
        string cleaned = (value ?? string.Empty).Trim();
        if (cleaned.StartsWith("@"))
        {
            cleaned = cleaned.Substring(1);
        }
        return string.IsNullOrWhiteSpace(cleaned) ? "зритель" : cleaned;
    }

    private bool ParseBoolArg(string key, bool fallback)
    {
        bool parsed;
        return bool.TryParse(GetArg(key, string.Empty), out parsed) ? parsed : fallback;
    }

    private string GetArg(string key, string fallback)
    {
        return args.ContainsKey(key) ? args[key]?.ToString() ?? fallback : fallback;
    }
}
