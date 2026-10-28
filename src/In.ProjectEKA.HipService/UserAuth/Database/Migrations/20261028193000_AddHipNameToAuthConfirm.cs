using In.ProjectEKA.HipService.UserAuth.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace In.ProjectEKA.HipService.Migrations
{
    [DbContext(typeof(AuthContext))]
    [Migration("20261028193000_AddHipNameToAuthConfirm")]
    public class AddHipNameToAuthConfirm : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HipName",
                table: "AuthConfirm",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HipName",
                table: "AuthConfirm");
        }
    }
}
