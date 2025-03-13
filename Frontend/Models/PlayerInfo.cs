using System;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string SteamId { get; set; } = string.Empty;
        public int Ping { get; set; }
        public int Score { get; set; }
        public string Team { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public DateTime JoinTime { get; set; }
        public string Loadout { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}