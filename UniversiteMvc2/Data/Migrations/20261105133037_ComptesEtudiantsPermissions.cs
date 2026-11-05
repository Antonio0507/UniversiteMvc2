using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace UniversiteMvc2.Data.Migrations
{
    /// <inheritdoc />
    public partial class ComptesEtudiantsPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EtudiantId",
                table: "Comptes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Section = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Acces = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_EtudiantId",
                table: "Comptes",
                column: "EtudiantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Role_Section",
                table: "Permissions",
                columns: new[] { "Role", "Section" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Comptes_Utilisateurs_EtudiantId",
                table: "Comptes",
                column: "EtudiantId",
                principalTable: "Utilisateurs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comptes_Utilisateurs_EtudiantId",
                table: "Comptes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Comptes_EtudiantId",
                table: "Comptes");

            migrationBuilder.DropColumn(
                name: "EtudiantId",
                table: "Comptes");
        }
    }
}
