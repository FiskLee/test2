using System.Threading.Tasks;

namespace ArmaReforgerServerMonitor.Frontend.Services
{
    /// <summary>
    /// Interface for interacting with Pastebin API to share logs and error reports
    /// </summary>
    public interface IPastebinService
    {
        /// <summary>
        /// Creates a new paste on Pastebin with the specified content
        /// </summary>
        /// <param name="content">The content to paste</param>
        /// <param name="title">Optional title for the paste</param>
        /// <param name="format">The syntax highlighting format (e.g., "csharp")</param>
        /// <returns>The URL of the created paste</returns>
        Task<string> CreatePasteAsync(string content, string title = "", string format = "text");
    }
}