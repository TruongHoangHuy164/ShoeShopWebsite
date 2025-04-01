using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShoeShopWebsite.Migrations
{
    /// <inheritdoc />
    public partial class AddProvinceDistrictWardRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts");

            migrationBuilder.DropForeignKey(
                name: "FK_Wards_Districts_DistrictId",
                table: "Wards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Wards",
                table: "Wards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Provinces",
                table: "Provinces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Districts",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Wards");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Provinces");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Districts");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Wards",
                newName: "DivisionType");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Provinces",
                newName: "DivisionType");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Districts",
                newName: "DivisionType");

            migrationBuilder.AlterColumn<int>(
                name: "DistrictId",
                table: "Wards",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "Code",
                table: "Wards",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "Code",
                table: "Provinces",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "ProvinceId",
                table: "Districts",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<int>(
                name: "Code",
                table: "Districts",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Wards",
                table: "Wards",
                column: "Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Provinces",
                table: "Provinces",
                column: "Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Districts",
                table: "Districts",
                column: "Code");

            migrationBuilder.AddForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Wards_Districts_DistrictId",
                table: "Wards",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Code",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts");

            migrationBuilder.DropForeignKey(
                name: "FK_Wards_Districts_DistrictId",
                table: "Wards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Wards",
                table: "Wards");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Provinces",
                table: "Provinces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Districts",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Wards");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Provinces");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Districts");

            migrationBuilder.RenameColumn(
                name: "DivisionType",
                table: "Wards",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "DivisionType",
                table: "Provinces",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "DivisionType",
                table: "Districts",
                newName: "Type");

            migrationBuilder.AlterColumn<string>(
                name: "DistrictId",
                table: "Wards",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Wards",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Provinces",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ProvinceId",
                table: "Districts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                table: "Districts",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Wards",
                table: "Wards",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Provinces",
                table: "Provinces",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Districts",
                table: "Districts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Districts_Provinces_ProvinceId",
                table: "Districts",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Wards_Districts_DistrictId",
                table: "Wards",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
