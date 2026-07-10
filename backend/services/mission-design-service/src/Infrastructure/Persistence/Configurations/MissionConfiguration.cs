using umbral_backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> builder)
    {
        builder.ToTable("Missions");

        builder.HasKey(mission => mission.Id);

        builder.Property(mission => mission.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(mission => mission.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(mission => mission.ActivationState)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(mission => mission.IsActive)
            .IsRequired();

        builder.Property(mission => mission.ArchivedAt);

        builder.Property(mission => mission.Created)
            .IsRequired();

        builder.Property(mission => mission.LastModified)
            .IsRequired();

        builder.Property(mission => mission.CreatedBy);
        builder.Property(mission => mission.LastModifiedBy);

        builder.OwnsOne(mission => mission.Difficulty, difficultyBuilder =>
        {
            difficultyBuilder.Property(difficulty => difficulty.Value)
                .HasColumnName("Difficulty")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.OwnsOne(mission => mission.MaximumTime, maximumTimeBuilder =>
        {
            maximumTimeBuilder.Property(maximumTime => maximumTime.Minutes)
                .HasColumnName("MaximumTimeMinutes")
                .IsRequired();
        });

        builder.OwnsMany<Stage>("_stages", stageBuilder =>
        {
            stageBuilder.ToTable("MissionStages");
            stageBuilder.WithOwner().HasForeignKey("MissionId");

            stageBuilder.HasKey(stage => stage.Id);

            stageBuilder.Property(stage => stage.Id)
                .ValueGeneratedOnAdd();

            stageBuilder.Property(stage => stage.Title)
                .HasMaxLength(200)
                .IsRequired();

            stageBuilder.Property(stage => stage.SequenceOrder)
                .IsRequired();

            stageBuilder.Ignore(stage => stage.NodeType);
            stageBuilder.Ignore(stage => stage.Children);
            stageBuilder.Ignore(stage => stage.Substages);
            stageBuilder.Ignore(stage => stage.DomainEvents);

            stageBuilder.HasIndex("MissionId", nameof(Stage.SequenceOrder))
                .IsUnique();

            stageBuilder.OwnsMany<Substage>("_substages", substageBuilder =>
            {
                substageBuilder.ToTable("MissionSubstages");
                substageBuilder.WithOwner().HasForeignKey("StageId");

                substageBuilder.HasKey(substage => substage.Id);

                substageBuilder.Property(substage => substage.Id)
                    .ValueGeneratedOnAdd();

                substageBuilder.Property(substage => substage.Title)
                    .HasMaxLength(200)
                    .IsRequired();

                substageBuilder.Property(substage => substage.SequenceOrder)
                    .IsRequired();

                substageBuilder.Property(substage => substage.PlayMode)
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                substageBuilder.Property(substage => substage.TriviaQuizId);

                substageBuilder.Ignore(substage => substage.NodeType);
                substageBuilder.Ignore(substage => substage.Children);
                substageBuilder.Ignore(substage => substage.Clues);
                substageBuilder.Ignore(substage => substage.Targets);
                substageBuilder.Ignore(substage => substage.DomainEvents);

                substageBuilder.HasIndex("StageId", nameof(Substage.SequenceOrder))
                    .IsUnique();

                substageBuilder.OwnsMany<Target>("_targets", targetBuilder =>
                {
                    targetBuilder.ToTable("MissionTargets");
                    targetBuilder.WithOwner().HasForeignKey("SubstageId");

                    targetBuilder.HasKey(target => target.Id);

                    targetBuilder.Property(target => target.Id)
                        .ValueGeneratedOnAdd();

                    targetBuilder.Property(target => target.Name)
                        .HasMaxLength(200)
                        .IsRequired();

                    targetBuilder.Property(target => target.QrCode)
                        .HasMaxLength(500)
                        .IsRequired();

                    targetBuilder.Property(target => target.SequenceOrder)
                        .IsRequired();

                    targetBuilder.Property(target => target.IsActive)
                        .IsRequired();

                    targetBuilder.Property(target => target.ClueId);

                    targetBuilder.OwnsOne(target => target.Score, scoreBuilder =>
                    {
                        scoreBuilder.Property(score => score.Points)
                            .HasColumnName("Score");
                    });

                    targetBuilder.Navigation(target => target.Score).IsRequired();

                    targetBuilder.Ignore(target => target.DomainEvents);

                    targetBuilder.HasIndex("SubstageId", nameof(Target.SequenceOrder))
                        .IsUnique();
                });

                substageBuilder.OwnsMany<Clue>("_clues", clueBuilder =>
                {
                    clueBuilder.ToTable("MissionClues");
                    clueBuilder.WithOwner().HasForeignKey("SubstageId");

                    clueBuilder.HasKey(clue => clue.Id);

                    clueBuilder.Property(clue => clue.Id)
                        .ValueGeneratedOnAdd();

                    clueBuilder.Property(clue => clue.Title)
                        .HasMaxLength(200)
                        .IsRequired();

                    clueBuilder.Property(clue => clue.SequenceOrder)
                        .IsRequired();

                    clueBuilder.Property(clue => clue.Text)
                        .HasMaxLength(2000)
                        .IsRequired();

                    clueBuilder.Property(clue => clue.Visibility)
                        .HasConversion<string>()
                        .HasMaxLength(50)
                        .IsRequired();

                    clueBuilder.Ignore(clue => clue.NodeType);
                    clueBuilder.Ignore(clue => clue.Children);
                    clueBuilder.Ignore(clue => clue.DomainEvents);

                    clueBuilder.HasIndex("SubstageId", nameof(Clue.SequenceOrder))
                        .IsUnique();
                });
            });
        });

        builder.Ignore(mission => mission.Stages);

        builder.HasIndex(mission => mission.IsActive);
    }
}
