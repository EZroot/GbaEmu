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
                if (ImGui.BeginTable("CPU_Registers", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("##RegL");
                    ImGui.TableSetupColumn("##RegR");

                    void TableRow(string leftReg, int leftVal, string rightReg, int rightVal)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.Text($"{leftReg}:{leftVal:X2}");
                        ImGui.TableSetColumnIndex(1);
                        ImGui.Text($"{rightReg}:{rightVal:X2}");
                    }

                    TableRow("A", _gameboy.CPU.A, "E", _gameboy.CPU.E);
                    TableRow("B", _gameboy.CPU.B, "F", _gameboy.CPU.F);
                    TableRow("C", _gameboy.CPU.C, "H", _gameboy.CPU.H);
                    TableRow("D", _gameboy.CPU.D, "L", _gameboy.CPU.L);

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text($"PC: {_gameboy.CPU.PC:X2}");
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text("Intrpt:"+(_gameboy.CPU._interruptMasterEnable ? "Enabled" : "Disabled"));
                    ImGui.EndTable();
                }

                // You can limit how many rows you show or use scrolling.
                // Here we show all 64KB in a 16-column table.
                var log = "";
                foreach (var entry in _gameboy.CPU.OpcodeLogEntries)
                {
                    log += $"PC:{entry.PC} OP:{entry.Opcode}\n";
                }
                ImGui.TextUnformatted(log);
                ImGui.SameLine();
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