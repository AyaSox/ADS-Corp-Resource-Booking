using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResourceBooking.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringAndUnavailabilityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnavailabilityReason",
                table: "Resources",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnavailabilityType",
                table: "Resources",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnavailableUntil",
                table: "Resources",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecurring",
                table: "Bookings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentBookingId",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceEndDate",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceInterval",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceType",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Resources",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "UnavailabilityReason", "UnavailabilityType", "UnavailableUntil" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                table: "Resources",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UnavailabilityReason", "UnavailabilityType", "UnavailableUntil" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ParentBookingId",
                table: "Bookings",
                column: "ParentBookingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Bookings_ParentBookingId",
                table: "Bookings",
                column: "ParentBookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Bookings_ParentBookingId",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ParentBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "UnavailabilityReason",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "UnavailabilityType",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "UnavailableUntil",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "IsRecurring",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ParentBookingId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "RecurrenceType",
                table: "Bookings");
        }
    }
}
