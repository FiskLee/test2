using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ArmaReforgerServerMonitor.Frontend.Models
{
    /// <summary>
    /// Data transfer object representing operating system metrics and performance data.
    /// Contains information about CPU, memory, disk, and network usage.
    /// </summary>
    /// <remarks>
    /// This class is used to transfer system performance data from the backend
    /// to the frontend. It provides a snapshot of the system's resource usage
    /// at a specific point in time.
    /// 
    /// The data is typically collected at regular intervals (configured by PollIntervalSeconds)
    /// and used to display system health and performance metrics in the UI.
    /// </remarks>
    public class OSDataDTO
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<OSDataDTO>();

        /// <summary>
        /// CPU usage percentage across all cores (0-100).
        /// </summary>
        /// <remarks>
        /// Represents the average CPU utilization across all available cores.
        /// Updated every polling interval.
        /// </remarks>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Overall CPU usage percentage across all cores (0-100).
        /// </summary>
        /// <remarks>
        /// Represents the average CPU utilization across all available cores.
        /// Updated every polling interval.
        /// </remarks>
        public double OverallCpuUsage { get; set; }

        private readonly List<double> _perCoreCpuUsage = new List<double>();
        public IReadOnlyList<double> PerCoreCpuUsage => _perCoreCpuUsage;

        /// <summary>
        /// Total physical memory in bytes.
        /// </summary>
        /// <remarks>
        /// The total amount of RAM installed in the system.
        /// This value typically remains constant.
        /// </remarks>
        public ulong TotalMemory { get; set; }

        /// <summary>
        /// Currently used physical memory in bytes.
        /// </summary>
        /// <remarks>
        /// The amount of RAM currently in use by all processes.
        /// Updated every polling interval.
        /// </remarks>
        public ulong UsedMemory { get; set; }

        /// <summary>
        /// Available physical memory in bytes.
        /// </summary>
        /// <remarks>
        /// The amount of RAM currently available for allocation.
        /// Calculated as TotalMemory - UsedMemory.
        /// </remarks>
        public ulong AvailableMemory { get; set; }

        /// <summary>
        /// Memory usage percentage (0-100).
        /// </summary>
        /// <remarks>
        /// Calculated as (UsedMemory / TotalMemory) * 100.
        /// Provides a quick overview of memory utilization.
        /// </remarks>
        public double MemoryUsage { get; set; }

        /// <summary>
        /// Memory usage in GB.
        /// </summary>
        /// <remarks>
        /// Calculated as UsedMemory / (1024.0 * 1024.0 * 1024.0).
        /// </remarks>
        public double MemoryUsedGB => UsedMemory / (1024.0 * 1024.0 * 1024.0);

        /// <summary>
        /// Total memory in GB.
        /// </summary>
        /// <remarks>
        /// Calculated as TotalMemory / (1024.0 * 1024.0 * 1024.0).
        /// </remarks>
        public double TotalMemoryGB => TotalMemory / (1024.0 * 1024.0 * 1024.0);

        /// <summary>
        /// Memory usage percentage (0-100).
        /// </summary>
        /// <remarks>
        /// Calculated as (UsedMemory / TotalMemory) * 100.
        /// Provides a quick overview of memory utilization.
        /// </remarks>
        public double MemoryUsagePercentage => (double)UsedMemory / TotalMemory * 100;

        public ICollection<DiskInfo> Disks { get; set; } = new List<DiskInfo>();

        /// <summary>
        /// Network interface statistics.
        /// </summary>
        /// <remarks>
        /// Contains information about network throughput,
        /// bytes sent/received, and connection status.
        /// </remarks>
        public NetworkStats NetworkStats { get; set; } = new();

        /// <summary>
        /// Disk read speed in MBps.
        /// </summary>
        /// <remarks>
        /// Calculated over the last polling interval.
        /// </remarks>
        public double DiskReadMBps { get; set; }

        /// <summary>
        /// Disk write speed in MBps.
        /// </summary>
        /// <remarks>
        /// Calculated over the last polling interval.
        /// </remarks>
        public double DiskWriteMBps { get; set; }

        /// <summary>
        /// Disk usage percentage (0-100).
        /// </summary>
        /// <remarks>
        /// Calculated as (UsedSpace / TotalSpace) * 100.
        /// </remarks>
        public double DiskUsagePercentage { get; set; }

        /// <summary>
        /// Network input speed in MBps.
        /// </summary>
        /// <remarks>
        /// Calculated over the last polling interval.
        /// </remarks>
        public double NetworkInMBps { get; set; }

        /// <summary>
        /// Network output speed in MBps.
        /// </summary>
        /// <remarks>
        /// Calculated over the last polling interval.
        /// </remarks>
        public double NetworkOutMBps { get; set; }

        /// <summary>
        /// Frames per second (FPS).
        /// </summary>
        /// <remarks>
        /// Represents the number of frames rendered per second.
        /// Updated every polling interval.
        /// </remarks>
        public float FPS { get; set; }

        /// <summary>
        /// Frame time in milliseconds.
        /// </summary>
        /// <remarks>
        /// Represents the time taken to render a single frame.
        /// Updated every polling interval.
        /// </remarks>
        public float FrameTime { get; set; }

        /// <summary>
        /// Number of active players.
        /// </summary>
        /// <remarks>
        /// Represents the number of players currently connected to the server.
        /// Updated every polling interval.
        /// </remarks>
        public int ActivePlayers { get; set; }

        /// <summary>
        /// System uptime in seconds.
        /// </summary>
        /// <remarks>
        /// The time elapsed since the last system boot.
        /// Updated every polling interval.
        /// </remarks>
        public double Uptime { get; set; }

        /// <summary>
        /// Timestamp when this data was collected.
        /// </summary>
        /// <remarks>
        /// Used to track when the metrics were gathered
        /// and calculate data freshness.
        /// </remarks>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Disk usage information.
        /// </summary>
        public double DiskUsage { get; set; }

        /// <summary>
        /// Network usage information.
        /// </summary>
        public double NetworkUsage { get; set; }

        public OSDataDTO()
        {
            _logger.Verbose("Initializing OSDataDTO with default values");
            LogSystemMetrics();
        }

        private void LogSystemMetrics()
        {
            _logger.Verbose("CPU Metrics - Usage: {Usage}%, Overall: {Overall}%, Cores: {CoreCount}",
                CpuUsage,
                OverallCpuUsage,
                PerCoreCpuUsage.Count);

            for (int i = 0; i < PerCoreCpuUsage.Count; i++)
            {
                _logger.Verbose("Core {Index} Usage: {Usage}%", i, PerCoreCpuUsage[i]);
            }

            _logger.Verbose("Memory Metrics - Total: {Total}GB, Used: {Used}GB, Available: {Available}GB, Usage: {Usage}%",
                TotalMemoryGB,
                MemoryUsedGB,
                AvailableMemory / (1024.0 * 1024.0 * 1024.0),
                MemoryUsagePercentage);

            _logger.Verbose("Disk Metrics - Read: {Read}MB/s, Write: {Write}MB/s, Usage: {Usage}%, Drive Count: {DriveCount}",
                DiskReadMBps,
                DiskWriteMBps,
                DiskUsagePercentage,
                Disks.Count);

            foreach (var disk in Disks)
            {
                _logger.Verbose("Disk {Name} - Total: {Total}GB, Used: {Used}GB, Free: {Free}GB, Usage: {Usage}%",
                    disk.Name,
                    disk.TotalSpace / (1024.0 * 1024.0 * 1024.0),
                    disk.UsedSpace / (1024.0 * 1024.0 * 1024.0),
                    disk.FreeSpace / (1024.0 * 1024.0 * 1024.0),
                    disk.UsagePercentage);
            }

            _logger.Verbose("Network Metrics - In: {In}MB/s, Out: {Out}MB/s, Active: {Active}, Connections: {Connections}",
                NetworkInMBps,
                NetworkOutMBps,
                NetworkStats.IsActive,
                NetworkStats.ConnectionCount);

            _logger.Verbose("Performance Metrics - FPS: {FPS}, Frame Time: {FrameTime}ms, Active Players: {Players}, Uptime: {Uptime}s",
                FPS,
                FrameTime,
                ActivePlayers,
                Uptime);

            _logger.Verbose("Timestamp: {Timestamp}", Timestamp);
        }
    }

    /// <summary>
    /// Information about a disk drive.
    /// </summary>
    public class DiskInfo
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<DiskInfo>();

        /// <summary>
        /// Name or drive letter of the disk.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Total space in bytes.
        /// </summary>
        public ulong TotalSpace { get; set; }

        /// <summary>
        /// Used space in bytes.
        /// </summary>
        public ulong UsedSpace { get; set; }

        /// <summary>
        /// Free space in bytes.
        /// </summary>
        public ulong FreeSpace { get; set; }

        /// <summary>
        /// Usage percentage (0-100).
        /// </summary>
        public double UsagePercentage { get; set; }

        public DiskInfo()
        {
            _logger.Verbose("Initializing DiskInfo with default values");
            LogDiskInfo();
        }

        private void LogDiskInfo()
        {
            _logger.Verbose("Disk {Name} - Total: {Total}GB, Used: {Used}GB, Free: {Free}GB, Usage: {Usage}%",
                Name,
                TotalSpace / (1024.0 * 1024.0 * 1024.0),
                UsedSpace / (1024.0 * 1024.0 * 1024.0),
                FreeSpace / (1024.0 * 1024.0 * 1024.0),
                UsagePercentage);
        }
    }
}
