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

    public bool TryBeginUserMovement(PetState direction)
    {
        if (!IsVisible || State == PetState.Dragging || direction is not (PetState.WalkingLeft or
            PetState.WalkingRight or PetState.RunningLeft or PetState.RunningRight)) return false;
        TransitionTo(direction);
        return true;
    }

    public bool TryBeginUserSleeping()
    {
        if (!IsVisible || State == PetState.Dragging) return false;
        TransitionTo(PetState.Sleeping);
        return true;
    }

    public static bool IsFetchAction(PetState state) => state is PetState.JumpingLeft or PetState.JumpingRight or
        PetState.PickingUpLeft or PetState.PickingUpRight or PetState.CarryingLeft or PetState.CarryingRight or
        PetState.DroppingLeft or PetState.DroppingRight or PetState.Celebrating;

    public bool TryBeginFetchAction(PetState action)
    {
        if (!IsVisible || State == PetState.Dragging || !IsFetchAction(action)) return false;
        TransitionTo(action);
        return true;
    }

    public void CompleteFetchAction()
    {
        if (IsFetchAction(State)) TransitionTo(ActiveState());
    }

    public bool TryBeginSleeping() => TryBeginTemporary(PetState.Sleeping);

    public bool TryBeginInteraction(PetState action)
    {
        if (!IsVisible || State == PetState.Dragging ||
            action is not (PetState.Petting or PetState.Feeding or PetState.Happy)) return false;
        // Reset first so repeated interactions restart their single-shot animation.
        if (State == action) TransitionTo(ActiveState());
        TransitionTo(action);
        return true;
    }

    public bool TryBeginNaturalAction(PetState action)
    {
        if (!IsVisible || !AutoBehaviorEnabled || State != PetState.Idle ||
            action is not (PetState.Stretching or PetState.Yawning or PetState.LookingLeft or
                PetState.LookingRight or PetState.Sleeping)) return false;
        TransitionTo(action);
        return true;
    }

    public void CompleteTemporaryAction(PetState expectedState)
    {
        if (State == expectedState && expectedState is (PetState.Happy or PetState.Petting or
            PetState.Feeding or PetState.Stretching or PetState.Yawning or PetState.LookingLeft or
            PetState.LookingRight or PetState.Sleeping)) TransitionTo(ActiveState());
    }

    public void CancelNaturalAction()
    {
        if (State is PetState.Stretching or PetState.Yawning or PetState.LookingLeft or
            PetState.LookingRight or PetState.Sleeping) TransitionTo(ActiveState());
    }

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
        if (State is PetState.WalkingLeft or PetState.WalkingRight or PetState.RunningLeft or PetState.RunningRight)
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
