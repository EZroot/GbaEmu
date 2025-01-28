public class Cartridge
{
    public byte[] ROM { get; }
    public byte[] RAM { get; private set; }
    private int currentROMBank = 1;
    private int currentRAMBank = 0;
    private bool ramEnabled = false;
    private int mode = 0; // 0 for ROM banking mode, 1 for RAM banking mode

    public Cartridge(string romPath)
    {
        if (string.IsNullOrEmpty(romPath))
            throw new ArgumentException("ROM path is empty.");
        if (!File.Exists(romPath))
            throw new FileNotFoundException("ROM file not found", romPath);

        ROM = File.ReadAllBytes(romPath);
        RAM = new byte[0x8000]; // Up to 32K of RAM
    }

    public void HandleBanking(int address, byte value)
    {
        if (address < 0x2000)
        {
            // Enable RAM
            ramEnabled = (value & 0x0F) == 0x0A;
        }
        else if (address < 0x4000)
        {
            // Change ROM bank
            currentROMBank = value & 0x1F;
            if (currentROMBank == 0) currentROMBank = 1;
        }
        else if (address < 0x6000)
        {
            // Change RAM bank or ROM bank for upper area
            if (mode == 0)
            {
                currentROMBank = (currentROMBank & 0x1F) | ((value & 0x03) << 5);
            }
            else
            {
                currentRAMBank = value & 0x03;
            }
        }
        else if (address < 0x8000)
        {
            // Change mode
            mode = value & 0x01;
        }
    }
}