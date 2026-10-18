using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private static readonly Random Rng = new Random();

    public bool Execute()
    {
        string automation = CPH.GetGlobalVar<string>("AutomationState", true);
        if (!string.IsNullOrWhiteSpace(automation) && JObject.Parse(automation)["ViewerModes"]?["viewer_modes_enabled"]?.Value<bool>() == false) return true;
        string challenger = ResolveChallenger();
        string opponent = ResolveOpponent(challenger);

        if (string.IsNullOrWhiteSpace(challenger))
        {
            challenger = "Гость";
        }

        if (string.IsNullOrWhiteSpace(opponent))
        {
            CPH.SendMessage($"@{challenger} укажи соперника для дуэли.");
            return false;
        }

        if (string.Equals(challenger, opponent, StringComparison.OrdinalIgnoreCase))
        {
            CPH.SendMessage($"@{challenger} дуэль с самим собой выглядит эпично, но нужен настоящий соперник.");
            return false;
        }

        JObject state = ReadViewerGameState();
        JObject challengerPlayer = EnsurePlayer(state, challenger);
        JObject opponentPlayer = EnsurePlayer(state, opponent);

        DuelLoadout challengerLoadout = BuildLoadout(challengerPlayer);
        DuelLoadout opponentLoadout = BuildLoadout(opponentPlayer);

        int challengerRoll = Rng.Next(30, 101);
        int opponentRoll = Rng.Next(30, 101);
        int challengerPower = challengerRoll + challengerLoadout.Bonus;
        int opponentPower = opponentRoll + opponentLoadout.Bonus;

        string winner = challengerPower >= opponentPower ? challenger : opponent;
        string loser = string.Equals(winner, challenger, StringComparison.OrdinalIgnoreCase) ? opponent : challenger;
        int winnerPower = string.Equals(winner, challenger, StringComparison.OrdinalIgnoreCase) ? challengerPower : opponentPower;
        int loserPower = string.Equals(loser, challenger, StringComparison.OrdinalIgnoreCase) ? challengerPower : opponentPower;

        UpdateDuelStats(challengerPlayer, opponentPlayer, challenger, opponent, winner);
        state["LastEventId"] = Guid.NewGuid().ToString("N");
        CPH.SetGlobalVar("ViewerGameState", state.ToString(Formatting.None), true);

        string line = BuildBattleLine(challenger, opponent, winner, winnerPower, loserPower);
        CPH.SendMessage(line);
        PublishRuntimeEvent(challenger, opponent, winner, challengerPower, opponentPower, challengerLoadout, opponentLoadout);

        CPH.LogInfo($"[DuelMiniGame] {challenger} vs {opponent} => {winner}");
        return true;
    }

    private string ResolveChallenger()
    {
        string userName = NormalizeUser(GetArg("userName", GetArg("user", string.Empty)));
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        string raw = GetArg("rawInput", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string[] parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? NormalizeUser(parts[0]) : string.Empty;
    }

    private string ResolveOpponent(string challenger)
    {
        string opponent = NormalizeUser(GetArg("opponent", GetArg("targetUser", string.Empty)));
        if (!string.IsNullOrWhiteSpace(opponent))
        {
            return opponent;
        }

        string raw = GetArg("rawInput", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string[] parts = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string candidate = NormalizeUser(part);
            if (!string.IsNullOrWhiteSpace(candidate) && !string.Equals(candidate, challenger, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private string BuildBattleLine(string challenger, string opponent, string winner, int winnerPower, int loserPower)
    {
        string[] templates =
        {
            $"{challenger} и {opponent} сталкиваются в пиксельной пыли. Побеждает {winner} ({winnerPower}:{loserPower}).",
            $"Дуэль {challenger} vs {opponent}: арена дрожит, чат орёт, и {winner} вырывает победу ({winnerPower}:{loserPower}).",
            $"После текстового ульта и грязного ролла сильнее оказался {winner}. Счёт силы {winnerPower}:{loserPower}."
        };

        return templates[Rng.Next(templates.Length)];
    }

    private DuelLoadout BuildLoadout(JObject player)
    {
        JArray inventory = player["inventory"] as JArray ?? new JArray();
        JObject topItem = inventory
            .OfType<JObject>()
            .OrderByDescending(item => item.Value<int?>("bonus").GetValueOrDefault())
            .ThenByDescending(item => item.Value<long?>("obtainedAt").GetValueOrDefault())
            .FirstOrDefault();

        return new DuelLoadout
        {
            Bonus = topItem?.Value<int?>("bonus").GetValueOrDefault() ?? 0,
            ItemName = topItem?.Value<string>("name") ?? "Голые кулаки",
            Rarity = topItem?.Value<string>("rarity") ?? "common"
        };
    }

    private void UpdateDuelStats(JObject challengerPlayer, JObject opponentPlayer, string challenger, string opponent, string winner)
    {
        string now = DateTime.UtcNow.ToString("o");
        challengerPlayer["lastDuelAt"] = now;
        challengerPlayer["lastSeenAt"] = now;
        opponentPlayer["lastDuelAt"] = now;
        opponentPlayer["lastSeenAt"] = now;

        if (string.Equals(winner, challenger, StringComparison.OrdinalIgnoreCase))
        {
            challengerPlayer["wins"] = challengerPlayer.Value<int?>("wins").GetValueOrDefault() + 1;
            opponentPlayer["losses"] = opponentPlayer.Value<int?>("losses").GetValueOrDefault() + 1;
        }
        else
        {
            opponentPlayer["wins"] = opponentPlayer.Value<int?>("wins").GetValueOrDefault() + 1;
            challengerPlayer["losses"] = challengerPlayer.Value<int?>("losses").GetValueOrDefault() + 1;
        }
    }

    private void PublishRuntimeEvent(
        string challenger,
        string opponent,
        string winner,
        int challengerPower,
        int opponentPower,
        DuelLoadout challengerLoadout,
        DuelLoadout opponentLoadout)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var body = new
        {
            type = "duel.result.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.duel",
            target = "runtime",
            priority = 86,
            ttlMs = 8200,
            payload = new
            {
                title = $"{challenger} VS {opponent}",
                subtitle = $"Победитель: {winner}",
                body = $"{challenger}: {challengerLoadout.ItemName} (+{challengerLoadout.Bonus}) | {opponent}: {opponentLoadout.ItemName} (+{opponentLoadout.Bonus})",
                winner = winner,
                fighters = new object[]
                {
                    new { name = challenger, power = challengerPower, item = challengerLoadout.ItemName, rarity = challengerLoadout.Rarity },
                    new { name = opponent, power = opponentPower, item = opponentLoadout.ItemName, rarity = opponentLoadout.Rarity }
                },
                stats = new[]
                {
                    $"{challenger}: {challengerPower}",
                    $"{opponent}: {opponentPower}",
                    $"Winner: {winner}"
                }
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "duel-result",
                minDisplayMs = 2000,
                cooldownKey = "duel-result",
                cooldownMs = 1200
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
            CPH.LogDebug($"[DuelMiniGame] Runtime publish failed: {ex.Message}");
        }
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

    private JObject EnsurePlayer(JObject state, string userName)
    {
        JObject players = state["Players"] as JObject ?? new JObject();
        state["Players"] = players;

        JObject player = players.GetValue(userName, StringComparison.OrdinalIgnoreCase) as JObject;
        if (player == null)
        {
            player = new JObject
            {
                ["inventory"] = new JArray(),
                ["wins"] = 0,
                ["losses"] = 0,
                ["drops"] = 0,
                ["rareDrops"] = 0,
                ["legendaryDrops"] = 0,
                ["lastDropAt"] = string.Empty,
                ["lastDuelAt"] = string.Empty,
                ["lastSeenAt"] = string.Empty
            };
            players[userName] = player;
        }

        if (!(player["inventory"] is JArray))
        {
            player["inventory"] = new JArray();
        }

        return player;
    }

    private string NormalizeUser(string value)
    {
        string cleaned = (value ?? string.Empty).Trim();
        if (cleaned.StartsWith("@"))
        {
            cleaned = cleaned.Substring(1);
        }

        if (string.Equals(cleaned, "!дуэль", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(cleaned, "!duel", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return cleaned;
    }

    private string GetArg(string key, string fallback)
    {
        return args.ContainsKey(key) ? args[key]?.ToString() ?? fallback : fallback;
    }

    private sealed class DuelLoadout
    {
        public int Bonus { get; set; }
        public string ItemName { get; set; }
        public string Rarity { get; set; }
    }
}
