using System;

namespace GbaEmu.Core
{
    public class CPU
    {
        // 8-bit registers
        public byte A, B, C, D, E, F, H, L;

        // 16-bit registers
        public ushort PC;
        public ushort SP;

        // Flag bit masks
        private const byte FLAG_Z = 0x80; // Zero
        private const byte FLAG_N = 0x40; // Subtract
        private const byte FLAG_H = 0x20; // Half-Carry
        private const byte FLAG_C = 0x10; // Carry

        // Internal references
        private bool _interruptMasterEnable;

        private readonly MMU _mmu;
        private readonly Timer _timer; // ADD THIS

        public CPU(MMU mmu)
        {
            _mmu = mmu;
            _timer = new Timer(mmu); // initialize

            Reset();
        }

        /// <summary>
        /// Set CPU registers to typical GB startup values.
        /// </summary>
        public void Reset()
        {
            // From real GB init values
            A = 0x01;
            F = 0xB0; // indicates Z=1, N=0, H=1, C=1 at reset
            B = 0x00;
            C = 0x13;
            D = 0x00;
            E = 0xD8;
            H = 0x01;
            L = 0x4D;
            SP = 0xFFFE;
            PC = 0x0100;

            // Interrupt Master Enable on startup (some sources say off; 
            // but typically the BIOS sets it properly).
            _interruptMasterEnable = true;
        }

        /// <summary>
        /// Executes one opcode, handles interrupts, returns cycles used.
        /// </summary>
        public int Step()
        {
            // Handle interrupts
            if (_interruptMasterEnable)
            {
                HandleInterrupts();
            }

            // Fetch + execute opcode
            byte opcode = _mmu.ReadByte(PC);
            PC++;
            int cyclesUsed = ExecuteOpcode(opcode);

            // Update Timer with cycles used
            _timer.Update(cyclesUsed);

            return cyclesUsed;
        }

        /// <summary>
        /// Your main opcode dispatcher. 
        /// This is where you’ll implement each LR35902 instruction you need.
        /// </summary>
        private int ExecuteOpcode(byte opcode)
        {
            int cyclesUsed = 0;

            switch (opcode)
            {
                case 0x00: // NOP
                    cyclesUsed = 4;
                    break;

                case 0xF3: // DI - Disable interrupts
                    _interruptMasterEnable = false;
                    cyclesUsed = 4;
                    break;

                case 0xFB: // EI - Enable interrupts (sets IME, but technically one-instruction delay)
                    // Real GB sets IME *after* the next instruction. 
                    // Minimal approach: IME = true immediately, or schedule for next step.
                    _interruptMasterEnable = true;
                    cyclesUsed = 4;
                    break;

                case 0xC3: // JP a16
                    {
                        ushort addr = ReadU16();
                        PC = addr;
                        cyclesUsed = 16;
                        break;
                    }

                case 0xC9: // RET
                    {
                        byte low = _mmu.ReadByte(SP);
                        SP++;
                        byte high = _mmu.ReadByte(SP);
                        SP++;
                        PC = (ushort)((high << 8) | low);
                        cyclesUsed = 16;
                        break;
                    }

                case 0xD9: // RETI (Return and enable interrupts)
                    {
                        // Same as RET
                        byte low = _mmu.ReadByte(SP);
                        SP++;
                        byte high = _mmu.ReadByte(SP);
                        SP++;
                        PC = (ushort)((high << 8) | low);
                        // Also enable IME
                        _interruptMasterEnable = true;
                        cyclesUsed = 16;
                        break;
                    }

                // Add more opcodes here
                // e.g., 0x3E => LD A, d8
                //       0x06 => LD B, d8
                //       ...
                // This is where the bulk of the CPU logic will go.

                default:
                    Console.WriteLine($"Unhandled opcode 0x{opcode:X2} at PC=0x{PC - 1:X4}");
                    cyclesUsed = 4;
                    break;
            }

            return cyclesUsed;
        }

        /// <summary>
        /// Check IF & IE, handle highest‐priority interrupt if any. 
        /// Push PC, jump to the appropriate vector, clear IF bit.
        /// </summary>
        private void HandleInterrupts()
        {
            // If IME is off, skip. (Though we do check it above in Step().)
            if (!_interruptMasterEnable) 
                return;

            byte IF = _mmu.ReadByte(0xFF0F);  // Interrupt Flags
            byte IE = _mmu.ReadByte(0xFFFF); // Interrupt Enable

            // triggered is the set of bits that are both requested and enabled
            byte triggered = (byte)(IF & IE);
            if (triggered == 0)
                return; // No active interrupts

            // Interrupt priority order: VBlank(0), LCDC(1), Timer(2), Serial(3), Joypad(4)
            for (int i = 0; i < 5; i++)
            {
                byte mask = (byte)(1 << i);
                if ((triggered & mask) != 0)
                {
                    // Clear IF bit for this interrupt
                    IF &= (byte)~mask;
                    _mmu.WriteByte(0xFF0F, IF);

                    // IME goes off as soon as we accept an interrupt
                    _interruptMasterEnable = false;

                    // Push PC onto stack, low byte first
                    SP--;
                    _mmu.WriteByte(SP, (byte)(PC & 0xFF));
                    SP--;
                    _mmu.WriteByte(SP, (byte)((PC >> 8) & 0xFF));

                    // Jump to interrupt vector
                    switch (i)
                    {
                        case 0: PC = 0x40;  break; // VBlank
                        case 1: PC = 0x48;  break; // LCD STAT
                        case 2: PC = 0x50;  break; // Timer
                        case 3: PC = 0x58;  break; // Serial
                        case 4: PC = 0x60;  break; // Joypad
                    }

                    // We only service one interrupt per Step(), so break out
                    return;
                }
            }
        }

        #region Helper Methods

        /// <summary>
        /// Reads a 16-bit little-endian value from memory at PC, increments PC by 2.
        /// </summary>
        private ushort ReadU16()
        {
            byte low = _mmu.ReadByte(PC); 
            PC++;
            byte high = _mmu.ReadByte(PC);
            PC++;
            return (ushort)((high << 8) | low);
        }

        #endregion
    }
}
