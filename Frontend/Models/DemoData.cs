using System;
using System.Collections.Generic;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class DemoData
    {
        public DateTime Timestamp { get; set; }
        public string ServerName { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string ModList { get; set; }
        public NetworkStats NetworkStats { get; set; }
        public List<Player> Players { get; set; }
        public List<LogEntry> Logs { get; set; }

        public DemoData()
        {
            ServerName = string.Empty;
            ModList = string.Empty;
            NetworkStats = new NetworkStats();
            Players = new List<Player>();
            Logs = new List<LogEntry>();
        }
    }

    public class Player
    {
        public string Name { get; set; } = string.Empty;
        public int Ping { get; set; }
        public int Score { get; set; }
        public int Team { get; set; }
        public bool IsAdmin { get; set; }
    }
}