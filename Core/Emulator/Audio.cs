namespace GbaEmu.Core;

public class Audio
{
    // Game Boy has four channels. This is a stub.
    public void Initialize()
    {
        // SDL audio init if desired.
    }

    public void UpdateAudio(int cycles)
    {
        // Mix audio samples from channels (Pulse, Wave, Noise).
        // Complex system. Left as an exercise.
    }
}