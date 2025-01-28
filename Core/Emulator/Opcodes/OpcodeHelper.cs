
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core.Opcodes;

public static class OpcodeHelper
{
    public static int Execute(CPU cpu, byte opcode)
    {
        int cyclesUsed = 0;
        switch (opcode)
        {
            case 0x00: // NOP
                cyclesUsed = 4;
                break;

            case 0xF3: // DI
                // Disables interrupts
                // We'll just toggle CPU field via reflection or direct property
                SetInterruptMasterEnable(cpu, false);
                cyclesUsed = 4;
                break;

            case 0xFB: // EI
                // Enables interrupts
                SetInterruptMasterEnable(cpu, true);
                cyclesUsed = 4;
                break;

            case 0xC3: // JP a16
            {
                ushort addr = cpu.ReadU16();
                cpu.PC = addr;
                cyclesUsed = 16;
                break;
            }

            case 0xC9: // RET
            {
                byte lo = cpu._mmu.ReadByte(cpu.SP);
                cpu.SP++;
                byte hi = cpu._mmu.ReadByte(cpu.SP);
                cpu.SP++;
                cpu.PC = (ushort)((hi << 8) | lo);
                cyclesUsed = 16;
                break;
            }

            case 0xD9: // RETI
            {
                byte lo = cpu._mmu.ReadByte(cpu.SP);
                cpu.SP++;
                byte hi = cpu._mmu.ReadByte(cpu.SP);
                cpu.SP++;
                cpu.PC = (ushort)((hi << 8) | lo);
                SetInterruptMasterEnable(cpu, true);
                cyclesUsed = 16;
                break;
            }

            // ------------------------------------------------------
            // Implementations for "unhandled" example opcodes:
            // ------------------------------------------------------

            case 0xF0: // LDH A, [0xFF00 + imm8]
            {
                byte offset = cpu._mmu.ReadByte(cpu.PC);
                cpu.PC++;
                ushort addr = (ushort)(0xFF00 + offset);
                cpu.A = cpu._mmu.ReadByte(addr);
                cyclesUsed = 12;
                break;
            }

            case 0x44: // LD B,H
                cpu.B = cpu.H;
                cyclesUsed = 4;
                break;

            case 0xFE: // CP d8
            {
                byte value = cpu._mmu.ReadByte(cpu.PC);
                cpu.PC++;
                byte result = (byte)(cpu.A - value);
                cpu.SetFlag(CPU.FLAG_Z, (result == 0));
                cpu.SetFlag(CPU.FLAG_N, true);
                cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0xF) < (value & 0xF)));
                cpu.SetFlag(CPU.FLAG_C, (cpu.A < value));
                cyclesUsed = 8;
                break;
            }

            case 0x90: // SUB B
            {
                byte result = (byte)(cpu.A - cpu.B);
                cpu.SetFlag(CPU.FLAG_Z, (result == 0));
                cpu.SetFlag(CPU.FLAG_N, true);
                cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0xF) < (cpu.B & 0xF)));
                cpu.SetFlag(CPU.FLAG_C, (cpu.A < cpu.B));
                cpu.A = result;
                cyclesUsed = 4;
                break;
            }

            case 0x38: // JR C, r8
            {
                sbyte offset = (sbyte)cpu._mmu.ReadByte(cpu.PC);
                cpu.PC++;
                if (cpu.GetFlag(CPU.FLAG_C))
                {
                    cpu.PC = (ushort)(cpu.PC + offset);
                    cyclesUsed = 12;
                }
                else
                {
                    cyclesUsed = 8;
                }

                break;
            }

            case 0xFA: // LD A, [a16]
            {
                ushort addr = cpu.ReadU16();
                cpu.A = cpu._mmu.ReadByte(addr);
                cyclesUsed = 16;
                break;
            }

            case 0xAF: // XOR A
            {
                cpu.A = 0;
                cpu.F = CPU.FLAG_Z; // Z=1, others=0
                cyclesUsed = 4;
                break;
            }

            case 0xE0: // LDH [0xFF00 + imm8], A
            {
                byte offset = cpu._mmu.ReadByte(cpu.PC);
                cpu.PC++;
                ushort addr = (ushort)(0xFF00 + offset);
                cpu._mmu.WriteByte(addr, cpu.A);
                cyclesUsed = 12;
                break;
            }

            case 0x40: // LD B,B
                // No-op, but 4 cycles
                cpu.B = cpu.B;
                cyclesUsed = 4;
                break;

            case 0x21: // LD HL, d16
            {
                ushort val = cpu.ReadU16();
                cpu.H = (byte)(val >> 8);
                cpu.L = (byte)(val & 0xFF);
                cyclesUsed = 12;
                break;
            }

            case 0x11: // LD DE, d16
            {
                ushort val = cpu.ReadU16();
                cpu.D = (byte)(val >> 8);
                cpu.E = (byte)(val & 0xFF);
                cyclesUsed = 12;
                break;
            }

            case 0x50: // LD D,B
                cpu.D = cpu.B;
                cyclesUsed = 4;
                break;

            case 0x01: // LD BC, d16
            {
                ushort val = cpu.ReadU16();
                cpu.B = (byte)(val >> 8);
                cpu.C = (byte)(val & 0xFF);
                cyclesUsed = 12;
                break;
            }

            default:
                Console.WriteLine($"Unhandled opcode 0x{opcode:X2} at PC=0x{cpu.PC - 1:X4}");
                cyclesUsed = 4;
                break;
        }

        return cyclesUsed;
    }

    /// <summary>
    /// Simple helper for toggling CPU's IME field.
    /// Because it's private in CPU, we either expose it or do reflection or a public method.
    /// Here we assume you might add a public property or method in CPU.
    /// </summary>
    private static void SetInterruptMasterEnable(CPU cpu, bool enable)
    {
        cpu._interruptMasterEnable = enable;
    }
}