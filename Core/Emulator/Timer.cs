namespace GbaEmu.Core;

public class Timer
{
    private readonly MMU _mmu;

    // DIV increments every 256 cycles
    // TIMA increments depending on TAC frequency
    private int _divCounter;
    private int _timerCounter;

    public Timer(MMU mmu)
    {
        _mmu = mmu;
    }

    public void Update(int cycles)
    {
        // Update DIV
        _divCounter += cycles;
        while (_divCounter >= 256)
        {
            _divCounter -= 256;
            byte div = _mmu.ReadByte(0xFF04); // DIV
            div++;
            _mmu.WriteByte(0xFF04, div);
        }

        // Check if timer is enabled
        byte tac = _mmu.ReadByte(0xFF07);
        bool timerEnabled = (tac & 0x04) != 0;
        if (!timerEnabled) return;

        // Determine freq
        // Bit 0-1 => Input clock select:
        // 00: 4096 Hz; 01: 262144 Hz; 10: 65536 Hz; 11: 16384 Hz
        int freq;
        switch (tac & 0x03)
        {
            case 0: freq = 1024; break; // 4194304 / 4096
            case 1: freq = 16; break; // 4194304 / 262144
            case 2: freq = 64; break; // 4194304 / 65536
            default: freq = 256; break; // 4194304 / 16384
        }

        _timerCounter += cycles;
        while (_timerCounter >= freq)
        {
            _timerCounter -= freq;

            byte tima = _mmu.ReadByte(0xFF05);
            tima++;
            if (tima == 0)
            {
                // Overflow => reload TMA
                tima = _mmu.ReadByte(0xFF06);
                // Also request Timer interrupt (bit 2)
                byte IF = _mmu.ReadByte(0xFF0F);
                IF |= 0x04;
                _mmu.WriteByte(0xFF0F, IF);
            }

            _mmu.WriteByte(0xFF05, tima);
        }
    }
}