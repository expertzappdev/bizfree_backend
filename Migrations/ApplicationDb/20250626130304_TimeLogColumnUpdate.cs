using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BizfreeApp.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class TimeLogColumnUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_mins",
                table: "task_timelogs");

            migrationBuilder.DropColumn(
                name: "end_time",
                table: "task_timelogs");

            migrationBuilder.RenameColumn(
                name: "start_time",
                table: "task_timelogs",
                newName: "logged_at");

            migrationBuilder.AddColumn<string>(
                name: "duration",
                table: "task_timelogs",
                type: "varchar(5)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration",
                table: "task_timelogs");

            migrationBuilder.RenameColumn(
                name: "logged_at",
                table: "task_timelogs",
                newName: "start_time");

            migrationBuilder.AddColumn<int>(
                name: "duration_mins",
                table: "task_timelogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "end_time",
                table: "task_timelogs",
                type: "timestamp without time zone",
                nullable: true);
        }
    }
}
