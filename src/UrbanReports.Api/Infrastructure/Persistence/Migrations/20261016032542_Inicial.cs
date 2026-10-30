using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace UrbanReports.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "categorias",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false),
                    nome = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    grupo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    icone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categorias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    perfil = table.Column<short>(type: "smallint", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verificado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    senha_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "incidentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<short>(type: "smallint", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_aproximada = table.Column<Point>(type: "geography(Point,4326)", nullable: false),
                    raio_metros = table.Column<short>(type: "smallint", nullable: false),
                    rotulo_area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_incidentes", x => x.id);
                    table.ForeignKey(
                        name: "fk_incidentes_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_incidentes_usuarios_autor_id",
                        column: x => x.autor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "problemas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    categoria_id = table.Column<short>(type: "smallint", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: true),
                    localizacao = table.Column<Point>(type: "geography(Point,4326)", nullable: false),
                    endereco_aproximado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    confianca = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status_alterado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    condicao_resolucao_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_problemas", x => x.id);
                    table.ForeignKey(
                        name: "fk_problemas_categorias_categoria_id",
                        column: x => x.categoria_id,
                        principalTable: "categorias",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_problemas_usuarios_autor_id",
                        column: x => x.autor_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "solicitacoes_verificacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_vinculo = table.Column<short>(type: "smallint", nullable: false),
                    justificativa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    motivo_recusa = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    analisada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    analisada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitacoes_verificacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_solicitacoes_verificacao_usuarios_analisada_por_id",
                        column: x => x.analisada_por_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_solicitacoes_verificacao_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuarios_claims_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_usuarios_logins_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_usuarios_tokens_usuarios_user_id",
                        column: x => x.user_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "votos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    problema_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<short>(type: "smallint", nullable: false),
                    valor = table.Column<short>(type: "smallint", nullable: false),
                    perfil_no_momento = table.Column<short>(type: "smallint", nullable: false),
                    distancia_metros = table.Column<int>(type: "integer", nullable: true),
                    registrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_votos", x => x.id);
                    table.ForeignKey(
                        name: "fk_votos_problemas_problema_id",
                        column: x => x.problema_id,
                        principalTable: "problemas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_votos_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "categorias",
                columns: new[] { "id", "grupo", "icone", "nome", "tipo" },
                values: new object[,]
                {
                    { (short)1, "viaPublica", "buraco", "Buraco na via", (short)1 },
                    { (short)2, "viaPublica", "calcada", "Calçada danificada", (short)1 },
                    { (short)3, "eletrica", "poste", "Poste apagado", (short)1 },
                    { (short)4, "eletrica", "fio", "Fio caído", (short)1 },
                    { (short)5, "limpeza", "descarte", "Descarte irregular", (short)1 },
                    { (short)6, "limpeza", "lixo", "Lixo acumulado", (short)1 },
                    { (short)7, "seguranca", "roubo", "Roubo", (short)2 },
                    { (short)8, "transito", "acidente", "Acidente de trânsito", (short)2 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_incidentes_area_aproximada",
                table: "incidentes",
                column: "area_aproximada")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_incidentes_autor_id_registrado_em",
                table: "incidentes",
                columns: new[] { "autor_id", "registrado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_incidentes_categoria_id",
                table: "incidentes",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_incidentes_ocorrido_em",
                table: "incidentes",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_problemas_autor_id_criado_em",
                table: "problemas",
                columns: new[] { "autor_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_problemas_categoria_id",
                table: "problemas",
                column: "categoria_id");

            migrationBuilder.CreateIndex(
                name: "ix_problemas_localizacao",
                table: "problemas",
                column: "localizacao")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_problemas_status",
                table: "problemas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_verificacao_analisada_por_id",
                table: "solicitacoes_verificacao",
                column: "analisada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_verificacao_status_criada_em",
                table: "solicitacoes_verificacao",
                columns: new[] { "status", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_solicitacoes_verificacao_usuario_id_criada_em",
                table: "solicitacoes_verificacao",
                columns: new[] { "usuario_id", "criada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_normalized_email",
                table: "usuarios",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_normalized_user_name",
                table: "usuarios",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_claims_user_id",
                table: "usuarios_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_logins_user_id",
                table: "usuarios_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_votos_problema_id_usuario_id_tipo",
                table: "votos",
                columns: new[] { "problema_id", "usuario_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_votos_usuario_id_registrado_em",
                table: "votos",
                columns: new[] { "usuario_id", "registrado_em" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incidentes");

            migrationBuilder.DropTable(
                name: "solicitacoes_verificacao");

            migrationBuilder.DropTable(
                name: "usuarios_claims");

            migrationBuilder.DropTable(
                name: "usuarios_logins");

            migrationBuilder.DropTable(
                name: "usuarios_tokens");

            migrationBuilder.DropTable(
                name: "votos");

            migrationBuilder.DropTable(
                name: "problemas");

            migrationBuilder.DropTable(
                name: "categorias");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
