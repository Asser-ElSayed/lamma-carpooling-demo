using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Lamma.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Corridor = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RouteOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNodes", x => x.Id);
                    table.CheckConstraint("CK_MeetingNodes_Order", "[RouteOrder] > 0");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MembershipActive = table.Column<bool>(type: "bit", nullable: false),
                    HostApproved = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartureAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SeatCapacity = table.Column<int>(type: "int", nullable: false),
                    AvailableSeats = table.Column<int>(type: "int", nullable: false),
                    AllowsIntermediateStops = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rides", x => x.Id);
                    table.CheckConstraint("CK_Rides_Availability", "[AvailableSeats] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Rides_Capacity", "[SeatCapacity] = 3");
                    table.CheckConstraint("CK_Rides_State", "([AvailableSeats] = 0 AND [Status] = 'Full') OR ([AvailableSeats] > 0 AND [Status] = 'Published')");
                    table.ForeignKey(
                        name: "FK_Rides_MeetingNodes_DestinationNodeId",
                        column: x => x.DestinationNodeId,
                        principalTable: "MeetingNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rides_MeetingNodes_OriginNodeId",
                        column: x => x.OriginNodeId,
                        principalTable: "MeetingNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rides_Users_HostId",
                        column: x => x.HostId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seats = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    RequestFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_Seats", "[Seats] = 1");
                    table.CheckConstraint("CK_Bookings_State", "[Status] = 'Confirmed'");
                    table.ForeignKey(
                        name: "FK_Bookings_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Bookings_Users_RiderId",
                        column: x => x.RiderId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RideId",
                table: "Bookings",
                column: "RideId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RiderId_IdempotencyKey",
                table: "Bookings",
                columns: new[] { "RiderId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RiderId_RideId",
                table: "Bookings",
                columns: new[] { "RiderId", "RideId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNodes_Corridor_Direction_RouteOrder",
                table: "MeetingNodes",
                columns: new[] { "Corridor", "Direction", "RouteOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_DestinationNodeId",
                table: "Rides",
                column: "DestinationNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Rides_HostId_Status_DepartureAtUtc",
                table: "Rides",
                columns: new[] { "HostId", "Status", "DepartureAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_OriginNodeId_Status_DepartureAtUtc",
                table: "Rides",
                columns: new[] { "OriginNodeId", "Status", "DepartureAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bookings");

            migrationBuilder.DropTable(
                name: "Rides");

            migrationBuilder.DropTable(
                name: "MeetingNodes");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
