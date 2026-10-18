using System;
using System.Collections.Generic;

// =======================================================
// Init Globals
// Creates missing globals with defaults (safe).
// Use args: force=true to overwrite.
// =======================================================

public class CPHInline
{
    private const string DefaultStreamStateJson =
        "{\"SchemaVersion\":1,\"CurrentGame\":\"Unknown\",\"StreamMode\":\"live\",\"ActiveScene\":\"\",\"Music\":{\"TrackTitle\":\"\",\"Artist\":\"\",\"IsPlaying\":false},\"ModerationMode\":\"standard\",\"ProviderRouting\":{\"PrimaryLlmProvider\":\"openrouter\",\"PrimaryVisionProvider\":\"fireworks\",\"PrimaryTtsProvider\":\"inworld\",\"PrimaryImageProvider\":\"fireworks\",\"FallbackLlmProvider\":\"fireworks\",\"FallbackTtsProvider\":\"ntts\",\"TextModel\":\"\",\"VisionModel\":\"\",\"ImageModel\":\"accounts/fireworks/models/flux-kontext-max\",\"MaxLlmPriceUsd\":null},\"FeatureFlags\":{\"ChatBotEnabled\":true,\"SafeModeEnabled\":false,\"ViewerEventModesEnabled\":true}}";

    private const string DefaultPersonaStateJson =
        "{\"SchemaVersion\":1,\"ActivePersona\":\"Skuf\",\"ActiveVoiceProfile\":\"\",\"ActiveImageStyleProfile\":\"\",\"IntroImageAsset\":\"intro_Skuf.png\",\"IntroAudioAsset\":\"intro_Skuf.mp3\",\"BehaviorFlags\":{\"roleplay_enabled\":true,\"image_reaction_enabled\":true},\"Cooldowns\":{}}";

    private const string DefaultAutomationStateJson =
        "{\"SchemaVersion\":1,\"SafeModeEnabled\":false,\"PendingVisualEventIds\":[],\"ViewerModes\":{\"chatbot_enabled\":true,\"chatbot_auto_enabled\":false,\"viewer_modes_enabled\":true},\"RewardConfig\":{},\"LastRecapId\":\"\",\"LastFailureReason\":\"\"}";

    private const string DefaultViewerGameStateJson =
        "{\"SchemaVersion\":1,\"Players\":{},\"Season\":\"alpha\",\"LastEventId\":\"\"}";

    private const string DefaultViewerRelationshipStateJson =
        "{\"SchemaVersion\":1,\"Users\":{}}";

