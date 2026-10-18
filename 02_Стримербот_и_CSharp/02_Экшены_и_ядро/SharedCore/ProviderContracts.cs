using System;
using System.Collections.Generic;

namespace StreamerOs.Shared
{
    public sealed class ProviderRequestOptions
    {
        public string Provider { get; set; }
        public string Model { get; set; }
        public string FallbackProvider { get; set; }
        public string FallbackModel { get; set; }
        public decimal? MaxPriceUsd { get; set; }
        public bool AllowFallbacks { get; set; }
        public int TimeoutMs { get; set; }

        public ProviderRequestOptions()
        {
            Provider = string.Empty;
            Model = string.Empty;
            FallbackProvider = string.Empty;
            FallbackModel = string.Empty;
            MaxPriceUsd = null;
            AllowFallbacks = true;
            TimeoutMs = 30000;
        }
    }

    public sealed class ProviderResult
    {
        public bool Success { get; set; }
        public string Provider { get; set; }
        public string Model { get; set; }
        public string Content { get; set; }
        public string Error { get; set; }
        public string MetadataJson { get; set; }

        public ProviderResult()
        {
            Provider = string.Empty;
            Model = string.Empty;
            Content = string.Empty;
            Error = string.Empty;
            MetadataJson = "{}";
        }
    }

    public interface ILLMClient
    {
        ProviderResult Complete(string systemPrompt, string userPrompt, ProviderRequestOptions options);
    }

    public interface ITtsClient
    {
        ProviderResult Synthesize(string text, string voiceProfile, ProviderRequestOptions options);
    }

    public interface IImageClient
    {
        ProviderResult Generate(string prompt, ProviderRequestOptions options);
    }

    public interface ITranscriptionClient
    {
        ProviderResult Transcribe(byte[] audioBytes, ProviderRequestOptions options);
    }

    public sealed class PromptProfile
    {
        public string Key { get; set; }
        public string SystemPrompt { get; set; }
        public string VoiceProfile { get; set; }
        public string ImageStyleProfile { get; set; }
        public string ThemePreset { get; set; }

        public PromptProfile()
        {
            Key = string.Empty;
            SystemPrompt = string.Empty;
            VoiceProfile = string.Empty;
            ImageStyleProfile = string.Empty;
            ThemePreset = string.Empty;
        }
    }

    public sealed class PromptProfileRegistry
    {
        private readonly Dictionary<string, PromptProfile> _profiles;

        public PromptProfileRegistry()
        {
            _profiles = new Dictionary<string, PromptProfile>(StringComparer.OrdinalIgnoreCase);
        }

        public void Register(PromptProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Key))
            {
                return;
            }

            _profiles[profile.Key] = profile;
        }

        public PromptProfile Resolve(string key)
        {
            PromptProfile profile;
            return _profiles.TryGetValue(key ?? string.Empty, out profile) ? profile : null;
        }
    }
}
