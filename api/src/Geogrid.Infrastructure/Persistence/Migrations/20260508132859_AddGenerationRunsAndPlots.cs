using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Geogrid.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGenerationRunsAndPlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Algorithm = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    ParametersJson = table.Column<string>(type: "jsonb", nullable: false),
                    StatsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generation_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_generation_runs_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenerationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockIndex = table.Column<int>(type: "integer", nullable: false),
                    Geometry = table.Column<Polygon>(type: "geometry(Polygon, 4326)", nullable: false),
                    AreaSqM = table.Column<double>(type: "double precision", nullable: false),
                    RoadFrontageMeters = table.Column<double>(type: "double precision", nullable: false),
                    ValidationPassed = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plots_generation_runs_GenerationRunId",
                        column: x => x.GenerationRunId,
                        principalTable: "generation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plots_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_generation_runs_ProjectId",
                table: "generation_runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_generation_runs_ProjectId_Status",
                table: "generation_runs",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_plots_GenerationRunId",
                table: "plots",
                column: "GenerationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_plots_Geometry",
                table: "plots",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_plots_ProjectId",
                table: "plots",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plots");

            migrationBuilder.DropTable(
                name: "generation_runs");
        }
    }
}
