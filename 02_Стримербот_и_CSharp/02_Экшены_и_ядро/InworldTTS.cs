using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// =======================================================
// Unified TTS Integration for Streamer.bot
// Routing comes from globals:
// TtsProviderPrimary -> TtsProviderSecondary -> TtsProviderTertiary
// Default route: Inworld -> NTTS -> Fish
// =======================================================

public class CPHInline
{
    private const string INWORLD_TTS_URL = "https://api.inworld.ai/tts/v1/voice";
    private const string FISH_TTS_URL = "https://api.fish.audio/v1/tts";
    private const string NTTS_TTS_URL = "https://ntts.fdev.team/api/v1/tts";
    private const string NTTS_SPEAKERS_URL = "https://ntts.fdev.team/api/v1/tts/speakers";

    private const string AUTH_HEADER_GLOBAL = "InworldTTSAuth";
    private const string NTTS_API_KEY_GLOBAL = "NttsApiKey";
    private const string NTTS_MIX_PCT_GLOBAL = "NttsMixPct";
    private const string NTTS_VOICES_GLOBAL = "NttsVoiceList";
    private const string NTTS_EXT_GLOBAL = "NttsExt";
    private const string NTTS_EFFECT_GLOBAL = "NttsEffect";

    private const string FISH_ENABLED_GLOBAL = "FishAudioEnabled";
    private const string FISH_API_KEY_GLOBAL = "FishAudioApiKey";
    private const string FISH_MODEL_GLOBAL = "FishAudioModel";
    private const string FISH_REFERENCE_ID_GLOBAL = "FishAudioVoiceRefId";
    private const string FISH_FORMAT_GLOBAL = "FishAudioFormat";
    private const string FISH_LATENCY_GLOBAL = "FishAudioLatency";
    private const string INWORLD_MODEL_ID_GLOBAL = "InworldModelId";
    private const string INWORLD_VOICE_ID_GLOBAL = "InworldVoiceId";
    private const string INWORLD_AUDIO_ENCODING_GLOBAL = "InworldAudioEncoding";
    private const string INWORLD_SAMPLE_RATE_GLOBAL = "InworldSampleRateHertz";
    private const string INWORLD_SPEAKING_RATE_GLOBAL = "InworldSpeakingRate";
    private const string TTS_PROVIDER_PRIMARY_GLOBAL = "TtsProviderPrimary";
    private const string TTS_PROVIDER_SECONDARY_GLOBAL = "TtsProviderSecondary";
    private const string TTS_PROVIDER_TERTIARY_GLOBAL = "TtsProviderTertiary";

    private static readonly List<string> InworldVoices = new List<string>
    {
        "default-mtpxxj7uhgk_xn4yzgz4aq__chitani",
        "default-mtpxxj7uhgk_xn4yzgz4aq__stenli",
        "default-mtpxxj7uhgk_xn4yzgz4aq__papich",
        "default-mtpxxj7uhgk_xn4yzgz4aq__dzhek_vorobey",
        "default-mtpxxj7uhgk_xn4yzgz4aq__zhenya_nevskaya",
        "default-mtpxxj7uhgk_xn4yzgz4aq__shrek",
        "Svetlana", "Elena", "Dmitry", "Nikolai"
    };

    private static readonly List<string> AggressiveInworldVoices = new List<string>
    {
        "default-mtpxxj7uhgk_xn4yzgz4aq__papich",
        "default-mtpxxj7uhgk_xn4yzgz4aq__stenli",
        "default-mtpxxj7uhgk_xn4yzgz4aq__shrek"
    };

    private static List<string> _nttsVoicesCache = new List<string>();
    private static DateTime _lastFetch = DateTime.MinValue;
    private readonly Random _rng = new Random();

    private sealed class TtsResult
    {
        public byte[] Data { get; set; }
        public string Extension { get; set; }
        public string Provider { get; set; }
    }

    public bool Execute()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

