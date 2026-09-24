using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AsistOff.MES.Shared.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeOperatorDepartmentOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Operators_Departments_DepartmentId",
                schema: "config",
                table: "Operators");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                schema: "config",
                table: "Operators",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_Operators_Departments_DepartmentId",
                schema: "config",
                table: "Operators",
                column: "DepartmentId",
                principalSchema: "config",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Operators_Departments_DepartmentId",
                schema: "config",
                table: "Operators");

            migrationBuilder.AlterColumn<Guid>(
                name: "DepartmentId",
                schema: "config",
                table: "Operators",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Operators_Departments_DepartmentId",
                schema: "config",
                table: "Operators",
                column: "DepartmentId",
                principalSchema: "config",
                principalTable: "Departments",
                principalColumn: "Id");
        }
    }
}
