using System;

namespace GbaEmu.Core
{
    public class MMU
    {
        private const int MemorySize = 0x10000; // 64KB for original GB
        private readonly byte[] _memory = new byte[MemorySize];
        private readonly Cartridge _cartridge;

        public MMU(Cartridge cart)
        {
            _cartridge = cart;
            // Load first 0x8000 bytes (ROM bank 0) into _memory
            for (int i = 0; i < Math.Min(0x8000, cart.ROM.Length); i++)
                _memory[i] = cart.ROM[i];
        }

        public byte ReadByte(ushort addr)
        {
            if (addr < 0x4000)
            {
                // Bank 0
                return _memory[addr];
            }
            else if (addr < 0x8000)
            {
                // Banked region
                return _cartridge.ReadROMBanked(addr);
            }
            else if (addr >= 0xA000 && addr < 0xC000)
            {
                // External RAM
                return _cartridge.ReadRAMBanked(addr);
            }
            else if (addr == 0xFF00)
            {
                // Joypad stub
                return HandleJoypadRead();
            }
            else if (addr == 0xFF0F)
            {
                // IF register
                return _memory[addr];
            }
            else if (addr >= 0xFF10 && addr <= 0xFF3F)
            {
                // Audio registers stub
                return 0x00; 
            }
            else if (addr == 0xFF46)
            {
                // DMA stub read
                return _memory[addr];
            }
            // … Add more stubs as needed for I/O

            return _memory[addr];
        }

        public void WriteByte(ushort addr, byte value)
        {
            if (addr < 0x8000)
            {
                // MBC1 bank switching
                _cartridge.HandleBanking(addr, value);
                return;
            }

            if (addr >= 0xA000 && addr < 0xC000)
            {
                // External RAM
                _cartridge.WriteRAMBanked(addr, value);
                return;
            }

            if (addr == 0xFF00)
            {
                // Joypad stub
                HandleJoypadWrite(value);
                return;
            }
            else if (addr == 0xFF46)
            {
                // DMA stub
                OAMDMA(value);
                return;
            }
            // … More stubs for LCD, audio, etc.

            _memory[addr] = value;
        }

        // Example OAM DMA stub (copies from XX00-XX9F to FE00-FE9F)
        private void OAMDMA(byte startAddrHigh)
        {
            ushort startAddr = (ushort)(startAddrHigh << 8);
            for (int i = 0; i < 0xA0; i++)
            {
                byte data = ReadByte((ushort)(startAddr + i));
                WriteByte((ushort)(0xFE00 + i), data);
            }
        }

        private byte HandleJoypadRead()
        {
            // Very rough skeleton: real logic checks row/column bits
            return 0xCF; // Some default “no button pressed”
        }

        private void HandleJoypadWrite(byte value)
        {
            // Stub: do nothing or store bits
            _memory[0xFF00] = (byte)(value & 0x30);
        }
    }
}
