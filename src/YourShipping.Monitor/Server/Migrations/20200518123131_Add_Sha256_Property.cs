using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_Sha256_Property : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "Products",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "Departments",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "Departments");
        }
    }
}
