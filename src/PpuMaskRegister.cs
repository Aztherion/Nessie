namespace Nessie
{
    public struct PpuMaskRegister
    {
        public byte BGR { get; set; }
        public bool SpriteEnable { get; set; }
        public bool BackgroundEnable { get; set; }
        public bool SpriteLeftColumnEnable { get; set; }
        public bool BackgroundLeftColumnEnable { get; set; }
        public bool Grayscale { get; set; }

        public void Set(byte b)
        {
            BGR = 0;
            BGR = (byte)(IsBitSet(b, 7) ? 1 : 0);
            BGR = (byte)(BGR << 1);
            BGR += (byte)(IsBitSet(b, 6) ? 1 : 0);
            BGR = (byte)(BGR << 1);
            BGR += (byte)(IsBitSet(b, 5) ? 1 : 0);
            SpriteEnable = IsBitSet(b, 4);
            BackgroundEnable = IsBitSet(b, 3);
            SpriteLeftColumnEnable = IsBitSet(b, 2);
            BackgroundLeftColumnEnable = IsBitSet(b, 1);
            Grayscale = IsBitSet(b, 0);
        }

        public byte Get()
        {
            byte b = 0;
            b = (byte)(IsBitSet(BGR, 2) ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(IsBitSet(BGR, 1) ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(IsBitSet(BGR, 0) ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteEnable ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(BackgroundEnable? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteLeftColumnEnable? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(BackgroundLeftColumnEnable ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(Grayscale ? 1 : 0);
            return b;
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
