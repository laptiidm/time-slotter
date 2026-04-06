using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSlotter.Migrations
{
    /// <inheritdoc />
    public partial class BookingAssignedByAndCustomerFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedByProviderId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_AssignedByProviderId",
                table: "Bookings",
                column: "AssignedByProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Providers_AssignedByProviderId",
                table: "Bookings",
                column: "AssignedByProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Providers_CustomerId",
                table: "Bookings",
                column: "CustomerId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Providers_AssignedByProviderId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Providers_CustomerId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_AssignedByProviderId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CustomerId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AssignedByProviderId",
                table: "Bookings");
        }
    }
}
