using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace BattleNET
{
    public class CommandQueue : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentPriorityQueue<CommandItem> _queue;
        private readonly SemaphoreSlim _queueLock;
        private readonly SemaphoreSlim _processingLock;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly SecurityManager _securityManager;
        private bool _isDisposed;

        private Task _processingTask = Task.CompletedTask;
        private bool _isProcessing;
        private const int MAX_QUEUE_SIZE = 1000;
        private const int MAX_CONCURRENT_COMMANDS = 5;
        private const int COMMAND_TIMEOUT_MS = 30000;

        public CommandQueue(
            Serilog.ILogger logger,
            PerformanceMonitor performanceMonitor,
            SecurityManager securityManager)
        {
            _logger = logger ?? Log.ForContext<CommandQueue>();
            _queue = new ConcurrentPriorityQueue<CommandItem>();
            _queueLock = new SemaphoreSlim(1, 1);
            _processingLock = new SemaphoreSlim(MAX_CONCURRENT_COMMANDS, MAX_CONCURRENT_COMMANDS);
            _cancellationTokenSource = new CancellationTokenSource();
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _securityManager = securityManager ?? throw new ArgumentNullException(nameof(securityManager));
            _isProcessing = false;
        }

        public class CommandItem : IComparable<CommandItem>
        {
            public string Command { get; }
            public CommandPriority Priority { get; }
            public TaskCompletionSource<string> Response { get; }
            public DateTime EnqueueTime { get; }
            public int RetryCount { get; set; }

            public CommandItem(string command, CommandPriority priority)
            {
                Command = command ?? throw new ArgumentNullException(nameof(command));
                Priority = priority;
                Response = new TaskCompletionSource<string>();
                EnqueueTime = DateTime.UtcNow;
                RetryCount = 0;
            }

            public int CompareTo(CommandItem? other)
            {
                if (other == null) return 1;
                return other.Priority.CompareTo(Priority);
            }
        }

        public async Task<string> EnqueueCommandAsync(
            string command,
            CommandPriority priority = CommandPriority.Normal,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            if (_queue.Count >= MAX_QUEUE_SIZE)
            {
                _logger.Warning("Command queue is full");
                throw new InvalidOperationException("Command queue is full");
            }

            var commandItem = new CommandItem(command, priority);
            _queue.Enqueue(commandItem);

            _logger.Debug("Enqueued command with priority {Priority}", priority);

            try
            {
                using var timeoutCts = new CancellationTokenSource(COMMAND_TIMEOUT_MS);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                return await commandItem.Response.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Command timed out");
                throw new TimeoutException("Command execution timed out");
            }
        }

        public void StartProcessing()
        {
            if (_isProcessing)
            {
                return;
            }

            _isProcessing = true;
            _processingTask = ProcessCommandsAsync(_cancellationTokenSource.Token);
            _logger.Information("Started command queue processing");
        }

        public async Task StopProcessingAsync()
        {
            if (!_isProcessing)
            {
                return;
            }

            _cancellationTokenSource.Cancel();
            try
            {
                await _processingTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }

            _isProcessing = false;
            _logger.Information("Stopped command queue processing");
        }

        private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _queueLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (_queue.Count == 0)
                        {
                            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        var commandItem = _queue.Dequeue();
                        if (commandItem == null)
                        {
                            continue;
                        }

                        _ = ProcessCommandAsync(commandItem, cancellationToken);
                    }
                    finally
                    {
                        _queueLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing command queue");
                }
            }
        }

        private async Task ProcessCommandAsync(CommandItem commandItem, CancellationToken cancellationToken)
        {
            try
            {
                await _processingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    _performanceMonitor.StartTimer($"Command_{commandItem.Command}");

                    // Validate command
                    if (!await _securityManager.ValidateCommandAsync(commandItem.Command, "127.0.0.1").ConfigureAwait(false))
                    {
                        commandItem.Response.TrySetException(new InvalidOperationException("Command validation failed"));
                        return;
                    }

                    // Process command
                    // This is where you would implement the actual command processing logic
                    // For now, we'll just simulate processing
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);

                    commandItem.Response.TrySetResult("Command processed successfully");
                }
                finally
                {
                    _processingLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing command");
                commandItem.Response.TrySetException(ex);
            }
            finally
            {
                _performanceMonitor.StopTimer($"Command_{commandItem.Command}");
            }
        }

        public void ClearQueue()
        {
            _queue.Clear();
            _logger.Information("Cleared command queue");
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _queueLock.Dispose();
                _processingLock.Dispose();
                _queue.Clear();
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

        public int QueueSize => _queue.Count;
        public bool IsProcessing => _isProcessing;
    }

    public class ConcurrentPriorityQueue<T> where T : IComparable<T>
    {
        private readonly ConcurrentBag<T> _items;

        public ConcurrentPriorityQueue()
        {
            _items = new ConcurrentBag<T>();
        }

        public void Enqueue(T item)
        {
            _items.Add(item);
        }

        public T? Dequeue()
        {
            if (_items.IsEmpty)
            {
                return default;
            }

            var items = _items.ToArray();
            Array.Sort(items);
            _items.Clear();

            var result = items[0];
            foreach (var item in items.Skip(1))
            {
                _items.Add(item);
            }

            return result;
        }

        public void Clear()
        {
            _items.Clear();
        }

        public int Count => _items.Count;
        public bool IsEmpty => _items.IsEmpty;
    }
}