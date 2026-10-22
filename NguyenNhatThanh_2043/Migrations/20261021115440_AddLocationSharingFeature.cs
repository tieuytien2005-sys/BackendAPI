using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NguyenNhatThanh_2043.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationSharingFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CurrentLatitude",
                table: "AspNetUsers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentLongitude",
                table: "AspNetUsers",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSharingLocation",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLocationUpdatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "AspNetUsers",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocationHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Speed = table.Column<double>(type: "float", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationHistories_UserId_RecordedAt",
                table: "LocationHistories",
                columns: new[] { "UserId", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationHistories");

            migrationBuilder.DropColumn(
                name: "CurrentLatitude",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CurrentLongitude",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsSharingLocation",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLocationUpdatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "AspNetUsers");
        }
    }
}
