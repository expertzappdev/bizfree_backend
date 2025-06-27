using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata; 

#nullable disable

namespace BizfreeApp.Migrations.ApplicationDb
{
    /// <inheritdoc />
    public partial class AddCompanyForeignKeyToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
       
            migrationBuilder.AddColumn<int>(
                name: "company_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_company_id",
                table: "users",
                column: "company_id");

            
            migrationBuilder.AddForeignKey(
                name: "FK_users_companies_company_id", 
                table: "users",
                column: "company_id",
                principalTable: "companies", 
                principalColumn: "company_id", 
                onDelete: ReferentialAction.Restrict); 
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_companies_company_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_company_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "users");

   
        }
    }
}
