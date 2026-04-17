using System.Runtime.CompilerServices;

namespace R28
{
    // ADR-016: branch-free, byte-wise XOR integrity.
    // Computes over [0, lenBeforeShadow) which MUST be a multiple of 8.
    // The shadow field itself is NOT included in the computation (FIX-D2).
    internal static unsafe class XorShadow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Compute(byte* p, int lenBeforeShadow, ulong salt)
        {
            ulong acc = salt;
            for (int i = 0; i < lenBeforeShadow; i += 8)
                acc ^= Unsafe.ReadUnaligned<ulong>(p + i);
            return acc;
        }
    }
}
