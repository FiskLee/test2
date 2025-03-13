using Serilog;
using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace BattleNET
{
    public class PacketBuffer : IDisposable
    {
        private readonly MemoryPool<byte> _pool;
        private readonly ILogger _logger;
        private readonly int _maxSize;
        private bool _disposed;

        public PacketBuffer(ILogger logger, int maxSize = 4096)
        {
            _pool = MemoryPool<byte>.Shared;
            _logger = logger ?? Log.ForContext<PacketBuffer>();
            _maxSize = maxSize;
        }

        public async Task<byte[]> ReadPacketAsync(Socket socket)
        {
            if (socket == null || !socket.Connected)
            {
                throw new InvalidOperationException("Socket is not connected");
            }

            using var memory = _pool.Rent(_maxSize);
            var buffer = memory.Memory;

            try
            {
                // Read header (4 bytes)
                var headerBytes = await socket.ReceiveAsync(buffer.Slice(0, 4), SocketFlags.None);
                if (headerBytes != 4)
                {
                    _logger.Warning("Failed to read packet header. Received {Bytes} bytes", headerBytes);
                    throw new InvalidOperationException("Failed to read packet header");
                }

                // Parse packet size
                var packetSize = BitConverter.ToUInt32(buffer.Span.Slice(0, 4));
                if (packetSize > _maxSize)
                {
                    _logger.Warning("Packet too large: {Size} bytes (max: {MaxSize})", packetSize, _maxSize);
                    throw new InvalidOperationException($"Packet too large: {packetSize} bytes");
                }

                // Read payload
                var payloadBytes = await socket.ReceiveAsync(buffer.Slice(4, (int)packetSize - 4), SocketFlags.None);
                if (payloadBytes != packetSize - 4)
                {
                    _logger.Warning("Failed to read packet payload. Expected {Expected}, received {Actual}",
                        packetSize - 4, payloadBytes);
                    throw new InvalidOperationException("Failed to read packet payload");
                }

                var packet = buffer.Slice(0, (int)packetSize).ToArray();
                _logger.Debug("Successfully read packet of size {Size} bytes", packet.Length);
                return packet;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error reading packet");
                throw;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // Clean up managed resources
            }

            _disposed = true;
        }

        ~PacketBuffer()
        {
            Dispose(false);
        }
    }
}