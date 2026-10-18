using System;
using System.Net.Http;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading; // For Thread.Sleep
using System.Threading.Tasks; // For Task.Run, Task.WaitAll
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Linq; // REQUIRED FOR ORDERBY

// ==================================================================================
//  [AI] Обработчик Донатов
//  Возможности: Персоны, JSON, Картинки (Google), Озвучка (ElevenLabs), История
// ==================================================================================

public class CPHInline
{
    private const bool LOADING_OFF_CLOSE_FALLBACK = true;
    // ==========================================
    // НАСТРОЙКИ (ПО УМОЛЧАНИЮ)
    // ==========================================
    private const string OPENROUTER_IMAGE_MODEL = "google/gemini-3.1-flash-image-preview";

    // Inworld TTS Fallback
    private const string INWORLD_TTS_URL = "https://api.inworld.ai/tts/v1/voice";
    private const string INWORLD_AUTH_GLOBAL = "InworldTTSAuth";

    // Маппинг персон на голоса Inworld (фоллбэк когда ElevenLabs сдох)
    private static readonly Dictionary<string, string> PersonaInworldVoice = new Dictionary<string, string>
    {
        { "Toxic",    "default-mtpxxj7uhgk_xn4yzgz4aq__stenli" },
        { "Skuf",     "default-mtpxxj7uhgk_xn4yzgz4aq__papich" },
        { "UwU",      "default-mtpxxj7uhgk_xn4yzgz4aq__chitani" },
        { "Mistress", "default-mtpxxj7uhgk_xn4yzgz4aq__zhenya_nevskaya" }
    };

    // ИНСТРУКЦИИ ДЛЯ ОЗВУЧКИ (System Prompt)
    private const string AUDIO_INSTRUCTIONS = """
    # 40 EXTENDED AUDIO TAGS (ELEVENLABS v3) - USE FREQUENTLY!
    You are using a customized TTS engine. Inject these tags to express emotion.

    **Voice-related (Vocal delivery)**:
    - [laughs], [laughs harder], [starts laughing], [wheezing], [giggles]
    - [whispers], [shouting], [sings]
    - [sighs], [exhales], [gasp], [snorts]
    - [sarcastic], [curious], [excited], [crying], [mischievously]
    - [panicked], [tired], [trembling], [serious], [robotically], [amazed]

    **Sound Effects (SFX)**:
    - [gunshot], [applause], [clapping], [explosion]
    - [swallows], [gulps]
    - [door slams], [heartbeat]
    - [thunder], [rainfall], [distant echo], [underwater], [echoes]
    
    **Unique/Special**:
    - [strong Russian accent], [strong X accent]
    - [woo], [fart], [asmr mode]
    
    **Punctuation & Style**:
    - Ellipses (...) for pauses. CAPITALIZATION for emphasis.
    - DO NOT list tags. Speak THROUGH them.
    - Example: '[whispers] Listen... [thunder] Did you hear that? [panicked] RUN! [door slams]'
    """;

    // ==========================================
    // ОПИСАНИЕ ПЕРСОН
    // ==========================================
    public class Persona
    {
        public string SystemPrompt { get; set; }
        public string ImageStyle { get; set; }
        public string VoiceId { get; set; }
    }

    // ==========================================
    // CORE v3.2 STRUCTURES & STATE
    // ==========================================
    private static readonly object _stateLock = new object();
    private const int TTS_CACHE_MAX_FILES = 80;
    private const int TTS_CACHE_MAX_AGE_HOURS = 24;

    public class AiPersonaState
    {
        public Dictionary<string, PersonaStats> States { get; set; } = new Dictionary<string, PersonaStats>();
    }

    public class PersonaStats
    {
        public double Value { get; set; } // Drunk/Rage level (0-100)
        public DateTime LastUpdate { get; set; }
    }
    
    // CHAOS ENGINE DEFINITIONS
    private readonly Dictionary<string, string[]> ChaosLights = new Dictionary<string, string[]> {
        { "Toxic", new[] { "red neon rim lighting", "cold blue monitor glow", "pitch black room with cigarette ember", "green matrix code reflection" } },
        { "Skuf", new[] { "dim yellow tungsten light", "flashing tv screen reflection", "morning sunlight from dirty window", "dark room with only monitor light" } },
        { "UwU", new[] { "soft pink ring light", "dreamy sunset backlighting", "pastel sparkles overlay", "disco ball reflections" } },
        { "Mistress", new[] { "dramatic noir shadows", "fireplace glow", "candlelight", "cold hospital white light" } }
    };
    
    private readonly Dictionary<string, string[]> ChaosProps = new Dictionary<string, string[]> {
        { "Toxic", new[] { "empty energy drink cans", "ashtray with smoke", "broken keyboard keys", "hoodie covering face" } },
        { "Skuf", new[] { "fly sitting on the lens", "plate with half-eaten food", "sweaty forehead", "dirty tank top stains", "pile of cigarette butts" } },
        { "UwU", new[] { "floating hearts", "plushie collection in background", "cat ear headphones glowing", "magical girl wand" } },
        { "Mistress", new[] { "riding crop", "glass of red wine", "black leather gloves", "heavy golden chains" } }
    };

