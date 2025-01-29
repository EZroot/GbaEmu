using SDL2Engine.Core.Utils;
using System;

namespace GbaEmu.Core.Opcodes
{
    public static class OpcodeHelper
    {
        public static int Execute(CPU cpu, byte opcode)
        {
            int cyclesUsed = 0;

            switch (opcode)
            {
                case 0x00: // NOP - No Operation
                    cyclesUsed = 4;
                    break;
                
                case 0x31: // LD SP, d16
                    cpu.SP = cpu.ReadU16();
                    cyclesUsed = 12;
                    break;
                
                case 0xFF: // RST 38h (Reset to 0x0038)
                    PushToStack(cpu, cpu.PC);
                    cpu.PC = 0x0038;
                    cyclesUsed = 16;
                    break;
                
                // Removed case 0xDF as it's not a standard Game Boy opcode
                
                case 0xEA: // LD (a16), A
                    ushort addr = cpu.ReadU16();
                    cpu._mmu.WriteByte(addr, cpu.A);
                    cyclesUsed = 16;
                    break;
                
                case 0xD6: // SUB d8 (Subtract immediate from A)
                {
                    byte value = cpu.ReadU8();
                    byte aBefore = cpu.A;
                    cpu.A = (byte)(cpu.A - value);
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, (aBefore & 0x0F) < (value & 0x0F));
                    cpu.SetFlag(CPU.FLAG_C, aBefore < value);
                    cyclesUsed = 8;
                    break;
                }
                
                case 0x21: // LD HL, d16
                    cpu.HL = cpu.ReadU16();
                    cyclesUsed = 12;
                    break;
                
                case 0x8F: // ADC A, A
                    cpu.A = (byte)(cpu.A + cpu.A + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0));
                    SetFlagsAfterAdd(cpu, cpu.A);
                    cyclesUsed = 4;
                    break;
                
                case 0x0B: // DEC BC
                    cpu.BC--;
                    cyclesUsed = 8;
                    break;
                
                case 0xCD: // CALL a16
                {
                    ushort callAddr = cpu.ReadU16();
                    PushToStack(cpu, cpu.PC);
                    cpu.PC = callAddr;
                    cyclesUsed = 24;
                    break;
                }
                
                case 0xA3: // AND E
                    cpu.A &= cpu.E;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, true);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 4;
                    break;
                
                case 0x02: // LD (BC), A
                    cpu._mmu.WriteByte(cpu.BC, cpu.A);
                    cyclesUsed = 8;
                    break;
                
