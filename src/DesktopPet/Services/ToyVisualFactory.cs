using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace DesktopPet.Services;

/// <summary>Small, soft rubber props drawn at their native 32-DIP size.</summary>
public static class ToyVisualFactory
{
    // The football's lower curve plus outline ends at 24.85 in the 32-DIP canvas.
    // Ground contact uses this visible edge; carrying and collision bounds stay square.
    public static double GroundContactY(ToyKind kind, double diameter) =>
        diameter * (kind == ToyKind.Ball ? 24.85 / 32 : 1);

    public static FrameworkElement Create(ToyKind kind)
    {
        // Everything fits inside the centered 15-DIP radius, including while spinning.
        var canvas = new Canvas
        {
            Width = 32,
            Height = 32,
            IsHitTestVisible = false,
            ClipToBounds = true
        };
        if (kind == ToyKind.Ball) DrawBall(canvas);
        else DrawFrisbee(canvas);
        return canvas;
    }

    private static void DrawBall(Canvas canvas)
    {
        // The reference is a chunky rubber football: blunt ends, blue wraps, and raised laces.
        const string silhouette =
            "M 2.7,16 C 2.7,13.1 4.3,10.9 7.5,9.3 C 12.3,6.9 19.7,6.9 24.5,9.3 " +
            "C 27.7,10.9 29.3,13.1 29.3,16 C 29.3,18.9 27.7,21.1 24.5,22.7 " +
            "C 19.7,25.1 12.3,25.1 7.5,22.7 C 4.3,21.1 2.7,18.9 2.7,16 Z";
        var rubber = new RadialGradientBrush
        {
            GradientOrigin = new Point(.35, .2),
            Center = new Point(.4, .32),
            RadiusX = .85,
            RadiusY = .9,
            GradientStops =
            {
                new GradientStop(Color("#F18156"), 0),
                new GradientStop(Color("#DF5534"), .48),
                new GradientStop(Color("#9D2F24"), 1)
            }
        };
        AddPath(canvas, silhouette, rubber);

        var surface = new Canvas
        {
            Width = 32,
            Height = 32,
            Clip = Geometry.Parse(silhouette)
        };
        canvas.Children.Add(surface);

        // Low-contrast, staggered bumps give the molded rubber a pebbled surface.
        var pebble = new RadialGradientBrush
        {
            GradientOrigin = new Point(.28, .18),
            Center = new Point(.4, .3),
            RadiusX = .8,
            RadiusY = .85,
            Opacity = .55,
            GradientStops =
            {
                new GradientStop(Color("#FFAB73"), 0),
                new GradientStop(Color("#EE7547"), .45),
                new GradientStop(Color("#B73B28"), 1)
            }
        };
        var crevice = Brush("#872E22", .28);
        for (var row = 0; row < 7; row++)
        {
            for (var column = 0; column < 13; column++)
            {
                var left = 3.65 + column * 2.05 + (row % 2) * .95;
                var top = 9.1 + row * 2.05;
                var width = (row + column) % 3 == 0 ? 1.6 : 1.4;
                var height = (row + column) % 2 == 0 ? 1.45 : 1.25;
                AddEllipse(surface, left, top, width, height, pebble, crevice, .22);
            }
        }
        AddPath(surface,
            "M 4.5,19.7 C 9.3,24.3 22.7,24.3 27.5,19.7",
            null, Brush("#842E24", .35), .75);

        // Curved bands wrap around the rounded ends, leaving red rubber end caps visible.
        var blue = Gradient("#62D0D9", "#218FB4", 90);
        AddPath(surface,
            "M 6.5,9.8 C 5.4,13.4 5.4,18.6 6.5,22.2 L 8,23 " +
            "C 6.75,18.9 6.75,13.1 8,9 Z",
            blue, Brush("#196C8B", .55), .35);
        AddPath(surface,
            "M 24,9 C 25.25,13.1 25.25,18.9 24,23 L 25.5,22.2 " +
            "C 26.6,18.6 26.6,13.4 25.5,9.8 Z",
            blue, Brush("#196C8B", .55), .35);
        AddPath(surface, "M 7.1,9.45 C 5.8,13.2 5.8,18.8 7.1,22.55",
            null, Brush("#A5E5E2", .45), .35);
        AddPath(surface, "M 24.9,9.45 C 26.2,13.2 26.2,18.8 24.9,22.55",
            null, Brush("#A5E5E2", .45), .35);

        // The photo's short white stitches sit on a shallow red molding above the midpoint.
        AddPath(surface,
            "M 8.8,11.45 C 13.1,10.65 18.9,10.65 23.2,11.45 L 23,14.95 " +
            "C 18.9,15.7 13.1,15.7 9,14.95 Z",
            Brush("#D54C30", .6), Brush("#A63726", .4), .4);
        var stitches = new[]
        {
            "M 10.15,11.95 C 10,12.65 10.08,13.5 10.25,14.2",
            "M 12.05,11.65 C 11.95,12.45 12.02,13.45 12.15,14.25",
            "M 13.95,11.5 C 13.9,12.35 13.95,13.45 14.05,14.3",
            "M 15.85,11.45 C 15.85,12.3 15.9,13.45 16,14.3",
            "M 17.75,11.5 C 17.85,12.35 17.9,13.45 17.85,14.3",
            "M 19.65,11.65 C 19.82,12.45 19.87,13.45 19.75,14.25",
            "M 21.55,11.95 C 21.78,12.65 21.82,13.5 21.65,14.2"
        };
        foreach (var stitch in stitches)
        {
            AddPath(surface, stitch, null, Brush("#873729", .6), 1.6);
            AddPath(surface, stitch, null, Brush("#FFF1DC"), 1.1);
        }
        AddPath(canvas, silhouette, null, Brush("#97392B"), .7);
    }

