using System;
using System.Collections.Generic;
using StreamerOs.Shared.Contracts;

namespace StreamerOs.Shared.Runtime
{
    public sealed class VisualEventQueue
    {
        private readonly List<RuntimeEventEnvelope> _pending = new List<RuntimeEventEnvelope>();
        private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private RuntimeEventEnvelope _active;
        private DateTime _activeSinceUtc = DateTime.MinValue;

        public int PendingCount
        {
            get { return _pending.Count; }
        }

        public RuntimeEventEnvelope Peek()
        {
            return _pending.Count == 0 ? null : _pending[0];
        }

        public void MarkActive(RuntimeEventEnvelope envelope, DateTime nowUtc)
        {
            _active = envelope;
            _activeSinceUtc = nowUtc;
        }

        public void Enqueue(RuntimeEventEnvelope envelope, DateTime nowUtc)
        {
            if (envelope == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(envelope.CooldownKey))
            {
                DateTime untilUtc;
                if (_cooldowns.TryGetValue(envelope.CooldownKey, out untilUtc) && untilUtc > nowUtc)
                {
                    return;
                }
            }

            if (!string.IsNullOrEmpty(envelope.ReplaceKey))
            {
                for (int i = _pending.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(_pending[i].ReplaceKey, envelope.ReplaceKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _pending.RemoveAt(i);
                    }
                }
            }

            _pending.Add(envelope);
            _pending.Sort(CompareByPriorityDesc);
        }

        public RuntimeEventEnvelope TryDequeueReady(DateTime nowUtc)
        {
            if (_active != null)
            {
                var minReleaseUtc = _activeSinceUtc.AddMilliseconds(Math.Max(0, _active.MinDisplayMs));
                if (nowUtc < minReleaseUtc)
                {
                    return null;
                }
            }

            if (_pending.Count == 0)
            {
                return null;
            }

            RuntimeEventEnvelope next = _pending[0];
            _pending.RemoveAt(0);

            if (!string.IsNullOrEmpty(next.CooldownKey))
            {
                _cooldowns[next.CooldownKey] = nowUtc.AddMilliseconds(Math.Max(0, next.TtlMs));
            }

            return next;
        }

        private static int CompareByPriorityDesc(RuntimeEventEnvelope left, RuntimeEventEnvelope right)
        {
            return right.Priority.CompareTo(left.Priority);
        }
    }
}
