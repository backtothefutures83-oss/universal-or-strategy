using System.Runtime.InteropServices.JavaScript;
using Routa.OfficeWasmReader;

public static partial class DocxReader
{
    [JSExport]
    public static byte[] ExtractDocxProto(byte[] bytes, bool ignoreErrors)
    {
        try
        {
            return DocxDocumentProtoReader.Read(bytes);
        }
        catch when (ignoreErrors)
        {
            return [];
        }
    }
}
