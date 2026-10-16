using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ocorrencias.Api.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Índices GIST de expressão em <c>::geometry</c> para o recorte retangular (planar) da área visível do
    /// mapa (<c>GET /problemas</c> e <c>GET /incidentes/calor</c>). O EF não modela índice de expressão.
    /// </summary>
    public partial class IndicesRetanguloGeometria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE INDEX ix_problemas_localizacao_geometry ON problemas USING gist ((localizacao::geometry));");
            migrationBuilder.Sql("CREATE INDEX ix_incidentes_area_aproximada_geometry ON incidentes USING gist ((area_aproximada::geometry));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_incidentes_area_aproximada_geometry;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_problemas_localizacao_geometry;");
        }
    }
}
