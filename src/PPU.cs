using System;

namespace Nessie
{
    unsafe public class PPU
    {
        public bool FrameComplete = false;

        private readonly Bus _bus;
        private Cartridge _cartridge;
        private byte[] _tblName = new byte[2048];
        private byte[] _tblPalette = new byte[32];
        private byte[][] _tblPattern = new byte[2][];
        private ushort _cycle;
        private short _scanline = -1;

        private PpuControlRegister _ctrlRegister;
        private PpuMaskRegister _maskRegister;
        private PpuStatusRegister _statusRegister;

        private UInt32[][] _sprNameTables = new uint[2][];
        private UInt32[][] _sprPatternTables = new UInt32[2][];
        private UInt32[][] _sprFrames = new UInt32[2][];

        private byte _activeFrame = 0;
        Random rnd = new Random();

        private NesPixel[] _palScreen = new NesPixel[0x40];

        public PPU() 
        {
            InitPalScreen();
            InitSprFrames();
            InitSprPatternTables();
            InitSprNameTables();
            _tblPattern[0] = new byte[4096];
            _tblPattern[1] = new byte[4096];
        }

        private void InitPalScreen()
        {
            _palScreen[0x00] = new NesPixel(84, 84, 84);
            _palScreen[0x01] = new NesPixel(0, 30, 116);
            _palScreen[0x02] = new NesPixel(8, 16, 144);
            _palScreen[0x03] = new NesPixel(48, 0, 136);
            _palScreen[0x04] = new NesPixel(68, 0, 100);
            _palScreen[0x05] = new NesPixel(92, 0, 48);
            _palScreen[0x06] = new NesPixel(84, 4, 0);
            _palScreen[0x07] = new NesPixel(60, 24, 0);
            _palScreen[0x08] = new NesPixel(32, 42, 0);
            _palScreen[0x09] = new NesPixel(8, 58, 0);
            _palScreen[0x0A] = new NesPixel(0, 64, 0);
            _palScreen[0x0B] = new NesPixel(0, 60, 0);
            _palScreen[0x0C] = new NesPixel(0, 50, 60);
            _palScreen[0x0D] = new NesPixel(0, 0, 0);
            _palScreen[0x0E] = new NesPixel(0, 0, 0);
            _palScreen[0x0F] = new NesPixel(0, 0, 0);

            _palScreen[0x10] = new NesPixel(152, 150, 152);
            _palScreen[0x11] = new NesPixel(8, 76, 196);
            _palScreen[0x12] = new NesPixel(48, 50, 236);
            _palScreen[0x13] = new NesPixel(92, 30, 228);
            _palScreen[0x14] = new NesPixel(136, 20, 176);
            _palScreen[0x15] = new NesPixel(160, 20, 100);
            _palScreen[0x16] = new NesPixel(152, 34, 32);
            _palScreen[0x17] = new NesPixel(120, 60, 0);
            _palScreen[0x18] = new NesPixel(84, 90, 0);
            _palScreen[0x19] = new NesPixel(40, 114, 0);
            _palScreen[0x1A] = new NesPixel(8, 124, 0);
            _palScreen[0x1B] = new NesPixel(0, 118, 40);
            _palScreen[0x1C] = new NesPixel(0, 102, 120);
            _palScreen[0x1D] = new NesPixel(0, 0, 0);
            _palScreen[0x1E] = new NesPixel(0, 0, 0);
            _palScreen[0x1F] = new NesPixel(0, 0, 0);

            _palScreen[0x20] = new NesPixel(236, 238, 236);
            _palScreen[0x21] = new NesPixel(76, 154, 236);
            _palScreen[0x22] = new NesPixel(120, 124, 236);
            _palScreen[0x23] = new NesPixel(176, 98, 236);
            _palScreen[0x24] = new NesPixel(228, 84, 236);
            _palScreen[0x25] = new NesPixel(236, 88, 180);
            _palScreen[0x26] = new NesPixel(236, 106, 100);
            _palScreen[0x27] = new NesPixel(212, 136, 32);
            _palScreen[0x28] = new NesPixel(160, 170, 0);
            _palScreen[0x29] = new NesPixel(116, 196, 0);
            _palScreen[0x2A] = new NesPixel(76, 208, 32);
            _palScreen[0x2B] = new NesPixel(56, 204, 108);
            _palScreen[0x2C] = new NesPixel(56, 180, 204);
            _palScreen[0x2D] = new NesPixel(60, 60, 60);
            _palScreen[0x2E] = new NesPixel(0, 0, 0);
            _palScreen[0x2F] = new NesPixel(0, 0, 0);

            _palScreen[0x30] = new NesPixel(236, 238, 236);
            _palScreen[0x31] = new NesPixel(168, 204, 236);
            _palScreen[0x32] = new NesPixel(188, 188, 236);
            _palScreen[0x33] = new NesPixel(212, 178, 236);
            _palScreen[0x34] = new NesPixel(236, 174, 236);
            _palScreen[0x35] = new NesPixel(236, 174, 212);
            _palScreen[0x36] = new NesPixel(236, 180, 176);
            _palScreen[0x37] = new NesPixel(228, 196, 144);
            _palScreen[0x38] = new NesPixel(204, 210, 120);
            _palScreen[0x39] = new NesPixel(180, 222, 120);
            _palScreen[0x3A] = new NesPixel(168, 226, 144);
            _palScreen[0x3B] = new NesPixel(152, 226, 180);
            _palScreen[0x3C] = new NesPixel(160, 214, 228);
            _palScreen[0x3D] = new NesPixel(160, 162, 160);
            _palScreen[0x3E] = new NesPixel(0, 0, 0);
            _palScreen[0x3F] = new NesPixel(0, 0, 0);
        }
        private void InitSprFrames()
        {
            _sprFrames[0] = new UInt32[342 * 262];
            _sprFrames[1] = new UInt32[342 * 262];
            for(var i = 0; i < 341 * 261; i++)
            {
                _sprFrames[0][i] = 0xFF0000FF;
                _sprFrames[1][i] = 0xFF00FF00;
            }
            _activeFrame = 0;
        }

