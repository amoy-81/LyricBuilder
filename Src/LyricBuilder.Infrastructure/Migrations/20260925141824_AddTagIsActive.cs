using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LyricBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTagIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing tags were all visible before this column existed, so they start active.
            // The default only backfills them: the model sets IsActive on every insert.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Tags",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Tags");
        }
    }
}
