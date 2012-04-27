using System;

namespace Jurassic
{
    // Fracture - Open source C# game development framework
    // MIT LICENSE
    // http://code.google.com/p/fracture/

    public static class XBitConverter
    {
        public static long DoubleToInt64Bits(double d)
        {
#if XBOX
        return BitConverter.ToInt64(BitConverter.GetBytes(d), 0);
#else
        return BitConverter.DoubleToInt64Bits(d);
#endif
        }

        public static double Int64BitsToDouble(long l)
        {
#if XBOX
        return BitConverter.ToDouble(BitConverter.GetBytes(l), 0);
#else
        return BitConverter.Int64BitsToDouble(l);
#endif
        }
    }
}
