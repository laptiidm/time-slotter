namespace TimeSlotter.Models;

/// <summary>Client booking: name and phone. Admin holds do not use this entity.</summary>
public class Booking
{
    public int Id { get; set; }
    public int SlotId { get; set; }

    /// <summary>Logged-in customer account (<see cref="Provider"/>); null for anonymous public booking or walk-in from admin.</summary>
    public int? CustomerId { get; set; }

    /// <summary>Schedule owner who created this booking from the admin panel; null for self-service (public or logged-in client).</summary>
    public int? AssignedByProviderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Slot Slot { get; set; } = null!;
    public Provider? Customer { get; set; }
    public Provider? AssignedByProvider { get; set; }
}
