using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Survsys.Data.Migrations
{
    /// <inheritdoc />
    public partial class CountyEquivalents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "patient_county",
                table: "system_messages",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "county_equivalent",
                table: "person_addresses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "fips",
                table: "code_values",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "code_values",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "code_values",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ct_towns",
                columns: table => new
                {
                    town = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    county = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    planning_region = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ct_towns", x => x.town);
                });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 381,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 382,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", null });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 383,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 384,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 385,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 386,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 387,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 388,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 389,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 390,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 391,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 392,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 393,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 394,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 395,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 396,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 397,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 398,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 399,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 400,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 401,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 402,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 403,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 404,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 405,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 406,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 407,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 408,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 409,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.UpdateData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 410,
                columns: new[] { "fips", "kind", "state" },
                values: new object[] { null, "county", "UT" });

            migrationBuilder.InsertData(
                table: "code_values",
                columns: new[] { "id", "active", "code", "description", "fips", "kind", "list", "sort_order", "state" },
                values: new object[,]
                {
                    { 1158, true, "09001", "Fairfield", "09001", "county", "county", 200, "CT" },
                    { 1159, true, "09003", "Hartford", "09003", "county", "county", 205, "CT" },
                    { 1160, true, "09005", "Litchfield", "09005", "county", "county", 210, "CT" },
                    { 1161, true, "09007", "Middlesex", "09007", "county", "county", 215, "CT" },
                    { 1162, true, "09009", "New Haven", "09009", "county", "county", 220, "CT" },
                    { 1163, true, "09011", "New London", "09011", "county", "county", 225, "CT" },
                    { 1164, true, "09013", "Tolland", "09013", "county", "county", 230, "CT" },
                    { 1165, true, "09015", "Windham", "09015", "county", "county", 235, "CT" },
                    { 1166, true, "09110", "Capitol Planning Region", "09110", "county-equivalent", "county", 300, "CT" },
                    { 1167, true, "09120", "Greater Bridgeport Planning Region", "09120", "county-equivalent", "county", 305, "CT" },
                    { 1168, true, "09130", "Lower Connecticut River Valley Planning Region", "09130", "county-equivalent", "county", 310, "CT" },
                    { 1169, true, "09140", "Naugatuck Valley Planning Region", "09140", "county-equivalent", "county", 315, "CT" },
                    { 1170, true, "09150", "Northeastern Connecticut Planning Region", "09150", "county-equivalent", "county", 320, "CT" },
                    { 1171, true, "09160", "Northwest Hills Planning Region", "09160", "county-equivalent", "county", 325, "CT" },
                    { 1172, true, "09170", "South Central Connecticut Planning Region", "09170", "county-equivalent", "county", 330, "CT" },
                    { 1173, true, "09180", "Southeastern Connecticut Planning Region", "09180", "county-equivalent", "county", 335, "CT" },
                    { 1174, true, "09190", "Western Connecticut Planning Region", "09190", "county-equivalent", "county", 340, "CT" }
                });

            migrationBuilder.InsertData(
                table: "ct_towns",
                columns: new[] { "town", "county", "planning_region" },
                values: new object[,]
                {
                    { "Andover", "09013", "09110" },
                    { "Ansonia", "09009", "09140" },
                    { "Ashford", "09015", "09150" },
                    { "Avon", "09003", "09110" },
                    { "Barkhamsted", "09005", "09160" },
                    { "Beacon Falls", "09009", "09140" },
                    { "Berlin", "09003", "09110" },
                    { "Bethany", "09009", "09170" },
                    { "Bethel", "09001", "09190" },
                    { "Bethlehem", "09005", "09140" },
                    { "Bloomfield", "09003", "09110" },
                    { "Bolton", "09013", "09110" },
                    { "Bozrah", "09011", "09180" },
                    { "Branford", "09009", "09170" },
                    { "Bridgeport", "09001", "09120" },
                    { "Bridgewater", "09005", "09190" },
                    { "Bristol", "09003", "09140" },
                    { "Brookfield", "09001", "09190" },
                    { "Brooklyn", "09015", "09150" },
                    { "Burlington", "09003", "09160" },
                    { "Canaan", "09005", "09160" },
                    { "Canterbury", "09015", "09150" },
                    { "Canton", "09003", "09110" },
                    { "Chaplin", "09015", "09150" },
                    { "Cheshire", "09009", "09140" },
                    { "Chester", "09007", "09130" },
                    { "Clinton", "09007", "09130" },
                    { "Colchester", "09011", "09180" },
                    { "Colebrook", "09005", "09160" },
                    { "Columbia", "09013", "09110" },
                    { "Cornwall", "09005", "09160" },
                    { "Coventry", "09013", "09110" },
                    { "Cromwell", "09007", "09130" },
                    { "Danbury", "09001", "09190" },
                    { "Darien", "09001", "09190" },
                    { "Deep River", "09007", "09130" },
                    { "Derby", "09009", "09140" },
                    { "Durham", "09007", "09130" },
                    { "East Granby", "09003", "09110" },
                    { "East Haddam", "09007", "09130" },
                    { "East Hampton", "09007", "09130" },
                    { "East Hartford", "09003", "09110" },
                    { "East Haven", "09009", "09170" },
                    { "East Lyme", "09011", "09180" },
                    { "East Windsor", "09003", "09110" },
                    { "Eastford", "09015", "09150" },
                    { "Easton", "09001", "09120" },
                    { "Ellington", "09013", "09110" },
                    { "Enfield", "09003", "09110" },
                    { "Essex", "09007", "09130" },
                    { "Fairfield", "09001", "09120" },
                    { "Farmington", "09003", "09110" },
                    { "Franklin", "09011", "09180" },
                    { "Glastonbury", "09003", "09110" },
                    { "Goshen", "09005", "09160" },
                    { "Granby", "09003", "09110" },
                    { "Greenwich", "09001", "09190" },
                    { "Griswold", "09011", "09180" },
                    { "Groton", "09011", "09180" },
                    { "Guilford", "09009", "09170" },
                    { "Haddam", "09007", "09130" },
                    { "Hamden", "09009", "09170" },
                    { "Hampton", "09015", "09150" },
                    { "Hartford", "09003", "09110" },
                    { "Hartland", "09003", "09160" },
                    { "Harwinton", "09005", "09160" },
                    { "Hebron", "09013", "09110" },
                    { "Kent", "09005", "09160" },
                    { "Killingly", "09015", "09150" },
                    { "Killingworth", "09007", "09130" },
                    { "Lebanon", "09011", "09180" },
                    { "Ledyard", "09011", "09180" },
                    { "Lisbon", "09011", "09180" },
                    { "Litchfield", "09005", "09160" },
                    { "Lyme", "09011", "09130" },
                    { "Madison", "09009", "09170" },
                    { "Manchester", "09003", "09110" },
                    { "Mansfield", "09013", "09110" },
                    { "Marlborough", "09003", "09110" },
                    { "Meriden", "09009", "09170" },
                    { "Middlebury", "09009", "09140" },
                    { "Middlefield", "09007", "09130" },
                    { "Middletown", "09007", "09130" },
                    { "Milford", "09009", "09170" },
                    { "Monroe", "09001", "09120" },
                    { "Montville", "09011", "09180" },
                    { "Morris", "09005", "09160" },
                    { "Naugatuck", "09009", "09140" },
                    { "New Britain", "09003", "09110" },
                    { "New Canaan", "09001", "09190" },
                    { "New Fairfield", "09001", "09190" },
                    { "New Hartford", "09005", "09160" },
                    { "New Haven", "09009", "09170" },
                    { "New London", "09011", "09180" },
                    { "New Milford", "09005", "09190" },
                    { "Newington", "09003", "09110" },
                    { "Newtown", "09001", "09190" },
                    { "Norfolk", "09005", "09160" },
                    { "North Branford", "09009", "09170" },
                    { "North Canaan", "09005", "09160" },
                    { "North Haven", "09009", "09170" },
                    { "North Stonington", "09011", "09180" },
                    { "Norwalk", "09001", "09190" },
                    { "Norwich", "09011", "09180" },
                    { "Old Lyme", "09011", "09130" },
                    { "Old Saybrook", "09007", "09130" },
                    { "Orange", "09009", "09170" },
                    { "Oxford", "09009", "09140" },
                    { "Plainfield", "09015", "09150" },
                    { "Plainville", "09003", "09110" },
                    { "Plymouth", "09005", "09140" },
                    { "Pomfret", "09015", "09150" },
                    { "Portland", "09007", "09130" },
                    { "Preston", "09011", "09180" },
                    { "Prospect", "09009", "09140" },
                    { "Putnam", "09015", "09150" },
                    { "Redding", "09001", "09190" },
                    { "Ridgefield", "09001", "09190" },
                    { "Rocky Hill", "09003", "09110" },
                    { "Roxbury", "09005", "09160" },
                    { "Salem", "09011", "09180" },
                    { "Salisbury", "09005", "09160" },
                    { "Scotland", "09015", "09150" },
                    { "Seymour", "09009", "09140" },
                    { "Sharon", "09005", "09160" },
                    { "Shelton", "09001", "09140" },
                    { "Sherman", "09001", "09190" },
                    { "Simsbury", "09003", "09110" },
                    { "Somers", "09013", "09110" },
                    { "South Windsor", "09003", "09110" },
                    { "Southbury", "09009", "09140" },
                    { "Southington", "09003", "09110" },
                    { "Sprague", "09011", "09180" },
                    { "Stafford", "09013", "09110" },
                    { "Stamford", "09001", "09190" },
                    { "Sterling", "09015", "09150" },
                    { "Stonington", "09011", "09180" },
                    { "Stratford", "09001", "09120" },
                    { "Suffield", "09003", "09110" },
                    { "Thomaston", "09005", "09140" },
                    { "Thompson", "09015", "09150" },
                    { "Tolland", "09013", "09110" },
                    { "Torrington", "09005", "09160" },
                    { "Trumbull", "09001", "09120" },
                    { "Union", "09013", "09150" },
                    { "Vernon", "09013", "09110" },
                    { "Voluntown", "09011", "09150" },
                    { "Wallingford", "09009", "09170" },
                    { "Warren", "09005", "09160" },
                    { "Washington", "09005", "09160" },
                    { "Waterbury", "09009", "09140" },
                    { "Waterford", "09011", "09180" },
                    { "Watertown", "09005", "09140" },
                    { "West Hartford", "09003", "09110" },
                    { "West Haven", "09009", "09170" },
                    { "Westbrook", "09007", "09130" },
                    { "Weston", "09001", "09190" },
                    { "Westport", "09001", "09190" },
                    { "Wethersfield", "09003", "09110" },
                    { "Willington", "09013", "09110" },
                    { "Wilton", "09001", "09190" },
                    { "Winchester", "09005", "09160" },
                    { "Windham", "09015", "09180" },
                    { "Windsor", "09003", "09110" },
                    { "Windsor Locks", "09003", "09110" },
                    { "Wolcott", "09009", "09140" },
                    { "Woodbridge", "09009", "09170" },
                    { "Woodbury", "09005", "09140" },
                    { "Woodstock", "09015", "09150" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ct_towns");

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1158);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1159);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1160);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1161);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1162);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1163);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1164);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1165);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1166);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1167);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1168);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1169);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1170);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1171);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1172);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1173);

            migrationBuilder.DeleteData(
                table: "code_values",
                keyColumn: "id",
                keyValue: 1174);

            migrationBuilder.DropColumn(
                name: "patient_county",
                table: "system_messages");

            migrationBuilder.DropColumn(
                name: "county_equivalent",
                table: "person_addresses");

            migrationBuilder.DropColumn(
                name: "fips",
                table: "code_values");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "code_values");

            migrationBuilder.DropColumn(
                name: "state",
                table: "code_values");
        }
    }
}
