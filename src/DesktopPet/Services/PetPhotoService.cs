using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopPet.Services;

public sealed record PetPhotoFiles(string PostcardPath, string PortraitPath);

public static class PetPhotoService
{
    public static PetPhotoFiles Save(BitmapSource frame, string petName, string directory)
    {
        ArgumentNullException.ThrowIfNull(frame);
        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now;
        var stem = $"pet-{stamp:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
        var cardPath = Path.Combine(directory, stem + "-postcard.png");
        var portraitPath = Path.Combine(directory, stem + "-portrait.png");
        var visual = new DrawingVisual();
        using (var draw = visual.RenderOpen())
        {
            draw.DrawRectangle(Brush("#F8F5ED"), null, new Rect(0, 0, 640, 760));
            draw.DrawRoundedRectangle(Brush("#DDE8DF"), null, new Rect(32, 32, 576, 580), 12, 12);
            draw.DrawEllipse(Brush("#EEF2E7"), null, new Point(112, 122), 60, 60);
            draw.DrawEllipse(Brush("#D0DFD2"), null, new Point(535, 506), 42, 42);
            var fit = Math.Min(528d / frame.PixelWidth, 532d / frame.PixelHeight);
            var width = frame.PixelWidth * fit; var height = frame.PixelHeight * fit;
            draw.DrawImage(frame, new Rect((640-width)/2, 56+(532-height)/2, width, height));
            var title = new FormattedText(petName, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 30, Brush("#33594B"), 1)
                { MaxTextWidth = 550, MaxTextHeight = 44, Trimming = TextTrimming.CharacterEllipsis };
            draw.DrawText(title, new Point(42, 637));
            var caption = new FormattedText("今天也有好好陪着你", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 17, Brush("#64766B"), 1);
            draw.DrawText(caption, new Point(42, 687));
            var date = new FormattedText(stamp.ToString("yyyy.MM.dd"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 16, Brush("#64766B"), 1);
            draw.DrawText(date, new Point(640 - date.Width - 42, 690));
        }
        var card = new RenderTargetBitmap(640, 760, 96, 96, PixelFormats.Pbgra32);
        card.Render(visual);
        Write(card, cardPath);
        try { Write(frame, portraitPath); }
        catch { File.Delete(cardPath); throw; }
        return new(cardPath, portraitPath);
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static void Write(BitmapSource source, string path)
    {
        var created = false;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            created = true;
            encoder.Save(stream);
        }
        catch { if (created) File.Delete(path); throw; }
    }
}
