using System;
using System.IO;

namespace GbaEmu.Core
{
    public class Cartridge
    {
        public byte[] ROM { get; }
        public byte[] RAM { get; private set; }

        // MBC1 example
        private bool _ramEnabled;
        private int _romBank = 1;
        private int _ramBank;
        private int _bankingMode; // 0 = ROM banking, 1 = RAM banking

        public Cartridge(string romPath)
        {
            if (string.IsNullOrEmpty(romPath))
                throw new ArgumentException("ROM path is empty.");
            if (!File.Exists(romPath))
                throw new FileNotFoundException("ROM file not found", romPath);

            ROM = File.ReadAllBytes(romPath);
            RAM = new byte[0x8000]; // Up to 32KB (for MBC1 example)
        }

        public byte ReadROMBanked(int addr)
        {
            // For addresses 0x4000-0x7FFF (banked area):
            // bankSize = 0x4000
            int bankedAddr = (addr - 0x4000) + (_romBank * 0x4000);
            bankedAddr %= ROM.Length;
            return ROM[bankedAddr];
        }

        public byte ReadRAMBanked(int addr)
        {
            if (!_ramEnabled) return 0xFF;
            // Each bank is 0x2000 in MBC1
            int ramAddr = (addr - 0xA000) + (_ramBank * 0x2000);
            ramAddr %= RAM.Length;
            return RAM[ramAddr];
        }

        public void WriteRAMBanked(int addr, byte value)
        {
            if (!_ramEnabled) return;
            int ramAddr = (addr - 0xA000) + (_ramBank * 0x2000);
            ramAddr %= RAM.Length;
            RAM[ramAddr] = value;
        }

        public void HandleBanking(ushort addr, byte value)
        {
            // MBC1 range checks
            if (addr < 0x2000)
            {
                // RAM Enable
                _ramEnabled = ((value & 0x0F) == 0x0A);
            }
            else if (addr < 0x4000)
            {
                // Set lower 5 bits of ROM bank
                _romBank = (value & 0x1F);
                if (_romBank == 0) _romBank = 1;
            }
            else if (addr < 0x6000)
            {
                // Either upper bits of ROM bank or RAM bank
                if (_bankingMode == 0)
                {
                    // ROM banking
                    _romBank = (_romBank & 0x1F) | ((value & 0x03) << 5);
                    if ((_romBank & 0x1F) == 0) _romBank |= 1;
                }
                else
                {
                    // RAM banking
                    _ramBank = value & 0x03;
                }
            }
            else if (addr < 0x8000)
            {
                // Change mode
                _bankingMode = value & 0x01;
            }
        }
    }
}
