using Serilog;
using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;

namespace BattleNET
{
    public class PacketProcessor
    {
        private readonly ILogger _logger;
        private readonly MemoryPool<byte> _pool;
        private readonly int _maxPacketSize;
        private readonly int _minPacketSize;

        public PacketProcessor(ILogger logger, int maxPacketSize = 4096, int minPacketSize = 9)
        {
            _logger = logger ?? Log.ForContext<PacketProcessor>();
            _pool = MemoryPool<byte>.Shared;
            _maxPacketSize = maxPacketSize;
            _minPacketSize = minPacketSize;
        }

        public class ProcessedPacket
        {
            public BattlEyePacketType Type { get; }
            public int SequenceNumber { get; }
            public byte[] Data { get; }
            public string Message { get; }
            public bool IsValid { get; }

            public ProcessedPacket(BattlEyePacketType type, int sequenceNumber, byte[] data, string message, bool isValid)
            {
                Type = type;
                SequenceNumber = sequenceNumber;
                Data = data;
                Message = message;
                IsValid = isValid;
            }
        }

        public Task<ProcessedPacket> ProcessPacketAsync(byte[] packet)
        {
            try
            {
                if (!ValidatePacketStructure(packet))
                {
                    return Task.FromResult(new ProcessedPacket(
                        BattlEyePacketType.Unknown,
                        0,
                        Array.Empty<byte>(),
                        "Invalid packet structure",
                        false));
                }

                var packetType = (BattlEyePacketType)packet[1];
                var sequenceNumber = BitConverter.ToInt32(packet, 2);
                var dataLength = BitConverter.ToInt16(packet, 6);
                var data = new byte[dataLength];
                Array.Copy(packet, 8, data, 0, dataLength);
                var message = Encoding.UTF8.GetString(data);

                _logger.Debug("Processed packet:\n" +
                    "1. Type: {Type}\n" +
                    "2. Sequence: {Sequence}\n" +
                    "3. Length: {Length}\n" +
                    "4. Message: {Message}",
                    packetType,
                    sequenceNumber,
                    dataLength,
                    message);

                return Task.FromResult(new ProcessedPacket(packetType, sequenceNumber, data, message, true));
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Specific error message");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing packet");
                return Task.FromResult(new ProcessedPacket(
                    BattlEyePacketType.Unknown,
                    0,
                    Array.Empty<byte>(),
                    $"Error processing packet: {ex.Message}",
                    false));
            }
        }

        private bool ValidatePacketStructure(byte[] packet)
        {
            if (packet == null || packet.Length < _minPacketSize)
            {
                _logger.Warning("Invalid packet: null or too small");
                return false;
            }

            if (packet[0] != 0xBE)
            {
                _logger.Warning("Invalid packet marker");
                return false;
            }

            if (!Enum.IsDefined(typeof(BattlEyePacketType), packet[1]))
            {
                _logger.Warning("Invalid packet type");
                return false;
            }

            var dataLength = BitConverter.ToInt16(packet, 6);
            if (dataLength < 0 || dataLength > _maxPacketSize - 8)
            {
                _logger.Warning("Invalid data length");
                return false;
            }

            if (packet.Length != 8 + dataLength)
            {
                _logger.Warning("Invalid total packet length");
                return false;
            }

            return true;
        }

        public byte[] CreatePacket(BattlEyePacketType type, int sequenceNumber, string message)
        {
            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                var packet = new byte[messageBytes.Length + 8];

                packet[0] = 0xBE;
                packet[1] = (byte)type;
                packet[2] = (byte)(sequenceNumber & 0xFF);
                packet[3] = (byte)((sequenceNumber >> 8) & 0xFF);
                packet[4] = (byte)((sequenceNumber >> 16) & 0xFF);
                packet[5] = (byte)((sequenceNumber >> 24) & 0xFF);
                packet[6] = (byte)(messageBytes.Length & 0xFF);
                packet[7] = (byte)((messageBytes.Length >> 8) & 0xFF);

                Array.Copy(messageBytes, 0, packet, 8, messageBytes.Length);

                _logger.Debug("Created packet:\n" +
                    "1. Type: {Type}\n" +
                    "2. Sequence: {Sequence}\n" +
                    "3. Length: {Length}\n" +
                    "4. Message: {Message}",
                    type,
                    sequenceNumber,
                    messageBytes.Length,
                    message);

                return packet;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating packet");
                throw;
            }
        }
    }
}