using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Web.Script.Serialization;

namespace StreamerOs.Shared.Providers
{
    public sealed class SpeechResult { public byte[] Data; public string Extension; public string Provider; }

    public static class SpeechClient
    {
        public static SpeechResult Synthesize(string text, Func<string, string> get, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var route = new[] { Value(get, "TtsProviderPrimary", "inworld"), Value(get, "TtsProviderSecondary", "ntts"), Value(get, "TtsProviderTertiary", "fish") };
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in route)
            {
                string provider = item.Trim().ToLowerInvariant();
                if (provider == "none" || !seen.Add(provider)) continue;
                try
                {
                    SpeechResult result = Generate(provider, text, get);
                    if (result != null && result.Data.Length > 0) return result;
                }
                catch (Exception ex) { log("[TTS] " + provider + ": " + ex.GetBaseException().Message); }
            }
            log("[TTS] No audio. Check provider order, keys and voice IDs.");
            return null;
        }

        private static SpeechResult Generate(string provider, string text, Func<string, string> get)
        {
            string key, url, extension, authName = "Authorization", authValue;
            object body;
            bool inworld = false;
            switch (provider)
            {
                case "elevenlabs":
                    key = get("ElevenLabsKey");
                    string elevenVoice = Voice(get, "Voice", "ElevenLabsVoiceId");
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(elevenVoice)) return null;
                    url = "https://api.elevenlabs.io/v1/text-to-speech/" + Uri.EscapeDataString(elevenVoice) + "?output_format=mp3_44100_128";
                    authName = "xi-api-key"; authValue = key;
                    body = new { text = text, model_id = Value(get, "ElevenLabsModel", "eleven_multilingual_v2") };
                    extension = "mp3";
                    break;
                case "fish":
                    key = get("FishAudioApiKey");
                    string fishVoice = Voice(get, "FishRefId", "FishAudioVoiceRefId");
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(fishVoice)) return null;
                    url = "https://api.fish.audio/v1/tts"; authValue = "Bearer " + key;
                    extension = Value(get, "FishAudioFormat", "mp3").ToLowerInvariant();
                    if (extension != "mp3" && extension != "wav" && extension != "opus") throw new ArgumentException("FishAudioFormat: choose mp3, wav or opus for playback.");
                    body = new { text = text, reference_id = fishVoice, format = extension, normalize = true, latency = Value(get, "FishAudioLatency", "normal") };
                    break;
                case "inworld":
                    authValue = get("InworldTTSAuth");
                    if (string.IsNullOrWhiteSpace(authValue)) return null;
                    if (text.Length > 2000) throw new ArgumentException("Inworld accepts at most 2000 UTF-16 characters.");
                    url = "https://api.inworld.ai/tts/v1/voice";
                    string encoding = Value(get, "InworldAudioEncoding", "LINEAR16").ToUpperInvariant();
                    switch (encoding)
                    {
                        case "LINEAR16": case "WAV": extension = "wav"; break;
                        case "MP3": extension = "mp3"; break;
                        case "OGG_OPUS": extension = "ogg"; break;
                        case "FLAC": extension = "flac"; break;
                        default: throw new ArgumentException("Choose a container encoding, not raw PCM.");
                    }
                    double rate;
                    if (!double.TryParse(Value(get, "InworldSpeakingRate", "1.0"), NumberStyles.Float, CultureInfo.InvariantCulture, out rate) || rate < 0.5 || rate > 1.5) throw new ArgumentException("InworldSpeakingRate must be 0.5..1.5.");
                    int sampleRate = int.Parse(Value(get, "InworldSampleRateHertz", "24000"), CultureInfo.InvariantCulture);
                    body = new { text = text, voiceId = Voice(get, "InworldVoiceId", "InworldVoiceId"), modelId = Value(get, "InworldModelId", "inworld-tts-2"), audioConfig = new { audioEncoding = encoding, sampleRateHertz = sampleRate, speakingRate = rate } };
                    inworld = true;
                    break;
                case "ntts": return Ntts(text, get);
                default: throw new ArgumentException("Unknown TTS provider: " + provider);
            }
            var json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
            using (var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
            {
                client.Timeout = TimeSpan.FromSeconds(25);
                client.DefaultRequestHeaders.TryAddWithoutValidation(authName, authValue);
                if (provider == "fish") client.DefaultRequestHeaders.Add("model", Value(get, "FishAudioModel", "s2.1-pro"));
                using (var response = client.PostAsync(url, new StringContent(json.Serialize(body), Encoding.UTF8, "application/json")).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    byte[] data;
                    if (inworld)
                    {
                        var envelope = json.Deserialize<Dictionary<string, object>>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                        data = Convert.FromBase64String((string)envelope["audioContent"]);
                    }
                    else data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    return new SpeechResult { Data = data, Extension = extension, Provider = provider };
                }
            }
        }

        private static SpeechResult Ntts(string text, Func<string, string> get)
        {
            string key = get("NttsApiKey");
            if (string.IsNullOrWhiteSpace(key)) return null;
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + key);
                var voices = new List<string>();
                string manual = get("NttsVoiceList");
                if (!string.IsNullOrWhiteSpace(manual)) voices.AddRange(manual.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));
                else
                {
                    string raw = client.GetStringAsync("https://ntts.fdev.team/api/v1/tts/speakers").GetAwaiter().GetResult();
                    var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(raw);
                    foreach (Dictionary<string, object> voice in (IEnumerable)json["voices"])
                        foreach (object speaker in (IEnumerable)voice["speakers"]) voices.Add(speaker.ToString());
                }
                if (voices.Count == 0) throw new InvalidOperationException("NTTS returned no speakers.");
                string ext = Value(get, "NttsExt", "wav");
                string url = "https://ntts.fdev.team/api/v1/tts?speaker=" + Uri.EscapeDataString(voices[new Random().Next(voices.Count)].Trim()) + "&text=" + Uri.EscapeDataString(text) + "&ext=" + Uri.EscapeDataString(ext);
                string effect = get("NttsEffect");
                if (!string.IsNullOrWhiteSpace(effect)) url += "&effect=" + Uri.EscapeDataString(effect);
                using (var response = client.GetAsync(url).GetAwaiter().GetResult())
                {
                    response.EnsureSuccessStatusCode();
                    return new SpeechResult { Data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult(), Extension = ext, Provider = "ntts" };
                }
            }
        }

        private static string Voice(Func<string, string> get, string suffix, string global)
        {
            string persona = Value(get, "CurrentPersona", Value(get, "AiSystemPrompt", "Skuf"));
            return Value(get, "Persona_" + persona + "_" + suffix, Value(get, global, ""));
        }
        private static string Value(Func<string, string> get, string key, string fallback)
        {
            string value = get(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
