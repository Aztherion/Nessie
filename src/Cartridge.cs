using System.IO;
using System.Runtime.InteropServices;

namespace Nessie
{
    public unsafe class Cartridge
    {
        private byte[] _prgMemory;
        private byte[] _chrMemory;

        byte mapperId = 0;
        byte numberOfPrgBanks = 0;
        byte numberOfChrBanks = 0;

        private Mapper _mapper;

        public Mirror Mirror;

        public Cartridge(string filename)
        {
            Load(filename);
        }

        public bool CpuWrite(ushort address, byte data) 
        {
            uint mappedAddress = 0x0;
            if (_mapper.CpuMapWrite(address, ref mappedAddress))
            {
                _prgMemory[mappedAddress] = data;
                return true;
            }
            return false;
        }

        public bool CpuRead(ushort address, ref byte data) 
        {
            uint mappedAddress = 0x0;
            if (_mapper.CpuMapRead(address, ref mappedAddress))
            {
                data = _prgMemory[mappedAddress];
                return true;
            }
            return false;
        }

        public bool PpuWrite(ushort address, byte data) 
        {
            uint mappedAddress = 0x0;
            if (_mapper.PpuMapWrite(address, ref mappedAddress))
            {
                _chrMemory[mappedAddress] = data;
                return true;
            }
            return false;
        }

        public bool PpuRead(ushort address, ref byte data) 
        {
            uint mappedAddress = 0x0;
            if (_mapper.PpuMapRead(address, ref mappedAddress))
            {
                data = _chrMemory[mappedAddress];
                return true;
            }
            return false;
        }

        private void Load(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                var offset = 0;
                var headerBuffer = new byte[16];
                fs.Read(headerBuffer, 0, 16);
                offset += 16;
                var header = GetHeader(headerBuffer);
                if ((byte)(header.mapper1 & 0x04) > 0)
                {
                    offset += 512;
                }

                mapperId = (byte)(((header.mapper2 >> 4) << 4) | (header.mapper1 >> 4));
                Mirror = (header.mapper1 & 0x01) == 0x01 ? Mirror.Vertical : Mirror.Horizontal;

                byte fileType = 1;

                if (fileType == 0) { }

                if (fileType == 1)
                {
                    numberOfPrgBanks = header.prgRomChunks;
                    _prgMemory = new byte[numberOfPrgBanks * 16384];
                    fs.Read(_prgMemory, 0, _prgMemory.Length);
                    offset += _prgMemory.Length;

                    numberOfChrBanks = header.chrRomChunks;
                    _chrMemory = new byte[numberOfChrBanks * 8192];
                    fs.Read(_chrMemory, 0, _chrMemory.Length);
                    offset += _chrMemory.Length;
                }

                switch (mapperId)
                {
                    case 0:
                        _mapper = new Mapper_000(numberOfPrgBanks, numberOfChrBanks);
                        break;
                }
            }
        }

        private NESFileHeader GetHeader(byte[] data)
        {
            var pinnedHeader = GCHandle.Alloc(data, GCHandleType.Pinned);
            NESFileHeader header = (NESFileHeader)Marshal.PtrToStructure(pinnedHeader.AddrOfPinnedObject(), typeof(NESFileHeader));
            pinnedHeader.Free();
            return header;
        }
    }

    unsafe struct NESFileHeader
    {
        public fixed byte Name[4];
        public byte prgRomChunks;
        public byte chrRomChunks;
        public byte mapper1;
        public byte mapper2;
        public byte prgRamSize;
        public byte tvSystem1;
        public byte tvSystem2;
        public fixed byte unused[5];
    }

    public enum Mirror
    {
        Horizontal,
        Vertical,
        OneScreenLow,
        OneScreenHigh
    }
}
