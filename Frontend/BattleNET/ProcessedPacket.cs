using System;

namespace BattleNET
{
    public class ProcessedPacket
    {
        public byte Type { get; set; }
        public int SequenceNumber { get; set; }
        public byte[] Data { get; set; }
        public string Message { get; set; }
        public bool IsValid { get; set; }

        public ProcessedPacket(byte type, int sequenceNumber, byte[] data, string message, bool isValid)
        {
            Type = type;
            SequenceNumber = sequenceNumber;
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            IsValid = isValid;
        }
    }
}