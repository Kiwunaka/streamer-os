using System;
using System.Collections.Generic;

namespace StreamerOs.Shared
{
    public enum EventPriority
    {
        Low = 10,
        Normal = 50,
        High = 80,
        Critical = 100
    }

    public enum VisualQueueRejectReason
    {
        None = 0,
        CooldownActive = 1,
        EventMissing = 2
    }

    public sealed class QueuePolicy
    {
        public bool Interruptible { get; set; }
        public string ReplaceByKey { get; set; }
        public int MinDisplayMs { get; set; }
        public string CooldownKey { get; set; }
        public int CooldownMs { get; set; }

        public QueuePolicy()
        {
            Interruptible = true;
            ReplaceByKey = string.Empty;
            MinDisplayMs = 0;
            CooldownKey = string.Empty;
            CooldownMs = 0;
        }
    }

    public sealed class EventEnvelope
    {
        public string Type { get; set; }
        public int Version { get; set; }
        public string EventId { get; set; }
        public string Timestamp { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public EventPriority Priority { get; set; }
        public int TtlMs { get; set; }
        public string PayloadJson { get; set; }
        public QueuePolicy Queue { get; set; }
        public string ReplaceKey
        {
            get { return Queue == null ? string.Empty : Queue.ReplaceByKey; }
        }

        public string CooldownKey
        {
            get { return Queue == null ? string.Empty : Queue.CooldownKey; }
        }

        public EventEnvelope()
        {
            Type = string.Empty;
            Version = 1;
            EventId = Guid.NewGuid().ToString("N");
            Timestamp = DateTime.UtcNow.ToString("o");
            Source = string.Empty;
            Target = string.Empty;
            Priority = EventPriority.Normal;
            TtlMs = 15000;
            PayloadJson = "{}";
            Queue = new QueuePolicy();
        }

        public static EventEnvelope Create(
            string type,
            string source,
            string target,
            EventPriority priority,
            int ttlMs,
            string payloadJson,
            string replaceKey,
            string cooldownKey,
            int cooldownMs)
        {
            var envelope = new EventEnvelope
            {
                Type = type ?? string.Empty,
                Version = ParseVersion(type),
                Source = source ?? string.Empty,
                Target = target ?? string.Empty,
                Priority = priority,
                TtlMs = ttlMs,
                PayloadJson = payloadJson ?? "{}"
            };
            envelope.Queue.ReplaceByKey = replaceKey ?? string.Empty;
            envelope.Queue.CooldownKey = cooldownKey ?? string.Empty;
            envelope.Queue.CooldownMs = cooldownMs;
            return envelope;
        }

        public static int ParseVersion(string type)
        {
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new InvalidOperationException("Event type is required.");
            }

            var marker = ".v";
            var index = type.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index <= 0 || index + marker.Length >= type.Length)
            {
                throw new InvalidOperationException("Event type must end with .v{number}.");
            }

            int version;
            if (!int.TryParse(type.Substring(index + marker.Length), out version) || version != 1)
            {
                throw new InvalidOperationException("Only v1 event contracts are supported.");
            }

            return version;
        }
    }

    public static class EventEnvelopeJson
    {
        public static string Serialize(EventEnvelope envelope)
        {
            return StateSerializer.Serialize(envelope);
        }

        public static EventEnvelope Deserialize(string json)
        {
            var envelope = StateSerializer.Deserialize<EventEnvelope>(json);
            envelope.Version = EventEnvelope.ParseVersion(envelope.Type);
            return envelope;
        }
    }

    public sealed class OverlayShowPayload
    {
        public string Template { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Body { get; set; }
        public string MediaUrl { get; set; }
        public string Accent { get; set; }
        public string ReplaceKey { get; set; }
    }

    public sealed class PersonaSwitchPayload
    {
        public string Persona { get; set; }
        public string VoiceProfile { get; set; }
        public string ThemePreset { get; set; }
        public Dictionary<string, string> Assets { get; set; }

        public PersonaSwitchPayload()
        {
            Assets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public sealed class SystemToastPayload
    {
        public string Message { get; set; }
        public string Level { get; set; }
    }

    public sealed class EnqueueResult
    {
        public bool Accepted { get; set; }
        public VisualQueueRejectReason Reason { get; set; }
    }

    public sealed class VisualEventQueue
    {
        private readonly List<EventEnvelope> _entries = new List<EventEnvelope>();
        private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public int Count
        {
            get { return _entries.Count; }
        }

        public EnqueueResult Enqueue(EventEnvelope envelope, DateTime nowUtc)
        {
            if (envelope == null)
            {
                return new EnqueueResult { Accepted = false, Reason = VisualQueueRejectReason.EventMissing };
            }

            bool replacingExisting = false;
            if (!string.IsNullOrWhiteSpace(envelope.Queue.ReplaceByKey))
            {
                replacingExisting = _entries.Exists(item => string.Equals(item.Queue.ReplaceByKey, envelope.Queue.ReplaceByKey, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(envelope.Queue.CooldownKey) && envelope.Queue.CooldownMs > 0)
            {
                DateTime until;
                if (!replacingExisting && _cooldowns.TryGetValue(envelope.Queue.CooldownKey, out until) && until > nowUtc)
                {
                    return new EnqueueResult { Accepted = false, Reason = VisualQueueRejectReason.CooldownActive };
                }

                _cooldowns[envelope.Queue.CooldownKey] = nowUtc.AddMilliseconds(envelope.Queue.CooldownMs);
            }

            if (!string.IsNullOrWhiteSpace(envelope.Queue.ReplaceByKey))
            {
                _entries.RemoveAll(item => string.Equals(item.Queue.ReplaceByKey, envelope.Queue.ReplaceByKey, StringComparison.OrdinalIgnoreCase));
            }

            _entries.Add(envelope);
            return new EnqueueResult { Accepted = true, Reason = VisualQueueRejectReason.None };
        }

        public EventEnvelope Peek()
        {
            return _entries.Count == 0 ? null : _entries[0];
        }

        public EventEnvelope TryDequeue(DateTime nowUtc)
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            _entries.Sort((left, right) =>
            {
                var priorityCompare = right.Priority.CompareTo(left.Priority);
                if (priorityCompare != 0)
                {
                    return priorityCompare;
                }

                return string.CompareOrdinal(left.Timestamp, right.Timestamp);
            });

            var next = _entries[0];
            _entries.RemoveAt(0);
            return next;
        }
    }
}
