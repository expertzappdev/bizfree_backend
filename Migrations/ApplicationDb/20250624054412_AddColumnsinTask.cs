using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddColumnsinTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "daily_log",
                table: "tasks",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "end_date",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "end_time",
                table: "tasks",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "start_date",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "start_time",
                table: "tasks",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "daily_log",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "end_date",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "end_time",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "start_date",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "start_time",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "code",
                table: "projects");
        }
    }
}