    public bool Execute()
    {
        bool force = args.ContainsKey("force") && args["force"].ToString().ToLower() == "true";

        var defaults = new Dictionary<string, string>
        {
            // Keys
            { "OpenRouterKey", "" },
            { "FireworksKey", "" },
            { "InworldTTSAuth", "" },
            { "NttsApiKey", "" },
            { "NttsExt", "wav" },
            { "NttsEffect", "" },
            { "NttsVoiceList", "" },
            { "NttsMixPct", "70" },
            { "ElevenLabsKey", "" },
            { "OpenRouterSiteUrl", "https://twitch.tv/your_channel" },
            { "OpenRouterAppName", "StreamerOS Bot" },
            { "LlmProvider", "openrouter" },
            { "FireworksModelChat", "accounts/fireworks/models/kimi-k2-instruct-0905" },
            { "OpenRouterDefaultTextModel", "xiaomi/mimo-v2-flash" },
            { "FireworksDefaultTextModel", "accounts/fireworks/models/kimi-k2-instruct-0905" },
            { "ChatBotProvider", "" },
            { "ModProvider", "" },
            { "ModResponseProvider", "" },
            { "OracleProvider", "" },
            { "DonationProvider", "" },
            { "StreamRecapProvider", "" },
            { "OracleModel", "xiaomi/mimo-v2-flash" },
            { "VisualRuntimeEndpoint", "http://127.0.0.1:1608/events" },

            // Persona / Roleplay
            { "AiSystemPrompt", "Skuf" },
            { "CurrentPersona", "Skuf" },
            { "RoleplayEnabled", "true" },
            { "CurrentGame", "Unknown" },

            // Moderator
            { "ModBlacklist", "badword1,badword2" },
            { "ModLlmMessageScan", "false" },
            { "ModTimeoutSeconds", "60" },
            { "ModModel", "google/gemini-2.0-flash-001" },
            { "ModResponseModel", "google/gemini-2.0-flash-001" },
            { "ModResponsePrompt", "" },

            // Chat bot
            { "ChatBotEnabled", "true" },
            { "ChatBotAutoEnabled", "false" },
            { "ChatBotSystemPrompt", "" },
            { "ChatBotMaxChars", "500" },
            { "ChatBotMinIntervalSec", "20" },
            { "ChatBotCommandsCacheTtlSec", "300" },
            { "ChatBotModel", "xiaomi/mimo-v2-flash" },
            { "ChatBotTemperature", "0.7" },
            { "ChatBotMaxTokens", "140" },
            { "ChatBotRelationshipEnabled", "true" },
            { "ChatBotMemorySize", "5" },
            { "ChatBotRelationshipDecayMinutes", "30" },
            { "ChatBotAllowSpicyRoasts", "true" },
            { "ChatBotTtsEnabled", "true" },
            { "ChatBotTtsAction", "TTS" },
            { "ChatBotStrictSafety", "true" },
            { "ChatBotSharpnessMode", "safe" },
            { "ChatBotReasoningEffort", "none" },
            { "FishAudioEnabled", "false" },
            { "FishAudioApiKey", "" },
            { "FishAudioModel", "s2-pro" },
            { "FishAudioVoiceRefId", "" },
            { "FishAudioFormat", "mp3" },
            { "FishAudioLatency", "normal" },
            { "TtsProviderPrimary", "inworld" },
            { "TtsProviderSecondary", "ntts" },
            { "TtsProviderTertiary", "fish" },
            { "InworldModelId", "inworld-tts-1.5-max" },
            { "InworldVoiceId", "default-mtpxxj7uhgk_xn4yzgz4aq__chitani" },
            { "InworldAudioEncoding", "LINEAR16" },
            { "InworldSampleRateHertz", "22050" },
            { "InworldSpeakingRate", "1.0" },

            // Donation
            { "DonationModel", "xiaomi/mimo-v2-flash" },
            { "StreamRecapModel", "xiaomi/mimo-v2-flash" },

            { "ImageReactionEnabled", "true" },
            { "ImageReactionTtsAction", "TTS" },
            { "ImageReactionMaxWords", "12" },
            { "ImageReactionTimeoutMs", "3000" },
            { "ImageReactionStrictSafety", "true" },
            { "ImageReactionStyle", "sarcastic_soft" },
            { "ImageRefineProvider", "openrouter" },
            { "ImageRefineModel", "google/gemini-2.0-flash-001" },
            { "ImageReactionProvider", "fireworks" },
            { "ImageGenerationProvider", "fireworks" },
            { "ImageGenerationModel", "accounts/fireworks/models/flux-kontext-max" },
            { "ImageGenerationWorkflowUrl", "https://api.fireworks.ai/inference/v1/workflows/accounts/fireworks/models/flux-kontext-max" },
            { "ImageRefineReasoningEffort", "none" },
            { "ImageReactionFireworksModel", "accounts/fireworks/models/kimi-k2p5" },
            { "ImageReactionReasoningEffort", "none" },
            { "ImageReactionFallbackFireworksModel", "accounts/fireworks/models/minimax-m2p1" },
            { "ImageReactionFallbackReasoningEffort", "none" },
            { "ImageReactionOpenRouterModel", "google/gemini-2.0-flash-001" },
            { "StreamState", DefaultStreamStateJson },
            { "PersonaState", DefaultPersonaStateJson },
            { "AutomationState", DefaultAutomationStateJson },
            { "ViewerGameState", DefaultViewerGameStateJson },
            { "ViewerRelationshipState", DefaultViewerRelationshipStateJson },

            // Twitch API (auto game)
        };

        int updated = 0;
        int existing = 0;
        var missingKeys = new List<string>();
        foreach (var kvp in defaults)
        {
            string key = kvp.Key;
            string value = kvp.Value;

            string current = CPH.GetGlobalVar<string>(key, true);
            if (force || string.IsNullOrEmpty(current))
            {
                CPH.SetGlobalVar(key, value, true);
                updated++;
                missingKeys.Add(key);
            }
            else
            {
                existing++;
            }
        }

        CPH.SendMessage($"InitGlobals: updated {updated}, existing {existing}. Force={force}");
        if (missingKeys.Count > 0)
        {
            CPH.LogInfo("[InitGlobals] Created/updated: " + string.Join(", ", missingKeys));
        }
        return true;
    }
}
