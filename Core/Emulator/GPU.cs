using System;
using SDL2;
using SDL2Engine.Core.Rendering.Interfaces;
using SDL2Engine.Core.Windowing.Interfaces;

namespace GbaEmu.Core
{
    public class GPU : IDisposable
    {
        private const int GB_WIDTH = 160;
        private const int GB_HEIGHT = 144;
        private const byte MODE_HBLANK = 0;
        private const byte MODE_VBLANK = 1;
        private const byte MODE_OAM = 2;
        private const byte MODE_VRAM = 3;
        private const int SCANLINE_CYCLES = 456;

        private int _scanlineCounter;
        public byte LCDC;
        public byte STAT;
        public byte SCY;
        public byte SCX;
        public byte LY;
        public byte LYC;

        private readonly uint[] _frameBuffer = new uint[GB_WIDTH * GB_HEIGHT];
        private readonly int _windowWidth;
        private readonly int _windowHeight;
        private readonly IRenderService _renderService;
        private IntPtr _texture;
        private readonly MMU _mmu;

        public GPU(IWindowService windowService, IRenderService renderService, MMU mmu)
        {
            _mmu = mmu;
            _renderService = renderService;

            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
                throw new Exception("Could not initialize SDL Video.");

            SDL.SDL_GetWindowSize(windowService.WindowPtr, out _windowWidth, out _windowHeight);

            _texture = SDL.SDL_CreateTexture(
                _renderService.RenderPtr,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                GB_WIDTH, GB_HEIGHT);

            if (_texture == IntPtr.Zero)
                throw new Exception($"SDL_CreateTexture failed: {SDL.SDL_GetError()}");
        }

        public void UpdateGraphics(int cycles)
        {
            // Read registers each frame
            LCDC = _mmu.ReadByte(0xFF40);
            SCY  = _mmu.ReadByte(0xFF42);
            SCX  = _mmu.ReadByte(0xFF43);

            _scanlineCounter += cycles;
            if (_scanlineCounter >= SCANLINE_CYCLES)
            {
                _scanlineCounter -= SCANLINE_CYCLES;
                LY++;
                if (LY == 144)
                {
                    SetMode(MODE_VBLANK);
                    // Request VBlank interrupt
                    byte IF = _mmu.ReadByte(0xFF0F);
                    IF |= 0x01;
                    _mmu.WriteByte(0xFF0F, IF);
                }
                else if (LY > 153)
                {
                    LY = 0;
                }
                if (LY < 144)
                {
                    RenderScanline(LY);
                }
            }

            if (LY >= 144 && LY <= 153)
            {
                SetMode(MODE_VBLANK);
            }
            else
            {
                int modeClock = _scanlineCounter;
                if (modeClock < 80) SetMode(MODE_OAM);
                else if (modeClock < 172) SetMode(MODE_VRAM);
                else SetMode(MODE_HBLANK);
            }
        }

        private void RenderScanline(byte line)
        {
            // If BG is disabled, we'd skip or fill blank. 
            // For now, assume BG is on if bit 0 of LCDC is set:
            bool bgEnabled = (LCDC & 0x01) != 0;
            if (!bgEnabled)
            {
                // Just fill row with white or black
                for (int x = 0; x < GB_WIDTH; x++)
                    _frameBuffer[line * GB_WIDTH + x] = 0xFFFFFFFF; // white
                return;
            }

            // Read the BGP palette
            byte bgp = _mmu.ReadByte(0xFF47);
            // Extract color shades from BGP
            // Bits: [7-6]=color3, [5-4]=color2, [3-2]=color1, [1-0]=color0
            // Each color is two bits 0..3
            // Map them to ARGB
            uint[] palette = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                // shift out the two bits for color i
                int shade = (bgp >> (i * 2)) & 0x03;
                // You can map shade 0 => White, 1 => LightGray, 2 => DarkGray, 3 => Black:
                switch (shade)
                {
                    case 0: palette[i] = 0xFFFFFFFF; break; // White
                    case 1: palette[i] = 0xFFAAAAAA; break; // Light gray
                    case 2: palette[i] = 0xFF555555; break; // Dark gray
                    case 3: palette[i] = 0xFF000000; break; // Black
                }
            }

            // Which BG tile map? Bit 3 => 0x9C00 or 0x9800
            bool bgMapSelect = (LCDC & 0x08) != 0;
            ushort bgMapBase = bgMapSelect ? (ushort)0x9C00 : (ushort)0x9800;

            // Which tile data block? Bit 4 => 0x8000 or 0x8800
            bool tileDataSelect = (LCDC & 0x10) != 0;
            ushort tileDataBase = tileDataSelect ? (ushort)0x8000 : (ushort)0x8800;

            byte bgY = (byte)((SCY + line) & 0xFF);
            int tileRow = bgY / 8;

            for (int x = 0; x < GB_WIDTH; x++)
            {
                byte bgX = (byte)((SCX + x) & 0xFF);
                int tileCol = bgX / 8;
                ushort mapAddr = (ushort)(bgMapBase + tileRow * 32 + tileCol);

                byte tileIndex = _mmu.ReadByte(mapAddr);

                // If using 0x8800 range, tileIndex is signed. Typically:
                // if (!tileDataSelect && tileIndex < 128) tileIndex += 256;
                // For a simple example, skip that detail unless your ROM needs it.

                int tileAddr = tileDataBase + (tileIndex * 16);
                int tileY = bgY % 8;
                int tileRowAddr = tileAddr + tileY * 2;

                // Read 2 bytes for that tile row
                byte lowBits = _mmu.ReadByte((ushort)tileRowAddr);
                byte highBits = _mmu.ReadByte((ushort)(tileRowAddr + 1));

                int bitIndex = 7 - (bgX % 8);
                // Combine to get color index 0..3
                int colorId = ((highBits >> bitIndex) & 1) << 1;
                colorId |= ((lowBits >> bitIndex) & 1);

                // Now get actual ARGB color from the BGP-based palette
                _frameBuffer[line * GB_WIDTH + x] = palette[colorId];
            }
        }

        private void SetMode(byte mode)
        {
            STAT = (byte)(STAT & 0xFC);
            STAT |= mode;
        }

        public unsafe void Render()
        {
            fixed (uint* ptr = &_frameBuffer[0])
            {
                SDL.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, GB_WIDTH * 4);
            }
            var dstRect = new SDL.SDL_Rect
            {
                x = 0,
                y = 0,
                w = _windowWidth,
                h = _windowHeight
            };
            SDL.SDL_RenderCopy(_renderService.RenderPtr, _texture, IntPtr.Zero, ref dstRect);
        }

        public void Dispose()
        {
            if (_texture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_texture);
                _texture = IntPtr.Zero;
            }
        }
    }
}
