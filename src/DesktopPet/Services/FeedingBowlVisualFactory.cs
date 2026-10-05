using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapePath = System.Windows.Shapes.Path;

namespace DesktopPet.Services;

public static class FeedingBowlVisualFactory
{
    public static FrameworkElement Create()
    {
        var canvas = new Canvas { Width = 84, Height = 32, IsHitTestVisible = false, ClipToBounds = true };
        Ellipse(canvas, 8, 27, 68, 4, Brush("#302E28", .14));
        Ellipse(canvas, 13, 23, 58, 5, Gradient("#97A79A", "#7F9084"));
        canvas.Children.Add(new ShapePath
        {
            Data = Geometry.Parse("M 5,11 C 5,3 79,3 79,11 L 72,22 C 67,29 17,29 12,22 Z"),
            Fill = Gradient("#FFFDF7", "#DCD7CC"), Stroke = Brush("#BAB5A8"), StrokeThickness = .6,
            StrokeLineJoin = PenLineJoin.Round
        });
        Ellipse(canvas, 4, 3, 76, 16, Gradient("#FFFEF9", "#E8E3D7"), Brush("#C1BCB0"), .65);
        Ellipse(canvas, 10, 6, 64, 10, Gradient("#847464", "#BAA68A"), Brush("#D4CBBB"), .6);

        // A loose, low layer of kibble leaves the shallow ceramic rim visible.
        foreach (var (x, y) in new[] { (20d, 9d), (30d, 7d), (41d, 8d), (53d, 7.5d),
                     (61d, 10d), (27d, 11.5d), (38d, 11d), (48d, 12d), (55d, 11.5d) })
            Ellipse(canvas, x, y, 6, 3, Gradient("#B99368", "#795636"), Brush("#72563C", .7), .3);

        canvas.Children.Add(new ShapePath
        {
            Data = Geometry.Parse("M 8,14 C 23,21 61,21 76,14"), Stroke = Brush("#FFFEF9"),
            StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        });
        return new Viewbox { Child = canvas, Stretch = Stretch.Uniform, IsHitTestVisible = false };
    }

    private static void Ellipse(Canvas canvas, double x, double y, double width, double height,
        Brush fill, Brush? stroke = null, double thickness = 0)
    {
        var shape = new Ellipse { Width = width, Height = height, Fill = fill, Stroke = stroke, StrokeThickness = thickness };
        Canvas.SetLeft(shape, x); Canvas.SetTop(shape, y); canvas.Children.Add(shape);
    }
    private static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush Brush(string hex, double opacity = 1) => new(Color(hex)) { Opacity = opacity };
    private static LinearGradientBrush Gradient(string top, string bottom) => new(Color(top), Color(bottom), 90);
}
