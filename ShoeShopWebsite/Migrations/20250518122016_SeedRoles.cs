using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class SeedRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "3a6f9f49-309f-4e11-8f44-c75fcb0b9e89", "02a02f43-81f4-48dd-bb13-37db2347c18f", "Customer", "CUSTOMER" },
                    { "b1d1f35e-7d18-4a17-b88a-c8e1e8d9a210", "f21a49e8-5e1a-4234-9f92-0f430f0a1467", "Admin", "ADMIN" },
                    { "dd76877a-4e07-4d84-a55f-c94a1d4f45a3", "cd17c4b9-11b9-41bc-9b72-6f4e0deee5a4", "Employee", "EMPLOYEE" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "3a6f9f49-309f-4e11-8f44-c75fcb0b9e89");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "b1d1f35e-7d18-4a17-b88a-c8e1e8d9a210");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "dd76877a-4e07-4d84-a55f-c94a1d4f45a3");
        }
    }
}
