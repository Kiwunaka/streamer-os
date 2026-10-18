using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Action name: Streamer OS | Settings. Use a separate non-blocked queue.
public class CPHInline
{
    private static readonly HashSet<string> Allowed = new HashSet<string>("OpenRouterKey FireworksKey FalApiKey InworldTTSAuth FishAudioApiKey ElevenLabsKey NttsApiKey LlmProvider OpenRouterDefaultTextModel FireworksDefaultTextModel ChatBotProvider ChatBotModel ChatBotEnabled ChatBotAutoEnabled ChatBotTtsEnabled ChatBotMaxChars ChatBotMinIntervalSec ChatBotTemperature ChatBotMaxTokens ChatBotSystemPrompt ModProvider ModModel ModLlmMessageScan ModBlacklist ModResponsePrompt OracleProvider OracleModel DonationProvider DonationModel StreamRecapProvider StreamRecapModel TtsProviderPrimary TtsProviderSecondary TtsProviderTertiary InworldModelId InworldVoiceId InworldAudioEncoding InworldSampleRateHertz InworldSpeakingRate FishAudioModel FishAudioVoiceRefId FishAudioFormat FishAudioLatency ElevenLabsModel ElevenLabsVoiceId NttsVoiceList NttsExt NttsEffect ImageGenerationProvider FalImageModel ImageGenerationModel ImageRefineProvider ImageRefineModel ImageRefinePrompt ImageReactionEnabled ImageReactionProvider ImageReactionOpenRouterModel ImageReactionFireworksModel ImageShareToChat DonationImageProvider DonationImageModel Persona_Skuf_System Persona_Skuf_Style Persona_Skuf_Voice Persona_Skuf_FishRefId Persona_Skuf_InworldVoiceId Persona_Toxic_System Persona_Toxic_Style Persona_Toxic_Voice Persona_Toxic_FishRefId Persona_Toxic_InworldVoiceId Persona_UwU_System Persona_UwU_Style Persona_UwU_Voice Persona_UwU_FishRefId Persona_UwU_InworldVoiceId Persona_Mistress_System Persona_Mistress_Style Persona_Mistress_Voice Persona_Mistress_FishRefId Persona_Mistress_InworldVoiceId maClientId maClientSecret".Split(' '), StringComparer.Ordinal);
    private static readonly HashSet<string> Booleans = new HashSet<string>("ChatBotEnabled ChatBotAutoEnabled ChatBotTtsEnabled ModLlmMessageScan ImageReactionEnabled ImageShareToChat".Split(' '), StringComparer.Ordinal);
    private static readonly Dictionary<string, string[]> Choices = new Dictionary<string, string[]>
    {
        { "LlmProvider", new[] { "openrouter", "fireworks" } },
        { "ChatBotProvider", new[] { "", "openrouter", "fireworks" } },
        { "ModProvider", new[] { "", "openrouter", "fireworks" } },
        { "OracleProvider", new[] { "", "openrouter", "fireworks" } },
        { "DonationProvider", new[] { "", "openrouter", "fireworks" } },
        { "StreamRecapProvider", new[] { "", "openrouter", "fireworks" } },
        { "TtsProviderPrimary", new[] { "inworld", "fish", "elevenlabs", "ntts", "none" } },
        { "TtsProviderSecondary", new[] { "inworld", "fish", "elevenlabs", "ntts", "none" } },
        { "TtsProviderTertiary", new[] { "inworld", "fish", "elevenlabs", "ntts", "none" } },
        { "InworldAudioEncoding", new[] { "LINEAR16", "WAV", "MP3", "OGG_OPUS", "FLAC" } },
        { "FishAudioModel", new[] { "s2.1-pro", "s2.1-pro-free", "s2-pro", "s1", "drama-3" } },
        { "FishAudioFormat", new[] { "mp3", "wav", "opus" } },
        { "FishAudioLatency", new[] { "normal", "balanced" } },
        { "NttsExt", new[] { "wav", "mp3" } },
        { "ImageGenerationProvider", new[] { "fal", "fireworks" } },
        { "FalImageModel", new[] { "fal-ai/flux/schnell", "fal-ai/flux/dev" } },
        { "ImageRefineProvider", new[] { "openrouter", "fireworks" } },
        { "ImageReactionProvider", new[] { "openrouter", "fireworks" } },
        { "DonationImageProvider", new[] { "fal", "openrouter", "none" } }
    };

