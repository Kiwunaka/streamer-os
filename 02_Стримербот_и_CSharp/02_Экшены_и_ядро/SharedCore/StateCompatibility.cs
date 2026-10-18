using System;
using System.Collections.Generic;

namespace StreamerOs.Shared
{
    public static class StateCompatibility
    {
        public static StreamState LoadStreamState(string json, IDictionary<string, string> legacyGlobals)
        {
            var state = string.IsNullOrWhiteSpace(json) ? StreamState.CreateDefault() : StateSerializer.Deserialize<StreamState>(json);

            if (string.IsNullOrWhiteSpace(state.CurrentGame) || string.Equals(state.CurrentGame, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                state.CurrentGame = GetLegacyValue(legacyGlobals, "CurrentGame", state.CurrentGame);
            }

            if (string.IsNullOrWhiteSpace(state.ProviderRouting.PrimaryLlmProvider))
            {
                state.ProviderRouting.PrimaryLlmProvider = GetLegacyValue(legacyGlobals, "LlmProvider", state.ProviderRouting.PrimaryLlmProvider);
            }

            if (!state.FeatureFlags.ChatBotEnabled && GetLegacyBool(legacyGlobals, "ChatBotEnabled", false))
            {
                state.FeatureFlags.ChatBotEnabled = true;
            }

            return state;
        }

        public static PersonaState LoadPersonaState(string json, IDictionary<string, string> legacyGlobals)
        {
            var state = string.IsNullOrWhiteSpace(json) ? PersonaState.CreateDefault() : StateSerializer.Deserialize<PersonaState>(json);

            if (string.IsNullOrWhiteSpace(state.ActivePersona))
            {
                state.ActivePersona = GetLegacyValue(legacyGlobals, "AiSystemPrompt", state.ActivePersona);
            }

            if (string.IsNullOrWhiteSpace(state.ActiveVoiceProfile))
            {
                state.ActiveVoiceProfile = GetLegacyValue(legacyGlobals, "FishVoiceProfile", state.ActiveVoiceProfile);
            }

            return state;
        }

        public static AutomationState LoadAutomationState(string json, IDictionary<string, string> legacyGlobals)
        {
            var state = string.IsNullOrWhiteSpace(json) ? AutomationState.CreateDefault() : StateSerializer.Deserialize<AutomationState>(json);
            state.SafeModeEnabled = state.SafeModeEnabled || GetLegacyBool(legacyGlobals, "SafeModeEnabled", false);
            return state;
        }

        private static string GetLegacyValue(IDictionary<string, string> legacyGlobals, string key, string fallback)
        {
            string value;
            if (legacyGlobals != null && legacyGlobals.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return fallback;
        }

        private static bool GetLegacyBool(IDictionary<string, string> legacyGlobals, string key, bool fallback)
        {
            string value;
            if (legacyGlobals != null && legacyGlobals.TryGetValue(key, out value))
            {
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            }

            return fallback;
        }
    }
}
