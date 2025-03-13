using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace BattleNET
{
    public class EventSystem : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<BattlEyeEventType, ConcurrentBag<EventHandler<BattlEyeEventArgs>>> _handlers;
        private readonly ConcurrentDictionary<string, object> _eventData;
        private readonly SemaphoreSlim _eventLock;
        private bool _isDisposed;
        private const int EVENT_TIMEOUT_MS = 5000;

        public EventSystem(Serilog.ILogger logger)
        {
            _logger = logger ?? Log.ForContext<EventSystem>();
            _handlers = new ConcurrentDictionary<BattlEyeEventType, ConcurrentBag<EventHandler<BattlEyeEventArgs>>>();
            _eventData = new ConcurrentDictionary<string, object>();
            _eventLock = new SemaphoreSlim(1, 1);
        }

        public void Subscribe(BattlEyeEventType eventType, EventHandler<BattlEyeEventArgs> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var handlers = _handlers.GetOrAdd(eventType, _ => new ConcurrentBag<EventHandler<BattlEyeEventArgs>>());
            handlers.Add(handler);
            _logger.Debug("Subscribed to event: {EventType}", eventType);
        }

        public void Unsubscribe(BattlEyeEventType eventType, EventHandler<BattlEyeEventArgs> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                var newHandlers = new ConcurrentBag<EventHandler<BattlEyeEventArgs>>();
                foreach (var h in handlers)
                {
                    if (h != handler)
                    {
                        newHandlers.Add(h);
                    }
                }
                _handlers.TryUpdate(eventType, newHandlers, handlers);
                _logger.Debug("Unsubscribed from event: {EventType}", eventType);
            }
        }

        public async Task RaiseEventAsync(BattlEyeEventType eventType, BattlEyeEventArgs args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        await Task.Run(() => handler(this, args)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error handling event: {EventType}", eventType);
                        throw;
                    }
                }
            }
        }

        public void SetEventData(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            _eventData.AddOrUpdate(key, value, (_, __) => value);
            _logger.Debug("Set event data: {Key}", key);
        }

        public T? GetEventData<T>(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }

            if (_eventData.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return default;
        }

        public void ClearEventData()
        {
            _eventData.Clear();
            _logger.Debug("Cleared event data");
        }

        public int GetHandlerCount(BattlEyeEventType eventType)
        {
            return _handlers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
        }

        public async Task<bool> WaitForEventAsync(BattlEyeEventType eventType, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();
            var handler = new EventHandler<BattlEyeEventArgs>((_, __) => tcs.TrySetResult(true));

            try
            {
                Subscribe(eventType, handler);
                using var timeoutCts = new CancellationTokenSource(EVENT_TIMEOUT_MS);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                return await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                Unsubscribe(eventType, handler);
            }
        }

        public async Task<T?> WaitForEventDataAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<T?>();
            var handler = new EventHandler<BattlEyeEventArgs>((_, __) =>
            {
                if (_eventData.TryGetValue(key, out var value) && value is T typedValue)
                {
                    tcs.TrySetResult(typedValue);
                }
            });

            try
            {
                Subscribe(BattlEyeEventType.PacketReceived, handler);
                using var timeoutCts = new CancellationTokenSource(EVENT_TIMEOUT_MS);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                return await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return default;
            }
            finally
            {
                Unsubscribe(BattlEyeEventType.PacketReceived, handler);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _eventLock.Dispose();
                _handlers.Clear();
                _eventData.Clear();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during disposal");
                throw;
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
}