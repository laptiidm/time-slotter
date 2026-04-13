using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Data;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

/// <summary>
/// Site <c>Admin</c> role: reserve/release slot status. Used from <see cref="IndexModel"/> (and optionally elsewhere).
/// Razor Pages do not support <c>[Authorize(Roles = "Admin")]</c> on named handlers, so the role is checked here.
/// </summary>
public static class AdminSlotRoleHandlers
{
    public static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<IActionResult> ReserveSlotAsync(ClaimsPrincipal user, AppDbContext context, int slotId)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Admin"))
        {
            return new JsonResult(new { success = false, error = "Доступ заборонено." }, JsonWriteOptions)
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }

        await using var tx = await context.Database.BeginTransactionAsync();
        var updated = await context.Slots
            .Where(s => s.Id == slotId && s.Status == SlotStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, SlotStatus.ReservedByAdmin));
        if (updated == 0)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Слот уже недоступний або не знайдено." }, JsonWriteOptions)
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }

        await tx.CommitAsync();
        return new JsonResult(new { success = true, newStatus = "reserved", note = "admin_reserve" }, JsonWriteOptions);
    }

    public static async Task<IActionResult> ReleaseSlotAsync(ClaimsPrincipal user, AppDbContext context, int slotId)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole("Admin"))
        {
            return new JsonResult(new { success = false, error = "Доступ заборонено." }, JsonWriteOptions)
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }

        await using var tx = await context.Database.BeginTransactionAsync();
        var updated = await context.Slots
            .Where(s => s.Id == slotId && s.Status == SlotStatus.ReservedByAdmin)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, SlotStatus.Available));
        if (updated == 0)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Слот не в адмін-блоці або не знайдено." }, JsonWriteOptions)
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }

        await tx.CommitAsync();
        return new JsonResult(new { success = true, newStatus = "available" }, JsonWriteOptions);
    }
}
