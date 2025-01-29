using GbaEmu.Core.Utils;
using ImGuiNET;
using Debug = SDL2Engine.Core.Utils.Debug;

namespace GbaEmu.Core.Gui;

public class RomWindow
{
    public const string DefaultRomPath = EmuInfo.ROM_PATH + EmuInfo.HELLO_WORLD_PATH;//EmuInfo.TEST_CPU_INSTRS_PATH;
    
    private Gameboy _gameboy;
    private string _romPathBuffer = DefaultRomPath;
    private bool _dirExists;
    private Gameboy.CycleMode _cycleMode = Gameboy.CycleMode.StepCycle;
    private int _stepCycle = 69905;
    private int _totalCycles = 0;
    public RomWindow(Gameboy gameboy)
    {
        _gameboy = gameboy;
    }

    public void ShowWindow()
    {
        if (ImGui.Begin("RomWindow"))
        {
            ImGui.Text($"Cycle: {_totalCycles}");
            ImGui.SameLine();
            ImGui.InputInt("##cycles", ref _stepCycle);
            ImGui.SameLine();
            if (ImGui.Button("Step"))
            {
                for (var i = 0; i < _stepCycle; i++)
                {
                    _totalCycles += _gameboy.StepCycle(true);
                }
            }

            if (ImGui.Button("Stop"))
            {
                _gameboy.ShutDown();
            }

            ImGui.SameLine();
            
            if (ImGui.BeginCombo("Cycle Mode", _cycleMode.ToString()))
            {
                foreach (Gameboy.CycleMode mode in System.Enum.GetValues(typeof(Gameboy.CycleMode)))
                {
                    bool isSelected = (mode == _cycleMode);
                    if (ImGui.Selectable(mode.ToString(), isSelected))
                    {
                        _cycleMode = mode;
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            ImGui.Separator();
            ImGui.InputText("Rom Path", ref _romPathBuffer, 1024);
            if (Directory.Exists(_romPathBuffer))
            {
                var files = Directory.GetFiles(_romPathBuffer);
                for (var i = 0; i < files.Length; i++)
                {
                    var file = files[i];
                    if (!File.Exists(file)) continue;

                    if (ImGui.Button($"Load##{i}"))
                    {
                        _gameboy.Start(file,_cycleMode);
                    }

                    ImGui.SameLine();
                    ImGui.Text(file);
                }
            }

        }
        ImGui.End();

    }
}