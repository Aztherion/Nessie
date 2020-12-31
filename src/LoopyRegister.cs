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
        
        public ushort Reg { get { return Get(); } set { Set(value); } }

        public ushort Get()
        {
            ushort s = 0x0000;
            s = (ushort)(s << 3);
            s = (ushort)(s | FineY);
            s = (ushort)(s << 1);
            s = (ushort)(s | (NameTableY ? 1 : 0));
            s = (ushort)(s << 1);
            s = (ushort)(s | (NameTableX ? 1 : 0));
            s = (ushort)(s << 5);
            s = (ushort)(s | CoarseY);
            s = (ushort)(s << 5);
            s = (ushort)(s | CoarseX);
            return s;
        }

        public void Set(ushort s)
        {
            CoarseX = (byte)(s & 0x1F);
            CoarseY = (byte)((s >> 5) & 0x1F);
            NameTableX = IsBitSet(s, 10);
            NameTableY = IsBitSet(s, 11);
            FineY = (byte)((s >> 12) & 0x7);
        }

        private bool IsBitSet(ushort s, int pos)
        {
            return (s & (1 << pos)) != 0;
        }
    }
}
