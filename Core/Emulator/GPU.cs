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

        // Public fields (still accessible if you want them)
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

            // _mmu.WriteByte(0xFF40, 0x91); 
            // // SCY=0, SCX=0
            // _mmu.WriteByte(0xFF42, 0x00);
            // _mmu.WriteByte(0xFF43, 0x00);
            //
            // // Simple tile #0 in VRAM => a solid block of 'color 3' for each pixel
            // byte[] tile0 =
            // {
            //     0xFF, 0xFF, // row0
            //     0xFF, 0xFF, // row1
            //     0xFF, 0xFF,
            //     0xFF, 0xFF,
            //     0xFF, 0xFF,
            //     0xFF, 0xFF,
            //     0xFF, 0xFF,
            //     0xFF, 0xFF
            // };
            // for (int i = 0; i < 16; i++)
            //     _mmu.WriteByte((ushort)(0x8000 + i), tile0[i]);
            //
            // // Fill BG map area with tile #0
            // for (int i = 0; i < 32 * 32; i++)
            //     _mmu.WriteByte((ushort)(0x9800 + i), 0x00);
        }

        public void UpdateGraphics(int cycles)
        {
            // Read register values from memory each frame so we actually use them
            LCDC = _mmu.ReadByte(0xFF40);
            SCY  = _mmu.ReadByte(0xFF42);
            SCX  = _mmu.ReadByte(0xFF43);
            // If you want to check LYC or others, do so here:
            // LYC = _mmu.ReadByte(0xFF45);

            _scanlineCounter += cycles;
            if (_scanlineCounter >= SCANLINE_CYCLES)
            {
                _scanlineCounter -= SCANLINE_CYCLES;
                LY++;
                if (LY == 144)
                {
                    SetMode(MODE_VBLANK);
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
                if (modeClock < 80)
                    SetMode(MODE_OAM);
                else if (modeClock < 172)
                    SetMode(MODE_VRAM);
                else
                    SetMode(MODE_HBLANK);
            }
        }

        private void RenderScanline(byte line)
        {
            // If BG disabled in LCDC, skip or fill blank. We assume BG on.
            // Check bit 0 of LCDC if you want: if ((LCDC & 0x01) == 0)...

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

                int tileAddr = tileDataBase + (tileIndex * 16);
                int tileY = bgY % 8;
                int tileRowAddr = tileAddr + tileY * 2;

                byte lowBits = _mmu.ReadByte((ushort)tileRowAddr);
                byte highBits = _mmu.ReadByte((ushort)(tileRowAddr + 1));

                int bitIndex = 7 - (bgX % 8);

                int colorId = ((highBits >> bitIndex) & 1) << 1;
                colorId |= ((lowBits >> bitIndex) & 1);

                uint color;
                switch (colorId)
                {
                    case 0:  color = 0xFFCCCCCC; break; // lightest
                    case 1:  color = 0xFF999999; break;
                    case 2:  color = 0xFF666666; break;
                    case 3:  color = 0xFF000000; break; // darkest
                    default: color = 0xFF000000; break;
                }

                _frameBuffer[line * GB_WIDTH + x] = color;
            }
        }

        public unsafe void Render()
        {
            // Just copy our framebuffer to the SDL texture
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

        private void SetMode(byte mode)
        {
            // Clear lower 2 bits of STAT, then set mode
            STAT = (byte)(STAT & 0xFC);
            STAT |= mode;
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
