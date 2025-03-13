/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * BattleNET v1.3.4 - BattlEye Library and Client            *
 *                                                         *
 *  Copyright (C) 2018 by it's authors.                    *
 *  Some rights reserved. See license.txt, authors.txt.    *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using Serilog;
using System.Security.Cryptography;

namespace BattleNET
{
    /// <summary>
    /// Implements CRC32 checksum calculation.
    /// </summary>
    public class CRC32 : HashAlgorithm
    {
        private static readonly Serilog.ILogger _logger = Log.ForContext<CRC32>();
        private static readonly uint[] Table;
        private uint _crc;

        static CRC32()
        {
            _logger.Verbose("Initializing CRC32 lookup table");
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                var crc = i;
                for (var j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
                Table[i] = crc;
            }
            _logger.Verbose("CRC32 lookup table initialized with {Count} entries", Table.Length);
        }

        /// <summary>
        /// Initializes a new instance of the CRC32 class.
        /// </summary>
        public CRC32()
        {
            _logger.Verbose("Creating new CRC32 instance");
            HashSizeValue = 32;
            Initialize();
        }

        /// <summary>
        /// Initializes the CRC32 instance.
        /// </summary>
        public override void Initialize()
        {
            _logger.Verbose("Initializing CRC32 instance");
            _crc = 0xFFFFFFFF;
        }

        /// <summary>
        /// Routes data written to the object into the hash algorithm for computing the hash.
        /// </summary>
        /// <param name="array">The input to compute the hash code for.</param>
        /// <param name="ibStart">The offset into the byte array from which to begin using data.</param>
        /// <param name="cbSize">The number of bytes in the byte array to use as data.</param>
        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            _logger.Verbose("Computing hash for {Size} bytes starting at {Start}", cbSize, ibStart);
            for (var i = ibStart; i < ibStart + cbSize; i++)
            {
                _crc = (_crc >> 8) ^ Table[(_crc & 0xFF) ^ array[i]];
            }
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is processed by the cryptographic stream object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            _logger.Verbose("Finalizing hash computation");
            var hash = new byte[4];
            var finalCrc = ~_crc;
            hash[0] = (byte)((finalCrc >> 24) & 0xFF);
            hash[1] = (byte)((finalCrc >> 16) & 0xFF);
            hash[2] = (byte)((finalCrc >> 8) & 0xFF);
            hash[3] = (byte)(finalCrc & 0xFF);
            return hash;
        }

        /// <summary>
        /// Gets the size, in bits, of the computed hash code.
        /// </summary>
        public override int HashSize => 32;
    }
}
