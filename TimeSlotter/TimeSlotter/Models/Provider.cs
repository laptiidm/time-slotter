using Microsoft.AspNetCore.Identity;

namespace TimeSlotter.Models;

public class Provider : IdentityUser<int>
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Bio { get; set; }

    /// <summary>Minutes per piece when splitting a merged (<see cref="Slot.IsGrouped"/>) slot.</summary>
    public int DefaultSlotIntervalMinutes { get; set; } = 30;

    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
}
