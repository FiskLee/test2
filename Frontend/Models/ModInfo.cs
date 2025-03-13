using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class ModInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WorkshopId { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime LastUpdated { get; set; }
        public long Size { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}