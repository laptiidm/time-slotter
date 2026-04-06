using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSlotter.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeSlotStatusPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Former Pending (=1) is no longer used; coerce to Available (=0).
            migrationBuilder.Sql("UPDATE [Slots] SET [Status] = 0 WHERE [Status] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: Available (0) cannot be split into former Pending vs originally Available.
        }
    }
}