    private static void DrawFrisbee(Canvas canvas)
    {
        // The lower lip adds thickness; the inset face sits behind the rounded rim.
        AddEllipse(canvas, 1.7, 9.05, 28.6, 15.2,
            Gradient("#518D86", "#285E5C", 90), Brush("#285C59"), .7);
        AddEllipse(canvas, 1.7, 7.75, 28.6, 15.2,
            Gradient("#A4C9B8", "#4D928B", 85), Brush("#386F6A"), .8);
        AddEllipse(canvas, 5.05, 10.55, 21.9, 9.55,
            Gradient("#467F79", "#71AAA0", 90), Brush("#EFE3C8"), 1.15);
        AddPath(canvas, "M 5.8,15.2 C 6.3,12.7 10.8,11.4 15.9,11.4 C 21.1,11.4 25.3,12.8 26.1,15",
            null, Brush("#285F5B", .5), .65);
        AddPath(canvas, "M 4.4,12.8 C 6.5,9.8 11.6,8.8 17,8.9",
            null, Brush("#E2F0D7", .75), 1.05);
        AddPath(canvas, "M 7.5,22.05 C 13.4,24.1 23.3,22.3 27.7,18.9",
            null, Brush("#234F4D", .4), .75);

        // A tiny raised paw stamp, flattened to match the perspective of the disc.
        var stamp = new Canvas { Width = 32, Height = 32 };
        canvas.Children.Add(stamp);
        var emboss = Brush("#E7E9CD", .8);
        AddPath(stamp, "M 14.1,16.5 C 14.3,15.9 15.1,15.65 16,15.65 C 16.9,15.65 17.7,15.9 17.9,16.5 C 18.2,17.2 17.6,17.55 16.9,17.35 C 16.35,17.2 15.65,17.2 15.1,17.35 C 14.4,17.55 13.8,17.2 14.1,16.5 Z",
            emboss);
        AddEllipse(stamp, 12.85, 14.55, 1.55, 1.2, emboss);
        AddEllipse(stamp, 14.5, 13.8, 1.45, 1.2, emboss);
        AddEllipse(stamp, 16.25, 13.8, 1.45, 1.2, emboss);
        AddEllipse(stamp, 17.95, 14.55, 1.55, 1.2, emboss);
    }

    private static void AddEllipse(Canvas canvas, double left, double top, double width, double height,
        Brush fill, Brush? stroke = null, double thickness = 0)
    {
        var ellipse = new Ellipse
        {
            Width = width, Height = height, Fill = fill,
            Stroke = stroke, StrokeThickness = thickness
        };
        Canvas.SetLeft(ellipse, left);
        Canvas.SetTop(ellipse, top);
        canvas.Children.Add(ellipse);
    }

    private static void AddPath(Canvas canvas, string data, Brush? fill,
        Brush? stroke = null, double thickness = 0)
    {
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse(data), Fill = fill, Stroke = stroke,
            StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round
        });
    }

    private static Color Color(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static SolidColorBrush Brush(string hex, double opacity = 1) =>
        new(Color(hex)) { Opacity = opacity };

    private static LinearGradientBrush Gradient(string start, string end, double angle) =>
        new(Color(start), Color(end), angle);
}
