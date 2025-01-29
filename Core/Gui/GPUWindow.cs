using ImGuiNET;
using SDL2Engine.Core.Utils;

namespace GbaEmu.Core.Gui;

public class GPUWindow
{
    private readonly Gameboy _gameboy;

    public GPUWindow(Gameboy gameboy)
    {
        _gameboy = gameboy;
    }

    public void ShowWindow()
    {
        if (!_gameboy.IsStarted) return;

        if (ImGui.Begin("GPU Debugger"))
        {
            var gpu = _gameboy.GPU;
            ImGui.Text($"STAT:0x{gpu.STAT:X2} LY:0x{gpu.LY:X2} LYC:0x{gpu.LYC:X2} LCDC:0x{gpu.LCDC:X2} SCX:0x{gpu.SCX:X2} SCY:0x{gpu.SCY:X2}");
            ImGui.End();
        }
    }
}