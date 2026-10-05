using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopPet.Models;
using ShapePath = System.Windows.Shapes.Path;

namespace DesktopPet.Services;

/// <summary>A compact open plush bed and ceramic bowl, drawn in a 210-by-90-DIP space.</summary>
public static class PetHomeVisualFactory
{
    public static FrameworkElement Create(HomeTheme theme)
    {
        var palette = theme switch
        {
            HomeTheme.Mint => new Palette("#E6EFE3", "#B7CEBA", "#8CA995", "#789681",
                "#A0B9A5", "#FAFBEE", "#E4ECD8", "#D9C7B1", "#BCA58F", "#F0F4E7"),
            HomeTheme.Rose => new Palette("#F8E5E7", "#E3B9C4", "#BD8E9D", "#AC7D8C",
                "#CEA5B1", "#FFF7ED", "#F1E3D8", "#CBD4BF", "#A2B19B", "#F9ECE8"),
            _ => new Palette("#F5EBDD", "#DDC8AA", "#BAA184", "#A58B70",
                "#C9B493", "#FFFCF3", "#EFE2CB", "#C5D5C4", "#9FB59E", "#F6EBDD")
        };
        var canvas = new Canvas
        {
            Width = 210,
            Height = 90,
            IsHitTestVisible = false,
            ClipToBounds = true
        };

        DrawBed(canvas, palette);

        // Keep the home's small bowl identical to the separate feeding prop.
        var bowl = FeedingBowlVisualFactory.Create();
        bowl.Width = 43;
        bowl.Height = 16;
        bowl.IsHitTestVisible = false;
        Canvas.SetLeft(bowl, 164);
        Canvas.SetTop(bowl, 67);
        canvas.Children.Add(bowl);

        return new Viewbox
        {
            Child = canvas,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
    }

    private static void DrawBed(Canvas canvas, Palette palette)
    {
        // A shallow shadow and fabric base give the soft bed a grounded lower edge.
        AddEllipse(canvas, 6, 77, 153, 10, SoftGradient("#645447", .15));
        AddEllipse(canvas, 15, 78, 136, 6, Brush(palette.Outline, .18));
        AddPath(canvas,
            "M 8,51 C 8,37 39,29 81,29 C 122,29 154,38 154,51 L 153,66 C 150,78 121,84 81,84 C 41,84 12,78 9,66 Z",
            Gradient(palette.BolsterBottom, palette.Base), Brush(palette.Outline), .7);
        AddPath(canvas, "M 14,73 C 43,84 119,84 148,73",
            null, Brush(palette.Outline, .35), 1);

        // The rolled rear and side bolster surrounds a visibly recessed cushion.
        AddEllipse(canvas, 5, 25, 153, 54,
            Gradient(palette.BolsterTop, palette.BolsterBottom), Brush(palette.Outline), .8);
        AddEllipse(canvas, 21, 36, 121, 35,
            Gradient(palette.WellShade, palette.BolsterBottom), Brush(palette.Outline, .42), .55);
        AddEllipse(canvas, 24, 40, 115, 29,
            CushionGradient(palette.CushionTop, palette.CushionBottom), Brush(palette.Base, .26), .55);
        AddPath(canvas, "M 25,49 C 29,42 51,38 81,38 C 110,38 132,42 137,49",
            null, Brush(palette.Outline, .24), 1.2);
        AddPath(canvas, "M 17,46 C 21,35 47,29 81,29 C 112,29 137,34 145,44",
            null, Brush("#FFFEF7", .65), 1.5);
        AddPath(canvas, "M 29,58 C 46,67 114,67 133,57",
            null, Brush(palette.Base, .2), .65);

        // A small folded blanket sits to one side, leaving the sleeping area open.
        AddPath(canvas, "M 28,43 C 39,39 53,38 65,41 L 68,51 C 53,50 42,54 30,51 Z",
            Brush(palette.Outline, .14));
        AddPath(canvas, "M 29,41 C 40,37 53,38 65,40 L 67,49 C 54,47 42,52 30,49 Z",
            Gradient(palette.BlanketTop, palette.BlanketBottom), Brush(palette.BlanketBottom), .6);
        AddPath(canvas, "M 30,40 C 41,36 53,37 64,39 L 65,45 C 53,43 42,48 31,46 Z",
            Gradient("#F5F4E8", palette.BlanketTop), Brush(palette.BlanketBottom, .55), .55);
        AddPath(canvas, "M 33,44 C 43,46 53,42 62,43",
            null, Brush("#FFFEF4", .7), .8);
        AddPath(canvas, "M 35,39 L 36,43 M 40,38 L 41,42 M 57,39 L 58,42",
            null, Brush(palette.BlanketBottom, .34), .6);

        // The foreground roll overlaps the cushion, with piping and soft cloth gathers.
        AddPath(canvas,
            "M 6,49 C 12,60 38,68 81,68 C 124,68 149,60 156,49 L 155,60 C 153,75 122,82 81,82 C 40,82 9,75 7,60 Z",
            Gradient(palette.BolsterTop, palette.BolsterBottom), Brush(palette.Outline), .8);
        AddPath(canvas, "M 10,56 C 23,67 48,73 81,73 C 114,73 140,67 152,56",
            null, Brush("#FFFEF7", .55), 1.3);
        AddPath(canvas, "M 13,63 C 33,75 126,79 150,62",
            null, Brush(palette.Base, .45), .7);
        AddPath(canvas, "M 21,63 C 20,66 22,70 25,72 M 43,69 C 42,72 44,75 47,77 M 116,69 C 118,72 117,75 115,77 M 140,63 C 143,67 141,70 138,72",
            null, Brush(palette.Outline, .29), .65);
        AddPath(canvas, "M 12,45 C 9,48 8,51 10,54 M 151,45 C 154,48 155,51 153,54",
            null, Brush(palette.Base, .4), .7);

        // A restrained fabric paw label adds a recognizable finishing detail.
        AddPath(canvas, "M 74,72 Q 81,73 88,72 L 88,78 Q 81,80 74,78 Z",
            Brush(palette.Label), Brush(palette.Outline, .4), .55);
        var stamp = Brush(palette.Outline, .65);
        AddPath(canvas, "M 78.5,77 C 78.8,75.6 80,75.1 81,75.1 C 82,75.1 83.2,75.6 83.5,77 C 83.7,77.7 82.6,77.8 81,77.4 C 79.4,77.8 78.3,77.7 78.5,77 Z",
            stamp);
        AddEllipse(canvas, 77.7, 74.3, 1.5, 1.3, stamp);
        AddEllipse(canvas, 79.6, 73.6, 1.3, 1.3, stamp);
        AddEllipse(canvas, 81.4, 73.6, 1.3, 1.3, stamp);
        AddEllipse(canvas, 83.1, 74.3, 1.5, 1.3, stamp);
    }

    private static void AddEllipse(Canvas canvas, double left, double top, double width, double height,
        Brush fill, Brush? stroke = null, double thickness = 0)
    {
        var shape = new Ellipse
        {
            Width = width,
            Height = height,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        canvas.Children.Add(shape);
    }

    private static void AddPath(Canvas canvas, string data, Brush? fill,
        Brush? stroke = null, double thickness = 0)
    {
        canvas.Children.Add(new ShapePath
        {
            Data = Geometry.Parse(data),
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false
        });
    }

    private static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush Brush(string hex, double opacity = 1) => new(Color(hex)) { Opacity = opacity };
    private static LinearGradientBrush Gradient(string top, string bottom) => new(Color(top), Color(bottom), 90);

    private static RadialGradientBrush CushionGradient(string center, string edge) => new()
    {
        GradientOrigin = new Point(.42, .3),
        Center = new Point(.5, .45),
        RadiusX = .7,
        RadiusY = .8,
        GradientStops =
        {
            new GradientStop(Color(center), 0),
            new GradientStop(Color(edge), 1)
        }
    };

    private static RadialGradientBrush SoftGradient(string color, double opacity)
    {
        var shade = Color(color);
        return new RadialGradientBrush
        {
            RadiusX = .5,
            RadiusY = .5,
            Opacity = opacity,
            GradientStops =
            {
                new GradientStop(shade, 0),
                new GradientStop(System.Windows.Media.Color.FromArgb(0, shade.R, shade.G, shade.B), 1)
            }
        };
    }

    private readonly record struct Palette(string BolsterTop, string BolsterBottom, string Base,
        string Outline, string WellShade, string CushionTop, string CushionBottom,
        string BlanketTop, string BlanketBottom, string Label);
}
