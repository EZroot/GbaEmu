using System;
using GbaEmu.Core.Opcodes;
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core
{
    public class CPU
    {
        // 8-bit registers
        public byte A, B, C, D, E, F, H, L;

        // 16-bit registers
        public ushort PC;
        public ushort SP;

        // Flag bit masks (made public so OpcodeHelper can use them)
        public const byte FLAG_Z = 0x80; // Zero
        public const byte FLAG_N = 0x40; // Subtract
        public const byte FLAG_H = 0x20; // Half-Carry
        public const byte FLAG_C = 0x10; // Carry

        // Internal references
        private bool _interruptMasterEnable;
        internal MMU _mmu;      // Expose to OpcodeHelper
        private readonly Timer _timer;

        public CPU(MMU mmu)
        {
            _mmu = mmu;
            _timer = new Timer(mmu);
            Reset();
        }

        public void Reset()
        {
            A = 0x01;
            F = 0xB0;
            B = 0x00;
            C = 0x13;
            D = 0x00;
            E = 0xD8;
            H = 0x01;
            L = 0x4D;
            SP = 0xFFFE;
            PC = 0x0100;
            _interruptMasterEnable = true;
        }

        public int Step()
        {
            if (_interruptMasterEnable)
                HandleInterrupts();

            byte opcode = _mmu.ReadByte(PC);
            PC++;
            
            int cyclesUsed = OpcodeHelper.Execute(this, opcode); // Call the helper

            // Update Timer
            _timer.Update(cyclesUsed);

            return cyclesUsed;
        }

        /// <summary>
        /// Sets or clears a flag in register F.
        /// </summary>
        public void SetFlag(byte flagMask, bool condition)
        {
            if (condition)
                F |= flagMask;
            else
                F &= (byte)~flagMask;
        }

        /// <summary>
        /// Checks if a flag in register F is set.
        /// </summary>
        public bool GetFlag(byte flagMask)
        {
            return (F & flagMask) != 0;
        }

        /// <summary>
        /// Reads a 16-bit little-endian value from memory at PC, increments PC by 2.
        /// </summary>
        public ushort ReadU16()
        {
            byte low = _mmu.ReadByte(PC); 
            PC++;
            byte high = _mmu.ReadByte(PC);
            PC++;
            return (ushort)((high << 8) | low);
        }

        private void HandleInterrupts()
        {
            if (!_interruptMasterEnable) return;

            byte IF = _mmu.ReadByte(0xFF0F);
            byte IE = _mmu.ReadByte(0xFFFF);
            byte triggered = (byte)(IF & IE);
            if (triggered == 0) return;

            for (int i = 0; i < 5; i++)
            {
                byte mask = (byte)(1 << i);
                if ((triggered & mask) != 0)
                {
                    // Clear IF bit
                    IF &= (byte)~mask;
                    _mmu.WriteByte(0xFF0F, IF);
                    _interruptMasterEnable = false;

                    // Push PC
                    SP--;
                    _mmu.WriteByte(SP, (byte)(PC >> 8));
                    SP--;
                    _mmu.WriteByte(SP, (byte)(PC & 0xFF));

                    switch (i)
                    {
                        case 0: PC = 0x40;
                            Debug.Log("Hit vblank"); break; // VBlank
                        case 1: PC = 0x48; Debug.Log("Hit lcd stat");break; // LCD STAT
                        case 2: PC = 0x50; Debug.Log("Hit timer");break; // Timer
                        case 3: PC = 0x58; Debug.Log("Hit serial");break; // Serial
                        case 4: PC = 0x60; Debug.Log("Hit joypad");break; // Joypad
                    }

                    return;
                }
            }
        }
    }
}
