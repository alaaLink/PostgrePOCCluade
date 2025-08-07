using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PostgreMigrationPOC.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SmallIntField = table.Column<short>(type: "smallint", nullable: false),
                    BigIntField = table.Column<long>(type: "bigint", nullable: false),
                    TinyIntField = table.Column<byte>(type: "tinyint", nullable: false),
                    DecimalPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MoneyField = table.Column<decimal>(type: "money", nullable: false),
                    SmallMoneyField = table.Column<decimal>(type: "smallmoney", nullable: false),
                    FloatField = table.Column<double>(type: "float", nullable: false),
                    RealField = table.Column<float>(type: "real", nullable: false),
                    VarcharField = table.Column<string>(type: "varchar(100)", nullable: false),
                    NvarcharField = table.Column<string>(type: "nvarchar(200)", nullable: false),
                    CharField = table.Column<string>(type: "char(10)", nullable: false),
                    NcharField = table.Column<string>(type: "nchar(5)", nullable: false),
                    TextField = table.Column<string>(type: "text", nullable: true),
                    NtextField = table.Column<string>(type: "ntext", nullable: true),
                    DateTimeField = table.Column<DateTime>(type: "datetime", nullable: false),
                    DateTime2Field = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateField = table.Column<DateOnly>(type: "date", nullable: false),
                    TimeField = table.Column<TimeOnly>(type: "time", nullable: false),
                    DateTimeOffsetField = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SmallDateTimeField = table.Column<DateTime>(type: "smalldatetime", nullable: false),
                    BinaryField = table.Column<byte[]>(type: "binary(16)", nullable: true),
                    VarbinaryField = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    BooleanField = table.Column<bool>(type: "bit", nullable: false),
                    GuidField = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    XmlField = table.Column<string>(type: "xml", nullable: true),
                    HierarchyIdField = table.Column<string>(type: "hierarchyid", nullable: true),
                    GeographyField = table.Column<string>(type: "geography", nullable: true),
                    GeometryField = table.Column<string>(type: "geometry", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    DetailedDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ManufacturerInfo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetails_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductTags",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TagId = table.Column<int>(type: "int", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTags", x => new { x.ProductId, x.TagId });
                    table.ForeignKey(
                        name: "FK_ProductTags_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetails_ProductId",
                table: "ProductDetails",
                column: "ProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTags_TagId",
                table: "ProductTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_Tags_Name",
                table: "Tags",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductDetails");

            migrationBuilder.DropTable(
                name: "ProductTags");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
