using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "search_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    query = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    resolved_stock_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    resolved_company_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    searched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "summaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    stock_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    company_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    raw_data_json = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_summaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_search_history_searched_at",
                table: "search_history",
                column: "searched_at");

            migrationBuilder.CreateIndex(
                name: "idx_summaries_expires_at",
                table: "summaries",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "idx_summaries_stock_code",
                table: "summaries",
                column: "stock_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_history");

            migrationBuilder.DropTable(
                name: "summaries");
        }
    }
}
