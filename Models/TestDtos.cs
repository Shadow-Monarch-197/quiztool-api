namespace quizTool.Models
{
    public class UploadTestResultDto
    {
        public int TestId { get; set; }
        public string Title { get; set; }
        public int Questions { get; set; }
    }

    public class TestSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int QuestionCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // NEW
        public int? TimeLimitMinutes { get; set; } // NEW
    }

    public class TestDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; }

        // NEW: expose to take-test page
        public int? TimeLimitMinutes { get; set; } // NEW
        public List<QuestionDto> Questions { get; set; }
    }

    public class QuestionDto
    {
        public int Id { get; set; }
        public string Text { get; set; }

        public string Type { get; set; } = "objective";


        public string? ImageUrl { get; set; }

        public List<OptionDto> Options { get; set; }
    }

    public class OptionDto
    {
        public int Id { get; set; }
        public string Text { get; set; }
    }

    public class SubmitAttemptDto
    {
        public int TestId { get; set; }
        public string UserEmail { get; set; }
        public List<AnswerDto> Answers { get; set; }
    }

    public class AnswerDto
    {
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }

        public string? SubjectiveText { get; set; }
    }

    public class AttemptResultDto
    {
        public int AttemptId { get; set; }
        public int Score { get; set; }
        public int Total { get; set; }
    }

    public class AttemptListItemDto
    {
        public int Id { get; set; }
        public int TestId { get; set; }
        public string TestTitle { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public int Score { get; set; }
        public int Total { get; set; }
        public int Percent { get; set; }
        public DateTime AttemptedAt { get; set; }
    }

    public class AdminTestDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";

        //30 Aug
        public bool IsLocked { get; set; } = false; // NEW

        // NEW
        public int? TimeLimitMinutes { get; set; } // NEW
        public List<AdminQuestionDto> Questions { get; set; } = new();
    }

    public class AdminQuestionDto
    {
        public int Id { get; set; }
        public QuestionType Type { get; set; }
        public string Text { get; set; } = "";
        public string? ImageUrl { get; set; }
        public string? ModelAnswer { get; set; }
        public List<AdminOptionDto> Options { get; set; } = new();
    }

    public class AdminOptionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public bool IsCorrect { get; set; }
    }

    public class AttemptDetailDto
    {
        public int AttemptId { get; set; }
        public int TestId { get; set; }
        public string TestTitle { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public int Score { get; set; }
        public int Total { get; set; }
        public DateTime AttemptedAt { get; set; }
        public List<AttemptAnswerDetailDto> Answers { get; set; } = new();
    }
    ///// 03 sep ///////
    // NEW: request body to assign a test to users
    public class AssignTestDto // NEW
    {
        public List<string> Emails { get; set; } = new(); // NEW
    }

    // NEW: assignee list item
    public class TestAssigneeDto // NEW
    {
        public string Email { get; set; } = "";  // NEW
        public int? UserId { get; set; }         // NEW (if found)
        public string? Name { get; set; }        // NEW (if found)
        public DateTime AssignedAt { get; set; } // NEW
    }

    public class AttemptAnswerDetailDto
    {
        public int QuestionId { get; set; }
        public string QuestionText { get; set; } = "";

        public string Type { get; set; } = "objective";

        public string? ImageUrl { get; set; }

        public int? SelectedOptionId { get; set; }
        public string? SelectedOptionText { get; set; }
        public int? CorrectOptionId { get; set; }
        public string? CorrectOptionText { get; set; }
        public bool? IsCorrect { get; set; }

        public string? SubjectiveText { get; set; }
        public string? ModelAnswer { get; set; }
    }
    public class UpdateAttemptScoreDto
    {
        public int Score { get; set; }
    }

    //30 Aug
    // NEW: preview payload returned from parse-upload
    public class ParsedUploadDto // NEW
    {
        public string Title { get; set; } = "";

        public int? TimeLimitMinutes { get; set; } // NEW
        public List<AdminQuestionDto> Questions { get; set; } = new();
    }
    //30 Aug

    // NEW: body for save-parsed
    public class SaveParsedTestBody // NEW
    {
        public string Title { get; set; } = "";

        public int? TimeLimitMinutes { get; set; } // NEW
        public List<AdminQuestionDto> Questions { get; set; } = new();
    }
}