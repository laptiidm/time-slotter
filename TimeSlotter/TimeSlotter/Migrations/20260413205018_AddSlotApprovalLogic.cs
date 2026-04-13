using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSlotter.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotApprovalLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                table: "Slots",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                table: "Slots");
        }
    }
}
