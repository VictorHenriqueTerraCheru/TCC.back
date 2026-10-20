using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Ocorrencias.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GruposDeCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "grupos_categoria",
                columns: table => new
                {
                    chave = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    rotulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    cor = table.Column<short>(type: "smallint", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_categoria", x => x.chave);
                });

            migrationBuilder.InsertData(
                table: "grupos_categoria",
                columns: new[] { "chave", "cor", "ordem", "rotulo", "tipo" },
                values: new object[,]
                {
                    { "eletrica", (short)2, 2, "Elétrica", (short)1 },
                    { "limpeza", (short)3, 3, "Limpeza", (short)1 },
                    { "seguranca", (short)4, 4, "Segurança", (short)2 },
                    { "transito", (short)5, 5, "Trânsito", (short)2 },
                    { "viaPublica", (short)1, 1, "Via pública", (short)1 }
                });

            // Categorias criadas pelo CRUD antes de existir cadastro de grupos podem ter grupo fora do seed:
            // viram grupo (rótulo = chave, cor cinza, tipo da categoria) para a FK abaixo não falhar.
            migrationBuilder.Sql("""
                INSERT INTO grupos_categoria (chave, rotulo, tipo, cor, ordem)
                SELECT c.grupo, c.grupo, MIN(c.tipo), 10, 100 + (ROW_NUMBER() OVER (ORDER BY c.grupo))::int
                FROM categorias c
                WHERE NOT EXISTS (SELECT 1 FROM grupos_categoria g WHERE g.chave = c.grupo)
                GROUP BY c.grupo;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_grupo",
                table: "categorias",
                column: "grupo");

            migrationBuilder.CreateIndex(
                name: "ix_grupos_categoria_rotulo",
                table: "grupos_categoria",
                column: "rotulo",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_categorias_grupos_categoria_grupo",
                table: "categorias",
                column: "grupo",
                principalTable: "grupos_categoria",
                principalColumn: "chave",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_categorias_grupos_categoria_grupo",
                table: "categorias");

            migrationBuilder.DropTable(
                name: "grupos_categoria");

            migrationBuilder.DropIndex(
                name: "ix_categorias_grupo",
                table: "categorias");
        }
    }
}
