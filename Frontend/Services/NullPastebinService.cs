using Serilog;
using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    internal class NullPastebinService : IPastebinService
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<NullPastebinService>();

        public NullPastebinService()
        {
            _logger.Verbose("Initializing NullPastebinService");
        }

        public Task<string> CreatePasteAsync(string content, string title = "", string format = "text")
        {
            _logger.Verbose("CreatePasteAsync called - Title: {Title}, Format: {Format}, Content Length: {Length}",
                title,
                format,
                content.Length);

            _logger.Verbose("NullPastebinService always returns null");
            return Task.FromResult<string>(null!);
        }

        public Task<string> GetPasteAsync(string pasteId)
        {
            _logger.Verbose("GetPasteAsync called with paste ID: {PasteId}", pasteId);

            _logger.Verbose("NullPastebinService always returns null");
            return Task.FromResult<string>(null!);
        }

        public Task<bool> DeletePasteAsync(string pasteId)
        {
            _logger.Verbose("DeletePasteAsync called with paste ID: {PasteId}", pasteId);

            _logger.Verbose("NullPastebinService always returns false");
            return Task.FromResult(false);
        }
    }
}