        private void InitSprPatternTables()
        {
            _sprPatternTables[0] = new uint[128 * 128];
            _sprPatternTables[1] = new uint[128 * 128];
            for(var i = 0; i < 128 * 128; i++)
            {
                _sprPatternTables[0][i] = 0xFF000000;
                _sprPatternTables[1][i] = 0xFF000000;
            }
        }

        private void InitSprNameTables()
        {
            _sprNameTables[0] = new uint[256 * 240];
            _sprNameTables[1] = new uint[256 * 240];
            for(var i = 0; i < 256 * 240; i++)
            {
                _sprNameTables[0][i] = 0xFF000000;
                _sprNameTables[1][i] = 0xFF000000;
            }
        }

        public UInt32[] GetActiveFrame()
        {
            return _sprFrames[_activeFrame];
        }

        public UInt32[] GetPatternTable(int ix, byte palette)
        {
            for (var tileY = 0; tileY < 16; tileY++)
            {
                for(var tileX = 0; tileX < 16; tileX++)
                {
                    var offset = tileY * 256 + tileX * 16;
                    for(var row = 0; row < 8; row++)
                    {
                        byte tileLsb = PpuRead((ushort)(ix * 0x1000 + offset + row + 0));
                        byte tileMsb = PpuRead((ushort)(ix * 0x1000 + offset + row + 8));

                        for (var col = 0; col < 8; col++)
                        {
                            byte pixel = (byte)((tileLsb & 0x01) + (tileMsb & 0x01));
                            tileLsb = (byte)(tileLsb >> 1);
                            tileMsb = (byte)(tileMsb >> 1);

                            var x = tileX * 8 + (7 - col);
                            var y = tileY * 8 + row;

                            var pixelAddress = (128 * y) + x;
                            _sprPatternTables[ix][pixelAddress] = GetColorFromPaletteRam(palette, pixel).ToUInt32();
                        }
                    }
                }
            }
            return _sprPatternTables[ix];
        }

        private NesPixel GetColorFromPaletteRam(byte palette, byte pixel)
        {
            var address = (ushort)(0x3F00 + (palette << 2) + pixel);
            var paletteId = PpuRead(address);
            return _palScreen[paletteId & 0x3F];
        }

        public UInt32[] GetNameTable(int ix)
        {
            return _sprNameTables[ix];
        }

