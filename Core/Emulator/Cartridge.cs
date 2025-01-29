using System;
using System.IO;
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core
{
    // Enum to represent different cartridge types
    public enum CartridgeType : byte
    {
        ROM_ONLY = 0x00,
        MBC1 = 0x01,
        MBC1_RAM = 0x02,
        MBC1_RAM_BATTERY = 0x03,
        // Add more types as needed based on Game Boy specifications
    }

    public class Cartridge
    {
        public byte[] ROM { get; }
        public byte[] RAM { get; private set; }

        private bool _ramEnabled;
        private int _romBank = 1;
        private int _ramBank;
        private int _bankingMode;

        private byte[] _bootRom;
        public bool HasBootRom { get; private set; }

        private CartridgeType _cartridgeType;

        public Cartridge(string romPath, string bootRomPath = null)
        {
            if (string.IsNullOrEmpty(romPath))
                throw new ArgumentException("ROM path is empty.");
            if (!File.Exists(romPath))
                throw new FileNotFoundException("ROM file not found", romPath);

            ROM = File.ReadAllBytes(romPath);
            RAM = new byte[0x8000]; // For MBC1 example

            //Debug.Log($"Cartridge: Loaded ROM from {romPath}, size {ROM.Length} bytes.");

            // Read cartridge type from 0x0147
            if (ROM.Length > 0x0147)
            {
                byte typeByte = ROM[0x0147];
                if (Enum.IsDefined(typeof(CartridgeType), typeByte))
                {
                    _cartridgeType = (CartridgeType)typeByte;
                    Debug.Log($"Cartridge type: {_cartridgeType}");
                }
                else
                {
                    _cartridgeType = CartridgeType.ROM_ONLY; // default to ROM_ONLY if unknown
                    Debug.LogWarning($"Cartridge: Unknown cartridge type 0x{typeByte:X2}, defaulting to ROM_ONLY.");
                }

            }
            else
            {
                _cartridgeType = CartridgeType.ROM_ONLY;
                Debug.LogWarning("Cartridge: ROM too small to detect cartridge type, defaulting to ROM_ONLY.");
            }

            // Boot ROM
            if (!string.IsNullOrEmpty(bootRomPath) && File.Exists(bootRomPath))
            {
                _bootRom = File.ReadAllBytes(bootRomPath);
                HasBootRom = true;
                //Debug.Log($"Cartridge: Loaded Boot ROM from {bootRomPath}, size {_bootRom.Length} bytes.");
            }
            else
            {
                _bootRom = Array.Empty<byte>();
                HasBootRom = false;
                Debug.Log("Cartridge: No valid Boot ROM found, skipping.");
            }

            // Initialize RAM if cartridge has RAM (MBC1_RAM or MBC1_RAM_BATTERY)
            if (_cartridgeType == CartridgeType.MBC1_RAM || _cartridgeType == CartridgeType.MBC1_RAM_BATTERY)
            {
                RAM = new byte[0x2000 * 4]; // MBC1 can have up to 4 RAM banks
                //Debug.Log($"Cartridge: External RAM enabled, size {RAM.Length} bytes.");
            }
            else
            {
                RAM = null;
                Debug.Log("Cartridge: External RAM not present.");
            }
        }

        public byte ReadByte(int addr)
        {
            // Boot ROM read
            if (HasBootRom && addr >= 0x0000 && addr < 0x0100)
            {
                byte value = _bootRom[addr];
                //Debug.Log($"Cartridge: Read 0x{value:X2} from Boot ROM at 0x{addr:X4}");
                return value;
            }

            if (_cartridgeType == CartridgeType.ROM_ONLY)
            {
                if (addr < 0x8000)
                {
                    byte value = ROM[addr];
                    //Debug.Log($"Cartridge: Read 0x{value:X2} from ROM at 0x{addr:X4}");
                    return value;
                }
            }
            else if (_cartridgeType == CartridgeType.MBC1 || 
                     _cartridgeType == CartridgeType.MBC1_RAM || 
                     _cartridgeType == CartridgeType.MBC1_RAM_BATTERY)
            {
                if (addr < 0x4000)
                {
                    byte value = ROM[addr];
                    //Debug.Log($"Cartridge: Read 0x{value:X2} from ROM bank 0 at 0x{addr:X4}");
                    return value;
                }
                else if (addr < 0x8000)
                {
                    byte value = ReadROMBanked(addr);
                    //Debug.Log($"Cartridge: Read 0x{value:X2} from ROM bank {_romBank} at 0x{addr:X4}");
                    return value;
                }
            }

            if ((_cartridgeType == CartridgeType.MBC1 || 
                 _cartridgeType == CartridgeType.MBC1_RAM || 
                 _cartridgeType == CartridgeType.MBC1_RAM_BATTERY) &&
                addr >= 0xA000 && addr < 0xC000 && RAM != null)
            {
                byte value = ReadRAMBanked(addr);
                //Debug.Log($"Cartridge: Read 0x{value:X2} from RAM bank {_ramBank} at 0x{addr:X4}");
                return value;
            }

            //Debug.Log($"Cartridge: Read 0xFF from unmapped address 0x{addr:X4}");
            return 0xFF;
        }

        public void WriteByte(int addr, byte value)
        {
            if ((_cartridgeType == CartridgeType.MBC1 || 
                 _cartridgeType == CartridgeType.MBC1_RAM || 
                 _cartridgeType == CartridgeType.MBC1_RAM_BATTERY) &&
                addr < 0x8000)
            {
                HandleBanking((ushort)addr, value);
                //Debug.Log($"Cartridge: Wrote 0x{value:X2} to MBC register at 0x{addr:X4}");
                return;
            }
            else if ((_cartridgeType == CartridgeType.MBC1 || 
                      _cartridgeType == CartridgeType.MBC1_RAM || 
                      _cartridgeType == CartridgeType.MBC1_RAM_BATTERY) &&
                     addr >= 0xA000 && addr < 0xC000 && RAM != null)
            {
                WriteRAMBanked(addr, value);
                //Debug.Log($"Cartridge: Wrote 0x{value:X2} to RAM bank {_ramBank} at 0x{addr:X4}");
                return;
            }

            if (_cartridgeType == CartridgeType.ROM_ONLY)
            {
                // ROM_ONLY does not support external RAM or MBC1, ignore writes to ROM area
                if (addr < 0x8000)
                {
                    //Debug.Log($"Cartridge: Ignored write to ROM_ONLY cartridge at 0x{addr:X4}");
                    return;
                }
            }

            if (addr == 0xFF50 && value != 0)
            {
                // Disable boot ROM
                HasBootRom = false;
                Debug.Log("Cartridge: Boot ROM disabled via 0xFF50 write.");
                return;
            }

            //Debug.Log($"Cartridge: Attempted to write 0x{value:X2} to unmapped address 0x{addr:X4}");
        }

        private byte ReadROMBanked(int addr)
        {
            // For MBC1, banked ROM from 0x4000-0x7FFF
            int bankedAddr = (addr - 0x4000) + (_romBank * 0x4000);
            bankedAddr %= ROM.Length;
            return ROM[bankedAddr];
        }

        private byte ReadRAMBanked(int addr)
        {
            if (!_ramEnabled || RAM == null)
            {
                Debug.Log("Cartridge: RAM is disabled or not present, returning 0xFF.");
                return 0xFF;
            }

            int ramAddr = (addr - 0xA000) + (_ramBank * 0x2000);
            ramAddr %= RAM.Length;
            byte value = RAM[ramAddr];
            return value;
        }

        private void WriteRAMBanked(int addr, byte value)
        {
            if (!_ramEnabled || RAM == null)
            {
                Debug.Log("Cartridge: RAM is disabled or not present, write ignored.");
                return;
            }

            int ramAddr = (addr - 0xA000) + (_ramBank * 0x2000);
            ramAddr %= RAM.Length;
            RAM[ramAddr] = value;
            //Debug.Log($"Cartridge: RAM bank {_ramBank} at 0x{addr:X4} set to 0x{value:X2}");
        }

        private void HandleBanking(ushort addr, byte value)
        {
            switch (_cartridgeType)
            {
                case CartridgeType.MBC1:
                case CartridgeType.MBC1_RAM:
                case CartridgeType.MBC1_RAM_BATTERY:
                    HandleMBC1Banking(addr, value);
                    break;
                default:
                    // MBC0 does not handle banking
                    Debug.LogWarning($"Cartridge: Attempted to handle banking on unsupported cartridge type {_cartridgeType}");
                    break;
            }
        }

        private void HandleMBC1Banking(ushort addr, byte value)
        {
            if (addr < 0x2000)
            {
                // RAM Enable
                bool enable = (value & 0x0F) == 0x0A;
                _ramEnabled = enable;
                //Debug.Log($"Cartridge: RAM enable set to {_ramEnabled}");
            }
            else if (addr < 0x4000)
            {
                // Set lower 5 bits of ROM bank number
                int newRomBank = value & 0x1F;
                if (newRomBank == 0)
                    newRomBank = 1;
                _romBank = newRomBank;
                //Debug.Log($"Cartridge: Lower ROM bank set to {_romBank}");
            }
            else if (addr < 0x6000)
            {
                if (_bankingMode == 0)
                {
                    // ROM Banking Mode: set upper 2 bits of ROM bank number
                    int newRomBank = (_romBank & 0x1F) | ((value & 0x03) << 5);
                    if ((newRomBank & 0x1F) == 0)
                        newRomBank |= 1;
                    _romBank = newRomBank;
                    //Debug.Log($"Cartridge: Upper ROM bank bits set, new ROM bank = {_romBank}");
                }
                else
                {
                    // RAM Banking Mode: set RAM bank number
                    _ramBank = value & 0x03;
                    //Debug.Log($"Cartridge: RAM bank set to {_ramBank}");
                }
            }
            else if (addr < 0x8000)
            {
                // Change banking mode
                _bankingMode = value & 0x01;
                //Debug.Log($"Cartridge: Banking mode set to {_bankingMode}");
            }
        }
    }
}
