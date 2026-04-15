using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSlotter.Migrations
{
    /// <inheritdoc />
    public partial class AddClientCommentToSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientComment",
                table: "Slots",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientComment",
                table: "Slots");
        }
    }
}
