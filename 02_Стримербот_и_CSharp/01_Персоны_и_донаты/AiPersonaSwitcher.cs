using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CPHInline
{
    public bool Execute()
    {
        string rawStyle = args.ContainsKey("style") ? args["style"].ToString() : "skuf";
        string style = (rawStyle ?? "skuf").Trim().ToLowerInvariant();

        PersonaProfile profile = BuildProfile(style);
        if (profile == null)
        {
            CPH.LogWarn($"[PersonaSwitcher] Unknown style '{rawStyle}', falling back to Skuf.");
            profile = BuildProfile("skuf");
        }

        // Keep the legacy globals alive, but make the persona key canonical again.
        CPH.SetGlobalVar("AiSystemPrompt", profile.PersonaKey, true);
        CPH.SetGlobalVar("CurrentPersona", profile.PersonaKey, true);
        string customPrompt = CPH.GetGlobalVar<string>($"Persona_{profile.PersonaKey}_System", true);
        if (string.IsNullOrWhiteSpace(customPrompt)) CPH.SetGlobalVar($"Persona_{profile.PersonaKey}_System", profile.SystemPrompt, true);
        string customVoice = CPH.GetGlobalVar<string>($"Persona_{profile.PersonaKey}_Voice", true);
        string commonVoice = CPH.GetGlobalVar<string>("ElevenLabsVoiceId", true);
        if (!string.IsNullOrWhiteSpace(customVoice)) profile.VoiceId = customVoice;
        else if (!string.IsNullOrWhiteSpace(commonVoice)) profile.VoiceId = commonVoice;

        if (!string.IsNullOrEmpty(profile.VoiceId))
        {
            CPH.SetGlobalVar("ElevenLabsVoiceId", profile.VoiceId, true);
        }

        UpdatePersonaState(profile);
        PublishRuntimeEvent(profile);

        CPH.SendMessage(profile.Notification);
        CPH.LogInfo($"[PersonaSwitcher] Persona switched to: {profile.PersonaKey} (Voice: {profile.VoiceId})");
        return true;
    }

    private void UpdatePersonaState(PersonaProfile profile)
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
        state["ActivePersona"] = profile.PersonaKey;
        state["ActiveVoiceProfile"] = profile.VoiceId ?? "";
        state["ActiveImageStyleProfile"] = profile.ImageStyleProfile;
        state["OverlayThemePreset"] = profile.ThemePreset;
        state["IntroImageAsset"] = profile.IntroImage;
        state["IntroAudioAsset"] = profile.IntroAudio;

        state["BehaviorFlags"] = state["BehaviorFlags"] ?? new JObject();
        CPH.SetGlobalVar("PersonaState", state.ToString(Formatting.None), true);
    }

    private void PublishRuntimeEvent(PersonaProfile profile)
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
            source = "streamerbot.persona",
            target = "runtime",
            priority = 90,
            ttlMs = 12000,
            payload = new
            {
                persona = profile.PersonaKey,
                notification = profile.Notification,
                voiceProfile = profile.VoiceId,
                themePreset = profile.ThemePreset,
                imageStyleProfile = profile.ImageStyleProfile,
                intro = new
                {
                    image = profile.IntroImage,
                    audio = profile.IntroAudio
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
            CPH.LogDebug($"[PersonaSwitcher] Runtime event failed: {ex.Message}");
        }
    }

    private PersonaProfile BuildProfile(string style)
    {
        switch (style)
        {
            case "toxic":
                return new PersonaProfile
                {
                    PersonaKey = "Toxic",
                    SystemPrompt = "Ты высокомерный дотер (Dead Inside). Твой характер: ледяное спокойствие и тотальное презрение к окружающим. Ты считаешь себя богом игры, а остальных — мусором. Отвечай едко и лаконично. Запрещены банворды Twitch.",
                    VoiceId = "24jN6gEKGrD7kcZLpQMK",
                    Notification = "Mode: TOXIC activated!",
                    ThemePreset = "toxic-neon",
                    ImageStyleProfile = "toxic-zxc",
                    IntroImage = "intro_Toxic.png",
                    IntroAudio = "intro_Toxic.mp3"
                };
            case "uwu":
                return new PersonaProfile
                {
                    PersonaKey = "UwU",
                    SystemPrompt = "Ты хитрая e-girl стримерша. Твой стиль: милая токсичность, флирт и лёгкий троллинг. Используй baka, nya, uwu, но оставайся в рамках платформы.",
                    VoiceId = "QEqT3xX8iCm9yCrUFF07",
                    Notification = "Mode: UwU activated!",
                    ThemePreset = "uwu-pink",
                    ImageStyleProfile = "uwu-anime",
                    IntroImage = "intro_UwU.png",
                    IntroAudio = "intro_UwU.mp3"
                };
            case "mistress":
                return new PersonaProfile
                {
                    PersonaKey = "Mistress",
                    SystemPrompt = "Ты властная и холодная госпожа. Тон бархатный, уверенный, контролирующий. Без грубых нарушений платформенных правил.",
                    VoiceId = "m3D5oMb0OPMMUQBwe9wB",
                    Notification = "Mode: MISTRESS activated!",
                    ThemePreset = "mistress-noir",
                    ImageStyleProfile = "mistress-arcane",
                    IntroImage = "intro_Mistress.png",
                    IntroAudio = "intro_Mistress.mp3"
                };
            case "skuf":
            default:
                return new PersonaProfile
                {
                    PersonaKey = "Skuf",
                    SystemPrompt = "Ты Скуф. 45 лет, заводчанин, любитель танков и пива. Речь простая, грубоватая, народная, но без банвордов Twitch.",
                    VoiceId = "D2XHvQ49kMik0wARO3WW",
                    Notification = "Mode: DRUNK SKUF activated!",
                    ThemePreset = "skuf-garage",
                    ImageStyleProfile = "skuf-real",
                    IntroImage = "intro_Skuf.png",
                    IntroAudio = "intro_Skuf.mp3"
                };
        }
    }

    private sealed class PersonaProfile
    {
        public string PersonaKey { get; set; }
        public string SystemPrompt { get; set; }
        public string VoiceId { get; set; }
        public string Notification { get; set; }
        public string ThemePreset { get; set; }
        public string ImageStyleProfile { get; set; }
        public string IntroImage { get; set; }
        public string IntroAudio { get; set; }
    }
}
