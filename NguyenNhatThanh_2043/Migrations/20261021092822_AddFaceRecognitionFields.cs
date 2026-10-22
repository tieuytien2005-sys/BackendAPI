using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NguyenNhatThanh_2043.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceRecognitionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaceData",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaceImageUrl",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FaceRegisteredAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceData",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FaceImageUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FaceRegisteredAt",
                table: "AspNetUsers");
        }
    }
}
