using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceBooking.Migrations
{
    /// <inheritdoc />
    public partial class PerfIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ResourceId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_IsAvailable",
                table: "Resources",
                column: "IsAvailable");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Name",
                table: "Resources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId_StartTime_EndTime_Cancelled",
                table: "Bookings",
                columns: new[] { "ResourceId", "StartTime", "EndTime", "Cancelled" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StartTime",
                table: "Bookings",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_IsAvailable",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Resources_Name",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ResourceId_StartTime_EndTime_Cancelled",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_StartTime",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId",
                table: "Bookings",
                column: "ResourceId");
        }
    }
}
