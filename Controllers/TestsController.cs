using ClosedXML.Excel;                
using ClosedXML.Excel.Drawings;
using ExcelDataReader;                 
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;    
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using quizTool.Models;
using System.Data;
using System.Text;

// CHANGED: add TimeLimitMinutes on form
public class UploadTestForm
{
    public IFormFile File { get; set; } = default!;
    public string? Title { get; set; }
    public int? TimeLimitMinutes { get; set; } // NEW
}

public class AddQuestionForm
{
    public string Type { get; set; } = "objective"; 
    public string Text { get; set; } = string.Empty;
    public string? ModelAnswer { get; set; }         
    public IFormFile? Image { get; set; }          
    public string[]? Options { get; set; }           
    public int? CorrectIndex { get; set; }           
}

namespace quizTool.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TestsController : ControllerBase
    {
        private readonly QuizTool_Dbcontext _db;
        private readonly IWebHostEnvironment _env; 

        public TestsController(QuizTool_Dbcontext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

         // NEW: helper inside TestsController class
        private string CurrentEmail()
        {
            return (User?.Claims?.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.UniqueName || c.Type == "unique_name")?.Value ?? "")
                .Trim().ToLower();
        }


        private string EnsureUploadsDir()
        {
            var web = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var dir = Path.Combine(web, "uploads");
            Directory.CreateDirectory(dir);
            return dir;
        }

        private string SaveImage(Stream src, string ext)
        {
            var dir = EnsureUploadsDir();
            var file = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, file);
            using var fs = System.IO.File.Create(path);
            src.CopyTo(fs);
            return $"/uploads/{file}";
        }
        // 30 Aug
        // NEW: Parse without saving, return editable questions
        [Authorize(Roles = "admin")]
        [HttpPost("parse-upload")] // NEW
        [Consumes("multipart/form-data")] // NEW
        public async Task<ActionResult<ParsedUploadDto>> ParseUpload([FromForm] UploadTestForm form) // NEW
        {
            var file = form.File;
            if (file == null || file.Length == 0) return BadRequest("No file submitted.");

            var suggestedTitle = string.IsNullOrWhiteSpace(form.Title)
                ? (Path.GetFileNameWithoutExtension(file.FileName) ?? "Uploaded Test")
                : form.Title.Trim();

            var preview = new ParsedUploadDto { Title = suggestedTitle, Questions = new List<AdminQuestionDto>() };
            preview.TimeLimitMinutes = form.TimeLimitMinutes; // NEW

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);

                static string Norm(string s) =>
                    (s ?? "").Replace(" ", "").Replace("_", "").Replace("-", "")
                              .Trim().ToLowerInvariant();

                var header = ws.FirstRowUsed();
                var colMap = header.Cells().ToDictionary(
                    c => Norm(c.GetString()),
                    c => c.Address.ColumnNumber
                );

                int GetCol(params string[] names)
                {
                    foreach (var n in names)
                    {
                        if (colMap.TryGetValue(Norm(n), out var idx)) return idx;
                    }
                    return -1;
                }

                int colType = GetCol("Type");
                int colQ = GetCol("Question");
                int colO1 = GetCol("Option1", "Option 1");
                int colO2 = GetCol("Option2", "Option 2");
                int colO3 = GetCol("Option3", "Option 3");
                int colO4 = GetCol("Option4", "Option 4");
                int colCorr = GetCol("Correct", "CorrectOption", "Correct Option");
                int colModel = GetCol("ModelAnswer", "Model Answer", "Answer", "ReferenceAnswer", "Reference Answer");

                if (colType < 0 || colQ < 0)
                    return BadRequest("Missing required columns: Type, Question");

                var pics = ws.Pictures.ToList();
                int rowStart = header.RowBelow().RowNumber();
                int last = ws.LastRowUsed().RowNumber();

                for (int r = rowStart; r <= last; r++)
                {
                    string qText = ws.Cell(r, colQ).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(qText)) continue;

                    var typeStr = ws.Cell(r, colType).GetString().Trim().ToLowerInvariant();
                    var kind = typeStr == "subjective" ? QuestionType.Subjective : QuestionType.Objective;

                    string? imageUrl = null;
                    var pic = pics.FirstOrDefault(p =>
                        p.TopLeftCell.Address.RowNumber == r &&
                        p.TopLeftCell.Address.ColumnNumber == colQ);

                    if (pic != null)
                    {
                        var imgExt = pic.Format switch
                        {
                            XLPictureFormat.Png => ".png",
                            XLPictureFormat.Jpeg => ".jpg",
                            XLPictureFormat.Gif => ".gif",
                            XLPictureFormat.Bmp => ".bmp",
                            XLPictureFormat.Tiff => ".tiff",
                            _ => ".png"
                        };

                        using var ms = new MemoryStream();
                        using (var src = pic.ImageStream)
                        {
                            if (src.CanSeek) src.Position = 0;
                            src.CopyTo(ms);
                        }
                        ms.Position = 0;

                        imageUrl = SaveImage(ms, imgExt);
                    }

                    var aq = new AdminQuestionDto
                    {
                        Id = 0,
                        Text = qText,
                        Type = kind,
                        ImageUrl = imageUrl,
                        Options = new List<AdminOptionDto>()
                    };

                    if (kind == QuestionType.Objective)
                    {
                        var opts = new List<string>();
                        if (colO1 > 0) { var v = ws.Cell(r, colO1).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO2 > 0) { var v = ws.Cell(r, colO2).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO3 > 0) { var v = ws.Cell(r, colO3).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO4 > 0) { var v = ws.Cell(r, colO4).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }

                        if (opts.Count == 0) continue;

                        int correctIndex = 0;
                        if (colCorr > 0)
                        {
                            var corrStr = ws.Cell(r, colCorr).GetString();
                            if (int.TryParse(corrStr, out var num) && num >= 1 && num <= opts.Count)
                                correctIndex = num - 1;
                        }

                        for (int i = 0; i < opts.Count; i++)
                            aq.Options.Add(new AdminOptionDto { Id = 0, Text = opts[i], IsCorrect = i == correctIndex });
                    }
                    else
                    {
                        if (colModel > 0)
                        {
                            var modelAns = ws.Cell(r, colModel).GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(modelAns)) aq.ModelAnswer = modelAns;
                        }
                    }

                    preview.Questions.Add(aq);
                }
            }
            else
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (dataSet.Tables.Count == 0) return BadRequest("Empty Excel.");
                var table = dataSet.Tables[0];

                foreach (DataRow row in table.Rows)
                {
                    string? Col(params string[] names)
                    {
                        foreach (DataColumn dc in table.Columns)
                        {
                            var name = dc.ColumnName?.Trim();
                            if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                                return row[dc]?.ToString();
                        }
                        return null;
                    }

                    var typeStr = (Col("Type") ?? "objective").Trim().ToLowerInvariant();
                    var kind = typeStr == "subjective" ? QuestionType.Subjective : QuestionType.Objective;

                    string q = (Col("Question") ?? (row.ItemArray.Length > 1 ? row[1]?.ToString() : "") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(q)) continue;

                    var aq = new AdminQuestionDto { Id = 0, Text = q, Type = kind, Options = new List<AdminOptionDto>() };

                    if (kind == QuestionType.Objective)
                    {
                        string? o1 = Col("Option 1", "Option1");
                        string? o2 = Col("Option 2", "Option2");
                        string? o3 = Col("Option 3", "Option3");
                        string? o4 = Col("Option 4", "Option4");

                        var opts = new[] { o1, o2, o3, o4 }
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Select(s => s!.Trim()).ToList();
                        if (opts.Count == 0) continue;

                        int correctIndex = 0;
                        var corr = (Col("Correct", "Correct Option", "CorrectOption") ?? "").Trim();
                        if (int.TryParse(corr, out var num) && num >= 1 && num <= opts.Count) correctIndex = num - 1;

                        for (int i = 0; i < opts.Count; i++)
                            aq.Options.Add(new AdminOptionDto { Id = 0, Text = opts[i], IsCorrect = i == correctIndex });
                    }
                    else
                    {
                        var model = (Col("ModelAnswer", "Model Answer", "Answer", "ReferenceAnswer", "Reference Answer") ?? "").Trim();
                        aq.ModelAnswer = string.IsNullOrWhiteSpace(model) ? null : model;
                    }

                    preview.Questions.Add(aq);
                }
            }

            if (preview.Questions.Count == 0)
                return BadRequest("No questions parsed.");

            return Ok(preview);
        }
        //
        //30 Aug

        // NEW: Save parsed questions as a locked test
        [Authorize(Roles = "admin")]
        [HttpPost("save-parsed")] // NEW
        public async Task<ActionResult<UploadTestResultDto>> SaveParsed([FromBody] SaveParsedTestBody body) // NEW
        {
            if (body == null) return BadRequest("Invalid payload.");
            var title = (body.Title ?? "").Trim();
            if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");
            if (body.Questions == null || body.Questions.Count == 0) return BadRequest("Provide at least one question.");

            var exists = await _db.Tests.AnyAsync(x => x.Title.ToLower() == title.ToLower());
            if (exists) return Conflict(new { message = $"A test named '{title}' already exists." });

            var test = new Test { Title = title, IsLocked = true }; // NEW: lock on create

            // NEW: apply time limit
            if (body.TimeLimitMinutes.HasValue && body.TimeLimitMinutes.Value > 0)
                test.TimeLimitMinutes = body.TimeLimitMinutes.Value; // NEW

            foreach (var qd in body.Questions)
            {
                var q = new Question
                {
                    Text = (qd.Text ?? "").Trim(),
                    Type = qd.Type,
                    ImageUrl = string.IsNullOrWhiteSpace(qd.ImageUrl) ? null : qd.ImageUrl,
                    ModelAnswer = qd.Type == QuestionType.Subjective ? (qd.ModelAnswer ?? "").Trim() : null
                };

                if (qd.Type == QuestionType.Objective)
                {
                    var options = (qd.Options ?? new List<AdminOptionDto>())
                        .Where(o => !string.IsNullOrWhiteSpace(o.Text))
                        .ToList();

                    if (options.Count == 0) return BadRequest("Objective question requires options.");

                    // Ensure exactly one correct (fallback to first)
                    if (!options.Any(o => o.IsCorrect)) options[0].IsCorrect = true;
                    bool firstCorrect = true;
                    foreach (var o in options)
                    {
                        var opt = new Option { Text = (o.Text ?? "").Trim(), IsCorrect = o.IsCorrect && firstCorrect };
                        if (o.IsCorrect) firstCorrect = false;
                        q.Options.Add(opt);
                    }
                }

                test.Questions.Add(q);
            }

            _db.Tests.Add(test);
            await _db.SaveChangesAsync();

            return Ok(new UploadTestResultDto { TestId = test.Id, Title = test.Title, Questions = test.Questions.Count });
        }
        //

        [Authorize(Roles = "admin")]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]    
        public async Task<ActionResult<UploadTestResultDto>> Upload([FromForm] UploadTestForm form)
        {
            var file = form.File;
            var title = form.Title;

            if (file == null || file.Length == 0)
                return BadRequest("No file submitted.");

            var finalTitle = string.IsNullOrWhiteSpace(title)
                ? (Path.GetFileNameWithoutExtension(file.FileName) ?? "Uploaded Test")
                : title.Trim();

            var exists = await _db.Tests.AnyAsync(x => x.Title.ToLower() == finalTitle.ToLower());
            if (exists) return Conflict(new { message = $"A test named '{finalTitle}' already exists." });

            var test = new Test { Title = finalTitle };

            if (form.TimeLimitMinutes.HasValue && form.TimeLimitMinutes.Value > 0)
                test.TimeLimitMinutes = form.TimeLimitMinutes.Value; // NEW


            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (ext == ".xlsx")
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheet(1);

                static string Norm(string s) =>
                    (s ?? "").Replace(" ", "").Replace("_", "").Replace("-", "")
                              .Trim().ToLowerInvariant();

                var header = ws.FirstRowUsed();
                var colMap = header.Cells().ToDictionary(
                    c => Norm(c.GetString()),
                    c => c.Address.ColumnNumber
                );

                int GetCol(params string[] names)
                {
                    foreach (var n in names)
                    {
                        if (colMap.TryGetValue(Norm(n), out var idx)) return idx;
                    }
                    return -1;
                }

                int colType = GetCol("Type");
                int colQ = GetCol("Question");
                int colO1 = GetCol("Option1", "Option 1");
                int colO2 = GetCol("Option2", "Option 2");
                int colO3 = GetCol("Option3", "Option 3");
                int colO4 = GetCol("Option4", "Option 4");
                int colCorr = GetCol("Correct", "CorrectOption", "Correct Option");
                int colModel = GetCol("ModelAnswer", "Model Answer", "Answer", "ReferenceAnswer", "Reference Answer");

                if (colType < 0 || colQ < 0)
                    return BadRequest("Missing required columns: Type, Question");

                var pics = ws.Pictures.ToList(); 
                int rowStart = header.RowBelow().RowNumber();
                int last = ws.LastRowUsed().RowNumber();

                for (int r = rowStart; r <= last; r++)
                {
                    string qText = ws.Cell(r, colQ).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(qText)) continue;

                    var typeStr = ws.Cell(r, colType).GetString().Trim().ToLowerInvariant();
                    var kind = typeStr == "subjective" ? QuestionType.Subjective : QuestionType.Objective;

                 
                    string? imageUrl = null;
                    var pic = pics.FirstOrDefault(p =>
                        p.TopLeftCell.Address.RowNumber == r &&
                        p.TopLeftCell.Address.ColumnNumber == colQ);

                    if (pic != null)
                    {
                        var imgExt = pic.Format switch
                        {
                            XLPictureFormat.Png => ".png",
                            XLPictureFormat.Jpeg => ".jpg",
                            XLPictureFormat.Gif => ".gif",
                            XLPictureFormat.Bmp => ".bmp",
                            XLPictureFormat.Tiff => ".tiff",
                            _ => ".png"
                        };

                        using var ms = new MemoryStream();
                        using (var src = pic.ImageStream)
                        {
                            if (src.CanSeek) src.Position = 0;
                            src.CopyTo(ms);
                        }
                        ms.Position = 0;

                        imageUrl = SaveImage(ms, imgExt); 
                    }

                    var question = new Question
                    {
                        Text = qText,
                        Type = kind,
                        ImageUrl = imageUrl
                    };

                    if (kind == QuestionType.Objective)
                    {
                        var opts = new List<string>();
                        if (colO1 > 0) { var v = ws.Cell(r, colO1).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO2 > 0) { var v = ws.Cell(r, colO2).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO3 > 0) { var v = ws.Cell(r, colO3).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }
                        if (colO4 > 0) { var v = ws.Cell(r, colO4).GetString(); if (!string.IsNullOrWhiteSpace(v)) opts.Add(v.Trim()); }

                        if (opts.Count == 0) continue;

                        int correctIndex = 0;
                        if (colCorr > 0)
                        {
                            var corrStr = ws.Cell(r, colCorr).GetString();
                            if (int.TryParse(corrStr, out var num) && num >= 1 && num <= opts.Count)
                                correctIndex = num - 1;
                        }

                        for (int i = 0; i < opts.Count; i++)
                            question.Options.Add(new Option { Text = opts[i], IsCorrect = i == correctIndex });
                    }
                    else
                    {
                        if (colModel > 0)
                        {
                            var modelAns = ws.Cell(r, colModel).GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(modelAns)) question.ModelAnswer = modelAns;
                        }
                    }

                    test.Questions.Add(question);
                }
            }
            else
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                using var stream = file.OpenReadStream();
                using var reader = ExcelReaderFactory.CreateReader(stream);
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                if (dataSet.Tables.Count == 0) return BadRequest("Empty Excel.");
                var table = dataSet.Tables[0];

                foreach (DataRow row in table.Rows)
                {
                    string? Col(params string[] names)
                    {
                        foreach (DataColumn dc in table.Columns)
                        {
                            var name = dc.ColumnName?.Trim();
                            if (names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                                return row[dc]?.ToString();
                        }
                        return null;
                    }

                    var typeStr = (Col("Type") ?? "objective").Trim().ToLowerInvariant();
                    var kind = typeStr == "subjective" ? QuestionType.Subjective : QuestionType.Objective;

                    string q = (Col("Question") ?? (row.ItemArray.Length > 1 ? row[1]?.ToString() : "") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(q)) continue;

                    var question = new Question { Text = q, Type = kind };

                    if (kind == QuestionType.Objective)
                    {
                        string? o1 = Col("Option 1", "Option1");
                        string? o2 = Col("Option 2", "Option2");
                        string? o3 = Col("Option 3", "Option3");
                        string? o4 = Col("Option 4", "Option4");

                        var opts = new[] { o1, o2, o3, o4 }
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Select(s => s!.Trim()).ToList();
                        if (opts.Count == 0) continue;

                        int correctIndex = 0;
                        var corr = (Col("Correct", "Correct Option", "CorrectOption") ?? "").Trim();
                        if (int.TryParse(corr, out var num) && num >= 1 && num <= opts.Count) correctIndex = num - 1;

                        for (int i = 0; i < opts.Count; i++)
                            question.Options.Add(new Option { Text = opts[i], IsCorrect = i == correctIndex });
                    }
                    else
                    {
                        var model = (Col("ModelAnswer", "Model Answer", "Answer", "ReferenceAnswer", "Reference Answer") ?? "").Trim();
                        question.ModelAnswer = string.IsNullOrWhiteSpace(model) ? null : model;
                    }

                    test.Questions.Add(question);
                }
            }

            if (test.Questions.Count == 0)
                return BadRequest("No questions parsed. Check required columns: Type, Question (and Correct/options for objective, ModelAnswer for subjective).");

            _db.Tests.Add(test);
            await _db.SaveChangesAsync();

            return Ok(new UploadTestResultDto { TestId = test.Id, Title = test.Title, Questions = test.Questions.Count });
        }

       [Authorize]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TestSummaryDto>>> GetTests()
        {
            // CHANGED: admins see all; basic users only their assigned tests
            var isAdmin = User.IsInRole("admin"); // CHANGED

            IQueryable<Test> query = _db.Tests.AsQueryable(); // CHANGED

            if (!isAdmin) // CHANGED
            {
                var email = CurrentEmail(); // CHANGED
                var testIds = await _db.TestAssignments
                    .Where(a => a.UserEmail == email)
                    .Select(a => a.TestId)
                    .ToListAsync();

                query = query.Where(t => testIds.Contains(t.Id)); // CHANGED
            }

            var list = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new TestSummaryDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    CreatedAt = t.CreatedAt,
                    QuestionCount = t.Questions.Count,
                    TimeLimitMinutes = t.TimeLimitMinutes
                })
                .ToListAsync();

            return Ok(list);
        }

        [Authorize(Roles = "admin")]
        [HttpGet("attempts")]
        public async Task<ActionResult<IEnumerable<AttemptListItemDto>>> GetAttempts([FromQuery] int? testId)
        {
            var query =
                from a in _db.TestAttempts
                join t in _db.Tests on a.TestId equals t.Id into gj
                from t in gj.DefaultIfEmpty() 
                orderby a.AttemptedAt descending
                select new AttemptListItemDto
                {
                    Id = a.Id,
                    TestId = a.TestId,
                    TestTitle = t != null ? t.Title : $"[Deleted Test #{a.TestId}]",
                    UserEmail = a.UserEmail,
                    Score = a.Score,
                    Total = a.Total,
                    Percent = a.Total == 0 ? 0 : (int)Math.Round(100.0 * a.Score / a.Total),
                    AttemptedAt = a.AttemptedAt
                };

            if (testId.HasValue)
                query = query.Where(x => x.TestId == testId.Value)
                             .OrderByDescending(x => x.AttemptedAt);

            var list = await query.ToListAsync();
            return Ok(list);
        }

         //// 3 sep ////
 [Authorize]
 [HttpGet("{id:int}")]
 public async Task<ActionResult<TestDetailDto>> GetTest(int id)
 {
     var test = await _db.Tests
         .Include(t => t.Questions).ThenInclude(q => q.Options)
         .FirstOrDefaultAsync(t => t.Id == id);

     if (test == null) return NotFound();

     // NEW: only admin or assigned user can view
     if (!User.IsInRole("admin")) // NEW
     {
         var email = CurrentEmail(); // NEW
         var allowed = await _db.TestAssignments.AnyAsync(a => a.TestId == id && a.UserEmail == email); // NEW
         if (!allowed) return Forbid(); // NEW
     }

     return Ok(new TestDetailDto
     {
         Id = test.Id,
         Title = test.Title,
         TimeLimitMinutes = test.TimeLimitMinutes,
         Questions = test.Questions.Select(q => new QuestionDto
         {
             Id = q.Id,
             Text = q.Text,
             Type = q.Type == QuestionType.Subjective ? "subjective" : "objective",
             ImageUrl = q.ImageUrl,
             Options = q.Type == QuestionType.Objective
                 ? q.Options.Select(o => new OptionDto { Id = o.Id, Text = o.Text }).ToList()
                 : new List<OptionDto>()
         }).ToList()
     });
 }

        [Authorize(Roles = "admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteTest(int id)
        {
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == id);
            if (test == null) return NotFound(new { message = "Test not found." });

            _db.Tests.Remove(test);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<ActionResult<TestSummaryDto>> CreateTest([FromBody] Test t)
        {
            if (string.IsNullOrWhiteSpace(t?.Title)) return BadRequest("Title required.");
            var title = t.Title.Trim();

            var exists = await _db.Tests.AnyAsync(x => x.Title.ToLower() == title.ToLower());
            if (exists) return Conflict(new { message = $"A test named '{title}' already exists." });

            var test = new Test { Title = title };

            // NEW: accept time limit from body
            if (t.TimeLimitMinutes.HasValue && t.TimeLimitMinutes.Value > 0)
                test.TimeLimitMinutes = t.TimeLimitMinutes.Value; // NEW

            _db.Tests.Add(test);
            await _db.SaveChangesAsync();

            return Ok(new TestSummaryDto
            {
                Id = test.Id,
                Title = test.Title,
                CreatedAt = test.CreatedAt,
                QuestionCount = 0,
                TimeLimitMinutes = test.TimeLimitMinutes // NEW
            });
        }

        [Authorize(Roles = "admin")]
        [HttpPost("{testId:int}/questions")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> AddQuestion(int testId, [FromForm] AddQuestionForm form)
        {
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == testId);
            if (test == null) return NotFound("Test not found.");

            var kind = (form.Type ?? "").ToLowerInvariant() == "subjective"
                ? QuestionType.Subjective
                : QuestionType.Objective;

            string? imageUrl = null;
            if (form.Image is not null && form.Image.Length > 0)
            {
                using var s = form.Image.OpenReadStream();
                var e = Path.GetExtension(form.Image.FileName);
                imageUrl = SaveImage(s, string.IsNullOrWhiteSpace(e) ? ".png" : e);
            }

            var q = new Question
            {
                TestId = testId,
                Text = form.Text?.Trim() ?? "",
                Type = kind,
                ImageUrl = imageUrl
            };

            if (kind == QuestionType.Objective)
            {
                var opts = (form.Options ?? Array.Empty<string>())
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim())
                    .ToList();

                if (opts.Count == 0)
                    return BadRequest("Provide at least one option.");

                int ci = Math.Clamp(form.CorrectIndex ?? 0, 0, Math.Max(0, opts.Count - 1));
                for (int i = 0; i < opts.Count; i++)
                    q.Options.Add(new Option { Text = opts[i], IsCorrect = i == ci });
            }
            else
            {
                q.ModelAnswer = form.ModelAnswer?.Trim();
            }

            _db.Questions.Add(q);
            await _db.SaveChangesAsync();
            return Ok(new { questionId = q.Id });
        }

        [Authorize]
 [HttpPost("submit")]
 public async Task<ActionResult<AttemptResultDto>> Submit([FromBody] SubmitAttemptDto dto)
 {
     var test = await _db.Tests
         .Include(t => t.Questions)
         .ThenInclude(q => q.Options)
         .FirstOrDefaultAsync(t => t.Id == dto.TestId);

     if (test == null) return NotFound("Test not found.");

     // only admin or assigned user may submit
     if (!User.IsInRole("admin"))
     {
         var currentEmail = CurrentEmail(); // FIX: renamed from "email"
         var allowed = await _db.TestAssignments
             .AnyAsync(a => a.TestId == dto.TestId && a.UserEmail == currentEmail);
         if (!allowed) return Forbid();
     }

     int totalQuestions = test.Questions.Count;
     int score = 0;

     var submissionEmail = string.IsNullOrWhiteSpace(dto.UserEmail)   // FIX: renamed from "email"
         ? "anonymous@local"
         : dto.UserEmail.Trim().ToLower();

     var existing = await _db.TestAttempts
         .FirstOrDefaultAsync(a => a.TestId == dto.TestId && a.UserEmail.ToLower() == submissionEmail);

     if (existing != null)
     {
         return Conflict(new { message = "You have already submitted this test.", attemptId = existing.Id });
     }

     var attempt = new TestAttempt
     {
         TestId = test.Id,
         UserEmail = submissionEmail // FIX: use the normalized value
     };

     var correctByQuestion = test.Questions
         .Where(q => q.Type == QuestionType.Objective)
         .ToDictionary(
             q => q.Id,
             q => q.Options.FirstOrDefault(o => o.IsCorrect)?.Id
         );

     foreach (var ans in dto.Answers)
     {
         var q = test.Questions.FirstOrDefault(x => x.Id == ans.QuestionId);
         if (q == null) continue;

         if (q.Type == QuestionType.Objective)
         {
             bool isCorrect = ans.SelectedOptionId.HasValue &&
                              correctByQuestion.TryGetValue(ans.QuestionId, out var corrId) &&
                              corrId == ans.SelectedOptionId.Value;

             if (isCorrect) score++;

             attempt.Answers.Add(new TestAttemptAnswer
             {
                 QuestionId = ans.QuestionId,
                 SelectedOptionId = ans.SelectedOptionId,
                 IsCorrect = isCorrect,
                 SubjectiveText = null
             });
         }
         else
         {
             attempt.Answers.Add(new TestAttemptAnswer
             {
                 QuestionId = ans.QuestionId,
                 SelectedOptionId = null,
                 IsCorrect = false,
                 SubjectiveText = ans.SubjectiveText
             });
         }
     }

     attempt.Score = score;
     attempt.Total = totalQuestions;

     _db.TestAttempts.Add(attempt);
     await _db.SaveChangesAsync();

     return Ok(new AttemptResultDto
     {
         AttemptId = attempt.Id,
         Score = score,
         Total = totalQuestions
     });
 }



[Authorize(Roles = "admin")]
[HttpPost("{testId:int}/assign")]
public async Task<IActionResult> AssignUsers(int testId, [FromBody] AssignTestDto body) // NEW
{
    var testExists = await _db.Tests.AnyAsync(t => t.Id == testId);
    if (!testExists) return NotFound(new { message = "Test not found." });

    var emails = (body?.Emails ?? new()).Select(e => (e ?? "").Trim().ToLower())
        .Where(e => !string.IsNullOrWhiteSpace(e))
        .Distinct()
        .ToList();

    if (emails.Count == 0) return BadRequest(new { message = "No emails provided." });

    var existing = await _db.TestAssignments
        .Where(a => a.TestId == testId && emails.Contains(a.UserEmail))
        .Select(a => a.UserEmail)
        .ToListAsync();

    var toAdd = emails.Except(existing).ToList();
    foreach (var em in toAdd)
        _db.TestAssignments.Add(new TestAssignment { TestId = testId, UserEmail = em });

    await _db.SaveChangesAsync();

    return Ok(new { testId, assignedAdded = toAdd.Count, alreadyAssigned = existing.Count });
}

[Authorize(Roles = "admin")]
[HttpGet("{testId:int}/assignees")]
public async Task<ActionResult<IEnumerable<TestAssigneeDto>>> GetAssignees(int testId) // NEW
{
    var q =
        from a in _db.TestAssignments
        where a.TestId == testId
        join u in _db.Users on a.UserEmail equals u.email into gj
        from u in gj.DefaultIfEmpty()
        orderby a.AssignedAt descending
        select new TestAssigneeDto
        {
            Email = a.UserEmail,
            UserId = u != null ? u.userid : (int?)null,
            Name = u != null ? u.name : null,
            AssignedAt = a.AssignedAt
        };

    var list = await q.ToListAsync();
    return Ok(list);
}

// (Optional) Admin: unassign a single email
[Authorize(Roles = "admin")]
[HttpDelete("{testId:int}/assignees/{email}")]
public async Task<IActionResult> UnassignUser(int testId, string email) // NEW
{
    email = (email ?? "").Trim().ToLower();
    var rec = await _db.TestAssignments.FirstOrDefaultAsync(a => a.TestId == testId && a.UserEmail == email);
    if (rec == null) return NotFound();
    _db.TestAssignments.Remove(rec);
    await _db.SaveChangesAsync();
    return NoContent();
}


        [Authorize(Roles = "admin")]
        [HttpGet("{id:int}/admin")]
        public async Task<ActionResult<AdminTestDetailDto>> GetTestAdmin(int id)
        {
            var test = await _db.Tests
                .Include(t => t.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (test == null) return NotFound();

            var dto = new AdminTestDetailDto
            {
                Id = test.Id,
                Title = test.Title,
                IsLocked = test.IsLocked,
                TimeLimitMinutes = test.TimeLimitMinutes, // NEW
                Questions = test.Questions
                    .OrderBy(q => q.Id)
                    .Select(q => new AdminQuestionDto
                    {
                        Id = q.Id,
                        Type = q.Type,
                        Text = q.Text,
                        ImageUrl = q.ImageUrl,
                        ModelAnswer = q.ModelAnswer,
                        Options = q.Options.OrderBy(o => o.Id).Select(o => new AdminOptionDto
                        {
                            Id = o.Id,
                            Text = o.Text,
                            IsCorrect = o.IsCorrect
                        }).ToList()
                    }).ToList()
            };

            return Ok(dto);
        }

        [Authorize(Roles = "admin")]
        [HttpDelete("questions/{questionId:int}")]
        public async Task<IActionResult> DeleteQuestion(int questionId)
        {
            var q = await _db.Questions.FirstOrDefaultAsync(x => x.Id == questionId);
            if (q == null) return NotFound(new { message = "Question not found." });


            //30 Aug
            // NEW: block delete if locked
            var test = await _db.Tests.FirstOrDefaultAsync(t => t.Id == q.TestId); // NEW
            if (test?.IsLocked == true) return Conflict(new { message = "This test is locked and cannot be modified." }); // NEW

            _db.Questions.Remove(q);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [Authorize(Roles = "admin")]
        [HttpGet("attempts/{attemptId:int}")]
        public async Task<ActionResult<AttemptDetailDto>> GetAttemptDetail(int attemptId)
        {
            var attempt = await _db.TestAttempts
                .Include(a => a.Test)
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId);

            if (attempt == null) return NotFound();

            var qIds = attempt.Answers.Select(x => x.QuestionId).Distinct().ToList();
            var questionMap = await _db.Questions
                .Include(q => q.Options)
                .Where(q => qIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            var dto = new AttemptDetailDto
            {
                AttemptId = attempt.Id,
                TestId = attempt.TestId,
                TestTitle = attempt.Test?.Title ?? $"Test #{attempt.TestId}",
                UserEmail = attempt.UserEmail,
                Score = attempt.Score,
                Total = attempt.Total,
                AttemptedAt = attempt.AttemptedAt,
                Answers = new List<AttemptAnswerDetailDto>()
            };

            foreach (var a in attempt.Answers.OrderBy(x => x.Id))
            {
                if (!questionMap.TryGetValue(a.QuestionId, out var q))
                {
                    var inferredType = !string.IsNullOrWhiteSpace(a.SubjectiveText) ? "subjective" : "objective";
                    dto.Answers.Add(new AttemptAnswerDetailDto
                    {
                        QuestionId = a.QuestionId,
                        QuestionText = "[Deleted question]",
                        Type = inferredType,
                        ImageUrl = null,

                        SelectedOptionId = a.SelectedOptionId,
                        SelectedOptionText = null,
                        CorrectOptionId = null,
                        CorrectOptionText = null,
                        IsCorrect = inferredType == "objective" ? a.IsCorrect : (bool?)null,

                        SubjectiveText = a.SubjectiveText,
                        ModelAnswer = null
                    });
                    continue;
                }

                var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
                var selected = a.SelectedOptionId.HasValue
                    ? q.Options.FirstOrDefault(o => o.Id == a.SelectedOptionId.Value)
                    : null;

                dto.Answers.Add(new AttemptAnswerDetailDto
                {
                    QuestionId = q.Id,
                    QuestionText = q.Text,
                    Type = (q.Type == QuestionType.Subjective) ? "subjective" : "objective",
                    ImageUrl = q.ImageUrl,

                    SelectedOptionId = a.SelectedOptionId,
                    SelectedOptionText = selected?.Text,
                    CorrectOptionId = correct?.Id,
                    CorrectOptionText = correct?.Text,
                    IsCorrect = (q.Type == QuestionType.Objective) ? a.IsCorrect : (bool?)null,

                    SubjectiveText = a.SubjectiveText,
                    ModelAnswer = q.ModelAnswer
                });
            }

            return Ok(dto);
        }

        [Authorize(Roles = "admin")]
        [HttpPatch("attempts/{attemptId:int}/score")]
        public async Task<IActionResult> UpdateAttemptScore(int attemptId, [FromBody] UpdateAttemptScoreDto body)
        {
            var attempt = await _db.TestAttempts.FirstOrDefaultAsync(a => a.Id == attemptId);
            if (attempt == null) return NotFound();

            var newScore = Math.Max(0, Math.Min(body.Score, attempt.Total));
            attempt.Score = newScore;

            await _db.SaveChangesAsync();
            return Ok(new { attemptId, score = attempt.Score, total = attempt.Total });
        }
    }
}
