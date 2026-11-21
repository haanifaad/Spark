using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Infrastructure;

/// <summary>
/// Thread-safe in-process asynchronous event bus for decoupled Study Division events.
/// </summary>
public sealed class StudyEventBus : IStudyEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default) where TEvent : StudyEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        List<Delegate>? targets = null;
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                targets = new List<Delegate>(list);
            }
        }

        if (targets == null || targets.Count == 0)
        {
            return;
        }

        foreach (var d in targets)
        {
            if (d is Func<TEvent, CancellationToken, Task> typedHandler)
            {
                try
                {
                    await typedHandler(domainEvent, ct);
                }
                catch
                {
                    // Isolate subscriber errors so one failure does not halt other handlers
                }
            }
        }
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : StudyEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_lock)
        {
            var list = _handlers.GetOrAdd(typeof(TEvent), _ => []);
            list.Add(handler);
        }

        return new SubscriptionToken(() =>
        {
            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(TEvent), out var list))
                {
                    list.Remove(handler);
                }
            }
        });
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
        }
    }
}
