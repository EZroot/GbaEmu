// GbaEmuApp.cs
using SDL2Engine.Core;
using System;
using GbaEmu.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using SDL2Engine.Core.Rendering.Interfaces;
using SDL2Engine.Core.Windowing.Interfaces;

namespace GbaEmu.Core
{
    public class GbaEmuApp : IGame
    {
        private CPU _cpu;
        private MMU _mmu;
        private GPU _gpu;
        private Audio _audio; // Not shown, but referenced
        private JoyPad _joyPad;

        private const int GB_CPU_FREQ = 4194304;
        // If your engine calls Update() ~60 times/second, that's ~69905 cycles each update
        private const int CYCLES_PER_FRAME = 69905;

        public void Initialize(IServiceProvider serviceProvider)
        {
            // TODO: Set a valid ROM path here
            var romPath = EmuInfo.ROM_PATH + EmuInfo.HELLO_WORLD_PATH;//EmuInfo.TEST_CPU_INSTRS_PATH;
            if (string.IsNullOrEmpty(romPath))
                throw new ArgumentException("No ROM path specified.");

            var renderService = serviceProvider.GetService<IRenderService>() ?? throw new Exception();
            var windowService  = serviceProvider.GetService<IWindowService>() ?? throw new Exception();
            var cart = new Cartridge(romPath);
            _mmu = new MMU(cart);
            _cpu = new CPU(_mmu);
            _gpu = new GPU(windowService, renderService, _mmu);
            _audio = new Audio();
            _joyPad = new JoyPad();

            _audio.Initialize();
        }

        public void Update(float deltaTime)
        {
            int cyclesSoFar = 0;
            while (cyclesSoFar < CYCLES_PER_FRAME)
            {
                // Step the CPU
                int usedCycles = _cpu.Step();

                // Update GPU with actual cycles used
                _gpu.UpdateGraphics(usedCycles);

                // Update audio with actual cycles used
                _audio.UpdateAudio(usedCycles);

                cyclesSoFar += usedCycles;
            }
        }

        public void Render()
        {
            _gpu.Render();
        }

        public void RenderGui()
        {
            // GUI stuff if needed
        }

        public void Shutdown()
        {
            _gpu.Dispose();
            // Dispose other resources if needed
        }
    }
}