using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace YourShipping.Monitor.Server.Migrations
{
    public partial class Add_Store : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Added = table.Column<DateTime>(nullable: false),
                    CategoriesCount = table.Column<int>(nullable: false),
                    IsAvailable = table.Column<bool>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    ProductsCount = table.Column<int>(nullable: false),
                    Read = table.Column<DateTime>(nullable: false),
                    Sha256 = table.Column<string>(nullable: true),
                    Updated = table.Column<DateTime>(nullable: false),
                    Url = table.Column<string>(nullable: true),
                    DepartmentsCount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stores_Url",
                table: "Stores",
                column: "Url",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stores");
        }
    }
}
