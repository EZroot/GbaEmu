using GbaEmu.Core.Utils;
using ImGuiNET;
using Debug = SDL2Engine.Core.Utils.Debug;

namespace GbaEmu.Core.Gui;

public class RomWindow
{
    public const string DefaultRomPath = EmuInfo.ROM_PATH + EmuInfo.HELLO_WORLD_PATH;
    
    private Gameboy _gameboy;
    private string _romPathBuffer = DefaultRomPath;
    private bool dirExists;
    public RomWindow(Gameboy gameboy)
    {
        _gameboy = gameboy;
    }

    public void ShowWindow()
    {
        if (ImGui.Begin("RomWindow"))
        {
            if (ImGui.Button("Stop"))
            {
                _gameboy.ShutDown();
            }
            
            if (ImGui.InputText("Rom Path",ref _romPathBuffer, 1024))
            {
                dirExists = Directory.Exists(_romPathBuffer);
            }
            if (dirExists)
            {
                var files = Directory.GetFiles(_romPathBuffer);
                for (var i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    if (!File.Exists(file)) continue;

                    if (ImGui.Button($"Load##{i}"))
                    {
                        _gameboy.Start(file);
                    }

                    ImGui.SameLine();
                    ImGui.Text(file);
                }
            }

            ImGui.End();
        }
    }
}