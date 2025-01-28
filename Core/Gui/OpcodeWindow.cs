// OpcodeWindow.cs
using ImGuiNET;
using System.Numerics;

namespace GbaEmu.Core.Gui
{
    public class OpcodeWindow
    {
        private readonly Gameboy _gameboy;

        public OpcodeWindow(Gameboy gameboy)
        {
            _gameboy = gameboy;
        }

        public void ShowWindow()
        {
            if (!_gameboy.IsStarted) return;
            
            if (ImGui.Begin("Opcode Debugger"))
            {
                // You can limit how many rows you show or use scrolling.
                // Here we show all 64KB in a 16-column table.
                if (ImGui.BeginTable("MemTable", 16, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                {
                    // Optional: limit table height
                    ImGui.TableSetupScrollFreeze(0, 1);

                    for (int row = 0; row < 4096; row++)
                    {
                        ImGui.TableNextRow();
                        for (int col = 0; col < 16; col++)
                        {
                            ImGui.TableSetColumnIndex(col);
                            ushort addr = (ushort)(row * 16 + col);
                            byte val = _gameboy.CPU._mmu.ReadByte(addr);

                            // Highlight the cell if it's the current PC
                            if (addr == _gameboy.CPU.PC)
                            {
                                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0, 0, 1)); // red text
                                ImGui.Text($"{val:X2}");
                                ImGui.PopStyleColor();
                            }
                            else
                            {
                                ImGui.Text($"{val:X2}");
                            }
                        }
                    }
                    ImGui.EndTable();
                }
                ImGui.End();
            }
        }
    }
}