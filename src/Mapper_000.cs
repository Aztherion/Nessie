namespace Nessie
{
    public class Mapper_000 : Mapper
    {

        public Mapper_000(byte prgBanks, byte chrBanks) : base(prgBanks, chrBanks) { }

        public override bool CpuMapRead(ushort address, ref uint mappedAddress)
        {
            if (address >= 0x8000 && address <= 0xFFFF)
            {
                mappedAddress = (uint)(address & (ushort)(_prgBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }
            return false;
        }

        public override bool CpuMapWrite(ushort address, ref uint mappedAddress)
        {
            if (address >= 0x8000 && address <= 0xFFFF)
            {
                mappedAddress = (uint)(address & (ushort)(_prgBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }
            return false;
        }

        public override bool PpuMapRead(ushort address, ref uint mappedAddress)
        {
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                mappedAddress = (uint)address;
                return true;
            }
            return false;
        }

        public override bool PpuMapWrite(ushort address, ref uint mappedAddress)
        {
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                if (_chrBanks == 0)
                {
                    mappedAddress = (uint)address;
                    return true;
                }
            }
            return false;
        }

        public override void Reset()
        {
            
        }
    }
}

