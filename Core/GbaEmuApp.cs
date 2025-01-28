using SDL2Engine.Core;
using System;
using GbaEmu.Core.Gui;
using GbaEmu.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using SDL2Engine.Core.Rendering.Interfaces;
using SDL2Engine.Core.Windowing.Interfaces;

namespace GbaEmu.Core
{
    public class GbaEmuApp : IGame
    {
        private IRenderService _renderService;
        private IWindowService _windowService;
        
        private Gameboy _gameboy;

        private OpcodeWindow _opcodeWindow;
        private RomWindow _romWindow;
        public void Initialize(IServiceProvider serviceProvider)
        {
            _renderService = serviceProvider.GetService<IRenderService>() ?? throw new Exception();
            _windowService = serviceProvider.GetService<IWindowService>() ?? throw new Exception();

            _gameboy = new Gameboy(_renderService, _windowService);
            
            _romWindow = new RomWindow(_gameboy);
            _opcodeWindow = new OpcodeWindow(_gameboy);
        }

        public void Update(float deltaTime)
        {
            _gameboy.Update(deltaTime);
        }
        
        public void Render()
        {
            _gameboy.Render();
        }

        public void RenderGui()
        {
            _romWindow.ShowWindow();
            _opcodeWindow.ShowWindow();
        }

        public void Shutdown()
        {
        }
    }
}
