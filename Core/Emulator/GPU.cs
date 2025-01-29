using System;
using System.Collections.Generic;
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

        // Sprite Rendering Constants
        private const int OAM_START = 0xFE00;
        private const int OAM_SIZE = 0xA0; // 160 bytes for 40 sprites

        private readonly bool _debugMode;

        public GPU(IWindowService windowService, IRenderService renderService, MMU mmu, bool debugMode = false)
        {
            _debugMode = debugMode;
            _mmu = mmu;
            _renderService = renderService;

            if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
                throw new Exception($"SDL could not initialize! SDL_Error: {SDL.SDL_GetError()}");

            SDL.SDL_GetWindowSize(windowService.WindowPtr, out _windowWidth, out _windowHeight);
            Log($"Window Size: {_windowWidth}x{_windowHeight}");

            _texture = SDL.SDL_CreateTexture(
                _renderService.RenderPtr,
                SDL.SDL_PIXELFORMAT_ARGB8888,
                (int)SDL.SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING,
                GB_WIDTH, GB_HEIGHT);

            if (_texture == IntPtr.Zero)
                throw new Exception($"SDL_CreateTexture failed: {SDL.SDL_GetError()}");

            Log("GPU initialized successfully.");
        }

        public void UpdateGraphics(int cycles)
        {
            // Read registers each frame
            LCDC = _mmu.ReadByte(0xFF40);
            SCY  = _mmu.ReadByte(0xFF42);
            SCX  = _mmu.ReadByte(0xFF43);
            LYC  = _mmu.ReadByte(0xFF45);

            if (_debugMode)
            {
                Log($"Registers - LCDC: {LCDC:X2}, SCY: {SCY}, SCX: {SCX}, LYC: {LYC}");
            }

            _scanlineCounter += cycles;
            while (_scanlineCounter >= SCANLINE_CYCLES)
            {
                _scanlineCounter -= SCANLINE_CYCLES;
                LY++;
                Log($"LY incremented to {LY}");

                if (LY == 144)
                {
                    SetMode(MODE_VBLANK);
                    Log("Entered VBLANK mode.");

                    // Request VBlank interrupt
                    byte IF = _mmu.ReadByte(0xFF0F);
                    IF |= 0x01;
                    _mmu.WriteByte(0xFF0F, IF);
                    Log("VBlank interrupt requested.");
                }
                else if (LY > 153)
                {
                    LY = 0;
                    Log("LY reset to 0.");
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
                {
                    SetMode(MODE_OAM);
                }
                else if (modeClock < 252) // 80 + 172
                {
                    SetMode(MODE_VRAM);
                }
                else
                {
                    SetMode(MODE_HBLANK);
                }
            }

            // Log current mode
            if (_debugMode)
            {
                string mode = (STAT & 0x03) switch // Added parentheses here
                {
                    MODE_HBLANK => "HBLANK",
                    MODE_VBLANK => "VBLANK",
                    MODE_OAM => "OAM",
                    MODE_VRAM => "VRAM",
                    _ => "UNKNOWN"
                };
                Log($"Current Mode: {mode}");
            }

            // Check LY=LYC coincidence
            if (LY == LYC)
            {
                STAT |= 0x04; // Coincidence Flag
                // Log($"LY ({LY}) equals LYC ({LYC}): Coincidence flag set.");

                // Request STAT interrupt if enabled
                if ((STAT & 0x40) != 0)
                {
                    byte IF = _mmu.ReadByte(0xFF0F);
                    IF |= 0x02;
                    _mmu.WriteByte(0xFF0F, IF);
                    Log("STAT interrupt requested due to LY=LYC.");
                }
            }
            else
            {
                if ((STAT & 0x04) != 0)
                {
                    STAT &= 0xFB; // Reset Coincidence Flag
                    Log($"LY ({LY}) does not equal LYC ({LYC}): Coincidence flag reset.");
                }
            }
        }

        private void RenderScanline(byte line)
        {
            Log($"Rendering scanline {line}.");

            // Clear the current scanline in the framebuffer
            for (int x = 0; x < GB_WIDTH; x++)
                _frameBuffer[line * GB_WIDTH + x] = 0xFF000000; // Transparent or Black

            // Render Background
            bool bgEnabled = (LCDC & 0x01) != 0;
            if (bgEnabled)
            {
                Log("Background rendering enabled.");
                RenderBackground(line);
            }
            else
            {
                Log("Background rendering disabled.");
            }

            // Render Window
            bool windowEnabled = (LCDC & 0x20) != 0;
            if (windowEnabled)
            {
                Log("Window rendering enabled.");
                RenderWindow(line);
            }
            else
            {
                Log("Window rendering disabled.");
            }

            // Render Sprites
            bool objEnabled = (LCDC & 0x02) != 0;
            if (objEnabled)
            {
                Log("Sprite rendering enabled.");
                RenderSprites(line);
            }
            else
            {
                Log("Sprite rendering disabled.");
            }
        }

        private void RenderBackground(byte line)
        {
            // Read the BGP palette
            byte bgp = _mmu.ReadByte(0xFF47);
            Log($"Background Palette (BGP): {bgp:X2}");

            // Extract color shades from BGP
            uint[] palette = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                int shade = (bgp >> (i * 2)) & 0x03;
                palette[i] = shade switch
                {
                    0 => 0xFFFFFFFF, // White
                    1 => 0xFFAAAAAA, // Light Gray
                    2 => 0xFF555555, // Dark Gray
                    3 => 0xFF000000, // Black
                    _ => 0xFF000000
                };
                Log($"Background Palette Color {i}: {palette[i]:X8}");
            }

            // Select BG tile map
            bool bgMapSelect = (LCDC & 0x08) != 0;
            ushort bgMapBase = bgMapSelect ? (ushort)0x9C00 : (ushort)0x9800;
            Log($"Background Map Base: {bgMapBase:X4}");

            // Select tile data block
            bool tileDataSelect = (LCDC & 0x10) != 0;
            ushort tileDataBase = tileDataSelect ? (ushort)0x8000 : (ushort)0x9000;
            Log($"Tile Data Base: {tileDataBase:X4} (Tile Data Select: {(tileDataSelect ? "0x8000" : "0x9000")})");

            byte bgY = (byte)((SCY + line) & 0xFF);
            int tileRow = bgY / 8;
            Log($"Background Y: {bgY}, Tile Row: {tileRow}");

            for (int x = 0; x < GB_WIDTH; x++)
            {
                byte bgX = (byte)((SCX + x) & 0xFF);
                int tileCol = bgX / 8;
                ushort mapAddr = (ushort)(bgMapBase + tileRow * 32 + tileCol);
                byte tileIndex = _mmu.ReadByte(mapAddr);

                ushort tileAddr;
                if (!tileDataSelect)
                {
                    // Handle signed tile indices
                    sbyte signedTileIndex = (sbyte)tileIndex;
                    tileAddr = (ushort)(tileDataBase + (signedTileIndex * 16));
                }
                else
                {
                    // Unsigned tile indices
                    tileAddr = (ushort)(tileDataBase + (tileIndex * 16));
                }

                byte tileY = (byte)(bgY % 8);
                ushort tileRowAddr = (ushort)(tileAddr + tileY * 2);

                // Read two bytes for that tile row
                byte lowBits = _mmu.ReadByte(tileRowAddr);
                byte highBits = _mmu.ReadByte((ushort)(tileRowAddr + 1));

                int bitIndex = 7 - (bgX % 8);
                // Combine to get color index 0..3
                int colorId = ((highBits >> bitIndex) & 1) << 1;
                colorId |= ((lowBits >> bitIndex) & 1);

                // Get ARGB color from the palette
                uint color = palette[colorId];
                _frameBuffer[line * GB_WIDTH + x] = color;

                if (_debugMode)
                {
                    Log($"BG Pixel ({x},{line}): TileIdx={tileIndex}, TileAddr={tileAddr:X4}, TileY={tileY}, BitIdx={bitIndex}, ColorID={colorId}, Color={color:X8}");
                }
            }
        }

        private void RenderWindow(byte line)
        {
            // Read the BGP palette (assuming window uses BG palette)
            byte bgp = _mmu.ReadByte(0xFF47);
            Log($"Window Palette (BGP): {bgp:X2}");

            // Extract color shades from BGP
            uint[] palette = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                int shade = (bgp >> (i * 2)) & 0x03;
                palette[i] = shade switch
                {
                    0 => 0xFFFFFFFF, // White
                    1 => 0xFFAAAAAA, // Light Gray
                    2 => 0xFF555555, // Dark Gray
                    3 => 0xFF000000, // Black
                    _ => 0xFF000000
                };
                Log($"Window Palette Color {i}: {palette[i]:X8}");
            }

            // Window Position
            byte wy = _mmu.ReadByte(0xFF4A);
            byte wx = _mmu.ReadByte(0xFF4B);
            Log($"Window Position - WX: {wx}, WY: {wy}");

            if (line < wy)
            {
                Log($"Current line {line} is above the window (WY: {wy}). Skipping window rendering.");
                return; // Current line is above the window
            }

            // Select Window tile map
            bool windowMapSelect = (LCDC & 0x40) != 0;
            ushort windowMapBase = windowMapSelect ? (ushort)0x9C00 : (ushort)0x9800;
            Log($"Window Map Base: {windowMapBase:X4}");

            // Select tile data block (same as BG)
            bool tileDataSelect = (LCDC & 0x10) != 0;
            ushort tileDataBase = tileDataSelect ? (ushort)0x8000 : (ushort)0x9000;
            Log($"Tile Data Base for Window: {tileDataBase:X4} (Tile Data Select: {(tileDataSelect ? "0x8000" : "0x9000")})");

            byte windowY = (byte)(line - wy);
            int tileRow = windowY / 8;
            Log($"Window Y: {windowY}, Tile Row: {tileRow}");

            for (int x = 0; x < GB_WIDTH; x++)
            {
                if (x < (wx - 7))
                    continue; // Current pixel is left of the window

                byte windowX = (byte)(x - (wx - 7));
                int tileCol = windowX / 8;
                ushort mapAddr = (ushort)(windowMapBase + tileRow * 32 + tileCol);
                byte tileIndex = _mmu.ReadByte(mapAddr);

                ushort tileAddr;
                if (!tileDataSelect)
                {
                    // Handle signed tile indices
                    sbyte signedTileIndex = (sbyte)tileIndex;
                    tileAddr = (ushort)(tileDataBase + (signedTileIndex * 16));
                }
                else
                {
                    // Unsigned tile indices
                    tileAddr = (ushort)(tileDataBase + (tileIndex * 16));
                }

                byte tileY = (byte)(windowY % 8);
                ushort tileRowAddr = (ushort)(tileAddr + tileY * 2);

                // Read two bytes for that tile row
                byte lowBits = _mmu.ReadByte(tileRowAddr);
                byte highBits = _mmu.ReadByte((ushort)(tileRowAddr + 1));

                int bitIndex = 7 - (windowX % 8);
                // Combine to get color index 0..3
                int colorId = ((highBits >> bitIndex) & 1) << 1;
                colorId |= ((lowBits >> bitIndex) & 1);

                // Get ARGB color from the palette
                uint color = palette[colorId];
                _frameBuffer[line * GB_WIDTH + x] = color;

                if (_debugMode)
                {
                    Log($"Window Pixel ({x},{line}): TileIdx={tileIndex}, TileAddr={tileAddr:X4}, TileY={tileY}, BitIdx={bitIndex}, ColorID={colorId}, Color={color:X8}");
                }
            }
        }

        private void RenderSprites(byte line)
        {
            // Read OAM
            List<Sprite> sprites = new List<Sprite>();

            for (int i = 0; i < 40; i++)
            {
                int oamIndex = OAM_START + i * 4;
                byte y = _mmu.ReadByte((ushort)oamIndex);
                byte x = _mmu.ReadByte((ushort)(oamIndex + 1));
                byte tileIndex = _mmu.ReadByte((ushort)(oamIndex + 2));
                byte attributes = _mmu.ReadByte((ushort)(oamIndex + 3));

                // Skip sprites with x = 0 (not visible)
                if (x == 0)
                    continue;

                Sprite sprite = new Sprite
                {
                    Y = (byte)(y - 16), // Adjust Y position as per Game Boy specs
                    X = (byte)(x - 8),  // Adjust X position as per Game Boy specs
                    TileIndex = tileIndex,
                    Attributes = attributes
                };

                sprites.Add(sprite);
            }

            Log($"Total Sprites Found on Line {line}: {sprites.Count}");

            // Sort sprites by X, then by OAM index (already in order)
            sprites.Sort((a, b) => a.X.CompareTo(b.X));

            // Sprite size
            bool spriteSize = (LCDC & 0x04) != 0; // 0: 8x8, 1: 8x16
            int spriteHeight = spriteSize ? 16 : 8;
            Log($"Sprite Size: {(spriteSize ? "8x16" : "8x8")}");

            foreach (var sprite in sprites)
            {
                // Check if the sprite is on the current line
                if (line < sprite.Y || line >= (sprite.Y + spriteHeight))
                    continue;

                Log($"Rendering Sprite at (X: {sprite.X}, Y: {sprite.Y}), TileIdx: {sprite.TileIndex}, Attributes: {sprite.Attributes:X2}");

                // Calculate tile index for 8x16 sprites
                byte actualTileIndex = sprite.TileIndex;
                if (spriteSize && (line - sprite.Y) >= 8)
                {
                    actualTileIndex = (byte)(sprite.TileIndex & 0xFE); // Even tile index for second tile
                }

                // Calculate tile Y position
                byte tileY = (byte)((line - sprite.Y) % (spriteSize ? 16 : 8));

                ushort tileDataBase = ((LCDC & 0x10) != 0) ? (ushort)0x8000 : (ushort)0x9000;

                ushort tileAddr = (ushort)(tileDataBase + (actualTileIndex * 16));

                if ((LCDC & 0x10) == 0)
                {
                    // Handle signed tile indices
                    sbyte signedTileIndex = (sbyte)sprite.TileIndex;
                    tileAddr = (ushort)(0x9000 + (signedTileIndex * 16));
                }

                ushort tileRowAddr = (ushort)(tileAddr + tileY * 2);

                // Read two bytes for that tile row
                byte lowBits = _mmu.ReadByte(tileRowAddr);
                byte highBits = _mmu.ReadByte((ushort)(tileRowAddr + 1));

                if (_debugMode)
                {
                    Log($"Sprite Tile Address: {tileAddr:X4}, Tile Row Address: {tileRowAddr:X4}, LowBits: {lowBits:X2}, HighBits: {highBits:X2}");
                }

                // Handle sprite flipping
                bool xFlip = sprite.XFlip;
                bool yFlip = sprite.YFlip;

                for (int xPixel = 0; xPixel < 8; xPixel++)
                {
                    int spritePixel = xFlip ? (7 - xPixel) : xPixel;
                    int pixelX = sprite.X + xPixel;

                    if (pixelX < 0 || pixelX >= GB_WIDTH)
                        continue; // Pixel out of screen

                    // Combine to get color index 0..3
                    int colorId = ((highBits >> spritePixel) & 1) << 1;
                    colorId |= ((lowBits >> spritePixel) & 1);

                    if (colorId == 0)
                        continue; // Transparent pixel

                    // Get color from OBJ palette
                    byte paletteNumber = (byte)((sprite.Attributes & 0x10) >> 4); // OBP0 or OBP1
                    byte objPalette = paletteNumber == 0 ? _mmu.ReadByte(0xFF48) : _mmu.ReadByte(0xFF49);

                    uint[] paletteColors = new uint[4];
                    for (int i = 0; i < 4; i++)
                    {
                        int shade = (objPalette >> (i * 2)) & 0x03;
                        paletteColors[i] = shade switch
                        {
                            0 => 0xFFFFFFFF, // White
                            1 => 0xFFAAAAAA, // Light Gray
                            2 => 0xFF555555, // Dark Gray
                            3 => 0xFF000000, // Black
                            _ => 0xFF000000
                        };
                        if (_debugMode)
                        {
                            Log($"OBJ Palette {paletteNumber} Color {i}: {paletteColors[i]:X8}");
                        }
                    }
                    uint color = paletteColors[colorId];

                    // Check for priority
                    bool spriteBehindBG = (sprite.Attributes & 0x80) != 0;
                    if (spriteBehindBG && _frameBuffer[line * GB_WIDTH + pixelX] != 0xFF000000)
                    {
                        if (_debugMode)
                        {
                            Log($"Sprite pixel at ({pixelX},{line}) is behind background. Skipping.");
                        }
                        continue; // Don't overwrite background pixel
                    }

                    _frameBuffer[line * GB_WIDTH + pixelX] = color;

                    if (_debugMode)
                    {
                        Log($"Sprite Pixel ({pixelX},{line}): ColorID={colorId}, Color={color:X8}");
                    }
                }
            }
        }

        private void SetMode(byte mode)
        {
            STAT = (byte)((STAT & 0xFC) | (mode & 0x03));
            if (_debugMode)
            {
                string modeName = (STAT & 0x03) switch // Added parentheses here
                {
                    MODE_HBLANK => "HBLANK",
                    MODE_VBLANK => "VBLANK",
                    MODE_OAM => "OAM",
                    MODE_VRAM => "VRAM",
                    _ => "UNKNOWN"
                };
                // Log($"Set GPU Mode to {modeName} ({mode}). STAT Register: {STAT:X2}");
            }
        }

        public unsafe void Render()
        {
            fixed (uint* ptr = &_frameBuffer[0])
            {
                if (SDL.SDL_UpdateTexture(_texture, IntPtr.Zero, (IntPtr)ptr, GB_WIDTH * sizeof(uint)) < 0)
                {
                    Console.WriteLine($"SDL_UpdateTexture failed: {SDL.SDL_GetError()}");
                    return;
                }
            }

            var dstRect = new SDL.SDL_Rect
            {
                x = 0,
                y = 0,
                w = _windowWidth,
                h = _windowHeight
            };

            if (SDL.SDL_RenderCopy(_renderService.RenderPtr, _texture, IntPtr.Zero, ref dstRect) < 0)
            {
                Console.WriteLine($"SDL_RenderCopy failed: {SDL.SDL_GetError()}");
            }
        }

        public void Dispose()
        {
            if (_texture != IntPtr.Zero)
            {
                SDL.SDL_DestroyTexture(_texture);
                _texture = IntPtr.Zero;
                Log("Texture destroyed.");
            }
            SDL.SDL_Quit();
            Log("SDL Quit and GPU disposed.");
        }

        private void Log(string message)
        {
            //Console.WriteLine($"[GPU DEBUG] {message}");
        }
    }

    public class Sprite
    {
        public byte Y { get; set; } // Y position minus 16
        public byte X { get; set; } // X position minus 8
        public byte TileIndex { get; set; }
        public byte Attributes { get; set; }

        // Extract attributes
        public bool Priority => (Attributes & 0x80) != 0; // Sprite behind BG if set
        public bool YFlip => (Attributes & 0x40) != 0;
        public bool XFlip => (Attributes & 0x20) != 0;
        public byte PaletteNumber => (byte)((Attributes & 0x10) >> 4); // Only in CGB
        public bool PaletteBGOBJ => (Attributes & 0x08) != 0; // OBJ-to-BG Priority
    }
}
