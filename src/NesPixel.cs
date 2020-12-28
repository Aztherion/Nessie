using System;

namespace Nessie
{
    public class NesPixel
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public NesPixel(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public UInt32 ToUInt32()
        {
            UInt32 i = 0xFF;
            i = i << 8;
            i = R;
            i = i << 8;
            i += G;
            i = i << 8;
            i += B;
            return i;
        }
    }
}
