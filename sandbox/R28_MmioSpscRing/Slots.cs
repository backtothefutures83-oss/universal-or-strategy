using System.Runtime.InteropServices;

namespace R28
{
    // 64 bytes, Pack=8, shadow field is the final 8 bytes (offset 56).
    // lenBeforeShadow = 56 (multiple of 8 -- no tail branch in XorShadow.Compute).
    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
    internal struct OrderSlot
    {
        public long   Id;             // 0
        public long   SymbolHash;     // 8
        public double Price;          // 16
        public int    Quantity;       // 24
        public int    Side;           // 28
        public long   TimestampTicks; // 32
        public int    Account;        // 40
        public int    Pad0;           // 44
        public long   Reserved;       // 48
        public ulong  XorShadow;      // 56
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 64)]
    internal struct FillSlot
    {
        public long   OrderId;   // 0
        public long   FillId;    // 8
        public double FillPrice; // 16
        public int    FillQty;   // 24
        public int    Side;      // 28
        public long   FillTicks; // 32
        public long   ExecHash;  // 40
        public long   Reserved;  // 48
        public ulong  XorShadow; // 56
    }
}
