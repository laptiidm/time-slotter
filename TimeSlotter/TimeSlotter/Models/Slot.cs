using System.ComponentModel.DataAnnotations.Schema;

namespace TimeSlotter.Models;

public class Slot
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ProviderId { get; set; }
    public string? ResourceContext { get; set; }
    public SlotStatus Status { get; set; }

    /// <summary>True when this slot is not open for public booking (<see cref="SlotStatus.Available"/>).</summary>
    [NotMapped]
    public bool IsBooked => Status != SlotStatus.Available;

    public Provider Provider { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
