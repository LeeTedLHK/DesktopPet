using DesktopPet.Models;

namespace DesktopPet.Controllers;

public sealed class PetStateMachine
{
    public PetStateMachine(bool autoBehaviorEnabled = true)
    {
        AutoBehaviorEnabled = autoBehaviorEnabled;
        IsVisible = true;
        State = autoBehaviorEnabled ? PetState.Idle : PetState.Paused;
    }

    public PetState State { get; private set; }
    public bool AutoBehaviorEnabled { get; private set; }
    public bool IsVisible { get; private set; }

    public event Action<PetState>? StateChanged;

    public void SetAutoBehaviorEnabled(bool enabled)
    {
        if (AutoBehaviorEnabled == enabled)
        {
            return;
        }

        AutoBehaviorEnabled = enabled;
        if (State == PetState.Dragging)
        {
            return;
        }

        TransitionTo(enabled && IsVisible ? PetState.Idle : PetState.Paused);
    }

    public void SetVisible(bool visible)
    {
        if (IsVisible == visible)
        {
            return;
        }

        IsVisible = visible;
        if (State == PetState.Dragging)
        {
            return;
        }

        TransitionTo(visible && AutoBehaviorEnabled ? PetState.Idle : PetState.Paused);
    }

    public void BeginDrag()
    {
        TransitionTo(PetState.Dragging);
    }

    public void EndDrag()
    {
        if (State != PetState.Dragging)
        {
            return;
        }

        TransitionTo(IsVisible && AutoBehaviorEnabled ? PetState.Idle : PetState.Paused);
    }

    public bool TryBeginWalk(PetState walkingState)
    {
        if (walkingState is not (PetState.WalkingLeft or PetState.WalkingRight)
            || !IsVisible
            || !AutoBehaviorEnabled
            || State != PetState.Idle)
        {
            return false;
        }

        TransitionTo(walkingState);
        return true;
    }

    public bool TryBeginHappy() => TryBeginTemporary(PetState.Happy);

    public bool TryBeginSleeping() => TryBeginTemporary(PetState.Sleeping);

    public void CompleteHappy()
    {
        if (State == PetState.Happy)
        {
            TransitionTo(ActiveState());
        }
    }

    public void CompleteSleeping()
    {
        if (State == PetState.Sleeping)
        {
            TransitionTo(ActiveState());
        }
    }

    public void CompleteWalking()
    {
        if (State is PetState.WalkingLeft or PetState.WalkingRight)
        {
            TransitionTo(ActiveState());
        }
    }

    private bool TryBeginTemporary(PetState temporaryState)
    {
        var canBeginHappyWhilePaused = temporaryState == PetState.Happy
            && State == PetState.Paused
            && IsVisible
            && !AutoBehaviorEnabled;
        var canBeginFromIdle = State == PetState.Idle && IsVisible && AutoBehaviorEnabled;
        if (temporaryState is not (PetState.Happy or PetState.Sleeping)
            || (!canBeginFromIdle && !canBeginHappyWhilePaused))
        {
            return false;
        }

        TransitionTo(temporaryState);
        return true;
    }

    private PetState ActiveState() => IsVisible && AutoBehaviorEnabled
        ? PetState.Idle
        : PetState.Paused;

    private void TransitionTo(PetState nextState)
    {
        if (State == nextState)
        {
            return;
        }

        State = nextState;
        StateChanged?.Invoke(nextState);
    }
}