        string text = GetArg("rawInput") ?? GetArg("userInput") ?? GetArg("message") ?? GetArg("arg0");
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int annoyanceLevel = 0;
        int.TryParse(GetArg("annoyanceLevel") ?? "0", out annoyanceLevel);

        TtsResult result = null;
        foreach (string provider in GetProviderRoute())
        {
            result = TrySynthesize(provider, text, annoyanceLevel);
            if (result != null && result.Data != null && result.Data.Length > 0)
            {
                break;
            }
        }

        if (result == null || result.Data == null || result.Data.Length == 0)
        {
            CPH.LogWarn("[UnifiedTTS] All providers failed.");
            return false;
        }

        PlayAudio(result.Data, result.Extension);
        CPH.LogInfo($"[UnifiedTTS] Played via {result.Provider}");
        return true;
    }

    private TtsResult TrySynthesize(string provider, string text, int annoyanceLevel)
    {
        switch ((provider ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "inworld":
                return SynthesizeSpeechInworld(text, ResolveInworldVoiceId(annoyanceLevel), annoyanceLevel);
            case "ntts":
                return ShouldUseNtts() ? SynthesizeSpeechNtts(text) : null;
            case "fish":
                return ShouldUseFishAudio() ? SynthesizeSpeechFish(text) : null;
            default:
                return null;
        }
    }

    private TtsResult SynthesizeSpeechFish(string text)
    {
        string apiKey = GetFishApiKey();
        string referenceId = GetFishReferenceId();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(referenceId))
        {
            return null;
        }

        var body = new
        {
            text = text,
            reference_id = referenceId,
            format = GetFishFormat(),
            normalize = true,
            latency = GetFishLatency()
        };

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                client.DefaultRequestHeaders.Add("model", GetFishModel());
                var response = client.PostAsync(
                    FISH_TTS_URL,
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
                ).Result;

                if (!response.IsSuccessStatusCode)
                {
                    CPH.LogWarn($"[UnifiedTTS] Fish Audio returned {response.StatusCode}");
                    return null;
                }

                return new TtsResult
                {
                    Data = response.Content.ReadAsByteArrayAsync().Result,
                    Extension = GetFishFormat(),
                    Provider = "fish"
                };
            }
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[UnifiedTTS] Fish Audio failed: {ex.Message}");
            return null;
        }
    }

    private TtsResult SynthesizeSpeechNtts(string text)
    {
        string apiKey = GetNttsApiKey();
        string speaker = PickRandomNttsSpeaker(apiKey);
        if (string.IsNullOrWhiteSpace(speaker))
        {
            return null;
        }

        string url = $"{NTTS_TTS_URL}?speaker={Uri.EscapeDataString(speaker)}&text={Uri.EscapeDataString(text)}&ext={GetNttsExt()}";
        string effect = GetNttsEffect();
        if (!string.IsNullOrWhiteSpace(effect))
        {
            url += $"&effect={Uri.EscapeDataString(effect)}";
        }

        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                var response = client.GetAsync(url).Result;
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return new TtsResult
                {
                    Data = response.Content.ReadAsByteArrayAsync().Result,
                    Extension = GetNttsExt(),
                    Provider = "ntts"
                };
            }
        }
        catch
        {
            return null;
        }
    }

    private TtsResult SynthesizeSpeechInworld(string text, string voiceId, int annoyanceLevel)
    {
        double rate = GetInworldSpeakingRate();
        if (annoyanceLevel >= 10) rate = 1.35;
        else if (annoyanceLevel >= 7) rate = 1.18;
        else if (annoyanceLevel >= 4) rate = 1.05;

        var body = new
        {
            text = text,
            voiceId = voiceId,
            modelId = GetInworldModelId(),
            audioConfig = new
            {
                audioEncoding = GetInworldAudioEncoding(),
                sampleRateHertz = GetInworldSampleRate(),
                speakingRate = rate
            }
        };

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("Authorization", GetAuthHeader());
                var response = client.PostAsync(
                    INWORLD_TTS_URL,
                    new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
                ).Result;
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                return new TtsResult
                {
                    Data = Convert.FromBase64String(json["audioContent"].ToString()),
                    Extension = "wav",
                    Provider = "inworld"
                };
            }
        }
        catch
        {
            return null;
        }
    }

    private string ResolveInworldVoiceId(int annoyanceLevel)
    {
        string argVoice = GetArg("voiceId");
        if (!string.IsNullOrWhiteSpace(argVoice))
        {
            return argVoice;
        }

        string persona = CPH.GetGlobalVar<string>("CurrentPersona", true);
        if (string.IsNullOrWhiteSpace(persona))
        {
            persona = CPH.GetGlobalVar<string>("AiSystemPrompt", true);
        }

        if (!string.IsNullOrWhiteSpace(persona))
        {
            string scoped = CPH.GetGlobalVar<string>($"Persona_{persona}_InworldVoiceId", true);
            if (!string.IsNullOrWhiteSpace(scoped))
            {
                return scoped;
            }
        }

        string globalVoice = CPH.GetGlobalVar<string>(INWORLD_VOICE_ID_GLOBAL, true);
        if (!string.IsNullOrWhiteSpace(globalVoice))
        {
            return globalVoice;
        }

        if (annoyanceLevel >= 7)
        {
            return AggressiveInworldVoices[_rng.Next(AggressiveInworldVoices.Count)];
        }

        return InworldVoices[_rng.Next(InworldVoices.Count)];
    }

    private string PickRandomNttsSpeaker(string apiKey)
    {
        string manual = CPH.GetGlobalVar<string>(NTTS_VOICES_GLOBAL, true);
        if (!string.IsNullOrWhiteSpace(manual))
        {
            var split = manual.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0)
            {
                return split[_rng.Next(split.Length)].Trim();
            }
        }

        if (_nttsVoicesCache.Count == 0 || (DateTime.Now - _lastFetch).TotalMinutes > 30)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                    var response = client.GetAsync(NTTS_SPEAKERS_URL).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        var json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                        var list = new List<string>();
                        foreach (var voice in json["voices"])
                        {
                            foreach (var speaker in voice["speakers"])
                            {
                                list.Add(speaker.ToString());
                            }
                        }

                        if (list.Count > 0)
                        {
                            _nttsVoicesCache = list;
                            _lastFetch = DateTime.Now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CPH.LogWarn($"[UnifiedTTS] NTTS speaker fetch failed: {ex.Message}");
            }
        }

        return _nttsVoicesCache.Count > 0 ? _nttsVoicesCache[_rng.Next(_nttsVoicesCache.Count)] : null;
    }

    private bool ShouldUseNtts()
    {
        if (string.IsNullOrWhiteSpace(GetNttsApiKey()))
        {
            return false;
        }

        if (IsProviderExplicitlyRouted("ntts"))
        {
            return true;
        }

        int pct = 70;
        int.TryParse(CPH.GetGlobalVar<string>(NTTS_MIX_PCT_GLOBAL, true), out pct);
        return _rng.Next(100) < pct;
    }

    private bool ShouldUseFishAudio()
    {
        bool enabled;
        if (!bool.TryParse(CPH.GetGlobalVar<string>(FISH_ENABLED_GLOBAL, true), out enabled) || !enabled)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetFishApiKey()) && !string.IsNullOrWhiteSpace(GetFishReferenceId());
    }

    private void PlayAudio(byte[] data, string extension)
    {
        string ext = string.IsNullOrWhiteSpace(extension) ? "wav" : extension.Trim().TrimStart('.');
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "StreamerBot_AI_Cache", "tts",
            $"tts_{Guid.NewGuid().ToString().Substring(0, 8)}.{ext}"
        );

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
            CPH.PlaySound(path, 1.0f, true);
        }
        catch (Exception ex)
        {
            CPH.LogWarn($"[UnifiedTTS] Failed to play audio: {ex.Message}");
        }
    }

    private string GetArg(string key) => args.ContainsKey(key) ? args[key]?.ToString() : null;
    private string GetAuthHeader() => CPH.GetGlobalVar<string>(AUTH_HEADER_GLOBAL, true);
    private string GetNttsApiKey() => CPH.GetGlobalVar<string>(NTTS_API_KEY_GLOBAL, true);
    private string GetNttsExt() => CPH.GetGlobalVar<string>(NTTS_EXT_GLOBAL, true) ?? "wav";
    private string GetNttsEffect() => CPH.GetGlobalVar<string>(NTTS_EFFECT_GLOBAL, true);
    private string GetFishApiKey() => CPH.GetGlobalVar<string>(FISH_API_KEY_GLOBAL, true);
    private string GetFishModel() => CPH.GetGlobalVar<string>(FISH_MODEL_GLOBAL, true) ?? "s2-pro";
    private string GetFishFormat() => CPH.GetGlobalVar<string>(FISH_FORMAT_GLOBAL, true) ?? "mp3";
    private string GetFishLatency() => CPH.GetGlobalVar<string>(FISH_LATENCY_GLOBAL, true) ?? "normal";
    private string GetInworldModelId() => CPH.GetGlobalVar<string>(INWORLD_MODEL_ID_GLOBAL, true) ?? "inworld-tts-1.5-max";
    private string GetInworldAudioEncoding() => CPH.GetGlobalVar<string>(INWORLD_AUDIO_ENCODING_GLOBAL, true) ?? "LINEAR16";

    private int GetInworldSampleRate()
    {
        int value;
        return int.TryParse(CPH.GetGlobalVar<string>(INWORLD_SAMPLE_RATE_GLOBAL, true), out value) && value > 0
            ? value
            : 22050;
    }

    private double GetInworldSpeakingRate()
    {
        double value;
        return double.TryParse(CPH.GetGlobalVar<string>(INWORLD_SPEAKING_RATE_GLOBAL, true), out value) && value > 0
            ? value
            : 1.0;
    }

    private IEnumerable<string> GetProviderRoute()
    {
        string[] route =
        {
            CPH.GetGlobalVar<string>(TTS_PROVIDER_PRIMARY_GLOBAL, true),
            CPH.GetGlobalVar<string>(TTS_PROVIDER_SECONDARY_GLOBAL, true),
            CPH.GetGlobalVar<string>(TTS_PROVIDER_TERTIARY_GLOBAL, true)
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in route)
        {
            string provider = (raw ?? string.Empty).Trim().ToLowerInvariant();
            if (provider.Length == 0) continue;
            if (seen.Add(provider))
            {
                yield return provider;
            }
        }

        if (seen.Add("inworld")) yield return "inworld";
        if (seen.Add("ntts")) yield return "ntts";
        if (seen.Add("fish")) yield return "fish";
    }

    private bool IsProviderExplicitlyRouted(string providerName)
    {
        string target = (providerName ?? string.Empty).Trim().ToLowerInvariant();
        if (target.Length == 0) return false;

        string[] route =
        {
            CPH.GetGlobalVar<string>(TTS_PROVIDER_PRIMARY_GLOBAL, true),
            CPH.GetGlobalVar<string>(TTS_PROVIDER_SECONDARY_GLOBAL, true),
            CPH.GetGlobalVar<string>(TTS_PROVIDER_TERTIARY_GLOBAL, true)
        };

        foreach (string raw in route)
        {
            if (string.Equals((raw ?? string.Empty).Trim(), target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetFishReferenceId()
    {
        string persona = CPH.GetGlobalVar<string>("CurrentPersona", true);
        if (string.IsNullOrWhiteSpace(persona))
        {
            persona = CPH.GetGlobalVar<string>("AiSystemPrompt", true);
        }

        if (!string.IsNullOrWhiteSpace(persona))
        {
            string scoped = CPH.GetGlobalVar<string>($"Persona_{persona}_FishRefId", true);
            if (!string.IsNullOrWhiteSpace(scoped))
            {
                return scoped;
            }
        }

        return CPH.GetGlobalVar<string>(FISH_REFERENCE_ID_GLOBAL, true);
    }
}
