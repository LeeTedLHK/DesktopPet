using DesktopPet.Controllers;
using DesktopPet.Models;

namespace DesktopPet.Tests;

public sealed class PetStateMachineTests
{
    [Fact]
    public void DisabledMachine_StartsPausedAndResumesWhenEnabled()
    {
        var machine = new PetStateMachine(autoBehaviorEnabled: false);

        Assert.Equal(PetState.Paused, machine.State);
        machine.SetAutoBehaviorEnabled(true);
        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void WalkingRequiresVisibleEnabledIdleMachineAndValidDirection()
    {
        var machine = new PetStateMachine();

        Assert.False(machine.TryBeginWalk(PetState.Idle));
        Assert.True(machine.TryBeginWalk(PetState.WalkingLeft));
        Assert.False(machine.TryBeginWalk(PetState.WalkingRight));
        machine.CompleteWalking();
        Assert.Equal(PetState.Idle, machine.State);

        machine.SetVisible(false);
        Assert.False(machine.TryBeginWalk(PetState.WalkingLeft));
        machine.SetVisible(true);
        machine.SetAutoBehaviorEnabled(false);
        Assert.False(machine.TryBeginWalk(PetState.WalkingRight));
    }

    [Fact]
    public void DraggingDefersPausePreferenceUntilDragEnds()
    {
        var machine = new PetStateMachine();

        machine.BeginDrag();
        machine.SetAutoBehaviorEnabled(false);

        Assert.Equal(PetState.Dragging, machine.State);
        Assert.False(machine.AutoBehaviorEnabled);
        machine.CompleteWalking();
        Assert.Equal(PetState.Dragging, machine.State);
        machine.EndDrag();
        Assert.Equal(PetState.Paused, machine.State);
    }

    [Fact]
    public void HidingCancelsWalkAndShowingResumesAccordingToPreference()
    {
        var machine = new PetStateMachine();
        Assert.True(machine.TryBeginWalk(PetState.WalkingRight));

        machine.SetVisible(false);

        Assert.False(machine.IsVisible);
        Assert.Equal(PetState.Paused, machine.State);
        machine.SetVisible(true);
        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void HidingDuringDragKeepsDraggingUntilEndDrag()
    {
        var machine = new PetStateMachine();
        machine.BeginDrag();

        machine.SetVisible(false);

        Assert.Equal(PetState.Dragging, machine.State);
        machine.EndDrag();
        Assert.Equal(PetState.Paused, machine.State);
    }

    [Fact]
    public void StateChangedFiresOnlyForActualStateChanges()
    {
        var machine = new PetStateMachine();
        var states = new List<PetState>();
        machine.StateChanged += states.Add;

        machine.SetAutoBehaviorEnabled(true);
        machine.SetVisible(true);
        machine.CompleteWalking();
        machine.BeginDrag();
        machine.BeginDrag();
        machine.EndDrag();

        Assert.Equal([PetState.Dragging, PetState.Idle], states);
    }

    [Fact]
    public void ReapplyingCurrentPreferencesDoesNotCancelWalking()
    {
        var machine = new PetStateMachine();
        Assert.True(machine.TryBeginWalk(PetState.WalkingLeft));

        machine.SetAutoBehaviorEnabled(true);
        machine.SetVisible(true);

        Assert.Equal(PetState.WalkingLeft, machine.State);
    }

    [Fact]
    public void HappyStartsOnlyFromIdleAndCompletesBackToIdle()
    {
        var machine = new PetStateMachine();

        Assert.True(machine.TryBeginHappy());
        Assert.Equal(PetState.Happy, machine.State);
        Assert.False(machine.TryBeginHappy());

        machine.CompleteHappy();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void SleepingStartsOnlyFromIdleAndCompletesBackToIdle()
    {
        var machine = new PetStateMachine();

        Assert.True(machine.TryBeginSleeping());
        Assert.Equal(PetState.Sleeping, machine.State);
        Assert.False(machine.TryBeginSleeping());

        machine.CompleteSleeping();

        Assert.Equal(PetState.Idle, machine.State);
    }

    [Fact]
    public void TemporaryStatesRespectVisibilityAndAutoBehavior()
    {
        var machine = new PetStateMachine();
        machine.SetVisible(false);

        Assert.False(machine.TryBeginHappy());
        Assert.False(machine.TryBeginSleeping());

        machine.SetVisible(true);
        machine.SetAutoBehaviorEnabled(false);

        Assert.True(machine.TryBeginHappy());
        machine.CompleteHappy();
        Assert.Equal(PetState.Paused, machine.State);
        Assert.False(machine.TryBeginSleeping());
    }
}
