using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LivroRazao.Infraestrutura.Migracao
{
    /// <inheritdoc />
    public partial class CriarEstruturaInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_LANCAMENTO",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CORRELATION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    TIPO = table.Column<string>(type: "char(1)", fixedLength: true, maxLength: 1, nullable: false),
                    DATA_LANCAMENTO = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VALOR = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DESCRICAO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DATA_CRIACAO = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LANCAMENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_LANCAMENTO_OUTBOX",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CORRELATION_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    TIPO_EVENTO = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PAYLOAD = table.Column<string>(type: "jsonb", nullable: false),
                    DATA_OCORRENCIA = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DATA_CRIACAO = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DATA_PUBLICACAO = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DATA_ULTIMA_TENTATIVA = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TENTATIVAS = table.Column<int>(type: "integer", nullable: false),
                    ULTIMO_ERRO = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    STATUS = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LANCAMENTO_OUTBOX", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TB_LANCAMENTO_CORRELATION_ID",
                table: "TB_LANCAMENTO",
                column: "CORRELATION_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TB_LANCAMENTO_OUTBOX_CORRELATION_ID",
                table: "TB_LANCAMENTO_OUTBOX",
                column: "CORRELATION_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TB_LANCAMENTO_OUTBOX_STATUS_DATA_CRIACAO_DATA_ULTIMA_TENTAT~",
                table: "TB_LANCAMENTO_OUTBOX",
                columns: new[] { "STATUS", "DATA_CRIACAO", "DATA_ULTIMA_TENTATIVA" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_LANCAMENTO");

            migrationBuilder.DropTable(
                name: "TB_LANCAMENTO_OUTBOX");
        }
    }
}
