using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimTrack.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "ClaimNumbers");

            migrationBuilder.CreateSequence<int>(
                name: "PolicyNumbers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "ClaimNumbers");

            migrationBuilder.DropSequence(
                name: "PolicyNumbers");
        }
    }
}