    // ==========================================
    // ОПИСАНИЕ ПЕРСОН (ИСПРАВЛЕННЫЕ СТИЛИ v2)
    // ==========================================
    private Dictionary<string, Persona> Personas = new Dictionary<string, Persona>
    {
        // 1. TOXIC (Добавлен пол: "young male figure in black hoodie")
        { "Toxic", new Persona {
            VoiceId = "24jN6gEKGrD7kcZLpQMK", 
            SystemPrompt = 
                "Ты — Токсик (Dead Inside). Характер: ледяное презрение, психоз, депрессия. " +
                "Отвечай едко, используй сленг zxc. ЗАПРЕЩЕНЫ банворды.\n\n" +
                "VISUAL RULES (for 'visual_scene'): Describe DARK, EDGY actions. " +
                "Examples: 'Grabbing head in agony', 'Staring with dead eyes at glowing monitor', 'Slamming fist on table', 'Lighting a cigarette in dark room'.",
            // Kaneki / ghoul zxc anime style - Variant B
            ImageStyle = "anime illustration, skinny pale teenage boy in oversized black hoodie, hood up half-covering face, messy dark hair with white streak, red glowing eyes visible under hood, headphones around neck, sitting in dark room at messy desk with gaming keyboard and mouse, monster energy cans scattered, cigarette in hand, edgy dark aesthetic, high contrast, neon red accents on black"
        }},

        // 2. SKUF (Переставлены акценты: сначала мужик, потом камера)
        { "Skuf", new Persona {
            VoiceId = "D2XHvQ49kMik0wARO3WW", 
            SystemPrompt = 
                "Ты — Скуф (Василий, 45 лет). Заводчанин, пьян. Речь простая, матерная. Ругай молодежь.\n\n" +
                "VISUAL RULES (for 'visual_scene'): Describe MESSY, DRUNK actions. " +
                "Examples: 'Holding a beer bottle and shouting angrily', 'Sleeping on chair with mouth open', 'Pointing angry finger at camera', 'Scratching belly under dirty tank top'.",
            // v2: Based on reference — shirtless heavyset guy at computer desk
            ImageStyle = "Heavyset shirtless russian man age 40, short hair, stubble, round face, sitting at computer desk with gaming monitor glowing, vodka bottle and shot glass on desk, plates with simple food, webcam selfie angle, ordinary russian apartment room, warm tungsten indoor lighting, photorealistic"
        }},

        // 3. UWU (Убран шумный фон)
        { "UwU", new Persona {
            VoiceId = "EXAVITQu4vr4xnSDxMaL", 
            SystemPrompt = 
                "Ты — UwU (Аниме-стримерша). Голос милый (kawaii). Любишь семпаев, иногда цундере. Используй: 'baka', 'nya', 'uwu'.\n\n" +
                "VISUAL RULES (for 'visual_scene'): Describe EXAGGERATED ANIME ACTIONS. " +
                "Examples: 'Making heart shape with hands, blushing', 'Pouting angrily with crossed arms', 'Crying comic waterfall tears', 'Jumping with joy, sparkles around'.",
            // FIX: Заменил "detailed background" на "blurred pink gaming room background" для фокуса на персонаже
            ImageStyle = "anime key visual, Makoto Shinkai style, vibrant colors, cel shading, cute anime girl with pink hair and cat ears headphones, expressive emotion, sharp focus on character, blurred pink gaming room background with neon lights, soft lighting"
        }},

        // 4. MISTRESS (Убран мусор, закреплен фотореализм)
        { "Mistress", new Persona {
            VoiceId = "m3D5oMb0OPMMUQBwe9wB", 
            SystemPrompt = 
                "Ты — Госпожа (Dommy Mommy). Властная, холодная. Зрители — рабы. Тон бархатный, унижающий.\n\n" +
                "VISUAL RULES (for 'visual_scene'): Describe DOMINANT actions. Do NOT use abstract words like 'donation alert', use concrete objects like 'screen'. " +
                "Examples: 'Looking down at camera with cold sneer', 'Holding a glass of wine elegantly', 'Pointing imperiously at monitor screen', 'Laughing cruelly behind hand'.",
            // Approved B1: Arcane + Dommy Mommy + Subtle Gachi
            ImageStyle = "Arcane animated series painterly 3D art style by Fortiche studio, tall elegant beautiful dominant woman, extremely feminine but imposing, wearing a stylish black velvet evening gown with subtle leather corset details, sitting gracefully with legs crossed on a massive iron dungeon throne, subtle gachimuchi references like a stud collar and heavy chains in the blurry background, dark moody environment, neon magenta and deep purple rim lighting, arrogant mocking expression, looking down at viewer, masterpiece"
        }}
    };

