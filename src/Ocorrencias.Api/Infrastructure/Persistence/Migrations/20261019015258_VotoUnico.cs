using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocorrencias.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VotoUnico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Voto único "Ainda está lá?": o autor não vota mais, e de cada par existência/persistência
            // do mesmo usuário fica o voto mais recente (a última opinião dele sobre o problema).
            migrationBuilder.Sql("""
                DELETE FROM votos v USING problemas p
                WHERE p.id = v.problema_id AND p.autor_id = v.usuario_id;

                DELETE FROM votos v
                WHERE EXISTS (
                    SELECT 1 FROM votos o
                    WHERE o.problema_id = v.problema_id AND o.usuario_id = v.usuario_id
                      AND (o.registrado_em, o.id) > (v.registrado_em, v.id));

                -- A confiança gravada foi calculada só com os votos de existência. O job recalcula a dos
                -- não resolvidos ao subir a API, mas não passa pelos resolvidos (estado final): limpa a
                -- deles para não exibir um índice que não bate com os votos que sobraram.
                UPDATE problemas SET confianca = NULL WHERE status = 4;
                """);

            migrationBuilder.DropIndex(
                name: "ix_votos_problema_id_usuario_id_tipo",
                table: "votos");

            migrationBuilder.DropColumn(
                name: "tipo",
                table: "votos");

            migrationBuilder.CreateIndex(
                name: "ix_votos_problema_id_usuario_id",
                table: "votos",
                columns: new[] { "problema_id", "usuario_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_votos_problema_id_usuario_id",
                table: "votos");

            migrationBuilder.AddColumn<short>(
                name: "tipo",
                table: "votos",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1); // volta como existência

            migrationBuilder.CreateIndex(
                name: "ix_votos_problema_id_usuario_id_tipo",
                table: "votos",
                columns: new[] { "problema_id", "usuario_id", "tipo" },
                unique: true);
        }
    }
}
