using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class LabProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_labs_provider_id",
                table: "labs",
                column: "provider_id");

            // WITH NOCHECK, in place of the AddForeignKey EF Core generates: a
            // lab already on file whose provider_id names no provider is kept
            // as it is rather than changed, and DBCC CHECKCONSTRAINTS('labs')
            // reports it. Every lab written from here on is held to the key.
            migrationBuilder.Sql(
                "ALTER TABLE [labs] WITH NOCHECK ADD CONSTRAINT [FK_labs_providers_provider_id] "
                + "FOREIGN KEY ([provider_id]) REFERENCES [providers] ([id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_labs_providers_provider_id",
                table: "labs");

            migrationBuilder.DropIndex(
                name: "IX_labs_provider_id",
                table: "labs");
        }
    }
}
