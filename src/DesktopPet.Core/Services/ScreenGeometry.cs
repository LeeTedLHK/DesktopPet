namespace DesktopPet.Services;

public readonly record struct PetPoint(double X, double Y);

public readonly record struct PetSize(double Width, double Height);

public readonly record struct WorkArea(double Left, double Top, double Width, double Height)
{
    public double Right => SumFiniteOrStart(Left, Width);
    public double Bottom => SumFiniteOrStart(Top, Height);

    private static double SumFiniteOrStart(double start, double length)
    {
        var sum = start + length;
        return double.IsFinite(sum) ? sum : start;
    }
}

public static class ScreenGeometry
{
    public static PetPoint Clamp(PetPoint position, PetSize petSize, WorkArea workArea)
    {
        var geometry = Sanitize(petSize, workArea);
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y))
        {
            return DefaultPosition(geometry.PetSize, geometry.WorkArea);
        }

        return ClampFinite(position, geometry.PetSize, geometry.WorkArea);
    }

    public static PetPoint DefaultPosition(PetSize petSize, WorkArea workArea)
    {
        var geometry = Sanitize(petSize, workArea);
        var position = new PetPoint(
            geometry.WorkArea.Right - geometry.PetSize.Width - 50d,
            geometry.WorkArea.Bottom - geometry.PetSize.Height - 20d);
        return ClampFinite(position, geometry.PetSize, geometry.WorkArea);
    }

    public static PetPoint Restore(double? x, double? y, PetSize petSize, WorkArea workArea)
    {
        var geometry = Sanitize(petSize, workArea);
        if (x is not double savedX
            || y is not double savedY
            || !double.IsFinite(savedX)
            || !double.IsFinite(savedY)
            || !Intersects(new PetPoint(savedX, savedY), geometry.PetSize, geometry.WorkArea))
        {
            return DefaultPosition(geometry.PetSize, geometry.WorkArea);
        }

        return ClampFinite(new PetPoint(savedX, savedY), geometry.PetSize, geometry.WorkArea);
    }

    private static bool Intersects(PetPoint position, PetSize petSize, WorkArea workArea)
    {
        var petRight = position.X + petSize.Width;
        var petBottom = position.Y + petSize.Height;
        return petRight > workArea.Left
            && position.X < workArea.Right
            && petBottom > workArea.Top
            && position.Y < workArea.Bottom;
    }

    private static PetPoint ClampFinite(PetPoint position, PetSize petSize, WorkArea workArea)
    {
        var maximumX = Math.Max(workArea.Left, workArea.Right - petSize.Width);
        var maximumY = Math.Max(workArea.Top, workArea.Bottom - petSize.Height);
        return new PetPoint(
            Math.Clamp(position.X, workArea.Left, maximumX),
            Math.Clamp(position.Y, workArea.Top, maximumY));
    }

    private static (PetSize PetSize, WorkArea WorkArea) Sanitize(PetSize petSize, WorkArea workArea)
    {
        var width = FiniteNonnegative(petSize.Width);
        var height = FiniteNonnegative(petSize.Height);
        var left = double.IsFinite(workArea.Left) ? workArea.Left : 0d;
        var top = double.IsFinite(workArea.Top) ? workArea.Top : 0d;
        var areaWidth = FiniteNonnegative(workArea.Width);
        var areaHeight = FiniteNonnegative(workArea.Height);
        return (new PetSize(width, height), new WorkArea(left, top, areaWidth, areaHeight));
    }

    private static double FiniteNonnegative(double value) => double.IsFinite(value) && value > 0d ? value : 0d;
}
