using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UrbanReports.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CategoriasAtivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ativa",
                table: "categorias",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)1,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)2,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)3,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)4,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)5,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)6,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)7,
                column: "ativa",
                value: true);

            migrationBuilder.UpdateData(
                table: "categorias",
                keyColumn: "id",
                keyValue: (short)8,
                column: "ativa",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_categorias_nome",
                table: "categorias",
                column: "nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_categorias_nome",
                table: "categorias");

            migrationBuilder.DropColumn(
                name: "ativa",
                table: "categorias");
        }
    }
}
