using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace quizTool.Models
{

    public enum QuestionType
    {
        Objective = 0,
        Subjective = 1
    }
    public class Test
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = "Untitled Test";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // NEW: when true, test cannot be modified (add/delete questions) 30 aug
        public bool IsLocked { get; set; } = false; // NEW

        // NEW: optional time limit in minutes
        public int? TimeLimitMinutes { get; set; } // NEW

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }

    public class Question
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Text { get; set; }

        public QuestionType Type { get; set; } = QuestionType.Objective;

        public string? ModelAnswer { get; set; }

        public string? ImageUrl { get; set; }

        [ForeignKey(nameof(Test))]
        public int TestId { get; set; }
        public Test Test { get; set; }

        public ICollection<Option> Options { get; set; } = new List<Option>();
    }

    public class Option
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Text { get; set; }

        public bool IsCorrect { get; set; }

        [ForeignKey(nameof(Question))]
        public int QuestionId { get; set; }
        public Question Question { get; set; }
    }

    public class TestAttempt
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int TestId { get; set; }
        public Test Test { get; set; }

        [Required]
        public string UserEmail { get; set; }

        public int Score { get; set; }
        public int Total { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TestAttemptAnswer> Answers { get; set; } = new List<TestAttemptAnswer>();
    }

    public class TestAttemptAnswer
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int AttemptId { get; set; }
        public TestAttempt Attempt { get; set; }

        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }


        public string? SubjectiveText { get; set; }
    }
}