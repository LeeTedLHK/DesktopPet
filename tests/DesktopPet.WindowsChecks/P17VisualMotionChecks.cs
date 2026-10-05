using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P17VisualMotionChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    private static RenderTargetBitmap Bitmap(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        return bitmap;
    }

    public static void Run(PetController controller, PetWindow pet, Action<bool, string> check,
        Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool value, string name)
        { if (value) check(true, name); else { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
        var original = controller.Settings;
        var state = Field<PetStateMachine>(controller, "_state");
        var manager = Field<AnimationManager>(pet, "_animationManager");
        try
        {
            var ball = ToyVisualFactory.Create(ToyKind.Ball);
            var preview = new Viewbox { Width = 256, Height = 256, Child = ball };
            var bitmap = Bitmap(preview, 256, 256);
            var pixels = new byte[256 * 256 * 4]; bitmap.CopyPixels(pixels, 256 * 4, 0);
            var minX = 256; var minY = 256; var maxX = -1; var maxY = -1; var blue = 0; var white = 0;
            for (var y = 0; y < 256; y++) for (var x = 0; x < 256; x++)
            {
                var i = (y * 256 + x) * 4; if (pixels[i + 3] < 128) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                if (pixels[i] > pixels[i + 2] + 45 && pixels[i + 1] > pixels[i + 2] + 25) blue++;
                if (pixels[i] > 205 && pixels[i + 1] > 205 && pixels[i + 2] > 205) white++;
            }
            Probe(maxX - minX > (maxY - minY) * 1.35, "P17 ball has the reference toy's horizontal oblong silhouette");
            Probe(blue > 80 && white > 80, "P17 reference ball visibly includes blue end bands and white laces");
            Probe(!ball.IsHitTestVisible && ball.Width == 32 && ball.Height == 32,
                "P17 redesigned ball retains its noninteractive native prop bounds");
            render(preview, Path.Combine(evidence, "p17-reference-ball.png"));
            foreach (var angle in new[] { 0d, 45, 90, 135 })
            {
                ball.RenderTransform = new RotateTransform(angle, 16, 16);
                var rotated = Bitmap(preview, 256, 256); rotated.CopyPixels(pixels, 256 * 4, 0);
                Probe(pixels[3] == 0 && pixels[255 * 4 + 3] == 0 && pixels[(255 * 256) * 4 + 3] == 0,
                    $"P17 rotated ball retains transparent prop corners at {angle} degrees");
            }

            var homeFactory = typeof(PetWorldService).Assembly.GetType("DesktopPet.Services.PetHomeVisualFactory");
            var createHome = homeFactory?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
            Probe(createHome is not null, "P17 polished home has a reusable theme-aware visual factory");
            if (createHome is not null)
            {
                var hashes = new HashSet<string>();
                foreach (var theme in Enum.GetValues<HomeTheme>())
                {
                    var home = (FrameworkElement)createHome.Invoke(null, [theme])!;
                    var homeBitmap = Bitmap(home, 420, 180);
                    var data = new byte[420 * 180 * 4]; homeBitmap.CopyPixels(data, 420 * 4, 0);
                    hashes.Add(Convert.ToHexString(SHA256.HashData(data)));
                    Probe(home is Viewbox && !home.IsHitTestVisible && home.ActualWidth == 420 && home.ActualHeight == 180,
                        $"P17 {theme} home scales uniformly and lets mouse input through");
                    render(home, Path.Combine(evidence, $"p17-home-{theme}.png"));
                }
                Probe(hashes.Count == 3, "P17 cream, mint and rose home themes render distinct palettes");
            }

            controller.StopPlay();
            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                EnableAnimations = true, AnimationSpeedMultiplier = 1, HomeEnabled = true });
            controller.ShowPet();
            var world = Field<PetWorldService>(controller, "_world");
            var actualHome = Field<Window>(world, "_home");
            Probe(actualHome.IsVisible && actualHome.Content is Viewbox,
                "P17 home remains visible in the actual world when enabled");
            typeof(PetController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(controller, [PetState.Happy, "P17 happy motion check"]);
            var clock = Stopwatch.StartNew();
            pump(1000);
            Probe(state.State == PetState.Happy, "P17 actual happy gesture continues beyond the old fast head-shake duration");
            while (state.State == PetState.Happy && clock.Elapsed < TimeSpan.FromSeconds(3)) pump(100);
            Probe(state.State == PetState.Paused && clock.Elapsed >= TimeSpan.FromSeconds(1.45),
                "P17 slower happy gesture completes naturally and returns to paused behavior");

            foreach (var walking in new[] { PetState.WalkingLeft, PetState.WalkingRight })
            {
                controller.ApplySettings(controller.Settings);
                state.TryBeginUserMovement(walking); pump(650);
                var clip = Field<AnimationClip>(manager, "_currentClip");
                var expectedFrames = 8;
                Probe(clip.Kind == AnimationDefinition.ForState(walking) && !clip.IsFallback && clip.Frames.Count == expectedFrames,
                    $"P17 {walking} uses {expectedFrames} authored gait poses after its start transition");
                var before = pet.CurrentFrame; pump(140);
                Probe(!ReferenceEquals(before, pet.CurrentFrame) &&
                    !Field<TranslateTransform>(pet, "WalkingOffset").HasAnimatedProperties,
                    $"P17 {walking} advances authored paw poses without synthetic bobbing");
                render((FrameworkElement)pet.FindName("PetSurface"), Path.Combine(evidence, $"p17-{walking}.png"));
            }
            controller.ThrowToy(ToyKind.Ball);
            var fetch = Field<FetchSequence>(world, "_fetch");
            var advance = typeof(PetWorldService).GetMethod("AdvanceFetch", BindingFlags.NonPublic | BindingFlags.Instance)!;
            for (var i = 0; i < 2000 && world.IsToyVisible && fetch.Phase != FetchPhase.Celebrating; i++)
                advance.Invoke(world, [.05]);
            var toy = Field<Window>(world, "_toy");
            var toyArt = (FrameworkElement)toy.Content;
            toyArt.UpdateLayout();
            var toyPixels = new RenderTargetBitmap(320, 320, 96 * 320 / toy.Width,
                96 * 320 / toy.Height, PixelFormats.Pbgra32);
            toyPixels.Render(toyArt);
            var toyData = new byte[320 * 320 * 4]; toyPixels.CopyPixels(toyData, 320 * 4, 0);
            var opaqueBottom = -1;
            for (var y = 0; y < 320; y++) for (var x = 0; x < 320; x++)
                if (toyData[(y * 320 + x) * 4 + 3] >= 128) opaqueBottom = Math.Max(opaqueBottom, y);
            var screen = Field<ScreenBoundsService>(world, "_screen");
            var visibleBottom = screen.Position(toy).Y + (opaqueBottom + 1) / 320d * toy.Height;
            Console.WriteLine($"P17 floor probe: phase={fetch.Phase}, opaque={opaqueBottom}, art={toyArt.ActualWidth}x{toyArt.ActualHeight}, top={screen.Position(toy).Y}, size={toy.Height}, visibleBottom={visibleBottom}, ground={fetch.GroundLine}");
            Probe(fetch.Phase == FetchPhase.Celebrating && Math.Abs(visibleBottom - fetch.GroundLine) <= 1,
                "P17 delivered football's opaque rubber bottom rests on the actual paw floor");
            controller.StopPlay();
            controller.HidePet();
            Probe(!actualHome.IsVisible && !world.IsMoving,
                "P17 hiding still clears the redesigned home and world movement");
        }
        finally { controller.ApplySettings(original); controller.ShowPet(); }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));
    }
}
