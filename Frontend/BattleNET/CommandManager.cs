using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BattleNET
{
    public enum CommandPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    public class CommandManager : IDisposable
    {
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, CommandState> _commandStates;
        private readonly SemaphoreSlim _commandLock;
        private readonly int _maxRetries;
        private readonly int _commandTimeout;
        private readonly ConcurrentDictionary<int, CommandState> _commands;
        private int _nextCommandId;
        private const int RETRY_DELAY_MS = 1000;
        private readonly Dictionary<string, CommandHandler> _commandHandlers;
        private readonly Queue<CommandRequest> _commandQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _commandProcessorTask;

        public CommandManager(Serilog.ILogger logger, int maxRetries = 3, int commandTimeout = 30000)
        {
            _logger = logger ?? Log.ForContext<CommandManager>();
            _commandStates = new ConcurrentDictionary<string, CommandState>();
            _commandLock = new SemaphoreSlim(1, 1);
            _maxRetries = maxRetries;
            _commandTimeout = commandTimeout;
            _commands = new ConcurrentDictionary<int, CommandState>();
            _nextCommandId = 0;
            _commandHandlers = new Dictionary<string, CommandHandler>();
            _commandQueue = new Queue<CommandRequest>();
            _cancellationTokenSource = new CancellationTokenSource();
            _commandProcessorTask = ProcessCommandsAsync(_cancellationTokenSource.Token);
        }

        public class CommandState
        {
            public int Id { get; }
            public string Command { get; }
            public DateTime SentTime { get; }
            public TaskCompletionSource<string> Response { get; }
            public int RetryCount { get; set; }
            public CommandPriority Priority { get; }
            public bool IsCompleted { get; set; }

            public CommandState(int id, string command, CommandPriority priority)
            {
                Id = id;
                Command = command;
                SentTime = DateTime.UtcNow;
                Response = new TaskCompletionSource<string>();
                RetryCount = 0;
                Priority = priority;
                IsCompleted = false;
            }
        }

        public async Task<int> SendCommandAsync(string command, CommandPriority priority = CommandPriority.Normal)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            await _commandLock.WaitAsync();
            try
            {
                var id = GetNextCommandId();
                var state = new CommandState(id, command, priority);

                if (!_commands.TryAdd(id, state))
                {
                    throw new InvalidOperationException($"Failed to add command with ID {id}");
                }

                _logger.Debug("Added command {Id} with priority {Priority}", id, priority);
                return id;
            }
            finally
            {
                _commandLock.Release();
            }
        }

        public async Task<string> GetCommandResponseAsync(int commandId, TimeSpan timeout)
        {
            if (!_commands.TryGetValue(commandId, out var state))
            {
                throw new KeyNotFoundException($"Command with ID {commandId} not found");
            }

            try
            {
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                return await state.Response.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Command {Id} timed out after {Timeout}ms", commandId, timeout.TotalMilliseconds);
                throw new TimeoutException($"Command {commandId} timed out after {timeout.TotalMilliseconds}ms");
            }
        }

        public void CompleteCommand(int commandId, string response)
        {
            if (_commands.TryGetValue(commandId, out var state))
            {
                state.IsCompleted = true;
                state.Response.TrySetResult(response);
                _logger.Debug("Completed command {Id} with response", commandId);
            }
        }

        public void FailCommand(int commandId, Exception exception)
        {
            if (_commands.TryGetValue(commandId, out var state))
            {
                state.IsCompleted = true;
                state.Response.TrySetException(exception);
                _logger.Error(exception, "Failed command {Id}", commandId);
            }
        }

        public async Task RetryCommandAsync(int commandId)
        {
            if (!_commands.TryGetValue(commandId, out var state))
            {
                return;
            }

            if (state.RetryCount >= _maxRetries)
            {
                _logger.Warning("Command {Id} has exceeded maximum retry attempts", commandId);
                FailCommand(commandId, new InvalidOperationException("Maximum retry attempts exceeded"));
                return;
            }

            state.RetryCount++;
            _logger.Debug("Retrying command {Id} (attempt {Attempt}/{Max})",
                commandId, state.RetryCount, _maxRetries);

            await Task.Delay(RETRY_DELAY_MS * state.RetryCount);
        }

        public void RemoveCommand(int commandId)
        {
            if (_commands.TryRemove(commandId, out var state))
            {
                _logger.Debug("Removed command {Id}", commandId);
            }
        }

        private int GetNextCommandId()
        {
            return Interlocked.Increment(ref _nextCommandId);
        }

        public void ClearCommands()
        {
            foreach (var command in _commands.Values)
            {
                if (!command.IsCompleted)
                {
                    command.Response.TrySetCanceled();
                }
            }
            _commands.Clear();
            _nextCommandId = 0;
            _logger.Information("Cleared all commands");
        }

        public async Task<CommandResponse> ExecuteCommandAsync(string command, string[] arguments)
        {
            try
            {
                await _commandLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (!_commandHandlers.TryGetValue(command, out var handler))
                    {
                        return new CommandResponse
                        {
                            Success = false,
                            Message = $"Command '{command}' not found"
                        };
                    }

                    var request = new CommandRequest(command, arguments);
                    _commandQueue.Enqueue(request);

                    return await handler.ExecuteAsync(arguments).ConfigureAwait(false);
                }
                finally
                {
                    _commandLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing command {Command}", command);
                return new CommandResponse
                {
                    Success = false,
                    Message = $"Error executing command: {ex.Message}"
                };
            }
        }

        public void RegisterHandler(string command, CommandHandler handler)
        {
            _commandHandlers[command] = handler;
        }

        private async Task ProcessCommandsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (_commandQueue.Count > 0)
                        {
                            var request = _commandQueue.Dequeue();
                            _logger.Information("Processing command: {Command}", request.Command);
                        }
                    }
                    finally
                    {
                        _commandLock.Release();
                    }

                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error processing commands");
                }
            }
        }

        public async Task StopAsync()
        {
            try
            {
                await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                await _commandProcessorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _commandLock?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
        }
    }

    public class CommandRequest
    {
        public string Command { get; }
        public string[] Arguments { get; }

        public CommandRequest(string command, string[] arguments)
        {
            Command = command;
            Arguments = arguments;
        }
    }

    public class CommandResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public abstract class CommandHandler
    {
        public abstract Task<CommandResponse> ExecuteAsync(string[] arguments);
    }
}