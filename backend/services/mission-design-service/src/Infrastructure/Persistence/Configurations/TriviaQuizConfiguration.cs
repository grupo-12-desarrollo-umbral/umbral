using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class TriviaQuizConfiguration : IEntityTypeConfiguration<TriviaQuiz>
{
    public void Configure(EntityTypeBuilder<TriviaQuiz> builder)
    {
        builder.ToTable("TriviaQuizzes");

        builder.HasKey(triviaQuiz => triviaQuiz.Id);

        builder.Property(triviaQuiz => triviaQuiz.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(triviaQuiz => triviaQuiz.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(triviaQuiz => triviaQuiz.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(triviaQuiz => triviaQuiz.Created)
            .IsRequired();

        builder.Property(triviaQuiz => triviaQuiz.LastModified)
            .IsRequired();

        builder.Ignore(triviaQuiz => triviaQuiz.CreatedBy);
        builder.Ignore(triviaQuiz => triviaQuiz.LastModifiedBy);

        builder.OwnsMany(triviaQuiz => triviaQuiz.Questions, questionBuilder =>
        {
            questionBuilder.ToTable("TriviaQuestions");
            questionBuilder.WithOwner().HasForeignKey("TriviaQuizId");

            questionBuilder.HasKey(question => question.Id);

            questionBuilder.Property(question => question.Id)
                .ValueGeneratedOnAdd();

            questionBuilder.Property(question => question.Prompt)
                .HasMaxLength(2000)
                .IsRequired();

            questionBuilder.Property(question => question.SequenceOrder)
                .IsRequired();

            questionBuilder.Property(question => question.IsActive)
                .IsRequired();

            questionBuilder.HasIndex("TriviaQuizId", nameof(TriviaQuestion.SequenceOrder))
                .IsUnique();

            questionBuilder.OwnsMany(question => question.Options, optionBuilder =>
            {
                optionBuilder.ToTable("TriviaOptions");
                optionBuilder.WithOwner().HasForeignKey("TriviaQuestionId");

                optionBuilder.HasKey(option => option.Id);

                optionBuilder.Property(option => option.Id)
                    .ValueGeneratedOnAdd();

                optionBuilder.Property(option => option.OptionText)
                    .HasMaxLength(1000)
                    .IsRequired();

                optionBuilder.Property(option => option.SequenceOrder)
                    .IsRequired();

                optionBuilder.Property(option => option.IsCorrect)
                    .IsRequired();

                optionBuilder.HasIndex("TriviaQuestionId", nameof(TriviaOption.SequenceOrder))
                    .IsUnique();
            });

            questionBuilder.Navigation(question => question.Options)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Navigation(triviaQuiz => triviaQuiz.Questions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(triviaQuiz => triviaQuiz.Status);
    }
}
