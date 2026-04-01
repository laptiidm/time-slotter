namespace TimeSlotter.Models;

public class Booking
{
    public int Id { get; set; }
    public int SlotId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Slot Slot { get; set; } = null!;
}
