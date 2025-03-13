using Serilog;
using System;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    public class CacheChangedEventArgs : EventArgs
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<CacheChangedEventArgs>();

        public string Key { get; }
        public object? Value { get; }

        public CacheChangedEventArgs(string key, object? value)
        {
            _logger.Verbose("Creating CacheChangedEventArgs - Key: {Key}, Value Type: {ValueType}",
                key,
                value?.GetType().Name ?? "null");

            Key = key;
            Value = value;

            LogCacheChange();
        }

        private void LogCacheChange()
        {
            _logger.Verbose("Cache Change Details - Key: {Key}, Value Type: {ValueType}, Has Value: {HasValue}",
                Key,
                Value?.GetType().Name ?? "null",
                Value != null);
        }
    }
}