using SDL2;
using SDL2Engine.Core.Input;
using SDL2Engine.Core.Rendering.Interfaces;
using SDL2Engine.Core.Utils;
using SDL2Engine.Core.Windowing.Interfaces;

namespace GbaEmu.Core;

public class Gameboy
{
    private CPU _cpu;
    private MMU _mmu;
    private GPU _gpu;
    private Audio _audio; // Not shown
    private JoyPad _joyPad;

    private CycleMode _cycleMode = CycleMode.FullCycle;
    private bool _goToNextCycle;
    
    // ~60 FPS => ~69905 cycles/frame on real GB
    private const int CYCLES_PER_FRAME = 69905;

    private IRenderService _renderService;
    private IWindowService _windowService;
    
    public bool IsStarted { get; private set; }
    public CPU CPU => _cpu;
    public GPU GPU => _gpu;
    
    public Gameboy(IRenderService render, IWindowService window)
    {
        _renderService = render;
        _windowService = window;
    }

    public enum CycleMode
    {
        FullCycle,
        StepCycle
    }
    
    public void Start(string romPath, CycleMode cycleMode = CycleMode.FullCycle)
    {
        Debug.Log("Gameboy Started: " + romPath);
        IsStarted = true;
        _cycleMode = cycleMode;
        var cart = new Cartridge(romPath);
        _mmu = new MMU(cart);
        _cpu = new CPU(_mmu);
        _gpu = new GPU(_windowService, _renderService, _mmu);
        _audio = new Audio(); // not shown
        _joyPad = new JoyPad();

        // Force BG on, etc. in case the ROM doesn't do it right away:
        // 0x91 => 1001_0001 binary => LCD on, BG on, OBJ on, etc. 
        _mmu.WriteByte(0xFF40, 0x91);

        // Simple DMG palette (BGP). Bits are 2-bits per color, from right to left:
        // For example: 0xE4 => 11100100 => color0=00(white), color1=01(light), color2=11(darker), color3=11(darkest).
        // Choose your favorite. Let's do 0xFC => 11111100 => color0=00(white), color1=11, color2=11, color3=11(black)
        _mmu.WriteByte(0xFF47, 0xFC);

        _audio.Initialize();
    }
    
    public void Update(float deltaTime)
    {
        if (!IsStarted) return;

        if (_cycleMode == CycleMode.FullCycle)
        {
            int cyclesSoFar = 0;
            while (cyclesSoFar < CYCLES_PER_FRAME)
            {
                int usedCycles = StepCycle();
                cyclesSoFar += usedCycles;
            }
        }
    }

    public int StepCycle(bool logOpcode = false)
    {
        int usedCycles = _cpu.Step(logOpcode);
        _gpu.UpdateGraphics(usedCycles);
        _audio.UpdateAudio(usedCycles);
        return usedCycles;
    }

    public void Render()
    {
        if (!IsStarted) return;
        _gpu.Render();
    }

    public void ShutDown()
    {
        if (!IsStarted) return;
        _gpu.Dispose();
    }
}