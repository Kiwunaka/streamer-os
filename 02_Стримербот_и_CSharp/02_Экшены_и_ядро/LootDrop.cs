using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private readonly Random _rng = new Random();

    public bool Execute()
    {
        string user = NormalizeUser(GetArg("userName", GetArg("user", "зритель")));

        JObject state = ReadViewerGameState();
        JObject players = state["Players"] as JObject ?? new JObject();
        JObject player = EnsurePlayer(players, user);

        LootItem item = RollItem();
        item.ObtainedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        JArray inventory = player["inventory"] as JArray ?? new JArray();
        inventory.Add(JObject.FromObject(new
        {
            name = item.Name,
            rarity = item.Rarity,
            category = item.Category,
            bonus = item.Bonus,
            obtainedAt = item.ObtainedAt
        }));

        player["inventory"] = inventory;
        player["lastDropAt"] = item.ObtainedAt;
        player["drops"] = (player.Value<int?>("drops") ?? 0) + 1;
        player["lastRarity"] = item.Rarity;

        if (IsRareOrBetter(item.Rarity))
        {
            player["rareDrops"] = (player.Value<int?>("rareDrops") ?? 0) + 1;
        }

        if (string.Equals(item.Rarity, "legendary", StringComparison.OrdinalIgnoreCase))
        {
            player["legendaryDrops"] = (player.Value<int?>("legendaryDrops") ?? 0) + 1;
        }

        state["SchemaVersion"] = state["SchemaVersion"] ?? 1;
        state["Players"] = players;
        state["LastEventId"] = Guid.NewGuid().ToString("N");
        CPH.SetGlobalVar("ViewerGameState", state.ToString(Formatting.None), true);

        CPH.SendMessage(BuildChatMessage(user, item));
        PublishRuntimeLoot(user, item, inventory.Count);
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
            state["Season"] = state["Season"] ?? "alpha";
            state["LastEventId"] = state["LastEventId"] ?? string.Empty;
            return state;
        }
        catch
        {
            return new JObject
            {
                ["SchemaVersion"] = 1,
                ["Players"] = new JObject(),
                ["Season"] = "alpha",
                ["LastEventId"] = string.Empty
            };
        }
    }

    private JObject EnsurePlayer(JObject players, string user)
    {
        JObject player = players[user] as JObject;
        if (player == null)
        {
            player = new JObject();
            players[user] = player;
        }

        player["inventory"] = player["inventory"] ?? new JArray();
        player["wins"] = player["wins"] ?? 0;
        player["losses"] = player["losses"] ?? 0;
        player["drops"] = player["drops"] ?? 0;
        player["rareDrops"] = player["rareDrops"] ?? 0;
        player["legendaryDrops"] = player["legendaryDrops"] ?? 0;
        player["lastDropAt"] = player["lastDropAt"] ?? 0;
        player["lastDuelAt"] = player["lastDuelAt"] ?? 0;
        player["lastSeenAt"] = player["lastSeenAt"] ?? DateTime.UtcNow.ToString("o");
        return player;
    }

    private LootItem RollItem()
    {
        int roll = _rng.Next(1000);
        string rarity =
            roll < 8 ? "legendary" :
            roll < 45 ? "epic" :
            roll < 160 ? "rare" :
            roll < 420 ? "uncommon" :
            "common";

        List<LootTemplate> pool = GetPool(rarity);
        LootTemplate template = pool[_rng.Next(pool.Count)];

        return new LootItem
        {
            Name = template.Name,
            Category = template.Category,
            Rarity = rarity,
            Bonus = RollBonus(rarity)
        };
    }

    private int RollBonus(string rarity)
    {
        switch (rarity)
        {
            case "legendary": return _rng.Next(9, 13);
            case "epic": return _rng.Next(6, 9);
            case "rare": return _rng.Next(4, 7);
            case "uncommon": return _rng.Next(2, 4);
            default: return _rng.Next(0, 2);
        }
    }

    private List<LootTemplate> GetPool(string rarity)
    {
        switch (rarity)
        {
            case "legendary":
                return new List<LootTemplate>
                {
                    new LootTemplate("Корона патчей", "артефакт"),
                    new LootTemplate("Мифический банхаммер", "оружие"),
                    new LootTemplate("Тотем лута", "реликвия")
                };
            case "epic":
                return new List<LootTemplate>
                {
                    new LootTemplate("Фиолетовый геймпад судьбы", "снаряжение"),
                    new LootTemplate("Неоновый талисман критов", "талисман"),
                    new LootTemplate("Кассета драматичных побед", "реликвия")
                };
            case "rare":
                return new List<LootTemplate>
                {
                    new LootTemplate("Синий шлем уверенности", "броня"),
                    new LootTemplate("Руна чата", "талисман"),
                    new LootTemplate("Монета удачного ролла", "артефакт")
                };
            case "uncommon":
                return new List<LootTemplate>
                {
                    new LootTemplate("Потертый амулет стримера", "талисман"),
                    new LootTemplate("Кружка выживания", "утиль"),
                    new LootTemplate("Карманный бафф к морали", "расходник")
                };
            default:
                return new List<LootTemplate>
                {
                    new LootTemplate("Сухарик героя", "утиль"),
                    new LootTemplate("Пыльный пиксель", "мусор"),
                    new LootTemplate("Носок рейдера", "утиль")
                };
        }
    }

    private string BuildChatMessage(string user, LootItem item)
    {
        string rarityLabel = RarityLabel(item.Rarity);
        if (IsRareOrBetter(item.Rarity))
        {
            return $"@{user} выбил {rarityLabel} предмет: {item.Name} (+{item.Bonus} к дуэлям). Чат, фиксируем редкий дроп.";
        }

        return $"@{user} нашёл {item.Name} [{rarityLabel}, +{item.Bonus}]. В инвентарь уехало.";
    }

    private void PublishRuntimeLoot(string user, LootItem item, int inventoryCount)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        int priority = IsRareOrBetter(item.Rarity) ? 88 : 45;
        int ttl = IsRareOrBetter(item.Rarity) ? 9000 : 4500;

        var envelope = new
        {
            type = "loot.drop.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.loot",
            target = "runtime",
            priority = priority,
            ttlMs = ttl,
            payload = new
            {
                title = item.Name,
                subtitle = $"{RarityLabel(item.Rarity)} дроп для {user}",
                body = $"Категория: {item.Category}. Бонус к дуэлям: +{item.Bonus}. В инвентаре теперь {inventoryCount} предмет(ов).",
                rarity = item.Rarity,
                user = user,
                category = item.Category,
                bonus = item.Bonus,
                stats = new[]
                {
                    $"Редкость: {RarityLabel(item.Rarity)}",
                    $"Бонус: +{item.Bonus}",
                    $"Инвентарь: {inventoryCount}"
                }
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = IsRareOrBetter(item.Rarity) ? "loot-drop-rare" : "loot-drop",
                minDisplayMs = 1800,
                cooldownKey = "loot-drop",
                cooldownMs = IsRareOrBetter(item.Rarity) ? 500 : 1200
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
            CPH.LogDebug($"[LootDrop] Runtime publish failed: {ex.Message}");
        }
    }

    private bool IsRareOrBetter(string rarity)
    {
        return string.Equals(rarity, "rare", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rarity, "epic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(rarity, "legendary", StringComparison.OrdinalIgnoreCase);
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

    private string GetArg(string key, string fallback)
    {
        return args.ContainsKey(key) ? args[key]?.ToString() ?? fallback : fallback;
    }

    private sealed class LootTemplate
    {
        public LootTemplate(string name, string category)
        {
            Name = name;
            Category = category;
        }

        public string Name { get; }
        public string Category { get; }
    }

    private sealed class LootItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string Rarity { get; set; }
        public int Bonus { get; set; }
        public long ObtainedAt { get; set; }
    }
}
