namespace Nessie
{
    public abstract class Mapper
    {
        internal readonly byte _prgBanks;
        internal readonly byte _chrBanks;

        public Mapper(byte prgBanks, byte chrBanks)
        {
            _prgBanks = prgBanks;
            _chrBanks = chrBanks;
        }

        public virtual bool CpuMapRead(ushort address, ref uint mappedAddress) 
        {
            return false;
        }

        public virtual bool CpuMapWrite(ushort address, ref uint mappedAddress)
        {
            return false;
        }

        public virtual bool PpuMapRead(ushort address, ref uint mappedAddress)
        {
            return false;
        }

        public virtual bool PpuMapWrite(ushort address, ref uint mappedAddress)
        {
            return false;
        }

        public virtual void Reset() { }
    }
}

