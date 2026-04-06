namespace TimeSlotter.Models;

/// <summary>
/// Stored as int on <see cref="Slot"/>. Values 0, 2, 3 align with the previous Free / Booked / Blocked set.
/// </summary>
public enum SlotStatus
{
    /// <summary>Open for client booking.</summary>
    Available = 0,

    /// <summary>Booked by a client (has a <see cref="Booking"/>).</summary>
    BookedByClient = 2,

    /// <summary>Manually blocked by the provider in Admin.</summary>
    ReservedByAdmin = 3,
}
