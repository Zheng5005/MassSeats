using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "available_seats",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE events SET available_seats = total_seats;");

            migrationBuilder.AlterColumn<int>(
                name: "available_seats",
                table: "events",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "available_seats",
                table: "events");
        }
    }
}