    public bool Execute()
    {
        string id = args.ContainsKey("settingsRequestId") ? args["settingsRequestId"]?.ToString() : "";
        try
        {
            if (!Guid.TryParse(id, out Guid parsedId)) throw new ArgumentException("Missing settingsRequestId UUID.");
            string raw = args.ContainsKey("settingsJson") ? args["settingsJson"]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(raw) || raw.Length > 128 * 1024) throw new ArgumentException("Invalid settings payload size.");
            var settings = JObject.Parse(raw, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            var values = new Dictionary<string, string>();
            foreach (var property in settings.Properties())
            {
                string key = property.Name;
                if (!Allowed.Contains(key)) throw new ArgumentException("Unknown setting: " + key);
                if (property.Value.Type != JTokenType.String) throw new ArgumentException("Expected string: " + key);
                string value = property.Value.ToString().Trim();
                if (value.Length > 12000) throw new ArgumentException("Setting too long: " + key);
                if (Booleans.Contains(key) && value != "true" && value != "false") throw new ArgumentException("Expected true/false: " + key);
                if (Choices.ContainsKey(key) && Array.IndexOf(Choices[key], value) < 0) throw new ArgumentException("Unsupported option: " + key);
                switch (key)
                {
                    case "ChatBotMaxChars": CheckNumber(key, value, 50, 500, true); break;
                    case "ChatBotMinIntervalSec": CheckNumber(key, value, 1, 3600, true); break;
                    case "ChatBotMaxTokens": CheckNumber(key, value, 16, 2000, true); break;
                    case "ChatBotTemperature": CheckNumber(key, value, 0, 2, false); break;
                    case "InworldSpeakingRate": CheckNumber(key, value, 0.5, 1.5, false); break;
                    case "InworldSampleRateHertz": CheckNumber(key, value, 8000, 48000, true); break;
                    case "ImageGenerationModel":
                        if (value != "accounts/fireworks/models/flux-kontext-max" && value != "accounts/fireworks/models/flux-kontext-pro") throw new ArgumentException("Fireworks image model must be flux-kontext-max or flux-kontext-pro.");
                        break;
                }
                if (key == "InworldTTSAuth" && value.Length > 0 && !value.StartsWith("Basic ", StringComparison.Ordinal)) throw new ArgumentException("InworldTTSAuth must include Basic followed by the portal credential.");
                values.Add(key, value);
            }
            // Validate the whole request before changing any globals.
            foreach (var pair in values) CPH.SetGlobalVar(pair.Key, pair.Value, true);
            CPH.SetGlobalVar("StreamerOsSettingsResult", JsonConvert.SerializeObject(new { id, ok = true }), false);
            CPH.LogInfo("[Settings] Saved " + values.Count + " fields.");
            return true;
        }
        catch (Exception ex)
        {
            CPH.SetGlobalVar("StreamerOsSettingsResult", JsonConvert.SerializeObject(new { id, ok = false, error = ex.Message }), false);
            CPH.LogWarn("[Settings] " + ex.Message);
            return false;
        }
    }
    private static void CheckNumber(string key, string raw, double min, double max, bool integer)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max || (integer && value != Math.Truncate(value)))
            throw new ArgumentException(key + ": expected " + min.ToString(CultureInfo.InvariantCulture) + ".." + max.ToString(CultureInfo.InvariantCulture));
    }
}
