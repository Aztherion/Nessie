namespace Nessie
{
    public struct LoopyRegister
    {
        // Loopy Rox!
        public byte CoarseX;
        public byte CoarseY;
        public bool NameTableX;
        public bool NameTableY;
        public byte FineY;
        
        public ushort Reg;

        public ushort Get()
        {
            ushort s = 0x0000;
            s = CoarseX;
            s = (ushort)(s << 5);
            s += CoarseY;
            s = (ushort)(s << 5);
            s += (byte)(NameTableX ? 1 : 0);
            s = (ushort)(s << 1);
            s += (byte)(NameTableY ? 1 : 0);
            s = (ushort)(s << 1);
            s += FineY;
            s = (ushort)(s << 4);
            return s;
        }

        public void Set(ushort s)
        {
            CoarseX = (byte)((s >> 11) & 0x1F);
            CoarseY = (byte)((s >> 6) & 0x1F);
            NameTableX = IsBitSet(s, 5);
            NameTableY = IsBitSet(s, 4);
            FineY = (byte)((s >> 1) & 0x7);
        }

        private bool IsBitSet(ushort s, int pos)
        {
            return (s & (1 << pos)) != 0;
        }
    }
}
