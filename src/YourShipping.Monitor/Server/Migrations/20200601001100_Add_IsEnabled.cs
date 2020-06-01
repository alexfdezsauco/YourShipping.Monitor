using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_IsEnabled : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Stores",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Products",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Departments",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "Departments");
        }
    }
}
