using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimTrack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Policies_HolderId",
                table: "Policies",
                column: "HolderId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_AssignedAdjusterId",
                table: "Claims",
                column: "AssignedAdjusterId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Users_AssignedAdjusterId",
                table: "Claims",
                column: "AssignedAdjusterId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Policies_Users_HolderId",
                table: "Policies",
                column: "HolderId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Users_AssignedAdjusterId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Policies_Users_HolderId",
                table: "Policies");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Policies_HolderId",
                table: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_Claims_AssignedAdjusterId",
                table: "Claims");
        }
    }
}