    public bool Execute()
    {
        CPH.LogInfo("=== [AI] Donation Handler STARTED ===");
        bool loadingStarted = false;
        bool sentFinal = false;

        // 1. ПОЛУЧЕНИЕ НАСТРОЕК (Global Vars)
        // Читаем из переменных Streamer.bot
        string openRouterKey = CPH.GetGlobalVar<string>("OpenRouterKey", true);
        string elevenLabsKey = CPH.GetGlobalVar<string>("ElevenLabsKey", true);

        // Debug check (don't log full keys!)
        if (string.IsNullOrEmpty(openRouterKey)) { CPH.LogError("[AI] Missing 'OpenRouterKey' Global Var!"); return false; }
        if (string.IsNullOrEmpty(elevenLabsKey)) { CPH.LogError("[AI] Missing 'ElevenLabsKey' Global Var!"); return false; }

        // 2. НАСТРОЙКА ПУТЕЙ (Папка в Документах)
        // string baseDir = System.AppDomain.CurrentDomain.BaseDirectory; // обычно папка exe
        // Используем документы для надежности
        string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string cacheDir = Path.Combine(docsDir, "StreamerBot_AI_Cache"); 
        Directory.CreateDirectory(cacheDir);
        string ttsDir = Path.Combine(cacheDir, "tts");
        Directory.CreateDirectory(ttsDir);

        // 3. ПОЛУЧЕНИЕ ДАННЫХ КОМАНДЫ (Донат)
        string donorName = args.ContainsKey("userName") ? args["userName"].ToString() : "Аноним";
        string amount = args.ContainsKey("amount") ? args["amount"].ToString() : "0";
        string currency = args.ContainsKey("currency") ? args["currency"].ToString() : "RUB";
        string message = args.ContainsKey("message") ? args["message"].ToString() : "";
        
        string personaKey = CPH.GetGlobalVar<string>("AiSystemPrompt"); // e.g., "Skuf"
        if (string.IsNullOrEmpty(personaKey) || !Personas.ContainsKey(personaKey)) personaKey = "Skuf";
        
        // Загрузка базовой персоны
        Persona basePersona = Personas[personaKey];

        // ПРОВЕРКА UI ПЕРЕОПРЕДЕЛЕНИЙ (Настройки -> Globals)
        // Format: Persona_Skuf_Style, Persona_Toxic_System, etc.
        string overrideSystem = CPH.GetGlobalVar<string>($"Persona_{personaKey}_System", true);
        string overrideStyle = CPH.GetGlobalVar<string>($"Persona_{personaKey}_Style", true);
        string overrideVoice = CPH.GetGlobalVar<string>($"Persona_{personaKey}_Voice", true);

        // Применение настроек
        string finalSystem = !string.IsNullOrEmpty(overrideSystem) ? overrideSystem : basePersona.SystemPrompt;
        string finalStyle = !string.IsNullOrEmpty(overrideStyle) ? overrideStyle : basePersona.ImageStyle;
        string finalVoice = !string.IsNullOrEmpty(overrideVoice) ? overrideVoice : basePersona.VoiceId;

        CPH.SetGlobalVar("CurrentPersona", personaKey, true);
        CPH.LogInfo($"Donation: {donorName} ({amount}). Persona: {personaKey}");

        // 0. ОТПРАВКА СТАТУСА "ЗАГРУЗКА"
        var loadingPayload = new { 
            action = "loading", 
            persona = personaKey,
            donor = donorName,
            amount = amount
        };
        CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(loadingPayload));
        PublishRuntimeDonationLoading(personaKey, donorName, amount);
        loadingStarted = true;

