// JoyPad.cs
using SDL2;

namespace GbaEmu.Core
{
    public class JoyPad
    {
        public bool Up, Down, Left, Right, A, B, Start, Select;

        public void HandleInput()
        {
            while (SDL.SDL_PollEvent(out SDL.SDL_Event e) != 0)
            {
                if (e.type == SDL.SDL_EventType.SDL_KEYDOWN)
                {
                    switch (e.key.keysym.sym)
                    {
                        case SDL.SDL_Keycode.SDLK_UP: Up = true; break;
                        case SDL.SDL_Keycode.SDLK_DOWN: Down = true; break;
                        case SDL.SDL_Keycode.SDLK_LEFT: Left = true; break;
                        case SDL.SDL_Keycode.SDLK_RIGHT: Right = true; break;
                        case SDL.SDL_Keycode.SDLK_a: A = true; break;
                        case SDL.SDL_Keycode.SDLK_s: B = true; break;
                        case SDL.SDL_Keycode.SDLK_RETURN: Start = true; break;
                        case SDL.SDL_Keycode.SDLK_SPACE: Select = true; break;
                    }
                }
                else if (e.type == SDL.SDL_EventType.SDL_KEYUP)
                {
                    switch (e.key.keysym.sym)
                    {
                        case SDL.SDL_Keycode.SDLK_UP: Up = false; break;
                        case SDL.SDL_Keycode.SDLK_DOWN: Down = false; break;
                        case SDL.SDL_Keycode.SDLK_LEFT: Left = false; break;
                        case SDL.SDL_Keycode.SDLK_RIGHT: Right = false; break;
                        case SDL.SDL_Keycode.SDLK_a: A = false; break;
                        case SDL.SDL_Keycode.SDLK_s: B = false; break;
                        case SDL.SDL_Keycode.SDLK_RETURN: Start = false; break;
                        case SDL.SDL_Keycode.SDLK_SPACE: Select = false; break;
                    }
                }
                else if (e.type == SDL.SDL_EventType.SDL_QUIT)
                {
                    // Handle exit if needed
                }
            }
        }
    }
}
