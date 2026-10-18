using System;
using System.Collections.Generic;

namespace StreamerOs.Shared.Contracts
{
    public sealed class RuntimeEventEnvelope
    {
        public string Type { get; set; }
        public int Version { get; set; }
        public string EventId { get; set; }
        public string TimestampUtc { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public int Priority { get; set; }
        public int TtlMs { get; set; }
        public int MinDisplayMs { get; set; }
        public string ReplaceKey { get; set; }
        public string CooldownKey { get; set; }
        public bool Interruptible { get; set; }
        public Dictionary<string, object> Payload { get; set; }

        public RuntimeEventEnvelope()
        {
            Version = 1;
            Source = "streamerbot";
            Target = "runtime";
            Priority = 50;
            TtlMs = 15000;
            MinDisplayMs = 1200;
            Interruptible = true;
            Payload = new Dictionary<string, object>();
        }

        public static RuntimeEventEnvelope Create(string type, string target, Dictionary<string, object> payload)
        {
            return new RuntimeEventEnvelope
            {
                Type = type,
                EventId = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTime.UtcNow.ToString("o"),
                Target = string.IsNullOrEmpty(target) ? "runtime" : target,
                Payload = payload ?? new Dictionary<string, object>()
            };
        }

        public EventValidationResult Validate()
        {
            var result = new EventValidationResult();

            if (string.IsNullOrEmpty(Type))
            {
                result.Errors.Add("Type is required.");
            }
            else if (!Type.EndsWith(".v1", StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("Type must be a v1 contract.");
            }

            if (Version != 1)
            {
                result.Errors.Add("Version must equal 1.");
            }

            if (string.IsNullOrEmpty(EventId))
            {
                result.Errors.Add("EventId is required.");
            }

            if (string.IsNullOrEmpty(TimestampUtc))
            {
                result.Errors.Add("TimestampUtc is required.");
            }

            if (string.IsNullOrEmpty(Target))
            {
                result.Errors.Add("Target is required.");
            }

            if (Payload == null)
            {
                result.Errors.Add("Payload is required.");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
    }

    public sealed class EventValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; private set; }

        public EventValidationResult()
        {
            Errors = new List<string>();
        }
    }
}
