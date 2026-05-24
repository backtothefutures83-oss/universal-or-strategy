using System.Security.Cryptography;

namespace Routa.OfficeWasmReader;

internal static class Program
{
    private static readonly Type[] CompatibilityAssemblyMarkers =
    [
        typeof(Console),
        typeof(SHA256),
    ];

    public static Task Main()
    {
        GC.KeepAlive(CompatibilityAssemblyMarkers);
        return Task.CompletedTask;
    }
}
