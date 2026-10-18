namespace StreamerOs.Shared
{
    public static class LegacyStateMapper
    {
        public static StateBundle Map(LegacyGlobalSnapshot globals)
        {
            var bundle = new StateBundle();

            bundle.Stream.CurrentGame = globals.Get("CurrentGame", bundle.Stream.CurrentGame);
            bundle.Stream.ProviderRouting.PrimaryLlmProvider = globals.Get("LlmProvider", "openrouter");
            bundle.Stream.ProviderRouting.TextModel = globals.Get("ChatBotModel", globals.Get("FireworksModelChat", string.Empty));
            bundle.Stream.ProviderRouting.PrimaryVisionProvider = "fireworks";
            bundle.Stream.ProviderRouting.VisionModel = globals.Get("FireworksModelChat", string.Empty);
            bundle.Stream.ProviderRouting.PrimaryTtsProvider = globals.Get("TtsProviderPrimary", "inworld");
            bundle.Stream.FeatureFlags.SafeModeEnabled = globals.GetBool("SafeModeEnabled", false);

            bundle.Persona.ActivePersona = globals.Get("AiSystemPrompt", bundle.Persona.ActivePersona);
            bundle.Persona.BehaviorFlags["roleplay_enabled"] = globals.GetBool("RoleplayEnabled", false);
            bundle.Persona.BehaviorFlags["image_reaction_enabled"] = globals.GetBool("ImageReactionEnabled", true);
            bundle.Persona.ActiveVoiceProfile = globals.Get("PersonaVoiceProfile", "default");
            bundle.Persona.ActiveImageStyleProfile = globals.Get("PersonaImageStyleProfile", "default");

            bundle.Automation.ViewerModes["chatbot_enabled"] = globals.GetBool("ChatBotEnabled", true);
            bundle.Automation.ViewerModes["chatbot_auto_enabled"] = globals.GetBool("ChatBotAutoEnabled", false);
            bundle.Automation.ViewerModes["viewers_enabled"] = globals.GetBool("STREAMSKILL_VIEWERS_ENABLED", false);
            bundle.Automation.RewardConfig["moderation_mode"] = globals.Get("ModLlmMessageScan", "false");

            return bundle;
        }
    }
}
