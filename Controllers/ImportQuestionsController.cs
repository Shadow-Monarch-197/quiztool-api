//using ExcelDataReader;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using quizTool.Models;

//namespace quizTool.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    public class ImportQuestionsController : ControllerBase
//    {
//        public QuizTool_Dbcontext db;
//        public ImportQuestionsController(QuizTool_Dbcontext db)
//        {
//            this.db = db;
//        }

//        [HttpGet("test")]
//        public IActionResult Test() => Ok("Test works");

//        // 10 MB default; adjust as needed
//        [HttpPost("import")]
//        [RequestSizeLimit(10_000_000)]
//        public async Task<IActionResult> Import([FromForm] IFormFile file, CancellationToken ct)
//        {
//            if (file is null || file.Length == 0)
//                return BadRequest("No file uploaded.");
//            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
//            if (extension != ".xls" && extension != ".xlsx")
//                return BadRequest("Only .xls or .xlsx files are allowed.");

//            var created = 0;
//            var skipped = new List<string>(); // collect reasons to return to client

//            using var stream = file.OpenReadStream();
//            using var reader = CreateExcelReader(stream, extension);

//            // Read first worksheet as table
//            var ds = reader.AsDataSet(new ExcelDataSetConfiguration
//            {
//                ConfigureDataTable = _ => new ExcelDataTableConfiguration
//                {
//                    UseHeaderRow = true
//                }
//            });

//            if (ds.Tables.Count == 0) return BadRequest("Excel has no worksheets.");
//            var table = ds.Tables[0];

//            // Validate expected columns (case-insensitive)
//            var required = new[] { "Question", "Option1", "Option2", "Option3", "Option4", "Answer" };
//            foreach (var col in required)
//                if (!table.Columns.Contains(col))
//                    return BadRequest($"Missing column: {col}");

//            // Optional: keep duplicates from re-importing by text (customize to your rules)
//            var existingQuestionTexts = await db.Questions.Select(q => q.Text).ToListAsync(ct);
//            var existingSet = new HashSet<string>(existingQuestionTexts, StringComparer.OrdinalIgnoreCase);

//            foreach (System.Data.DataRow row in table.Rows)
//            {
//                var q = row["Question"]?.ToString()?.Trim();
//                var o1 = row["Option1"]?.ToString()?.Trim();
//                var o2 = row["Option2"]?.ToString()?.Trim();
//                var o3 = row["Option3"]?.ToString()?.Trim();
//                var o4 = row["Option4"]?.ToString()?.Trim();
//                var ans = row["Answer"]?.ToString()?.Trim();

//                if (string.IsNullOrWhiteSpace(q) || string.IsNullOrWhiteSpace(o1) ||
//                string.IsNullOrWhiteSpace(o2) || string.IsNullOrWhiteSpace(o3) ||
//                string.IsNullOrWhiteSpace(o4) || string.IsNullOrWhiteSpace(ans))
//                {
//                    skipped.Add($"Row {table.Rows.IndexOf(row) + 2}: One or more required cells empty.");
//                    continue;
//                }

//                // Skip duplicate question texts (optional)
//                if (existingSet.Contains(q))
//                {
//                    skipped.Add($"Row {table.Rows.IndexOf(row) + 2}: Duplicate question skipped.");
//                    continue;
//                }

//                // Decide which option is correct
//                var correctIndex = GetCorrectIndex(ans, o1, o2, o3, o4);
//                if (correctIndex is null)
//                {
//                    skipped.Add($"Row {table.Rows.IndexOf(row) + 2}: Could not map Answer to an option.");
//                    continue;
//                }

//                var question = new Question
//                {
//                    Id = Guid.NewGuid(),
//                    Text = q,
//                    Choices = new List<Choice>
//                         {
//                             new() { Id = Guid.NewGuid(), Text = o1, IsCorrect = correctIndex == 1 },
//                             new() { Id = Guid.NewGuid(), Text = o2, IsCorrect = correctIndex == 2 },
//                             new() { Id = Guid.NewGuid(), Text = o3, IsCorrect = correctIndex == 3 },
//                             new() { Id = Guid.NewGuid(), Text = o4, IsCorrect = correctIndex == 4 },
//                         }
//                };

//                await db.Questions.AddAsync(question, ct);
//                existingSet.Add(q);
//                created++;
//            }

//            await db.SaveChangesAsync(ct);

//            return Ok(new
//            {
//                message = "Import completed.",
//                created,
//                skippedCount = skipped.Count,
//                skipped
//            });
//        }

//        private static IExcelDataReader CreateExcelReader(Stream s, string extension)
//        {
//            return extension switch
//            {
//                ".xls" => ExcelReaderFactory.CreateBinaryReader(s),
//                ".xlsx" => ExcelReaderFactory.CreateOpenXmlReader(s),
//                _ => throw new InvalidOperationException("Unsupported file.")
//            };
//        }

//        /// Maps Answer to the 1..4 index.
//            /// Accepts: "A"/"B"/"C"/"D", "1"/"2"/"3"/"4", or exact option text.
//        private static int? GetCorrectIndex(string ans, string o1, string o2, string o3, string o4)
//        {
//            var a = ans.Trim();
//            // A/B/C/D
//            switch (a.ToUpperInvariant())
//            {
//                case "A": return 1;
//                case "B": return 2;
//                case "C": return 3;
//                case "D": return 4;
//            }
//            // 1/2/3/4
//            if (int.TryParse(a, out var n) && n >= 1 && n <= 4) return n;

//            // Match by text
//            if (string.Equals(a, o1, StringComparison.OrdinalIgnoreCase)) return 1;
//            if (string.Equals(a, o2, StringComparison.OrdinalIgnoreCase)) return 2;
//            if (string.Equals(a, o3, StringComparison.OrdinalIgnoreCase)) return 3;
//            if (string.Equals(a, o4, StringComparison.OrdinalIgnoreCase)) return 4;

//            return null;
//        }
//    }
//}