                case 0x8E: // ADC A, (HL)
                {
                    byte value = cpu._mmu.ReadByte(cpu.HL);
                    cpu.A = (byte)(cpu.A + value + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0));
                    SetFlagsAfterAdd(cpu, cpu.A);
                    cyclesUsed = 8;
                    break;
                }
                
                case 0x03: // INC BC
                    cpu.BC++;
                    cyclesUsed = 8;
                    break;
                
                case 0x79: // LD A, C
                    cpu.A = cpu.C;
                    cyclesUsed = 4;
                    break;
                
                case 0x5F: // LD E, A
                    cpu.E = cpu.A;
                    cyclesUsed = 4;
                    break;
                
                case 0x3E: // LD A, d8
                {
                    byte v = cpu.ReadU8();
                    cpu.A = v;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 8;
                    break;
                }

                case 0x43: // LD B, E
                    cpu.B = cpu.E;
                    cyclesUsed = 4;
                    break;

                case 0x50: // LD D, B
                    cpu.D = cpu.B;
                    cyclesUsed = 4;
                    break;

                case 0x55: // LD D, L
                    cpu.D = cpu.L;
                    cyclesUsed = 4;
                    break;

                case 0xD0: // RET NC (Return if Carry flag is not set)
                {
                    if (!cpu.GetFlag(CPU.FLAG_C))
                    {
                        cpu.PC = PopFromStack(cpu);
                        cyclesUsed = 20;
                    }
                    else
                    {
                        cyclesUsed = 8;
                    }
                    break;
                }
                
                case 0xC8: // RET Z (Return if Zero flag is set)
                {
                    if (cpu.GetFlag(CPU.FLAG_Z))
                    {
                        cpu.PC = PopFromStack(cpu);
                        cyclesUsed = 20;
                    }
                    else
                    {
                        cyclesUsed = 8;
                    }
                    break;
                }
                
                case 0xC9: // RET (Return)
                    cpu.PC = PopFromStack(cpu);
                    cyclesUsed = 16;
                    break;
                
                case 0xB7: // OR A, A
                {
                    cpu.A |= cpu.A;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x30: // JR NC, r8 (Jump relative if Carry flag is not set)
                {
                    sbyte offset = (sbyte)cpu.ReadU8();
                    if (!cpu.GetFlag(CPU.FLAG_C))
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
                
                case 0x1F: // RRA (Rotate A right through Carry)
                {
                    byte carryOut = (byte)(cpu.A & 0x01);
                    cpu.A = (byte)((cpu.A >> 1) | (cpu.GetFlag(CPU.FLAG_C) ? 0x80 : 0x00));
                    cpu.SetFlag(CPU.FLAG_Z, false);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, carryOut == 1);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xCE: // ADC A, d8 (Add immediate value to A with carry)
                {
                    byte a = cpu.ReadU8();
                    int result = cpu.A + a + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0);
                    cpu.SetFlag(CPU.FLAG_Z, (result & 0xFF) == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0x0F) + (a & 0x0F) + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, result > 0xFF);
                    cpu.A = (byte)result;
                    cyclesUsed = 8;
                    break;
                }
                
                case 0xC3: // JP a16 (Jump to address)
                {
                    cpu.PC = cpu.ReadU16();
                    cyclesUsed = 16;
                    break;
                }

                case 0x37: // SCF (Set Carry Flag)
                {
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, true);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x06: // LD B, d8 (Load immediate value into B)
                {
                    cpu.B = cpu.ReadU8();
                    cyclesUsed = 8;
                    break;
                }
                
                case 0x66: // LD H, (HL) (Load value at HL into H)
                {
                    cpu.H = cpu._mmu.ReadByte(cpu.HL);
                    cyclesUsed = 8;
                    break;
                }
                
                case 0xCC: // CALL Z, a16 (Call subroutine at address if Zero flag is set)
                {
                    ushort address = cpu.ReadU16();
                    if (cpu.GetFlag(CPU.FLAG_Z))
                    {
                        PushToStack(cpu, cpu.PC);
                        cpu.PC = address;
                        cyclesUsed = 24;
                    }
                    else
                    {
                        cyclesUsed = 12;
                    }
                    break;
                }
                
                case 0xF3: // DI (Disable Interrupts)
                {
                    cpu.DisableInterrupts();
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x3C: // INC A (Increment A)
                {
                    byte aBefore = cpu.A;
                    cpu.A++;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, (aBefore & 0x0F) == 0x0F);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xE0: // LDH [a8], A (Store A into address 0xFF00 + immediate 8-bit value)
                {
                    byte offset = cpu.ReadU8();
                    ushort address = (ushort)(0xFF00 + offset);
                    cpu._mmu.WriteByte(address, cpu.A);
                    cyclesUsed = 12;
                    break;
                }
                
                case 0x07: // RLCA (Rotate A left, copy bit 7 to Carry and bit 0)
                {
                    byte carryOut = (byte)((cpu.A & 0x80) >> 7);
                    cpu.A = (byte)((cpu.A << 1) | carryOut);
                    cpu.SetFlag(CPU.FLAG_Z, false);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, carryOut == 1);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xAF: // XOR A (XOR A with itself)
                {
                    cpu.A = 0;
                    cpu.SetFlag(CPU.FLAG_Z, true);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x26: // LD H, d8 (Load immediate 8-bit value into H)
                {
                    cpu.H = cpu.ReadU8();
                    cyclesUsed = 8;
                    break;
                }
                
                case 0x41: // LD B, C (Load the value of C into B)
                    cpu.B = cpu.C;
                    cyclesUsed = 4;
                    break;
                
                case 0x42: // LD B, D (Load the value of D into B)
                    cpu.B = cpu.D;
                    cyclesUsed = 4;
                    break;
                
                case 0x45: // LD B, L (Load the value of L into B)
                    cpu.B = cpu.L;
                    cyclesUsed = 4;
                    break;
                
                case 0xFC: // Unused/Undocumented Opcode
                    Debug.LogWarning($"Opcode 0xFC: Unused/undocumented instruction at PC=0x{cpu.PC - 1:X4}. Ignoring.");
                    cyclesUsed = 4; // Assume NOP behavior
                    break;
                
                case 0x47: // LD B, A (Load the value of A into B)
                    cpu.B = cpu.A;
                    cyclesUsed = 4;
                    break;
                
                case 0x80: // ADD A, B (Add the value of B to A)
                {
                    int result = cpu.A + cpu.B;
                    cpu.SetFlag(CPU.FLAG_Z, (result & 0xFF) == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0x0F) + (cpu.B & 0x0F)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, result > 0xFF);
                    cpu.A = (byte)result;
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xE5: // PUSH HL (Push the value of HL onto the stack)
                {
                    PushToStack(cpu, cpu.HL);
                    cyclesUsed = 16;
                    break;
                }
                
                case 0x10: // STOP (Halt CPU and stop until a button press)
                {
                    Debug.Log("STOP instruction executed. Halting CPU.");
                    cpu.Stop(); // Assume CPU has an IsStopped property
                    cyclesUsed = 4; // This can vary based on implementation
                    break;
                }
                
                case 0x44: // LD B, H
                    cpu.B = cpu.H;
                    cyclesUsed = 4;
                    break;
                
                case 0x90: // SUB B
                {
                    byte v = cpu.B;
                    byte aBefore = cpu.A;
                    cpu.A = (byte)(cpu.A - v);
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, (aBefore & 0x0F) < (v & 0x0F));
                    cpu.SetFlag(CPU.FLAG_C, aBefore < v);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xFE: // CP d8
                {
                    byte v = cpu.ReadU8();
                    byte aBefore = cpu.A;
                    byte result = (byte)(cpu.A - v);
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == v);
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, (aBefore & 0x0F) < (v & 0x0F));
                    cpu.SetFlag(CPU.FLAG_C, aBefore < v);
                    cyclesUsed = 8;
                    break;
                }
                
                case 0x20: // JR NZ, r8
                {
                    sbyte offset = (sbyte)cpu.ReadU8(); // Read the signed 8-bit offset
                    if (!cpu.GetFlag(CPU.FLAG_Z)) // If Zero flag is not set
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
                
                case 0x40: // LD B, B
                    // No Operation, B is loaded into itself
                    cyclesUsed = 4;
                    break;
                
                case 0x99: // SBC A, C
                {
                    byte v = cpu.C;
                    bool carry = cpu.GetFlag(CPU.FLAG_C);
                    int temp = cpu.A - v - (carry ? 1 : 0);
                    byte result = (byte)(temp & 0xFF);

                    // Set Zero flag
                    cpu.SetFlag(CPU.FLAG_Z, result == 0);
                    // Set Subtract flag
                    cpu.SetFlag(CPU.FLAG_N, true);
                    // Set Half Carry flag
                    cpu.SetFlag(CPU.FLAG_H, (cpu.A & 0x0F) < ((v & 0x0F) + (carry ? 1 : 0)));
                    // Set Carry flag
                    cpu.SetFlag(CPU.FLAG_C, temp < 0);

                    cpu.A = result; // Store the result back in A
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x28: // JR Z, r8
                {
                    sbyte offset = (sbyte)cpu.ReadU8();
                    if (cpu.GetFlag(CPU.FLAG_Z))
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
                
                case 0x35: // DEC (HL)
                {
                    byte v = cpu._mmu.ReadByte(cpu.HL);
                    byte result = (byte)(v - 1);
                    cpu._mmu.WriteByte(cpu.HL, result);

                    cpu.SetFlag(CPU.FLAG_Z, result == 0);
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, (v & 0x0F) == 0x00);

                    cyclesUsed = 12;
                    break;
                }
                
                case 0x18: // JR r8
                {
                    sbyte offset = (sbyte)cpu.ReadU8();
                    cpu.PC = (ushort)(cpu.PC + offset);
                    cyclesUsed = 12;
                    break;
                }
                
                case 0xFB: // EI (Enable Interrupts)
                {
                    cpu.IME_Set = true; // Delayed setting of IME
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xA7: // AND A
                {
                    cpu.A &= cpu.A;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, true);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xF8: // LDHL SP, r8
                {
                    sbyte offset = (sbyte)cpu.ReadU8();
                    int result = cpu.SP + offset;
                    cpu.HL = (ushort)result;

                    // Set flags
                    cpu.SetFlag(CPU.FLAG_Z, false);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.SP & 0x0F) + (offset & 0x0F)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, ((cpu.SP & 0xFF) + (offset & 0xFF)) > 0xFF);

                    cyclesUsed = 12;
                    break;
                }
                
                case 0xB1: // OR C
                {
                    cpu.A |= cpu.C;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.A == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, false);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x78: // LD A, B
                    cpu.A = cpu.B;
                    cyclesUsed = 4;
                    break;
                
                case 0x13: // INC DE
                    cpu.DE++;
                    cyclesUsed = 8;
                    break;
                
                case 0x1A: // LD A, (DE)
                    cpu.A = cpu._mmu.ReadByte(cpu.DE);
                    cyclesUsed = 8;
                    break;
                
                case 0xFA: // LD A, (a16)
                {
                    ushort address = cpu.ReadU16();
                    cpu.A = cpu._mmu.ReadByte(address);
                    cyclesUsed = 16;
                    break;
                }
                
                case 0x0F: // RRCA (Rotate A right, copy bit 0 to Carry and bit 7)
                {
                    byte carryOut = (byte)(cpu.A & 0x01);
                    cpu.A = (byte)((cpu.A >> 1) | (carryOut << 7));
                    cpu.SetFlag(CPU.FLAG_Z, false);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, false);
                    cpu.SetFlag(CPU.FLAG_C, carryOut == 1);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0xF0: // LDH A, (a8)
                {
                    byte offset = cpu.ReadU8();
                    ushort address = (ushort)(0xFF00 + offset);
                    cpu.A = cpu._mmu.ReadByte(address);
                    cyclesUsed = 12;
                    break;
                }
                
                case 0x0D: // DEC C (Decrement C)
                {
                    cpu.C--;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.C == 0);
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, (cpu.C & 0x0F) == 0x0F);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x73: // LD (HL), E (Store E into memory at HL)
                    cpu._mmu.WriteByte(cpu.HL, cpu.E);
                    cyclesUsed = 8;
                    break;
                
                case 0x83: // ADD A, E (Add E to A)
                {
                    int result = cpu.A + cpu.E;
                    cpu.SetFlag(CPU.FLAG_Z, (result & 0xFF) == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0x0F) + (cpu.E & 0x0F)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, result > 0xFF);
                    cpu.A = (byte)result;
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x0C: // INC C (Increment C)
                {
                    byte cBefore = cpu.C;
                    cpu.C++;
                    cpu.SetFlag(CPU.FLAG_Z, cpu.C == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, (cBefore & 0x0F) == 0x0F);
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x08: // LD (a16), SP (Store SP to address)
                {
                    ushort address = cpu.ReadU16();
                    cpu._mmu.WriteByte(address, (byte)(cpu.SP & 0xFF));
                    cpu._mmu.WriteByte((ushort)(address + 1), (byte)(cpu.SP >> 8));
                    cyclesUsed = 20;
                    break;
                }
                
                case 0x11: // LD DE, d16 (Load immediate 16-bit value into DE)
                    cpu.DE = cpu.ReadU16();
                    cyclesUsed = 12;
                    break;
                
                case 0x88: // ADC A, B (Add B to A with carry)
                {
                    int result = cpu.A + cpu.B + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0);
                    cpu.SetFlag(CPU.FLAG_Z, (result & 0xFF) == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0x0F) + (cpu.B & 0x0F) + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, result > 0xFF);
                    cpu.A = (byte)result;
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x89: // ADC A, C (Add C to A with carry)
                {
                    int result = cpu.A + cpu.C + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0);
                    cpu.SetFlag(CPU.FLAG_Z, (result & 0xFF) == 0);
                    cpu.SetFlag(CPU.FLAG_N, false);
                    cpu.SetFlag(CPU.FLAG_H, ((cpu.A & 0x0F) + (cpu.C & 0x0F) + (cpu.GetFlag(CPU.FLAG_C) ? 1 : 0)) > 0x0F);
                    cpu.SetFlag(CPU.FLAG_C, result > 0xFF);
                    cpu.A = (byte)result;
                    cyclesUsed = 4;
                    break;
                }
                
                case 0x01: // LD BC, d16
                    cpu.BC = cpu.ReadU16();
                    cyclesUsed = 12;
                    break;
                
                case 0x49: // LD C, C
                    // No Operation, C is loaded into itself
                    cyclesUsed = 4;
                    break;
                
                case 0x4E: // LD C, (HL)
                    cpu.C = cpu._mmu.ReadByte(cpu.HL);
                    cyclesUsed = 8;
                    break;
                
                case 0x53: // LD D, E
                    cpu.D = cpu.E;
                    cyclesUsed = 4;
                    break;
                
                case 0x54: // LD D, H
                    cpu.D = cpu.H;
                    cyclesUsed = 4;
                    break;
                
                case 0xF5: // PUSH AF
                {
                    ushort af = (ushort)((cpu.A << 8) | cpu.F);
                    PushToStack(cpu, af);
                    cyclesUsed = 16;
                    break;
                }
                
                case 0x2F: // CPL (Complement A - Flip all bits)
                    cpu.A = (byte)~cpu.A;
                    cpu.SetFlag(CPU.FLAG_N, true);
                    cpu.SetFlag(CPU.FLAG_H, true);
                    cyclesUsed = 4;
                    break;
                
                case 0x22: // LD (HL+), A
                    cpu._mmu.WriteByte(cpu.HL, cpu.A);
                    cpu.HL++;
                    cyclesUsed = 8;
                    break;
                
                default:
                    Debug.LogError($"Unhandled opcode 0x{opcode:X2} at PC=0x{cpu.PC - 1:X4}");
                    cyclesUsed = 4; // Assume NOP behavior for unhandled opcodes
                    break;
            }
            return cyclesUsed;
        }

        /// <summary>
        /// Pushes a 16-bit value onto the stack.
        /// </summary>
        private static void PushToStack(CPU cpu, ushort value)
        {
            cpu.SP -= 2;
            cpu._mmu.WriteByte((ushort)(cpu.SP + 1), (byte)(value >> 8)); // High byte
            cpu._mmu.WriteByte(cpu.SP, (byte)(value & 0xFF)); // Low byte
        }

        /// <summary>
        /// Pops a 16-bit value from the stack.
        /// </summary>
        private static ushort PopFromStack(CPU cpu)
        {
            byte low = cpu._mmu.ReadByte(cpu.SP);
            byte high = cpu._mmu.ReadByte((ushort)(cpu.SP + 1));
            cpu.SP += 2;
            return (ushort)(high << 8 | low);
        }

        /// <summary>
        /// Sets flags after an addition operation.
        /// </summary>
        private static void SetFlagsAfterAdd(CPU cpu, byte result)
        {
            cpu.SetFlag(CPU.FLAG_Z, result == 0);
            cpu.SetFlag(CPU.FLAG_N, false);
            cpu.SetFlag(CPU.FLAG_H, ((result & 0x0F) + (result & 0x0F)) > 0x0F); // Simplified
            cpu.SetFlag(CPU.FLAG_C, result < cpu.A); // Check if carry occurred
        }
    }
}
