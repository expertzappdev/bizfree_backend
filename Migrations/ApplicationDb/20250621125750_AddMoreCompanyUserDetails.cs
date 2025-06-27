using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata; // Keep this if you are using PostgreSQL specific annotations

#nullable disable

namespace BizfreeApp.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddMoreCompanyUserDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Removed/commented out all unrelated DropForeignKey, DropIndex, DropColumn, RenameColumn, AlterColumn, CreateIndex, AddForeignKey operations.
            // Only keeping the AddColumn operations for the new fields in 'company_users'.

            migrationBuilder.AddColumn<string>(
                name: "address_line1",
                table: "company_users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blood_group",
                table: "company_users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "company_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "company_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_no",
                table: "company_users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "company_users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "company_users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Corresponding DropColumn operations for the new fields in 'company_users'
            // (all other unrelated down migration steps are also removed/commented out for consistency)
            migrationBuilder.DropColumn(
                name: "address_line1",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "blood_group",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "city",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "phone_no",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "company_users");

            migrationBuilder.DropColumn(
                name: "state",
                table: "company_users");
        }
    }
}
