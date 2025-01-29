using System;
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core
{
    public class MMU
    {
        private const int MemorySize = 0x10000; // 64KB for original Game Boy
        private readonly byte[] _memory = new byte[MemorySize];
        private readonly Cartridge _cartridge;

        public Cartridge Cartridge => _cartridge;

        public MMU(Cartridge cart)
        {
            _cartridge = cart ?? throw new ArgumentNullException(nameof(cart));
            Debug.Log("MMU created. 64KB internal memory allocated.");
        }

        public byte ReadByte(ushort addr)
        {
            if (addr < 0x8000) // Cartridge ROM
            {
                return _cartridge.ReadByte(addr);
            }

            if (addr >= 0xA000 && addr < 0xC000) // External RAM
            {
                return _cartridge.ReadByte(addr);
            }

            if (addr == 0xFF00) // Joypad
            {
                return HandleJoypadRead();
            }

            if (addr == 0xFF0F) // Interrupt flag register
            {
                return _memory[addr];
            }

            if (addr >= 0xFF10 && addr <= 0xFF3F) // Audio registers (stubbed)
            {
                return 0x00;
            }

            if (addr == 0xFF46) // DMA register
            {
                return _memory[addr];
            }

            if (addr < MemorySize) // General memory
            {
                return _memory[addr];
            }

            Debug.LogWarning($"MMU: Read from unmapped address 0x{addr:X4}. Returning 0xFF.");
            return 0xFF; // Default value for unmapped memory
        }

        public void WriteByte(ushort addr, byte value)
        {
            if (addr < 0x8000) // Cartridge ROM
            {
                _cartridge.WriteByte(addr, value);
                return;
            }

            if (addr >= 0xA000 && addr < 0xC000) // External RAM
            {
                _cartridge.WriteByte(addr, value);
                return;
            }

            if (addr == 0xFF00) // Joypad
            {
                HandleJoypadWrite(value);
                return;
            }

            if (addr == 0xFF01) // Serial data register
            {
                _memory[addr] = value; // Store the data
                Debug.Log($"MMU: Serial data written: 0x{value:X2} ('{(char)value}')");
                return;
            }

            if (addr == 0xFF02) // Serial control register
            {
                _memory[addr] = value;
                if (value == 0x81) // Transfer complete
                {
                    byte data = _memory[0xFF01]; // Get the data from 0xFF01
                    Console.Write((char)data);  // Print ASCII character
                    Debug.Log($"MMU: Serial transfer complete. Output: '{(char)data}'");
                }
                return;
            }

            if (addr == 0xFF46) // DMA register
            {
                OAMDMA(value);
                return;
            }

            if (addr == 0xFF50) // Boot ROM disable
            {
                _cartridge.WriteByte(addr, value);
                return;
            }

            if (addr < MemorySize) // General memory
            {
                _memory[addr] = value;
                return;
            }

            Debug.LogWarning($"MMU: Write to unmapped address 0x{addr:X4} with value 0x{value:X2} ignored.");
        }

        private void OAMDMA(byte startAddrHigh)
        {
            ushort startAddr = (ushort)(startAddrHigh << 8);
            for (int i = 0; i < 0xA0; i++) // OAM is 160 bytes
            {
                byte data = ReadByte((ushort)(startAddr + i));
                _memory[0xFE00 + i] = data;
            }
            Debug.Log($"MMU: OAM DMA from 0x{startAddr:X4} to 0xFE00 completed.");
        }

        private byte HandleJoypadRead()
        {
            // Stubbed: No buttons pressed
            return 0xCF;
        }

        private void HandleJoypadWrite(byte value)
        {
            // Stubbed: Record row select
            _memory[0xFF00] = (byte)(value & 0x30);
        }
    }
}
