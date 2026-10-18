using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ==================================================================================
//  [AI] Ротация Персон
//  Переключает персону по кругу: Skuf → UwU → Toxic → Mistress → Skuf...
//  Отправляет intro-анимацию в web_overlay_v2.html
//  
//  ИСПОЛЬЗОВАНИЕ:
//  1. Создай Action в Streamer.bot с этим скриптом
//  2. Добавь Timed Trigger (например, 60 минут) ИЛИ вызывай вручную
//  3. Убедись что файлы intro_*.png и intro_*.mp3 лежат рядом с overlay
//
//  ГЛОБАЛКИ:
//  - AiSystemPrompt (читает/записывает) — ключ текущей персоны
//  - AiLastRotation (записывает) — timestamp последней ротации (дебаг)
// ==================================================================================

public class CPHInline
{
    // Порядок ротации
    private static readonly string[] PersonaOrder = { "Skuf", "UwU", "Toxic", "Mistress" };

    public bool Execute()
    {
        CPH.LogInfo("=== [AI] Persona Rotation START ===");

        // 1. Получить текущую персону
        string currentPersona = CPH.GetGlobalVar<string>("AiSystemPrompt", true);
        if (string.IsNullOrEmpty(currentPersona)) currentPersona = "Skuf";

        // 2. Найти индекс текущей персоны
        int currentIndex = Array.IndexOf(PersonaOrder, currentPersona);
        if (currentIndex < 0) currentIndex = 0; // Если не найдена — начать с Skuf

        // 3. Выбрать следующую персону
        int nextIndex = (currentIndex + 1) % PersonaOrder.Length;
        string newPersona = PersonaOrder[nextIndex];

        CPH.LogInfo($"[Rotation] {currentPersona} → {newPersona}");

        // 4. Записать новую персону в глобалку
        CPH.SetGlobalVar("AiSystemPrompt", newPersona, true);
        CPH.SetGlobalVar("CurrentPersona", newPersona, true);
        CPH.SetGlobalVar("AiLastRotation", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), true);

        // 5. Отправить intro_switch событие в оверлей
        var overlayPayload = new {
            id = "ROT-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            event_type = "intro_switch",
            persona = newPersona,
            image_file = $"intro_{newPersona}.png",
            audio_file = $"intro_{newPersona}.mp3"
        };

        string json = JsonConvert.SerializeObject(overlayPayload);
        CPH.WebsocketBroadcastJson(json);
        CPH.LogInfo($"[Rotation] Sent to overlay: {json}");
        UpdatePersonaState(newPersona);
        PublishRuntimePersonaSwitch(newPersona);

        // 6. (Опционально) Уведомление в чат
        // CPH.SendMessage($"🔄 Персона изменена: {newPersona}");

        CPH.LogInfo("=== [AI] Persona Rotation COMPLETE ===");
        return true;
    }

    private void UpdatePersonaState(string persona)
    {
        string raw = CPH.GetGlobalVar<string>("PersonaState", true);
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
        state["ActivePersona"] = persona;
        state["ActiveVoiceProfile"] = state["ActiveVoiceProfile"] ?? "";
        state["ActiveImageStyleProfile"] = persona.ToLowerInvariant();
        state["IntroImageAsset"] = $"intro_{persona}.png";
        state["IntroAudioAsset"] = $"intro_{persona}.mp3";
        state["BehaviorFlags"] = state["BehaviorFlags"] ?? new JObject();
        state["Cooldowns"] = state["Cooldowns"] ?? new JObject();
        CPH.SetGlobalVar("PersonaState", state.ToString(Formatting.None), true);
    }

    private void PublishRuntimePersonaSwitch(string persona)
    {
        string endpoint = CPH.GetGlobalVar<string>("VisualRuntimeEndpoint", true);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "http://127.0.0.1:1608/events";
        }

        var body = new
        {
            type = "persona.switch.v1",
            version = 1,
            eventId = Guid.NewGuid().ToString("N"),
            timestamp = DateTime.UtcNow.ToString("o"),
            source = "streamerbot.persona.rotation",
            target = "runtime",
            priority = 90,
            ttlMs = 12000,
            payload = new
            {
                persona = persona,
                voiceProfile = "default",
                themePreset = persona.ToLowerInvariant(),
                intro = new
                {
                    image = $"intro_{persona}.png",
                    audio = $"intro_{persona}.mp3"
                }
            },
            queue = new
            {
                interruptible = true,
                replaceByKey = "persona-switch",
                minDisplayMs = 2500,
                cooldownKey = "persona-switch",
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
            CPH.LogDebug($"[PersonaRotation] Runtime event failed: {ex.Message}");
        }
    }
}
