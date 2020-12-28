namespace Nessie
{
    public struct PpuControlRegister
    {
        public bool V { get; set; }
        public bool P { get; set; }
        public bool H { get; set; }
        public bool B { get; set; }
        public bool S { get; set; }
        public bool I { get; set; }
        public byte NameTableSelect { get; set; }

        public void Set(byte b)
        {
            V = IsBitSet(b, 7);
            P = IsBitSet(b, 6);
            H = IsBitSet(b, 5);
            B = IsBitSet(b, 4);
            S = IsBitSet(b, 3);
            I = IsBitSet(b, 2);
            NameTableSelect = (byte)(IsBitSet(b, 1) ? 1 : 0);
            NameTableSelect = (byte)(NameTableSelect << 1);
            NameTableSelect += (byte)(IsBitSet(b, 0) ? 1 : 0);
        }

        public byte Get()
        {
            byte b = 0;
            b = (byte)(V ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(P ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(H ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(B ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(S ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(I ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(IsBitSet(NameTableSelect, 1) ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(IsBitSet(NameTableSelect, 0) ? 1 : 0);
            return b;
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
