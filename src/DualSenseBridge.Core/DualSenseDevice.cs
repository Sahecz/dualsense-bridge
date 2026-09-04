namespace DualSenseBridge.Core;

public static class DualSenseDevice
{
    public const int SonyVendorId = 0x054C;
    public const int DualSenseProductId = 0x0CE6;
    public const int DualSenseEdgeProductId = 0x0DF2;

    public static bool IsSupported(int vendorId, int productId) =>
        vendorId == SonyVendorId && productId is DualSenseProductId or DualSenseEdgeProductId;

    public static string ModelName(int productId) => productId switch
    {
        DualSenseProductId => "DualSense",
        DualSenseEdgeProductId => "DualSense Edge",
        _ => "Control desconocido",
    };
}
