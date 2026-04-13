using System.ComponentModel.DataAnnotations.Schema;

namespace TimeSlotter.Models;

/// <summary>
/// Public availability is <see cref="SlotStatus.Available"/> (optionally with <see cref="RequiresApproval"/>).
/// Client-held states use <see cref="SlotStatus.Pending"/> or <see cref="SlotStatus.BookedByClient"/> (with a <see cref="Booking"/>).
/// Admin technical blocks use <see cref="SlotStatus.ReservedByAdmin"/> (no <see cref="Booking"/> row).
/// </summary>
public class Slot
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ProviderId { get; set; }
    public string? ResourceContext { get; set; }
    public SlotStatus Status { get; set; }

    /// <summary>True when this block was produced by merging adjacent slots (eligible for split back to standard intervals).</summary>
    public bool IsGrouped { get; set; }

    /// <summary>When true, the next public booking request moves the slot to <see cref="SlotStatus.Pending"/> instead of <see cref="SlotStatus.BookedByClient"/>.</summary>
    public bool RequiresApproval { get; set; }

    /// <summary>True when this slot is not open for public booking (<see cref="SlotStatus.Available"/>).</summary>
    [NotMapped]
    public bool IsBooked => Status != SlotStatus.Available;

    public Provider Provider { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
