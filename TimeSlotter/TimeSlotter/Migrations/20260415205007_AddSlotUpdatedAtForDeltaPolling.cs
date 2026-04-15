using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimeSlotter.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotUpdatedAtForDeltaPolling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Slots",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Slots");
        }
    }
}
