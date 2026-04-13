namespace TimeSlotter.Models;

/// <summary>
/// Stored as int on <see cref="Slot"/>. Values 0, 1, 2, 3: Available / Pending approval / Booked / Admin block.
/// </summary>
public enum SlotStatus
{
    /// <summary>Open for client booking.</summary>
    Available = 0,

    /// <summary>Public booking submitted; awaiting provider approval (has a <see cref="Booking"/>).</summary>
    Pending = 1,

    /// <summary>Booked by a client (has a <see cref="Booking"/>).</summary>
    BookedByClient = 2,

    /// <summary>Manually blocked by the provider in Admin.</summary>
    ReservedByAdmin = 3,
}