        try
        {
            // ==========================================
            // V3.2 LOGIC IMPLEMENTATION
            // ==========================================

            // 1. DETERMINISTIC RNG
            int seed = GetCryptoSeed();
            Random eventRng = new Random(seed);
            CPH.LogInfo($"[AI] Event Seed: {seed}");

            // 2. PARSE AMOUNT
            double amountVal = 0;
            // Handle generic currency parsing
            string amtStr = amount.Replace(",", "."); 
            double.TryParse(amtStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out amountVal);

            // 3. PERSONA STATE (Global Var Storage)
            string stateJson = CPH.GetGlobalVar<string>("AiPersonaState_V3", true);
            AiPersonaState globalState;
            try {
                globalState = string.IsNullOrEmpty(stateJson) ? new AiPersonaState() : JsonConvert.DeserializeObject<AiPersonaState>(stateJson);
            } catch { globalState = new AiPersonaState(); }

            double currentLevel = 0;
            string statusLabel = "Normal";

            lock(_stateLock) 
            {
                currentLevel = UpdatePersonaState(personaKey, amountVal, ref globalState);
                CPH.SetGlobalVar("AiPersonaState_V3", JsonConvert.SerializeObject(globalState), true);
            }

            // Determine Label
            if (personaKey == "Skuf") {
                if (currentLevel > 80) statusLabel = "WASTED (Drunk)";
                else if (currentLevel > 30) statusLabel = "TIPSY";
                else statusLabel = "SOBER";
            }
            else if (personaKey == "Toxic") {
                 if (currentLevel > 80) statusLabel = "RAGE MODE";
                 else if (currentLevel > 30) statusLabel = "IRRITATED";
                 else statusLabel = "CHILL";
            }

            // 4. RARITY ROLL (70/25/5)
            int roll = eventRng.Next(100);
            string rarity = "Common";
            string eventType = "Normal";
            
            if (roll >= 95) {
                // Legendary (5%)
                bool success = eventRng.Next(100) < 60; // 60% Success (Nat20), 40% Fail (Nat1)
                rarity = "LEGENDARY";
                eventType = success ? "CRITICAL_SUCCESS (God Mode)" : "CRITICAL_FAIL (Glitch/Disaster)";
            } 
            else if (roll >= 70) {
                 rarity = "RARE";
                 eventType = "Unusual_Event";
            }

            // 5. CHAOS ENGINE (Variant Layer)
            string chaosLight = "";
            string chaosProp = "";
            
            if (ChaosLights.ContainsKey(personaKey)) {
                var opts = ChaosLights[personaKey];
                chaosLight = opts[eventRng.Next(opts.Length)];
            }
            if (ChaosProps.ContainsKey(personaKey)) {
                var opts = ChaosProps[personaKey];
                chaosProp = opts[eventRng.Next(opts.Length)];
            }

            // ==========================================
            // PROMPT CONSTRUCTION V2 (With Safety)
            // ==========================================
            
            // Safe untrusted input handling
            var userInputSafe = new { donor_input = message, context = $"User {donorName} sent {amount} {currency}" };
            string userInputJson = JsonConvert.SerializeObject(userInputSafe);

            string promptInstructions = $$"""
            SYSTEM OVERRIDE: OUTPUT MUST BE RAW JSON. NO MARKDOWN. NO ```json BLOCKS.
            RETURN JSON ONLY using SCHEMA V2:
            {
              "schema_version": 2,
              "request_id": "{{seed}}", // ECHO THIS ID
              "event_type": "{{eventType}}",
              "reply": "Text (Russian). React to context! Max 350 chars.",
              "topic": "Summarize user message in 1-3 words (Russian)",
              "visual_director": {
                  "action": "What is character doing? (Max 80 chars)",
                  "emotion": "Facial expression",
                  "atmosphere": "Lighting/Vibe"
              },
              "overlay_fx": {
                  "type": "shake", // WHITELIST: glitch, shake, rain, confetti, none
                  "intensity": 0.5 // 0.0 to 1.0
              }
            }
            
            CONTEXT DATA:
            - STATUS: {statusLabel} (Level: {currentLevel:F0}%)
            - EVENT: {eventType} ({rarity})
            - CHAOS: {chaosLight}, {chaosProp}
            
            RULES:
            1. IF 'WASTED' or 'RAGE': Make reply slurred/aggressive. Image: messy/broken.
            2. IF 'LEGENDARY': Make it EPIC.
            
            SAFETY & TOS COMPLIANCE (CRITICAL):
            1. DO NOT use homophobic, racist, or banned Twitch slurs.
            2. NO 'pidor', 'n-word', 'f-word' or Russian equivalents.
            3. Keep strictly compliant with Twitch TOS.
            4. Be toxic/funny but NOT bannable.
            """;

            string fullSystemPrompt = finalSystem + "\n\n" + AUDIO_INSTRUCTIONS + "\n\n" + promptInstructions;

            string userPrompt = $"DONATION: {donorName} sent {amount} {currency}. Msg: {message}";

            // 5. ЗАПРОС К LLM (Grok)
            // Загрузка полной истории для контекста
            string historyJson = CPH.GetGlobalVar<string>("AiDonationHistory_Full", true); 
            List<string> historyList = new List<string>();
            try {
                if (!string.IsNullOrEmpty(historyJson))
                    historyList = JsonConvert.DeserializeObject<List<string>>(historyJson);
            } catch {
                CPH.LogWarn("[AI] Full History corrupted. Resetting.");
                historyList = new List<string>();
            }
            string historyText = string.Join("\n", historyList);

            string jsonResponse = GetAiText(userPrompt, fullSystemPrompt, openRouterKey, historyText);
            if (string.IsNullOrEmpty(jsonResponse)) return false;

            // 6. РАЗБОР JSON В ОТВЕТЕ (VALIDATION V2)
            string aiReply = "Error";
            string aiVisualScene = "neutral";
            string fxType = "none";
            
            try {
                JObject aiData = JObject.Parse(jsonResponse);
                
                // VALIDATION: Reply Length
                aiReply = aiData["reply"]?.ToString() ?? "Project Voice Error";
                if (aiReply.Length > 350) aiReply = aiReply.Substring(0, 350);
                
                // VALIDATION: Visual Director
                if (aiData["visual_director"] is JObject vis) {
                    string action = vis["action"]?.ToString() ?? "standing";
                    string emo = vis["emotion"]?.ToString() ?? "neutral";
                    aiVisualScene = $"{action}, {emo}, {chaosProp}".Trim(); // Inject Chaos Prop here contextually
                }
                else {
                    aiVisualScene = aiData["visual_scene"]?.ToString() ?? "talking to camera";
                }

                // VALIDATION: Overlay FX Whitelist
                 if (aiData["overlay_fx"] is JObject fx) {
                    string t = fx["type"]?.ToString().ToLower() ?? "none";
                    string[] allowed = { "glitch", "shake", "rain", "confetti", "none" };
                    if (Array.IndexOf(allowed, t) > -1) fxType = t;
                 }
                 
                 // MEMORY UPDATE (Donor Stats)
                 if (donorName != "Аноним" && !string.IsNullOrEmpty(donorName)) 
                 {
                     lock(_stateLock) {
                         UpdateDonorStats(donorName, amountVal);
                     }
                 }
            }
            catch {
                CPH.LogWarn("[AI] JSON Validation Logic Failed. Fallback.");
                aiReply = jsonResponse;
            }

            // ОЧИСТКА ТЕКСТА (Для генерации и оверлея)
            string visualText = Regex.Replace(aiReply, @"\[.*?\]", "").Trim();
            // Remove multiple spaces
            visualText = Regex.Replace(visualText, @"\s+", " ");

            // 7. ПАРАЛЛЕЛЬНАЯ ГЕНЕРАЦИЯ (Аудио + Картинка)
            // Запускаем обе задачи и ждем их выполнения
            
            // --- А) КАРТИНКА (OpenRouter: gemini-3.1-flash-image-preview) ---
            string[] cameraAngles = { "close-up portrait", "wide angle shot", "slightly low angle", "view from below", "side profile view", "dynamic medium shot" };
            // Use Deterministic RNG
            string randomAngle = cameraAngles[eventRng.Next(cameraAngles.Length)];

            // V3.2 COMPOSITION STRATEGY
            // 1. Anchor (Style)
            // 2. Variant (Chaos Props/Lights)
            // 3. Scene (Grok)
            // 4. Angle (RNG)
            
            string variantLayer = $"{chaosLight}, {chaosProp}".Trim(',', ' ');
            string fullImagePrompt = $"{finalStyle}, {variantLayer}, {aiVisualScene}, {randomAngle}, highly detailed, 8k resolution.";
            
            // Чистим двойные запятые и точки, если вдруг возникнут
            fullImagePrompt = fullImagePrompt.Replace(",,", ",").Replace("..", ".");
            
            CPH.LogInfo($"[OpenRouter Image Prompt] {fullImagePrompt}");
            
            string imageName = $"current_ai_image_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0,8)}.png";
            string currentImagePath = Path.Combine(cacheDir, imageName);
            
            // Генерация картинки (фон) — через OpenRouter
            var imageTask = Task.Run(() => GenerateOpenRouterImage(fullImagePrompt, openRouterKey, currentImagePath));

            // --- Б) ОЗВУЧКА (ElevenLabs) ---
            string audioPath = null;
            // Limit text for TTS cost/speed
            string ttsText = aiReply.Length > 450 ? aiReply.Substring(0, 450) : aiReply;
            
            // Run TTS in background
            var audioTask = Task.Run(() => GetTtsAudio(ttsText, elevenLabsKey, finalVoice, ttsDir));

            // ОЖИДАНИЕ ЗАВЕРШЕНИЯ ОБЕИХ ЗАДАЧ
            CPH.LogInfo("[AI] Waiting for Parallel Tasks (Image + Audio)...");
            bool tasksCompleted = Task.WaitAll(new Task[] { imageTask, audioTask }, TimeSpan.FromSeconds(100));
            if (!tasksCompleted)
            {
                CPH.LogWarn("[AI] Parallel tasks timed out. Continuing with partial data.");
            }
            
            bool imgSuccess = imageTask.Status == TaskStatus.RanToCompletion && imageTask.Result;
            audioPath = audioTask.Status == TaskStatus.RanToCompletion ? audioTask.Result : null;

            // 8. ПОДГОТОВКА ДАННЫХ ДЛЯ ОВЕРЛЕЯ
            string imageBase64 = "";
            string audioBase64 = ""; 

            // КОДИРОВАНИЕ КАРТИНКИ
            if (imgSuccess && File.Exists(currentImagePath))
            {
                try {
                    byte[] imgBytes = File.ReadAllBytes(currentImagePath);
                    imageBase64 = "data:image/png;base64," + Convert.ToBase64String(imgBytes);

                     // Обновляем локальный источник OBS (резерв)
                    CPH.SetArgument("aiGeneratedImage", currentImagePath);
                }
                catch (Exception ex) { CPH.LogWarn($"[AI] Image Read Error: {ex.Message}"); }
            }
            else { CPH.LogWarn("[AI] Image Generation Failed or Timed Out."); }

            // КОДИРОВАНИЕ АУДИО (Base64)
            if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
            {
                try {
                    byte[] audioBytes = File.ReadAllBytes(audioPath);
                    string audioMime = audioPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? "audio/wav" : "audio/mp3";
                    audioBase64 = $"data:{audioMime};base64," + Convert.ToBase64String(audioBytes);
                    CPH.LogInfo($"[AI] Audio ready for overlay ({audioBytes.Length} bytes).");
                }
                catch (Exception ex) { CPH.LogWarn($"[AI] Audio Read Error: {ex.Message}"); }
            }
            else { CPH.LogWarn("[AI] TTS Generation Failed. No audio."); }

            // Расчет длительности показа (если нет аудио)
            int wordCount = visualText.Split(' ').Length;
            int durationMs = 3000 + (wordCount * 400); 

            // ИСТОРИЯ ДЛЯ ВИДЖЕТА (Темы)
            // Note: aiTopic might be missing in V2, using simplified extraction or default
            string aiTopic = "AI Interaction";
            List<string> paramsHistory = UpdateHistoryUI(donorName, amount, message, aiTopic);

            // Отправка данных в оверлей
            var overlayPayload = new {
                id = Guid.NewGuid().ToString(), // Request ID for Auto-Kill
                persona = personaKey,
                message = visualText,
                image = imageBase64,
                audio = audioBase64, 
                duration = durationMs,
                history = paramsHistory, // Send History List
                // V3 FEATURES
                fx = fxType
            };
            
            CPH.LogInfo("[Overlay] Sync Data Sent (Text + Image + Audio).");
            CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(overlayPayload));
            PublishRuntimeDonationScene(personaKey, donorName, amount, visualText, imageBase64, audioBase64, fxType, durationMs);
            sentFinal = true;

