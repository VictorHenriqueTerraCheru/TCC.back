using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocorrencias.Api.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class VerificacaoPendenteUnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_verificacao_usuario_pendente",
                table: "solicitacoes_verificacao",
                column: "usuario_id",
                unique: true,
                filter: "status = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_solicitacoes_verificacao_usuario_pendente",
                table: "solicitacoes_verificacao");
        }
    }
}
