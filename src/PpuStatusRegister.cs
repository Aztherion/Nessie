namespace Nessie
{
    public struct PpuStatusRegister
    {
        public bool VerticalBlank { get; set; }
        public bool SpriteZeroHit { get; set; }
        public bool SpriteOverflow { get; set; }

        public void Set(byte b)
        {
            VerticalBlank = IsBitSet(b, 7);
            SpriteZeroHit = IsBitSet(b, 6);
            SpriteOverflow = IsBitSet(b, 5);
        }

        public byte Get()
        {
            byte b = 0;

            b = (byte)(VerticalBlank ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteZeroHit ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteOverflow ? 1 : 0);
            b = (byte)(b << 5);
            return b;
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
