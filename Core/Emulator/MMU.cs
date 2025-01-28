// MMU.cs
using System;

namespace GbaEmu.Core
{
    public class MMU
    {
        private const int MemorySize = 0x10000; // 64KB for original Game Boy
        private readonly byte[] _memory = new byte[MemorySize];
        private readonly Cartridge _cartridge;

        public MMU(Cartridge cart)
        {
            _cartridge = cart;
            LoadCartridge();
        }

        private void LoadCartridge()
        {
            // Simple load (ignores MBC)
            for (int i = 0; i < _cartridge.ROM.Length && i < 0x8000; i++)
            {
                _memory[i] = _cartridge.ROM[i];
            }
        }

        public byte ReadByte(ushort addr)
        {
            return _memory[addr];
        }

        public void WriteByte(ushort addr, byte value)
        {
            // Typically, writing to 0x0000-0x7FFF is MBC-specific; we ignore
            if (addr < 0x8000)
                return;

            _memory[addr] = value;
        }
    }
}