            // Воспроизведение и закрытие управляется JS оверлея
            
            // Сохранение полной истории (Контекст)
            UpdateHistoryContext(donorName, amount, message, aiReply);
        }
        catch (Exception ex)
        {
            CPH.LogError($"[AI] CRITICAL ERROR: {ex.Message} \n {ex.StackTrace}");
        }
        finally
        {
            if (loadingStarted && !sentFinal)
            {
                SendLoadingOff();
            }
        }

        return true;
    }

    // ==========================================
    // ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ
    // ==========================================

    private string GetAiText(string userPrompt, string sysPrompt, string key, string history)
    {
        try 
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(90); // RESTORED 90s
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
                
                string finalSystem = sysPrompt + "\n\n=== RECENT CHAT HISTORY ===\n" + history;

                string donationModel = CPH.GetGlobalVar<string>("DonationModel", true);
                if (string.IsNullOrEmpty(donationModel)) donationModel = "x-ai/grok-4.1-fast";

                var body = new { model = donationModel, messages = new[] { 
                    new { role = "system", content = finalSystem }, 
                    new { role = "user", content = userPrompt } 
                }};
                
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var res = client.PostAsync("https://openrouter.ai/api/v1/chat/completions", content).Result;
                string resStr = res.Content.ReadAsStringAsync().Result;

                if (!res.IsSuccessStatusCode) { 
                    CPH.LogError($"[OpenRouter] Error: {res.StatusCode} - {resStr}"); 
                    return null; 
                }
                
                // Debug log restored
                CPH.LogInfo($"[Grok Raw]: {resStr.Substring(0, Math.Min(resStr.Length, 100))}..."); 

                JObject json = JObject.Parse(resStr);
                string text = json["choices"]?[0]?["message"]?["content"]?.ToString();
                
                if (text == null) return null;
                
                CPH.LogInfo($"[Grok Content]: {text}"); // RESTORED DEBUG LOG

                return text.Replace("```json", "").Replace("```", "").Trim();
            }
        }
        catch (Exception ex) 
        {
            // Unwrapping AggregateException if it exists
            string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
            CPH.LogError($"[OpenRouter] Connection Exception: {msg}");
            return null;
        }
    }

    private string GetTtsAudio(string text, string key, string voiceId, string ttsDir)
    {
        // 1) Пробуем ElevenLabs
        string result = GetTtsAudioElevenLabs(text, key, voiceId, ttsDir);
        if (result != null) return result;

        // 2) Фоллбэк: Inworld TTS
        CPH.LogWarn("[TTS] ElevenLabs failed, trying Inworld fallback...");
        return GetTtsAudioInworld(text, ttsDir);
    }

    private string GetTtsAudioElevenLabs(string text, string key, string voiceId, string ttsDir)
    {
        try
        {
            CPH.LogInfo($"[TTS] ElevenLabs: VoiceID={voiceId}, Length={text.Length}");
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("xi-api-key", key);
      
                var body = new { text = text, model_id = "eleven_v3", voice_settings = new { stability = 0.5, similarity_boost = 0.5 } };
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                
                var res = client.PostAsync($"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}", content).Result;
                if (!res.IsSuccessStatusCode) { 
                    string err = res.Content.ReadAsStringAsync().Result;
                    CPH.LogError($"[TTS] ElevenLabs Error: {res.StatusCode} ({(int)res.StatusCode}) | {err}"); 
                    return null; 
                }
                
                byte[] bytes = res.Content.ReadAsByteArrayAsync().Result;
                string name = $"tts_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0,8)}.mp3";
                string outFile = Path.Combine(ttsDir ?? Path.GetTempPath(), name);
                File.WriteAllBytes(outFile, bytes);
                CleanupOldTtsFiles(ttsDir ?? Path.GetTempPath());
                CPH.LogInfo($"[TTS] ElevenLabs saved: {outFile} ({bytes.Length} bytes)");
                return outFile;
            }
        }
        catch (Exception ex)
        {
             CPH.LogError($"[TTS] ElevenLabs Exception: {ex.Message}");
             return null;
        }
    }

    private string GetTtsAudioInworld(string text, string ttsDir)
    {
        try
        {
            string auth = CPH.GetGlobalVar<string>(INWORLD_AUTH_GLOBAL, true);
            if (string.IsNullOrEmpty(auth))
            {
                CPH.LogWarn("[TTS] Inworld fallback skipped: no InworldTTSAuth global var.");
                return null;
            }

            // Подбираем голос по текущей персоне (или рандом)
            string personaKey = CPH.GetGlobalVar<string>("CurrentPersona", true) ?? "";
            string inworldVoice;
            if (!PersonaInworldVoice.TryGetValue(personaKey, out inworldVoice))
            {
                // Рандомный голос из маппинга
                var voices = new List<string>(PersonaInworldVoice.Values);
                inworldVoice = voices[new Random().Next(voices.Count)];
            }

            CPH.LogInfo($"[TTS] Inworld fallback: voice={inworldVoice}, len={text.Length}");

            var body = new {
                text = text,
                voiceId = inworldVoice,
                modelId = "inworld-tts-1.5-max",
                audioConfig = new { audioEncoding = "LINEAR16", sampleRateHertz = 24000, speakingRate = 1.0 }
            };

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Authorization", auth);
                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                var res = client.PostAsync(INWORLD_TTS_URL, content).Result;

                if (!res.IsSuccessStatusCode)
                {
                    string err = res.Content.ReadAsStringAsync().Result;
                    CPH.LogError($"[TTS] Inworld Error: {res.StatusCode} | {err}");
                    return null;
                }

                var json = JObject.Parse(res.Content.ReadAsStringAsync().Result);
                byte[] audioBytes = Convert.FromBase64String(json["audioContent"].ToString());

                string name = $"tts_iw_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0,8)}.wav";
                string outFile = Path.Combine(ttsDir ?? Path.GetTempPath(), name);
                File.WriteAllBytes(outFile, audioBytes);
                CPH.LogInfo($"[TTS] Inworld saved: {outFile} ({audioBytes.Length} bytes)");
                return outFile;
            }
        }
        catch (Exception ex)
        {
            CPH.LogError($"[TTS] Inworld Exception: {ex.Message}");
            return null;
        }
    }

    private bool GenerateOpenRouterImage(string prompt, string apiKey, string savePath)
    {
        int maxRetries = 5;
        int delayMs = 5000;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(60);
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    var payload = new {
                        model = OPENROUTER_IMAGE_MODEL,
                        messages = new[] {
                            new { role = "user", content = $"Generate a square 1:1 image based on this description. Respond ONLY with the image, no text: {prompt}" }
                        },
                        modalities = new[] { "image", "text" },
                        image_config = new {
                            aspect_ratio = "1:1",
                            image_size = "1K"
                        }
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                    var res = client.PostAsync("https://openrouter.ai/api/v1/chat/completions", content).Result;
                    string resText = res.Content.ReadAsStringAsync().Result;

                    if (res.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(resText);

                        // Try OpenRouter images array: choices[0].message.images[0].image_url.url
                        string b64Url = json["choices"]?[0]?["message"]?["images"]?[0]?["image_url"]?["url"]?.ToString();

                        // Fallback: content parts
                        if (string.IsNullOrEmpty(b64Url))
                        {
                            var parts = json["choices"]?[0]?["message"]?["content"];
                            if (parts is JArray partsArr)
                            {
                                foreach (var part in partsArr)
                                {
                                    if (part["type"]?.ToString() == "image_url")
                                    {
                                        b64Url = part["image_url"]?["url"]?.ToString();
                                        if (!string.IsNullOrEmpty(b64Url)) break;
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(b64Url))
                        {
                            string safeLog = resText.Length > 300 ? resText.Substring(0, 300) + "..." : resText;
                            CPH.LogWarn($"[OpenRouter Image] No image in response: {safeLog}");
                            return false;
                        }

                        // Extract raw base64 from data:image/...;base64,XXXX
                        string rawB64 = b64Url;
                        if (rawB64.Contains(",")) rawB64 = rawB64.Substring(rawB64.IndexOf(',') + 1);

                        byte[] bytes = Convert.FromBase64String(rawB64);
                        File.WriteAllBytes(savePath, bytes);
                        CPH.LogInfo($"[OpenRouter Image] Saved: {savePath} ({bytes.Length} bytes)");
                        return true;
                    }

                    // RETRY for 429
                    if ((int)res.StatusCode == 429)
                    {
                        CPH.LogInfo($"[OpenRouter Image] Rate Limit (429). Waiting {delayMs / 1000}s... (Attempt {i + 1}/{maxRetries})");
                        Thread.Sleep(delayMs);
                        continue;
                    }

                    string errLog = resText.Length > 300 ? resText.Substring(0, 300) + "..." : resText;
                    CPH.LogError($"[OpenRouter Image] API Error: {res.StatusCode} - {errLog}");
                    return false;
                }
            }
            catch (Exception ex) { CPH.LogWarn($"[OpenRouter Image] Ex: {ex.Message}"); }
        }
        return false;
    }

    // РАБОТА С ИСТОРИЕЙ
    
    // 1. ИСТОРИЯ UI (Виджет: 5 тем)
    private List<string> UpdateHistoryUI(string user, string amount, string message, string topic)
    {
        lock (_stateLock)
        {
            try {
                string historyJson = CPH.GetGlobalVar<string>("AiDonationHistory_UI", true);
                List<string> list;
                try {
                    list = string.IsNullOrEmpty(historyJson) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(historyJson);
                } catch { list = new List<string>(); }
                
                string entry = $"{user} ({amount}): {topic}";
                list.Add(entry);
                if (list.Count > 5) list.RemoveAt(0); 
                
                CPH.SetGlobalVar("AiDonationHistory_UI", JsonConvert.SerializeObject(list), true);
                return list;
            } catch { return new List<string>(); }
        }
    }

    // 2. ИСТОРИЯ КОНТЕКСТА (Память AI: 10 сообщений)
    private void UpdateHistoryContext(string user, string amount, string message, string reply)
    {
        lock (_stateLock)
        {
            try {
                string historyJson = CPH.GetGlobalVar<string>("AiDonationHistory_Full", true);
                List<string> list;
                try {
                    list = string.IsNullOrEmpty(historyJson) ? new List<string>() : JsonConvert.DeserializeObject<List<string>>(historyJson);
                } catch { list = new List<string>(); }
                
                string entry = $"[{DateTime.Now:HH:mm}] {user}: {message} -> AI: {reply}";
                
                list.Add(entry);
                if (list.Count > 10) list.RemoveAt(0); 
                
                CPH.SetGlobalVar("AiDonationHistory_Full", JsonConvert.SerializeObject(list), true);
                CPH.LogInfo($"[AI] Context History Updated ({list.Count} items).");
            } catch (Exception ex) { CPH.LogWarn($"[AI] Context Update Failed: {ex.Message}"); }
        }
    }

    private void CleanupOldTtsFiles(string dir)
    {
        try
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            var cutoff = DateTime.UtcNow.AddHours(-TTS_CACHE_MAX_AGE_HOURS);
            var files = new DirectoryInfo(dir).GetFiles("tts_*.mp3");

            foreach (var f in files)
            {
                if (f.CreationTimeUtc < cutoff)
                {
                    try { f.Delete(); } catch { }
                }
            }

            files = new DirectoryInfo(dir).GetFiles("tts_*.mp3");
            if (files.Length > TTS_CACHE_MAX_FILES)
            {
                foreach (var f in files.OrderBy(f => f.CreationTimeUtc).Take(files.Length - TTS_CACHE_MAX_FILES))
                {
                    try { f.Delete(); } catch { }
                }
            }
        }
        catch { }
    }

    private void SendLoadingOff()
    {
        try
        {
            var payload = new { action = "loading_off" };
            CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(payload));
            PublishRuntimeHide("donation-scene", 300);

            if (LOADING_OFF_CLOSE_FALLBACK)
            {
                var closePayload = new { action = "close" };
                CPH.WebsocketBroadcastJson(JsonConvert.SerializeObject(closePayload));
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[Overlay] loading_off failed: {ex.Message}");
        }
    }

    private void PublishRuntimeDonationLoading(string persona, string donor, string amount)
    {
        PublishRuntimeEvent(
            "overlay.show.v1",
            70,
            8000,
            new
            {
                template = "loading",
                persona = persona,
                donor = donor,
                amount = amount
            },
            "donation-loading",
            "donation-loading",
            400);
    }

    private void PublishRuntimeDonationScene(string persona, string donor, string amount, string message, string image, string audio, string fx, int durationMs)
    {
        PublishRuntimeEvent(
            "donation.scene.v1",
            100,
            durationMs,
            new
            {
                persona = persona,
                donor = donor,
                amount = amount,
                message = message,
                image = image,
                audio = audio,
                fx = fx
            },
            "donation-scene",
            "donation-scene",
            800);
    }

    private void PublishRuntimeHide(string replaceByKey, int cooldownMs)
    {
        PublishRuntimeEvent(
            "overlay.hide.v1",
            60,
            1500,
            new
            {
                replaceKey = replaceByKey
            },
            replaceByKey,
            replaceByKey,
            cooldownMs);
    }

    private void PublishRuntimeEvent(string type, int priority, int ttlMs, object payload, string replaceByKey, string cooldownKey, int cooldownMs)
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
            source = "streamerbot.donation",
            target = "runtime",
            priority = priority,
            ttlMs = ttlMs,
            payload = payload,
            queue = new
            {
                interruptible = true,
                replaceByKey = replaceByKey,
                minDisplayMs = 2000,
                cooldownKey = cooldownKey,
                cooldownMs = cooldownMs
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
            CPH.LogDebug($"[Donation] Runtime event failed: {ex.Message}");
        }
    }

    // ==========================================
    // V3.2 LOGIC HELPERS
    // ==========================================
    
    private int GetCryptoSeed() {
        // Deterministic Seed Generation
        using (var rng = new RNGCryptoServiceProvider()) {
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    // PERSONA STATE LOGIC (Update & Decay)
    private double UpdatePersonaState(string persona, double amount, ref AiPersonaState stateObj)
    {
        if (!stateObj.States.ContainsKey(persona)) {
            // Defaults
            double startVal = (persona == "Toxic") ? 50.0 : 10.0;
            stateObj.States[persona] = new PersonaStats { Value = startVal, LastUpdate = DateTime.Now };
        }

        var stats = stateObj.States[persona];
        double current = stats.Value;
        
        // 1. DECAY Logic (Tend to Target)
        // Skuf -> 10, Toxic -> 50, Rate = 0.5 per min
        double target = (persona == "Toxic") ? 50.0 : 10.0;
        double minutes = (DateTime.Now - stats.LastUpdate).TotalMinutes;
        if (minutes > 0) {
            // Value += Sign(Target - Value) * Rate * Min
            double change = Math.Sign(target - current) * 0.5 * minutes;
            if (Math.Abs(change) > Math.Abs(target - current)) current = target; // Don't overshoot
            else current += change;
        }

        // 2. BOOST Logic
        if (amount >= 0) // Only on donation
        {
            if (persona == "Skuf") 
            {
                // Skuf: +10 per donation (or math)
                 // Boost: clamp(3 + log10(amount + 1) * 6, 3, 15)
                double boost = Math.Max(3, Math.Min(15, 3 + Math.Log10(amount + 1) * 6));
                current += boost;
            }
            else if (persona == "Toxic")
            {
                // Toxic: Priority Thresholds
                if (amount < 100) current += 12;
                else if (amount <= 500) current += 5;
                else if (amount <= 2000) current -= 8;
                else current -= 15;
            }
        }

        // 3. CLAMP & SAVE
        current = Math.Max(0, Math.Min(100, current));
        stats.Value = current;
        stats.LastUpdate = DateTime.Now;
        
        return current;
    }
    // MEMORY SYSTEM (Donor Stats)
    public class DonorStat
    {
        public double Total { get; set; }
        public DateTime LastSeen { get; set; }
        public string Name { get; set; }
    }

    private void UpdateDonorStats(string name, double amount)
    {
        try {
            string json = CPH.GetGlobalVar<string>("AiDonorStats", true);
            var stats = string.IsNullOrEmpty(json) ? new Dictionary<string, DonorStat>() : JsonConvert.DeserializeObject<Dictionary<string, DonorStat>>(json);
            
            if (stats.ContainsKey(name)) {
                stats[name].Total += amount;
                stats[name].LastSeen = DateTime.Now;
            } else {
                stats[name] = new DonorStat { Name = name, Total = amount, LastSeen = DateTime.Now };
            }
            
            // CONSTRAINTS: Max 500 users (LRU Purge)
            if (stats.Count > 500) {
                var oldest = stats.OrderBy(k => k.Value.LastSeen).First().Key;
                stats.Remove(oldest);
            }
            
            CPH.SetGlobalVar("AiDonorStats", JsonConvert.SerializeObject(stats), true);
        } catch (Exception ex) { CPH.LogWarn($"[Memory] Error: {ex.Message}"); }
    }
}

