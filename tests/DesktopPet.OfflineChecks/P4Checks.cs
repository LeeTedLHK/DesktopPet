using DesktopPet.Models;
using DesktopPet.Services;

internal static class P4Checks
{
    public static void Run(Action<bool, string> check)
    {
        var left = new MonitorInfo("left", "Left", new(-1920,0,1920,1080),new(-1920,0,1920,1040),1.5,false);
        var main = new MonitorInfo("main", "Main",new(0,0,2560,1440),new(0,0,2560,1400),1,true);
        check(left.ToDip(new(-1770,150)) == new PetPoint(-1820,100), "monitor conversion handles negative physical origin and 150% DPI");
        check(left.ToPixels(new(-1820,100)) == new PetPoint(-1770,150), "monitor pixel/DIP round trip");
        check(left.DipWorkArea == new WorkArea(-1920,0,1280,1040d/1.5), "work area scales size with monitor origin preserved");
        check(MonitorGeometry.Resolve([left,main], "missing",new(-1000,100)).Id == "main", "disconnected preferred monitor falls back to primary");
        check(MonitorGeometry.Resolve([left,main], "",new(-1000,100)).Id == "left", "legacy position without monitor preference restores nearest screen");
        check(MonitorGeometry.Resolve([left,main], "main",new(-1000,100)).Id == "main", "explicit monitor preference wins over saved position");
        check(MonitorGeometry.At([left,main],new(9000,9000)).Id == "main", "nearest monitor fallback handles unplugged/offscreen position");
        var normalized = (new AppSettings { SelectedMonitorId=null!,SelectedSkinId="../../bad",HomeXPercent=double.NaN, HomeTheme=(HomeTheme)99 }).Normalize();
        check(normalized.SelectedSkinId == "default" && normalized.SelectedMonitorId == "" && normalized.HomeXPercent==85 && normalized.HomeTheme==HomeTheme.Cozy,
            "P4 settings normalize screen skin and home values");
        check(new AppSettings().PreciseHitTestEnabled && !new AppSettings().StartWithWindows && !new AppSettings().HomeEnabled,
            "P4 legacy defaults preserve opt-in startup and home");
        byte[] mask = [0,0,0,0, 0,0,0,255, 0,0,0,12, 0,0,0,200];
        check(!AlphaHitTest.IsOpaque(mask,2,2,8,0,0,16) && AlphaHitTest.IsOpaque(mask,2,2,8,1,0,16), "alpha test excludes transparent pixels");
        check(!AlphaHitTest.IsOpaque(mask,2,2,8,0,1,16) && !AlphaHitTest.IsOpaque(mask,2,2,8,-1,1,16), "alpha threshold and outside bounds pass through");
        check(StartupCommand.Build("C:\\Pet Folder\\DesktopPet.exe") == "\"C:\\Pet Folder\\DesktopPet.exe\" --startup", "startup command quotes executable with spaces");
        var toy = new ToySimulation(ToyKind.Ball,new(80,60),new(180,-150),new(-100,-100,400,300));
        toy.Advance(10);
        check(toy.Position.X < 110 && toy.Position.Y < 60, "toy clamps long frame stalls rather than teleporting");
        for (var i=0;i<300;i++) toy.Advance(1d/30);
        check(toy.IsSettled && toy.Position.Y==168 && toy.Position.X>=-100 && toy.Position.X<=268,
            "ball bounces and settles inside negative-origin activity bounds");
        var frisbee = new ToySimulation(ToyKind.Frisbee,new(50,50),new(200,-100),new(0,0,250,200));
        for (var i=0;i<150;i++) frisbee.Advance(1d/30);
        check(frisbee.IsSettled && frisbee.Angle!=0 && frisbee.Position.X<=218, "frisbee rotates and stops inside region");
        var home = WorldGeometry.HomePosition(new AppSettings {HomeXPercent=100,HomeYPercent=100},new(160,160),new(-500,0,300,250));
        check(home==new PetPoint(-360,90), "home target fits pet at activity boundary");
        var paused = new DesktopPet.Controllers.PetStateMachine(false);
        check(paused.TryBeginUserMovement(PetState.WalkingRight) && paused.State==PetState.WalkingRight, "explicit toy chase works with automatic behavior paused");
        paused.CompleteWalking();
        check(paused.TryBeginUserSleeping() && paused.State==PetState.Sleeping, "explicit home sleep works while paused");
        paused.SetVisible(false);
        check(!paused.TryBeginUserMovement(PetState.WalkingRight) && !paused.TryBeginUserSleeping(), "hidden pets reject user world actions");
    }
}
