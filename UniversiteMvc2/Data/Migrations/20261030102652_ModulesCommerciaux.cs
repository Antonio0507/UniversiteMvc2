using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace UniversiteMvc2.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModulesCommerciaux : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comptes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Email = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Nom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Actif = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MotDePasseHash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    DoitChangerMotDePasse = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EchecsConnexion = table.Column<int>(type: "int", nullable: false),
                    VerrouilleJusquA = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DerniereConnexion = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Jeton = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comptes", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JournalAudit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DateUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Auteur = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Entite = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    EntiteId = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalAudit", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InscriptionId = table.Column<int>(type: "int", nullable: false),
                    UEId = table.Column<int>(type: "int", nullable: false),
                    Valeur = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true),
                    Rattrapage = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Inscriptions_InscriptionId",
                        column: x => x.InscriptionId,
                        principalTable: "Inscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notes_UEs_UEId",
                        column: x => x.UEId,
                        principalTable: "UEs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tarifs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AnneeAcadId = table.Column<int>(type: "int", nullable: false),
                    FiliereId = table.Column<int>(type: "int", nullable: false),
                    NiveauId = table.Column<int>(type: "int", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarifs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarifs_AnneesAcad_AnneeAcadId",
                        column: x => x.AnneeAcadId,
                        principalTable: "AnneesAcad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarifs_Filieres_FiliereId",
                        column: x => x.FiliereId,
                        principalTable: "Filieres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tarifs_Niveaux_NiveauId",
                        column: x => x.NiveauId,
                        principalTable: "Niveaux",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UEs_Code",
                table: "UEs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Niveaux_Code",
                table: "Niveaux",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inscriptions_EtudiantId_AnneeAcadId",
                table: "Inscriptions",
                columns: new[] { "EtudiantId", "AnneeAcadId" },
                unique: true);

            // Après la création de l'index composite : la clé étrangère a besoin d'un index sur EtudiantId
            migrationBuilder.DropIndex(
                name: "IX_Inscriptions_EtudiantId",
                table: "Inscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_AnneesAcad_Libelle",
                table: "AnneesAcad",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Comptes_Email",
                table: "Comptes",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalAudit_DateUtc",
                table: "JournalAudit",
                column: "DateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_InscriptionId_UEId",
                table: "Notes",
                columns: new[] { "InscriptionId", "UEId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UEId",
                table: "Notes",
                column: "UEId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarifs_AnneeAcadId_FiliereId_NiveauId",
                table: "Tarifs",
                columns: new[] { "AnneeAcadId", "FiliereId", "NiveauId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarifs_FiliereId",
                table: "Tarifs",
                column: "FiliereId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarifs_NiveauId",
                table: "Tarifs",
                column: "NiveauId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Comptes");

            migrationBuilder.DropTable(
                name: "JournalAudit");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Tarifs");

            migrationBuilder.DropIndex(
                name: "IX_UEs_Code",
                table: "UEs");

            migrationBuilder.DropIndex(
                name: "IX_Niveaux_Code",
                table: "Niveaux");

            migrationBuilder.DropIndex(
                name: "IX_AnneesAcad_Libelle",
                table: "AnneesAcad");

            migrationBuilder.CreateIndex(
                name: "IX_Inscriptions_EtudiantId",
                table: "Inscriptions",
                column: "EtudiantId");

            migrationBuilder.DropIndex(
                name: "IX_Inscriptions_EtudiantId_AnneeAcadId",
                table: "Inscriptions");
        }
    }
}
