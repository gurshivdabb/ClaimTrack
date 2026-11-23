using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimTrack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimFlaggedOverdueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FlaggedOverdueAt",
                table: "Claims",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlaggedOverdueAt",
                table: "Claims");
        }
    }
}
