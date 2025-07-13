using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedIsTenantAdminColumnToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTenantAdmin",
                schema: "users",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTenantAdmin",
                schema: "users",
                table: "Users");
        }
    }
}
