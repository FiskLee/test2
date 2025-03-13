using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    public class ServerStatus
    {
        public bool IsRunning { get; set; }
        public string ServerName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public string CurrentMap { get; set; } = string.Empty;
        public string GameMode { get; set; } = string.Empty;
        private readonly Collection<string> _modList = new Collection<string>();
        public IReadOnlyCollection<string> ModList => _modList;
        public PerformanceMetrics Performance { get; set; } = new();
        public NetworkStats NetworkStats { get; set; } = new();
        public DateTime LastUpdate { get; set; }
        public DateTime StartTime { get; set; }
        public List<PlayerInfo> Players { get; set; } = new List<PlayerInfo>();
        public OSDataDTO SystemMetrics { get; set; } = new OSDataDTO();
        public ServerConfig Config { get; set; } = new ServerConfig();
        public bool IsPublic { get; set; }
        public bool BattlEyeEnabled { get; set; }
        public bool VoiceEnabled { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class PerformanceMetrics
    {
        public float FPS { get; set; }
        public float FrameTime { get; set; }
        public double CPUUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double NetworkLatency { get; set; }
        public double PacketLoss { get; set; }
        public float TickRate { get; set; }
        public int AICount { get; set; }
        public int VehicleCount { get; set; }
        public int TotalEntities { get; set; }
    }

    public class ServerConfig
    {
        public bool IsPublic { get; set; }
        public bool BattlEyeEnabled { get; set; }
        public bool VoiceEnabled { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<ModInfo> Mods { get; set; } = new List<ModInfo>();
    }

    // ModInfo class removed to resolve duplicate definition
}