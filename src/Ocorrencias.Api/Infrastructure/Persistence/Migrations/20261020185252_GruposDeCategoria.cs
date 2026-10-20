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

            // Categorias criadas pelo CRUD antes de existir cadastro de grupos podem ter grupo fora do seed, ou
            // com tipo diferente do grupo de mesma chave (a mesma chave valia para problema e incidente).
            // Cada par (grupo, tipo) sem grupo compatível vira grupo (rótulo = chave + " (migrado)", cinza, tipo da
            // categoria). Se a chave já é de um grupo de outro tipo, as categorias passam para a chave
            // <grupo>Estado / <grupo>Evento (com número no fim se ainda colidir), nunca para um tipo escolhido
            // ao acaso. Assim a FK abaixo não falha e nenhuma categoria fica em grupo de tipo incompatível.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    par record;
                    nova text;
                    novo_rotulo text;
                    n int;
                    proxima_ordem int := 100;
                BEGIN
                    FOR par IN
                        SELECT DISTINCT c.grupo, c.tipo
                        FROM categorias c
                        WHERE NOT EXISTS (SELECT 1 FROM grupos_categoria g WHERE g.chave = c.grupo AND g.tipo = c.tipo)
                        ORDER BY c.grupo, c.tipo
                    LOOP
                        nova := par.grupo;
                        IF EXISTS (SELECT 1 FROM grupos_categoria WHERE chave = nova) THEN
                            nova := left(par.grupo, 24) || CASE par.tipo WHEN 1 THEN 'Estado' ELSE 'Evento' END;
                        END IF;
                        n := 1;
                        WHILE EXISTS (SELECT 1 FROM grupos_categoria WHERE chave = nova) LOOP
                            n := n + 1;
                            nova := left(par.grupo, 24) || CASE par.tipo WHEN 1 THEN 'Estado' ELSE 'Evento' END;
                            nova := left(nova, 27) || n::text;
                        END LOOP;

                        -- Rótulo único sem distinguir caixa (chaves fooBar e foobar): é a regra do service.
                        novo_rotulo := nova || ' (migrado)';
                        n := 1;
                        WHILE EXISTS (SELECT 1 FROM grupos_categoria WHERE lower(rotulo) = lower(novo_rotulo)) LOOP
                            n := n + 1;
                            novo_rotulo := left(nova, 20) || ' (migrado ' || n::text || ')';
                        END LOOP;

                        proxima_ordem := proxima_ordem + 1;
                        INSERT INTO grupos_categoria (chave, rotulo, tipo, cor, ordem)
                        VALUES (nova, novo_rotulo, par.tipo, 10, proxima_ordem);

                        IF nova <> par.grupo THEN
                            UPDATE categorias SET grupo = nova WHERE grupo = par.grupo AND tipo = par.tipo;
                        END IF;
                    END LOOP;
                END $$;
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
