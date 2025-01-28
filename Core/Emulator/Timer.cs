namespace GbaEmu.Core
{
    public class Timer
    {
        private readonly MMU _mmu;
        private int _divCounter;
        private int _timerCounter;

        public Timer(MMU mmu)
        {
            _mmu = mmu;
        }

        public void Update(int cycles)
        {
            // Increment DIV every 256 cycles
            _divCounter += cycles;
            while (_divCounter >= 256)
            {
                _divCounter -= 256;
                byte div = _mmu.ReadByte(0xFF04);
                div++;
                _mmu.WriteByte(0xFF04, div);
            }

            // Check TAC for timer enable
            byte tac = _mmu.ReadByte(0xFF07);
            bool timerEnabled = (tac & 0x04) != 0;
            if (!timerEnabled) return;

            int freq;
            switch (tac & 0x03)
            {
                case 0: freq = 1024; break; // 4096 Hz
                case 1: freq = 16;   break; // 262144 Hz
                case 2: freq = 64;   break; // 65536 Hz
                default:freq = 256;  break; // 16384 Hz
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
                    byte IF = _mmu.ReadByte(0xFF0F);
                    IF |= 0x04; // Timer interrupt
                    _mmu.WriteByte(0xFF0F, IF);
                }
                _mmu.WriteByte(0xFF05, tima);
            }
        }
    }
}