namespace TimeSlotter.Models;

/// <summary>
/// Lifecycle of a time slot for booking workflows.
/// </summary>
public enum SlotStatus
{
    Free = 0,
    Pending = 1,
    Booked = 2,
    Blocked = 3
}
