using System;
using System.Collections.Generic;
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

        // Interrupt flags
        public bool IME { get; private set; } // Interrupt Master Enable
        public bool IME_Set { get; set; } // Delayed setting of IME

        // CPU States
        public bool IsStopped { get; private set; }
        public bool IsHalted { get; private set; }

        // References
        internal MMU _mmu;
        private readonly Timer _timer;

        private ulong _totalCyclesUsed = 0;
        
        // Opcode Logging
        public class OpcodeLogEntry
        {
            public string PC { get; }
            public string Opcode { get; }
            public int CyclesUsed { get; }
            public ulong TotalCyclesUsed { get; }
            public OpcodeLogEntry(string pc, string opcode, int cycles, ulong totalCyclesUsed)
            {
                PC = pc;
                Opcode = opcode;
                CyclesUsed = cycles;
                TotalCyclesUsed = totalCyclesUsed;
            }
        }

        public List<OpcodeLogEntry> OpcodeLogEntries { get; } = new List<OpcodeLogEntry>();

        public CPU(MMU mmu)
        {
            _mmu = mmu;
            _timer = new Timer(mmu);
            Debug.Log("CPU created. Calling Reset()...");
            Reset();
        }

        public void Reset()
        {
            // Reset CPU States
            IsStopped = false;
            IsHalted = false;
            IME = false;
            IME_Set = false;

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

            IME = false; // IME is disabled on reset
            IME_Set = false;
        }

        /// <summary>
        /// Executes a single CPU step (instruction).
        /// </summary>
        /// <param name="logOpcode">Whether to log the executed opcode.</param>
        /// <returns>The number of cycles used by the executed opcode.</returns>
        public int Step(bool logOpcode = false)
        {
            int cyclesUsed = 0;

            // Handle IME delay (for EI instruction)
            if (IME_Set)
            {
                IME = true;
                IME_Set = false;
                Debug.Log("IME has been enabled.");
            }

            // If CPU is stopped, do not execute instructions
            if (IsStopped)
            {
                Debug.Log("CPU is in STOP state. Waiting for button press...");
                return 4; // Assume minimal cycles consumed
            }

            // If CPU is halted and an interrupt is pending, resume execution
            if (IsHalted)
            {
                if (AreInterruptsPending())
                {
                    IsHalted = false;
                }
                else
                {
                    Debug.Log("CPU is in HALT state. No interrupts pending.");
                    return 4; // Assume minimal cycles consumed
                }
            }

            // Handle interrupts and add cycles if an interrupt was processed
            int interruptCycles = HandleInterrupts();
            cyclesUsed += interruptCycles;

            if (interruptCycles > 0)
            {
                _totalCyclesUsed += (ulong)interruptCycles;
                _timer.Update(interruptCycles);
                return interruptCycles;
            }

            byte opcode = _mmu.ReadByte(PC);
            PC++;

            var unmodifiedPC = PC;

            int opcodeCycles = OpcodeHelper.Execute(this, opcode);
            cyclesUsed += opcodeCycles;
            _totalCyclesUsed += (ulong)opcodeCycles;

            if (logOpcode)
            {
                OpcodeLogEntries.Add(new OpcodeLogEntry(
                    $"{unmodifiedPC - 1:X4}",
                    $"{opcode:X2} (A:{A:X2} B:{B:X2} C:{C:X2} D:{D:X2} E:{E:X2} F:{F:X2} H:{H:X2} L:{L:X2})",
                    opcodeCycles,
                    _totalCyclesUsed));
            }

            _timer.Update(opcodeCycles);

            return cyclesUsed;
        }

        /// <summary>
        /// Checks if any interrupts are pending.
        /// </summary>
        /// <returns>True if any interrupts are pending; otherwise, false.</returns>
        private bool AreInterruptsPending()
        {
            byte IF = _mmu.ReadByte(0xFF0F);
            byte IE = _mmu.ReadByte(0xFFFF);
            return (IF & IE) != 0;
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
            SetFlag(FLAG_N, false);
            SetFlag(FLAG_H, false);
            SetFlag(FLAG_C, false);
        }

        public void SetZeroFlag(bool condition)
        {
            SetFlag(FLAG_Z, condition);
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
        /// Pushes a 16-bit value onto the stack.
        /// </summary>
        /// <param name="value">The 16-bit value to push.</param>
        public void PushToStack(ushort value)
        {
            SP -= 2;
            _mmu.WriteByte((ushort)(SP + 1), (byte)(value >> 8)); // High byte
            _mmu.WriteByte(SP, (byte)(value & 0xFF)); // Low byte
        }

        /// <summary>
        /// Pops a 16-bit value from the stack.
        /// </summary>
        /// <returns>The 16-bit value popped from the stack.</returns>
        public ushort PopFromStack()
        {
            byte low = _mmu.ReadByte(SP);
            byte high = _mmu.ReadByte((ushort)(SP + 1));
            SP += 2;
            return (ushort)((high << 8) | low);
        }

        /// <summary>
        /// Pushes the current PC onto the stack.
        /// </summary>
        public void PushPC()
        {
            PushToStack(PC);
        }

        /// <summary>
        /// Pops the PC from the stack.
        /// </summary>
        public void PopPC()
        {
            PC = PopFromStack();
        }

        /// <summary>
        /// Pushes a byte onto the stack.
        /// </summary>
        /// <param name="value">The byte value to push.</param>
        public void Push(byte value)
        {
            _mmu.WriteByte(--SP, value);
        }

        /// <summary>
        /// Pops a byte from the stack.
        /// </summary>
        /// <returns>The byte value popped from the stack.</returns>
        public byte Pop()
        {
            byte value = _mmu.ReadByte(SP);
            SP++;
            return value;
        }

        /// <summary>
        /// Handles interrupts by checking pending interrupts and servicing them.
        /// </summary>
        private int HandleInterrupts()
        {
            if (!IME)
                return 0;

            byte IF = _mmu.ReadByte(0xFF0F);
            byte IE = _mmu.ReadByte(0xFFFF);
            byte triggered = (byte)(IF & IE);

            if (triggered == 0)
                return 0;

            // Check each interrupt source (from highest priority to lowest)
            for (int i = 0; i < 5; i++)
            {
                byte mask = (byte)(1 << i);
                if ((triggered & mask) != 0)
                {
                    // Acknowledge interrupt
                    IF &= (byte)~mask;
                    _mmu.WriteByte(0xFF0F, IF);

                    // Disable IME
                    IME = false;

                    // Push PC onto stack
                    PushPC();

                    // Jump to the correct interrupt vector
                    switch (i)
                    {
                        case 0:
                            PC = 0x40; // VBlank
                            Debug.Log("CPU: Jumped to VBlank interrupt vector at 0x40");
                            break;
                        case 1:
                            PC = 0x48; // LCD STAT
                            Debug.Log("CPU: Jumped to LCD STAT interrupt vector at 0x48");
                            break;
                        case 2:
                            PC = 0x50; // Timer
                            Debug.Log("CPU: Jumped to Timer interrupt vector at 0x50");
                            break;
                        case 3:
                            PC = 0x58; // Serial
                            Debug.Log("CPU: Jumped to Serial interrupt vector at 0x58");
                            break;
                        case 4:
                            PC = 0x60; // Joypad
                            Debug.Log("CPU: Jumped to Joypad interrupt vector at 0x60");
                            break;
                    }

                    // Return the total cycles consumed by handling the interrupt
                    return 20;
                }
            }

            return 0;
        }

        /// <summary>
        /// Sets the IME flag with delayed enabling if necessary.
        /// </summary>
        public void EnableInterrupts()
        {
            IME_Set = true;
        }

        /// <summary>
        /// Disables interrupts immediately.
        /// </summary>
        public void DisableInterrupts()
        {
            IME = false;
        }

        /// <summary>
        /// Sets the CPU to the halted state.
        /// </summary>
        public void Halt()
        {
            IsHalted = true;
            Debug.Log("CPU is now in HALT state.");
        }

        /// <summary>
        /// Sets the CPU to the stopped state.
        /// </summary>
        public void Stop()
        {
            IsStopped = true;
            Debug.Log("CPU is now in STOP state.");
        }
    }
}
