namespace DesktopPet.Services;

public static class AlphaHitTest
{
    public static bool IsOpaque(byte[] pixels, int width, int height, int stride, int x, int y, byte threshold = 16)
    {
        if (x < 0 || y < 0 || x >= width || y >= height || stride < width * 4) return false;
        var offset = (long)y * stride + (long)x * 4 + 3;
        return offset >= 0 && offset < pixels.Length && pixels[offset] >= threshold;
    }
}
