using System;
using System.Collections.Generic;

namespace StreamerOs.Shared
{
    public sealed class StreamState
    {
        public int SchemaVersion { get; set; }
        public string CurrentGame { get; set; }
        public string StreamMode { get; set; }
        public string ActiveScene { get; set; }
        public MusicState Music { get; set; }
        public string ModerationMode { get; set; }
        public StreamFeatureFlags FeatureFlags { get; set; }
        public ProviderRoutingState ProviderRouting { get; set; }

        public static StreamState CreateDefault()
        {
            return new StreamState
            {
                SchemaVersion = 1,
                CurrentGame = "Unknown",
                StreamMode = "live",
                ActiveScene = string.Empty,
                Music = new MusicState(),
                ModerationMode = "standard",
                FeatureFlags = new StreamFeatureFlags(),
                ProviderRouting = new ProviderRoutingState(),
            };
        }
    }

    public sealed class PersonaState
    {
        public int SchemaVersion { get; set; }
        public string ActivePersona { get; set; }
        public string ActiveVoiceProfile { get; set; }
        public string ActiveImageStyleProfile { get; set; }
        public string IntroImageAsset { get; set; }
        public string IntroAudioAsset { get; set; }
        public Dictionary<string, bool> BehaviorFlags { get; set; }
        public Dictionary<string, long> Cooldowns { get; set; }

        public static PersonaState CreateDefault()
        {
            return new PersonaState
            {
                SchemaVersion = 1,
                ActivePersona = "Skuf",
                ActiveVoiceProfile = string.Empty,
                ActiveImageStyleProfile = string.Empty,
                IntroImageAsset = string.Empty,
                IntroAudioAsset = string.Empty,
                BehaviorFlags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                Cooldowns = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase),
            };
        }
    }

    public sealed class AutomationState
    {
        public int SchemaVersion { get; set; }
        public bool SafeModeEnabled { get; set; }
        public List<string> PendingVisualEventIds { get; set; }
        public Dictionary<string, bool> ViewerModes { get; set; }
        public Dictionary<string, string> RewardConfig { get; set; }
        public string LastRecapId { get; set; }
        public string LastFailureReason { get; set; }

        public static AutomationState CreateDefault()
        {
            return new AutomationState
            {
                SchemaVersion = 1,
                SafeModeEnabled = false,
                PendingVisualEventIds = new List<string>(),
                ViewerModes = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
                RewardConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                LastRecapId = string.Empty,
                LastFailureReason = string.Empty,
            };
        }
    }

    public sealed class MusicState
    {
        public string TrackTitle { get; set; }
        public string Artist { get; set; }
        public bool IsPlaying { get; set; }

        public MusicState()
        {
            TrackTitle = string.Empty;
            Artist = string.Empty;
            IsPlaying = false;
        }
    }

    public sealed class StreamFeatureFlags
    {
        public bool ChatBotEnabled { get; set; }
        public bool SafeModeEnabled { get; set; }
        public bool ViewerEventModesEnabled { get; set; }

        public StreamFeatureFlags()
        {
            ChatBotEnabled = true;
            SafeModeEnabled = false;
            ViewerEventModesEnabled = true;
        }
    }

    public sealed class ProviderRoutingState
    {
        public string PrimaryLlmProvider { get; set; }
        public string PrimaryVisionProvider { get; set; }
        public string PrimaryTtsProvider { get; set; }
        public string PrimaryImageProvider { get; set; }
        public string FallbackLlmProvider { get; set; }
        public string FallbackTtsProvider { get; set; }
        public string TextModel { get; set; }
        public string VisionModel { get; set; }
        public decimal? MaxLlmPriceUsd { get; set; }

        public ProviderRoutingState()
        {
            PrimaryLlmProvider = "openrouter";
            PrimaryVisionProvider = "fireworks";
            PrimaryTtsProvider = "fish";
            PrimaryImageProvider = "fireworks";
            FallbackLlmProvider = "fireworks";
            FallbackTtsProvider = "inworld";
            TextModel = string.Empty;
            VisionModel = string.Empty;
            MaxLlmPriceUsd = null;
        }
    }

    public sealed class StateBundle
    {
        public StreamState Stream { get; set; }
        public PersonaState Persona { get; set; }
        public AutomationState Automation { get; set; }

        public StateBundle()
        {
            Stream = StreamState.CreateDefault();
            Persona = PersonaState.CreateDefault();
            Automation = AutomationState.CreateDefault();
        }
    }

    public sealed class LegacyGlobalSnapshot
    {
        private readonly IDictionary<string, string> _values;

        public LegacyGlobalSnapshot(IDictionary<string, string> values)
        {
            _values = values ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Get(string key, string fallback)
        {
            string value;
            if (_values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            string value;
            if (_values.TryGetValue(key, out value))
            {
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }

            return fallback;
        }
    }
}
