using Microsoft.AspNetCore.Identity;

namespace TimeSlotter.Models;

public class Provider : IdentityUser<int>
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Bio { get; set; }

    public ICollection<Slot> Slots { get; set; } = new List<Slot>();
}
