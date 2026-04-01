namespace TimeSlotter.Models;

public class Slot
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ProviderId { get; set; }
    public SlotStatus Status { get; set; }

    public Provider Provider { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
