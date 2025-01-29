using System;
using GbaEmu.Core.Opcodes;
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core
{
    public class CPU
    {
        // Registers
        public byte A, B, C, D, E, F, H, L;
        public ushort PC;
        public ushort SP;
        public ushort BC
        {
            get => (ushort)((B << 8) | C);
            set { B = (byte)(value >> 8); C = (byte)(value & 0xFF); }
        }

        public ushort DE
        {
            get => (ushort)((D << 8) | E);
            set { D = (byte)(value >> 8); E = (byte)(value & 0xFF); }
        }

        public ushort HL
        {
            get => (ushort)((H << 8) | L);
            set { H = (byte)(value >> 8); L = (byte)(value & 0xFF); }
        }

        
        // Flag bit masks
        public const byte FLAG_Z = 0x80;
        public const byte FLAG_N = 0x40;
        public const byte FLAG_H = 0x20;
        public const byte FLAG_C = 0x10;

        public bool _interruptMasterEnable;

        // References
        internal MMU _mmu;
        private readonly Timer _timer;

        public class OpcodeLogEntry
        {
            public string PC;
            public string Opcode;
            public OpcodeLogEntry(string pc, string opcode)
            {
                this.PC = pc;
                this.Opcode = opcode;
            }
        }
        
        public List<OpcodeLogEntry> OpcodeLogEntries = new List<OpcodeLogEntry>();
        
        public CPU(MMU mmu)
        {
            _mmu = mmu;
            _timer = new Timer(mmu);
            Debug.Log("CPU created. Calling Reset()...");
            Reset();
        }

        public void Reset()
        {
            // Check if cartridge has a boot ROM
            var cart = _mmu?.Cartridge;
            if (cart != null && cart.HasBootRom)
            {
                // Start at 0x0000 for boot
                A = 0x00; F = 0x00;
                B = 0x00; C = 0x00;
                D = 0x00; E = 0x00;
                H = 0x00; L = 0x00;
                SP = 0x0000;
                PC = 0x0000;

                Debug.Log("CPU Reset with Boot ROM present. PC=0x0000, all registers zeroed.");
            }
            else
            {
                // Post-boot defaults
                A = 0x01; F = 0xB0;
                B = 0x00; C = 0x13;
                D = 0x00; E = 0xD8;
                H = 0x01; L = 0x4D;
                SP = 0xFFFE;
                PC = 0x0100;

                Debug.Log("CPU Reset without Boot ROM. PC=0x0100, default registers set.");
            }

            _interruptMasterEnable = true;
        }

        public int Step(bool logOpcode = false)
        {
            if (_interruptMasterEnable)
                HandleInterrupts();

            byte opcode = _mmu.ReadByte(PC);
            //Debug.Log($"CPU: Read opcode 0x{opcode:X2} from PC=0x{PC:X4}");
            PC++;

            if (logOpcode)
            {
                Debug.Log($"0x{PC - 1:X4} - 0x{opcode:X2}");
                OpcodeLogEntries.Add(new OpcodeLogEntry($"0x{PC - 1:X4}", $"0x{opcode:X4}"));
            }

            int cyclesUsed = OpcodeHelper.Execute(this, opcode);
            //Debug.Log($"CPU: Executed Opcode 0x{opcode:X2}, Cycles used: {cyclesUsed}");

            _timer.Update(cyclesUsed);

            return cyclesUsed;
        }

        // Flag helper
        public void SetFlag(byte flagMask, bool condition)
        {
            if (condition)
                F |= flagMask;
            else
                F &= (byte)~flagMask;
        }

        public bool GetFlag(byte flagMask) => (F & flagMask) != 0;
        public void ResetFlags()
        {
            SetFlag(CPU.FLAG_N, false);
            SetFlag(CPU.FLAG_H, false);
            SetFlag(CPU.FLAG_C, false);
        }

        public void SetZeroFlag(bool condition)
        {
            SetFlag(CPU.FLAG_Z, condition);
        }

        public byte ReadU8()
        {
            byte value = _mmu.ReadByte(PC);
            PC++; // Increment PC after reading
            return value;
        }

        public ushort ReadU16()
        {
            byte low = _mmu.ReadByte(PC);
            PC++; // Increment PC after reading low byte
            byte high = _mmu.ReadByte(PC);
            PC++; // Increment PC after reading high byte
            return (ushort)((high << 8) | low);
        }

        /// <summary>
        /// Helper method to push the current PC onto the stack.
        /// </summary>
        private void PushPC()
        {
            _mmu.WriteByte((ushort)(SP - 1), (byte)(PC >> 8));
            _mmu.WriteByte((ushort)(SP - 2), (byte)(PC & 0xFF));
            SP -= 2;
        }

        /// <summary>
        /// Helper method to pop the PC from the stack.
        /// </summary>
        public ushort PopPC()
        {
            byte low = _mmu.ReadByte(SP);
            byte high = _mmu.ReadByte((ushort)(SP + 1));
            SP += 2;
            return (ushort)((high << 8) | low);
        }
        
        /// <summary>
        /// Pushes a byte onto the stack.
        /// </summary>
        public void Push(byte value)
        {
            _mmu.WriteByte(--SP, value);
        }

        /// <summary>
        /// Pops a byte from the stack.
        /// </summary>
        public byte Pop()
        {
            byte value = _mmu.ReadByte(SP);
            SP++;
            return value;
        }

        
        private void HandleInterrupts()
        {
            if (!_interruptMasterEnable) return;

            byte IF = _mmu.ReadByte(0xFF0F);
            byte IE = _mmu.ReadByte(0xFFFF);
            byte triggered = (byte)(IF & IE);

            if (triggered == 0) return;

            // Check each interrupt source
            for (int i = 0; i < 5; i++)
            {
                byte mask = (byte)(1 << i);
                if ((triggered & mask) != 0)
                {
                    // Acknowledge interrupt
                    IF &= (byte)~mask;
                    _mmu.WriteByte(0xFF0F, IF);
                    _interruptMasterEnable = false;

                    // Push PC onto stack
                    PushPC();

                    // Jump to the correct interrupt vector
                    switch (i)
                    {
                        case 0:
                            PC = 0x40;
                            Debug.Log("CPU: Jumped to VBlank interrupt vector at 0x40");
                            break; // VBlank
                        case 1:
                            PC = 0x48;
                            Debug.Log("CPU: Jumped to LCD STAT interrupt vector at 0x48");
                            break; // LCD STAT
                        case 2:
                            PC = 0x50;
                            Debug.Log("CPU: Jumped to Timer interrupt vector at 0x50");
                            break; // Timer
                        case 3:
                            PC = 0x58;
                            Debug.Log("CPU: Jumped to Serial interrupt vector at 0x58");
                            break; // Serial
                        case 4:
                            PC = 0x60;
                            Debug.Log("CPU: Jumped to Joypad interrupt vector at 0x60");
                            break; // Joypad
                    }
                    return;
                }
            }
        }
    }
}
