namespace Nessie
{
    public struct PpuControlRegister
    {
        public bool NmiEnable { get; set; }
        public bool PPUMasterSlave { get; set; }
        public bool SpriteSize { get; set; }
        public bool BackgroundTileSelect { get; set; }
        public bool SpriteTileSelect { get; set; }
        public bool IncrementMode { get; set; }
        public bool NameTableX { get; set; }
        public bool NameTableY { get; set; }

        public void Set(byte b)
        {
            NmiEnable = IsBitSet(b, 7);
            PPUMasterSlave = IsBitSet(b, 6);
            SpriteSize = IsBitSet(b, 5);
            BackgroundTileSelect = IsBitSet(b, 4);
            SpriteTileSelect = IsBitSet(b, 3);
            IncrementMode = IsBitSet(b, 2);
            NameTableY = IsBitSet(b, 1);
            NameTableX = IsBitSet(b, 0);
        }

        public byte Get()
        {
            byte b = 0;
            b = (byte)(NmiEnable ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(PPUMasterSlave ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteSize ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(BackgroundTileSelect ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(SpriteTileSelect ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(IncrementMode ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(NameTableX ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(NameTableY ? 1 : 0);
            return b;
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }
}
