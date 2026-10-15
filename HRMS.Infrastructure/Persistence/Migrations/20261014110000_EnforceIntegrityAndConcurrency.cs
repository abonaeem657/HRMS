using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace HRMS.Infrastructure.Persistence.Migrations;
public partial class EnforceIntegrityAndConcurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT Code FROM Positions GROUP BY Code HAVING COUNT(*) > 1)
                THROW 51000, 'Duplicate position codes must be resolved before migration.', 1;
            IF EXISTS (SELECT 1 FROM Positions WHERE LEN(Code) > 50)
                THROW 51001, 'Position codes longer than 50 characters require review before migration.', 1;
            IF EXISTS (SELECT EmployeeId FROM AttendanceRecords WHERE CheckInUtc IS NOT NULL AND CheckOutUtc IS NULL GROUP BY EmployeeId HAVING COUNT(*) > 1)
                THROW 51002, 'Multiple open attendance records require correction before migration.', 1;
            """);
        migrationBuilder.AlterColumn<string>(name: "Code", table: "Positions", type: "nvarchar(50)", maxLength: 50,
            nullable: false, oldClrType: typeof(string), oldType: "nvarchar(max)");
        migrationBuilder.CreateIndex(name: "IX_Positions_Code", table: "Positions", column: "Code", unique: true);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "Employees", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "AttendanceRecords", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.CreateIndex(name: "IX_AttendanceRecords_OpenShift", table: "AttendanceRecords", column: "EmployeeId",
            unique: true, filter: "[CheckInUtc] IS NOT NULL AND [CheckOutUtc] IS NULL");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AttendanceRecords_OpenShift", table: "AttendanceRecords");
        migrationBuilder.DropIndex(name: "IX_Positions_Code", table: "Positions");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Employees");
        migrationBuilder.DropColumn(name: "RowVersion", table: "AttendanceRecords");
        migrationBuilder.AlterColumn<string>(name: "Code", table: "Positions", type: "nvarchar(max)", nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50);
    }
}
