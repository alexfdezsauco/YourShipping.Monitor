namespace YourShipping.Monitor.Server.Migrations
{
    using System;

    using Microsoft.EntityFrameworkCore.Migrations;

    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Added = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Store = table.Column<string>(nullable: true),
                    ProductsCount = table.Column<int>(nullable: false),
                    Read = table.Column<DateTime>(nullable: false),
                    Updated = table.Column<DateTime>(nullable: false),
                    Url = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Added = table.Column<DateTime>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Price = table.Column<float>(nullable: false),
                    Read = table.Column<DateTime>(nullable: false),
                    Updated = table.Column<DateTime>(nullable: false),
                    Url = table.Column<string>(nullable: true),
                    Currency = table.Column<string>(nullable: true),
                    Store = table.Column<string>(nullable: true),
                    IsAvailable = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Url",
                table: "Departments",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Url",
                table: "Products",
                column: "Url",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
