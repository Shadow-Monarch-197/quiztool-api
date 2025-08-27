using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace quizTool.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectiveAndImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubjectiveText",
                table: "TestAttemptAnswers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelAnswer",
                table: "Questions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubjectiveText",
                table: "TestAttemptAnswers");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "ModelAnswer",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Questions");
        }
    }
}
