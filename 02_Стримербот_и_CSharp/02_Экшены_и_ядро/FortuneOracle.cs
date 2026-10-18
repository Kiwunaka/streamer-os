using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    private const string DEFAULT_OPENROUTER_MODEL = "xiaomi/mimo-v2-flash";
    private const string DEFAULT_FIREWORKS_MODEL = "accounts/fireworks/models/kimi-k2-instruct-0905";

    private readonly Random _rng = new Random();

    public bool Execute()
    {
        string mode = ResolveMode();
        string user = ResolveUser();
        string topic = ResolveTopic();

        string answer = GenerateAnswer(mode, user, topic);
        PublishRuntimeEvent(mode, user, topic, answer);
        CPH.SendMessage($"@{user} {answer}");

        if (ParseBoolArg("tts", false))
        {
            string ttsAction = CPH.GetGlobalVar<string>("ChatBotTtsAction", true);
            if (string.IsNullOrWhiteSpace(ttsAction))
            {
                ttsAction = "TTS";
            }

            CPH.SetArgument("rawInput", answer);
            CPH.RunAction(ttsAction);
        }

        CPH.LogInfo($"[FortuneOracle] Mode={mode} User={user} Topic={topic}");
        return true;
    }

    private string ResolveMode()
    {
        string raw = GetArg("mode", "");
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = GetArg("rawInput", "");
        }

        string lower = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (lower.Contains("таро") || lower.Contains("taro") || lower.Contains("tarot"))
        {
            return "tarot";
        }

        if (lower.Contains("гороскоп") || lower.Contains("horoscope"))
        {
            return "horoscope";
        }

        if (lower.Contains("анекдот") || lower.Contains("joke"))
        {
            return "anekdot";
        }

        return string.Equals(GetArg("mode", ""), "anekdot", StringComparison.OrdinalIgnoreCase)
            ? "anekdot"
            : "tarot";
    }

    private string ResolveUser()
    {
        string user = GetArg("userName", GetArg("user", ""));
        return string.IsNullOrWhiteSpace(user) ? "зритель" : user;
    }

    private string ResolveTopic()
    {
        string raw = GetArg("topic", "");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        raw = GetArg("rawInput", "");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string cleaned = Regex.Replace(raw, "!таро|!гороскоп|!анекдот|tarot|horoscope|anekdot", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

        return cleaned;
    }

    private string GenerateAnswer(string mode, string user, string topic)
    {
        string provider = GetGlobal("OracleProvider", GetGlobal("LlmProvider", "openrouter")).ToLowerInvariant();
        string key = provider == "fireworks"
            ? CPH.GetGlobalVar<string>("FireworksKey", true)
            : CPH.GetGlobalVar<string>("OpenRouterKey", true);

        if (!string.IsNullOrWhiteSpace(key))
        {
            string ai = provider == "fireworks"
                ? GenerateViaFireworks(mode, user, topic, key)
                : GenerateViaOpenRouter(mode, user, topic, key);
            if (!string.IsNullOrWhiteSpace(ai))
            {
                return CleanAnswer(ai.Trim(), user);
            }
        }

        return CleanAnswer(GenerateFallback(mode, user, topic), user);
    }

    private string GenerateViaOpenRouter(string mode, string user, string topic, string apiKey)
    {
        string systemPrompt;
        string userPrompt;

        switch (mode)
        {
            case "horoscope":
                systemPrompt = "Ты игровой астролог для стрима. Пиши коротко, смешно, на русском, 1-2 предложения, без занудства.";
                userPrompt = $"Сделай игровой гороскоп для зрителя {user}. Тема: {SafeTopic(topic, "сегодняшний стрим")}. Должно быть смешно, чуть абсурдно, но коротко.";
                break;
            case "anekdot":
                systemPrompt = "Ты генератор коротких стримовых анекдотов. Пиши по-русски, очень коротко, 1-2 фразы, без пошлости и жёсткого треша.";
                userPrompt = $"Придумай короткий анекдот для стрима. Если есть тема, используй её: {SafeTopic(topic, "чат, игры и стримерский быт")}.";
                break;
            default:
                systemPrompt = "Ты мистический стримовый таролог. Делаешь короткие, яркие, мемные предсказания по-русски. 1-2 предложения.";
                userPrompt = $"Сделай краткий tarot-style ответ для зрителя {user}. Тема: {SafeTopic(topic, "удача на стриме и игровые приключения")}.";
                break;
        }

        string model = CPH.GetGlobalVar<string>("OracleModel", true);
        if (string.IsNullOrWhiteSpace(model))
        {
            model = GetGlobal("OpenRouterDefaultTextModel", DEFAULT_OPENROUTER_MODEL);
        }

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                string site = CPH.GetGlobalVar<string>("OpenRouterSiteUrl", true);
                string appName = CPH.GetGlobalVar<string>("OpenRouterAppName", true);
                if (!string.IsNullOrWhiteSpace(site))
                {
                    client.DefaultRequestHeaders.Add("HTTP-Referer", site);
                }
                if (!string.IsNullOrWhiteSpace(appName))
                {
                    client.DefaultRequestHeaders.Add("X-Title", appName);
                }

                var payload = new
                {
                    model = model,
                    temperature = 0.9,
                    max_tokens = 120,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    }
                };

                var response = client.PostAsync(
                    "https://openrouter.ai/api/v1/chat/completions",
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
                ).Result;

                if (!response.IsSuccessStatusCode)
                {
                    CPH.LogWarn($"[FortuneOracle] OpenRouter returned {response.StatusCode}");
                    return null;
                }

                JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                return json["choices"]?[0]?["message"]?["content"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FortuneOracle] OpenRouter failed: {ex.Message}");
            return null;
        }
    }

    private string GenerateViaFireworks(string mode, string user, string topic, string apiKey)
    {
        string systemPrompt;
        string userPrompt;

        switch (mode)
        {
            case "horoscope":
                systemPrompt = "Ты игровой астролог для стрима. Пиши коротко, смешно, на русском, 1-2 предложения, без духоты.";
                userPrompt = $"Сделай игровой гороскоп для зрителя {user}. Тема: {SafeTopic(topic, "сегодняшний стрим")}. Должно быть смешно и коротко.";
                break;
            case "anekdot":
                systemPrompt = "Ты генератор коротких стримовых анекдотов. Пиши по-русски, очень коротко, 1-2 фразы, без пошлости.";
                userPrompt = $"Придумай короткий анекдот для стрима. Тема: {SafeTopic(topic, "чат, игры и стримерский быт")}.";
                break;
            default:
                systemPrompt = "Ты мистический стримовый таролог. Делаешь короткие, яркие, мемные предсказания по-русски. 1-2 предложения.";
                userPrompt = $"Сделай краткий tarot-style ответ для зрителя {user}. Тема: {SafeTopic(topic, "удача на стриме и игровые приключения")}.";
                break;
        }

        string model = GetGlobal("OracleModel", DEFAULT_FIREWORKS_MODEL);
        if (string.IsNullOrWhiteSpace(model))
        {
            model = DEFAULT_FIREWORKS_MODEL;
        }

        string reasoningEffort = NormalizeReasoningEffort(GetGlobal("OracleReasoningEffort", "none"), "none");
        JObject body = JObject.FromObject(new
        {
            model = model,
            temperature = 0.9,
            max_tokens = 120,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });

        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            body["reasoning_effort"] = reasoningEffort;
        }

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = PostJson(client, body);
                string raw = response.Content.ReadAsStringAsync().Result;

                if ((response.StatusCode == System.Net.HttpStatusCode.BadRequest || (int)response.StatusCode == 422) && body["reasoning_effort"] != null)
                {
                    body.Remove("reasoning_effort");
                    response = PostJson(client, body);
                    raw = response.Content.ReadAsStringAsync().Result;
                }

                if (!response.IsSuccessStatusCode)
                {
                    CPH.LogWarn($"[FortuneOracle] Fireworks returned {response.StatusCode}");
                    return null;
                }

                JObject json = JObject.Parse(raw);
                return json["choices"]?[0]?["message"]?["content"]?.ToString();
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[FortuneOracle] Fireworks failed: {ex.Message}");
            return null;
        }
    }

    private string GenerateFallback(string mode, string user, string topic)
    {
        string topicPart = string.IsNullOrWhiteSpace(topic) ? "" : $" Тема: {topic}.";

        if (mode == "horoscope")
        {
            string[] lines =
            {
                $"сегодня звёзды шепчут: не лезь в файт первым, но лут забирай последним.{topicPart}",
                $"твой игровой гороскоп на сегодня: крит по лицу возможен, зато чат будет на твоей стороне.{topicPart}",
                $"планеты встали так, что сегодня тебе либо повезёт, либо будет очень смешно всем остальным.{topicPart}"
            };
            return Pick(lines);
        }

        if (mode == "anekdot")
        {
            string[] lines =
            {
                $"Анекдот дня: стример сказал «один каточек и спать». Чат записал это как научную фантастику.{topicPart}",
                $"Анекдот дня: зритель пришёл за контентом, а получил лут, таро и дуэль за честь ника. И не ушёл.{topicPart}",
                $"Анекдот дня: ИИ предсказал победу. Победила, как обычно, неуверенность в настройках.{topicPart}"
            };
            return Pick(lines);
        }

        string[] tarotLines =
        {
            $"карты говорят: сегодня тебе выпадет либо редкий шанс, либо редкий кринж. Хватай первое быстрее.{topicPart}",
            $"таро стрима показывает аркан «Последний хитпоинт». Риск велик, но момент для легенды близко.{topicPart}",
            $"карты шуршат так, будто чат уже решил твою судьбу. Спойлер: будет шумно и весело.{topicPart}"
        };
        return Pick(tarotLines);
    }

    private void PublishRuntimeEvent(string mode, string user, string topic, string answer)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        string title = mode == "horoscope"
            ? $"Гороскоп для {user}"
            : mode == "anekdot"
                ? "Анекдот от оракула"
                : $"Таро для {user}";

        string subtitle = string.IsNullOrWhiteSpace(topic)
            ? "Стримовый мистический вброс"
            : $"Тема: {topic}";

        var envelope = new
        {
            type = "fortune.oracle.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.oracle",
            target = "runtime",
            priority = 60,
            ttlMs = 9000,
            payload = new
            {
                title = title,
                subtitle = subtitle,
                body = answer,
                mode = mode,
                user = user,
                topic = topic,
                stats = new[]
                {
                    $"Режим: {ModeLabel(mode)}",
                    $"Зритель: {user}"
                }
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "fortune-oracle",
                minDisplayMs = 2200,
                cooldownKey = "fortune-oracle",
                cooldownMs = 1000
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
            CPH.LogDebug($"[FortuneOracle] Runtime publish failed: {ex.Message}");
        }
    }

    private string ModeLabel(string mode)
    {
        switch (mode)
        {
            case "horoscope": return "Гороскоп";
            case "anekdot": return "Анекдот";
            default: return "Таро";
        }
    }

    private string SafeTopic(string topic, string fallback)
    {
        return string.IsNullOrWhiteSpace(topic) ? fallback : topic;
    }

    private string CleanAnswer(string answer, string user)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return "оракул задумался слишком сильно и просит повторить попытку позже.";
        }

        string cleaned = Regex.Replace(answer, Regex.Escape("@" + user) + "|@ зритель", "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();

        return cleaned.TrimStart(',', ':', '-', ' ');
    }

    private string Pick(IReadOnlyList<string> values)
    {
        return values[_rng.Next(values.Count)];
    }

    private HttpResponseMessage PostJson(HttpClient client, JObject body)
    {
        return client.PostAsync(
            "https://api.fireworks.ai/inference/v1/chat/completions",
            new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json")
        ).Result;
    }

    private string NormalizeReasoningEffort(string raw, string fallback)
    {
        string val = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (val == "none" || val == "low" || val == "medium" || val == "high") return val;
        return fallback;
    }

    private string GetGlobal(string key, string fallback)
    {
        try
        {
            string val = CPH.GetGlobalVar<string>(key, true);
            return string.IsNullOrWhiteSpace(val) ? fallback : val;
        }
        catch
        {
            return fallback;
        }
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
