using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Geogrid.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadsReservedSuggestive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reserved_areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Geometry = table.Column<Polygon>(type: "geometry(Polygon, 4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reserved_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reserved_areas_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Class = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Lanes = table.Column<int>(type: "integer", nullable: false),
                    WidthMeters = table.Column<double>(type: "double precision", nullable: false),
                    HasFootpath = table.Column<bool>(type: "boolean", nullable: false),
                    HasBikepath = table.Column<bool>(type: "boolean", nullable: false),
                    Geometry = table.Column<LineString>(type: "geometry(LineString, 4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_roads_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "suggestive_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    ToleranceMeters = table.Column<double>(type: "double precision", nullable: false),
                    Geometry = table.Column<LineString>(type: "geometry(LineString, 4326)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_suggestive_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_suggestive_lines_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reserved_areas_Geometry",
                table: "reserved_areas",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_reserved_areas_ProjectId",
                table: "reserved_areas",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_roads_Geometry",
                table: "roads",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_roads_ProjectId",
                table: "roads",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_suggestive_lines_Geometry",
                table: "suggestive_lines",
                column: "Geometry")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_suggestive_lines_ProjectId",
                table: "suggestive_lines",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reserved_areas");

            migrationBuilder.DropTable(
                name: "roads");

            migrationBuilder.DropTable(
                name: "suggestive_lines");
        }
    }
}
