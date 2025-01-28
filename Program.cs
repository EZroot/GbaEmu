using GbaEmu.Core;
using SDL2Engine.Core;

public static class Program
{
    public static void Main()
    {
        var app = new GameApp();
        app.Run(new GbaEmuApp());
    }
}