        public void CpuWrite(ushort address, byte data) 
        {
            switch (address)
            {
                case 0x0000: // Control
                    _ctrlRegister.Set(data);
                    break;
                case 0x0001: // Mask
                    _maskRegister.Set(data);
                    break;
                case 0x0002: // Status
                    break;
                case 0x0003: // OEM Address
                    break;
                case 0x0004: // OEM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    if (_addressLatch == 0)
                    {
                        _ppuAddress = (ushort)((_ppuAddress & 0x00FF) | (data << 8));
                        _addressLatch = 1;
                    }
                    else
                    {
                        _ppuAddress = (ushort)((_ppuAddress & 0xFF00) | data);
                        _addressLatch = 0;
                    }
                    break;
                case 0x0007: // PPU Data
                    PpuWrite(_ppuAddress, data);
                    _ppuAddress = (ushort)(_ppuAddress + (_ctrlRegister.I ? 32 : 1));
                    break;
            }
        }

        public byte CpuRead(ushort address) 
        {
            byte data = 0x0;
            switch (address)
            {
                case 0x0000: // Control
                    break;
                case 0x0001: // Mask
                    break;
                case 0x0002: // Status
                    _statusRegister.VerticalBlank = true;
                    data = (byte)((_statusRegister.Get() & 0xE0) | (_statusRegister.Get() & 0x1F));
                    _statusRegister.VerticalBlank = false;
                    _addressLatch = 0;
                    break;
                case 0x0003: // OEM Address
                    break;
                case 0x0004: // OEM Data
                    break;
                case 0x0005: // Scroll
                    break;
                case 0x0006: // PPU Address
                    break;
                case 0x0007: // PPU Data
                    data = _ppuDataBuffer;
                    _ppuDataBuffer = PpuRead(_ppuAddress);
                    if (_ppuAddress >= 0x3F00) data = _ppuDataBuffer;
                    _ppuAddress = (ushort)(_ppuAddress + (_ctrlRegister.I ? 32 : 1));
                    break;
            }

            return data;
        }

        private byte _addressLatch = 0x00;
        private byte _ppuDataBuffer = 0x00;
        private ushort _ppuAddress = 0x0000;

        public void PpuWrite(ushort address, byte data) 
        {
            address &= 0x3FFF;

            if (_cartridge.PpuWrite(address, data))
            {
                return;
            }
            else if (address >= 0x0000 && address <= 0x1FFF)
            {
                _tblPattern[(byte)((address & 0x1000) >> 12)][(ushort)(address & 0x0FFF)] = data;
            }
            else if (address >= 0x2000 && address <= 0x3EFF)
            {

            }
            else if (address >= 0x3F00 && address <= 0x3FFF)
            {
                address = (ushort)(address & 0x001F);
                if (address == 0x0010) address = 0x0000;
                if (address == 0x0014) address = 0x0004;
                if (address == 0x0018) address = 0x0008;
                if (address == 0x001C) address = 0x000C;
                _tblPalette[address] = data;
            }
        }

        public byte PpuRead(ushort address)
        {
            byte data = 0x00;
            address &= 0x3FFF;

            if (_cartridge.PpuRead(address, ref data))
            {
                return data;
            }
            else if (address >= 0x0000 && address <= 0x1FFF)
            {
                data = _tblPattern[(byte)((address & 0x1000) >> 12)][(ushort)(address & 0x0FFF)];
            } 
            else if (address >= 0x2000 && address <= 0x3EFF)
            {

            } 
            else if (address >= 0x3F00 && address <= 0x3FFF)
            {
                address = (ushort)(address & 0x001F);
                if (address == 0x0010) address = 0x0000;
                if (address == 0x0014) address = 0x0004;
                if (address == 0x0018) address = 0x0008;
                if (address == 0x001C) address = 0x000C;
                data = _tblPalette[address];
            }

            return data;
        }

        public void ConnectCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public void Clock() 
        {
            var frameIx = (byte)(_activeFrame == 0 ? 1 : 0);
            if (_cycle <= 256 && _scanline <= 240)
            {
                _sprFrames[frameIx][_cycle + ((_scanline + 1) * 341)] = _palScreen[rnd.Next(0x40)].ToUInt32();
            }
            _cycle++;

            if (_cycle >= 341)
            {
                _cycle = 0;
                _scanline++;
                if (_scanline >= 261)
                {
                    _scanline = -1;
                    FrameComplete = true;
                    _activeFrame = frameIx;
                }
            }
            _statusRegister.VerticalBlank = _scanline > 240;
        }
    }
}
