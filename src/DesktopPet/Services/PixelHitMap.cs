using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.Services;

public sealed class PixelHitMap
{
    private readonly byte[] _alpha;
    public int Width { get; }
    public int Height { get; }
    public PixelHitMap(BitmapSource source)
    {
        Width = source.PixelWidth;
        Height = source.PixelHeight;
        var converted = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32
            ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(Width * Height * 4)];
        converted.CopyPixels(pixels, Width * 4, 0);
        _alpha = new byte[checked(Width * Height)];
        for (var i = 0; i < _alpha.Length; i++) _alpha[i] = pixels[i * 4 + 3];
    }
    public bool Hit(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _alpha[y * Width + x] >= 16;
}
