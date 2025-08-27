using Microsoft.EntityFrameworkCore;

namespace quizTool.Models
{
    public class QuizTool_Dbcontext : DbContext
    {
        public QuizTool_Dbcontext(DbContextOptions<QuizTool_Dbcontext> options) : base(options) { }

        public DbSet<UserDataModel> Users { get; set; }
      
        public DbSet<Test> Tests { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Option> Options { get; set; }
        public DbSet<TestAttempt> TestAttempts { get; set; }
        public DbSet<TestAttemptAnswer> TestAttemptAnswers { get; set; }

        public DbSet<OtpCode> OtpCodes { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<UserDataModel>()
            .Property(u => u.role)
            .HasDefaultValue("basic");

            modelBuilder.Entity<Question>()
            .HasOne(q => q.Test)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TestId)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Option>()
            .HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TestAttemptAnswer>()
            .HasOne(a => a.Attempt)
            .WithMany(at => at.Answers)
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .Property(q => q.Type)
                .HasConversion<int>()                              // store enum as int
                .HasDefaultValue(QuestionType.Objective);


            modelBuilder.Entity<Question>()
                .Property(q => q.ModelAnswer)
                .HasColumnType("text");

            modelBuilder.Entity<Question>()
                .Property(q => q.ImageUrl)
                .HasColumnType("text");

            modelBuilder.Entity<TestAttemptAnswer>()
                .Property(a => a.SubjectiveText)
                .HasColumnType("text");

            modelBuilder.Entity<OtpCode>()
                .HasIndex(o => new { o.Email, o.Purpose, o.ExpiresAt });
        }

        public void SeedUsers()
        {
            if (!Users.Any())
            {
                Users.AddRange(
                    new UserDataModel
                    {
                        name = "Admin User",
                        email = "admin@example.com",
                        password = "Admin@123",  
                        role = "admin",
                        createddate = DateTime.UtcNow,
                        mobileno = "1234567890"
                    },
                    new UserDataModel
                    {
                        name = "Basic User",
                        email = "user@example.com",
                        password = "User@123",
                        role = "user",
                        createddate = DateTime.UtcNow,
                        mobileno = "0987654321"
                    }
                );

                SaveChanges();
            }
        }
    }
}


