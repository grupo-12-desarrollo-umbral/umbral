using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class LiveSessionConfiguration : IEntityTypeConfiguration<LiveSession>
{
    public void Configure(EntityTypeBuilder<LiveSession> builder)
    {
        builder.ToTable("live_sessions");

        builder.Ignore(session => session.Id);
        builder.Ignore(session => session.CreatedBy);
        builder.Ignore(session => session.LastModifiedBy);

        builder.HasKey(session => session.LiveSessionId);

        builder.Property(session => session.LiveSessionId)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(session => session.SessionMode)
            .HasColumnName("session_mode")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(session => session.SessionCode)
            .HasColumnName("session_code")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(session => session.TitleSnapshot)
            .HasColumnName("title_snapshot")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(session => session.State)
            .HasColumnName("state")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(session => session.ScheduledAt)
            .HasColumnName("scheduled_at")
            .IsRequired();

        builder.Property(session => session.StartedAt)
            .HasColumnName("started_at");

        builder.Property(session => session.PausedAt)
            .HasColumnName("paused_at");

        builder.Property(session => session.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(session => session.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.Property(session => session.LastStateChangedAt)
            .HasColumnName("last_state_changed_at")
            .IsRequired();

        builder.Property(session => session.StateReason)
            .HasColumnName("state_reason")
            .HasMaxLength(500);

        builder.Property<TimeSpan>("_sessionTimerTotalDuration")
            .HasColumnName("session_timer_total_duration")
            .IsRequired();

        builder.Property<TimeSpan>("_sessionTimerRemainingDuration")
            .HasColumnName("session_timer_remaining_duration")
            .IsRequired();

        builder.Property<DateTimeOffset?>("_sessionTimerAdvancingSince")
            .HasColumnName("session_timer_advancing_since");

        builder.Property<DateTimeOffset?>("_sessionTimerExpiredAt")
            .HasColumnName("session_timer_expired_at");

        builder.Property(session => session.AssignedOperatorUserId)
            .HasColumnName("assigned_operator_user_id");

        builder.Property(session => session.Created)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(session => session.LastModified)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.OwnsOne(session => session.Source, sourceBuilder =>
        {
            sourceBuilder.Property(source => source.SourceType)
                .HasColumnName("source_entity_type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            sourceBuilder.Property(source => source.SourceEntityId)
                .HasColumnName("source_entity_id")
                .IsRequired();

            sourceBuilder.Property(source => source.SourceTriviaQuizId)
                .HasColumnName("source_trivia_quiz_id");
        });

        builder.OwnsOne(session => session.MaximumTime, maximumTimeBuilder =>
        {
            maximumTimeBuilder.Property(maximumTime => maximumTime.Minutes)
                .HasColumnName("maximum_time_minutes")
                .IsRequired();
        });

        builder.OwnsOne(session => session.TriviaSnapshot, snapshotBuilder =>
        {
            snapshotBuilder.ToTable("live_session_trivia_snapshots");
            snapshotBuilder.WithOwner().HasForeignKey("live_session_id");

            snapshotBuilder.Ignore(snapshot => snapshot.Id);

            snapshotBuilder.Property<Guid>("live_session_id")
                .HasColumnName("live_session_id");

            snapshotBuilder.HasKey("live_session_id");

            snapshotBuilder.Property(snapshot => snapshot.QuizTitle)
                .HasColumnName("quiz_title")
                .HasMaxLength(200)
                .IsRequired();

            snapshotBuilder.OwnsMany(snapshot => snapshot.Questions, questionBuilder =>
            {
                questionBuilder.ToTable("live_session_trivia_snapshot_questions");
                questionBuilder.WithOwner().HasForeignKey("trivia_snapshot_live_session_id");

                questionBuilder.Property<int>("id")
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd();

                questionBuilder.Property<Guid>("trivia_snapshot_live_session_id")
                    .HasColumnName("live_session_id");

                questionBuilder.HasKey("id");

                questionBuilder.Property(question => question.Prompt)
                    .HasColumnName("prompt")
                    .HasMaxLength(4000)
                    .IsRequired();

                questionBuilder.Property(question => question.SequenceOrder)
                    .HasColumnName("sequence_order")
                    .IsRequired();

                questionBuilder.Property(question => question.ScoreValue)
                    .HasColumnName("score_value")
                    .IsRequired();

                questionBuilder.Property(question => question.TimeLimitSeconds)
                    .HasColumnName("time_limit_seconds")
                    .IsRequired();

                questionBuilder.Property(question => question.Explanation)
                    .HasColumnName("explanation")
                    .HasMaxLength(4000);

                questionBuilder.HasIndex("trivia_snapshot_live_session_id", nameof(Domain.ValueObjects.TriviaQuestionSnapshot.SequenceOrder))
                    .IsUnique();

                questionBuilder.OwnsMany(question => question.Options, optionBuilder =>
                {
                    optionBuilder.ToTable("live_session_trivia_snapshot_options");
                    optionBuilder.WithOwner().HasForeignKey("trivia_question_snapshot_id");

                    optionBuilder.Property<int>("id")
                        .HasColumnName("id")
                        .ValueGeneratedOnAdd();

                    optionBuilder.Property<int>("trivia_question_snapshot_id")
                        .HasColumnName("trivia_question_snapshot_id");

                    optionBuilder.HasKey("id");

                    optionBuilder.Property(option => option.OptionText)
                        .HasColumnName("option_text")
                        .HasMaxLength(2000)
                        .IsRequired();

                    optionBuilder.Property(option => option.SequenceOrder)
                        .HasColumnName("sequence_order")
                        .IsRequired();

                    optionBuilder.Property(option => option.IsCorrect)
                        .HasColumnName("is_correct")
                        .IsRequired();

                    optionBuilder.HasIndex("trivia_question_snapshot_id", nameof(Domain.ValueObjects.TriviaOptionSnapshot.SequenceOrder))
                        .IsUnique();
                });

                questionBuilder.Navigation(question => question.Options)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            snapshotBuilder.Navigation(snapshot => snapshot.Questions)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.OwnsMany(session => session.Teams, teamBuilder =>
        {
            teamBuilder.ToTable("live_session_teams");
            teamBuilder.WithOwner().HasForeignKey(team => team.LiveSessionId);

            teamBuilder.Ignore(team => team.Id);
            teamBuilder.HasKey(team => team.TeamId);

            teamBuilder.Property(team => team.TeamId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            teamBuilder.Property(team => team.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            teamBuilder.Property(team => team.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(200)
                .IsRequired();

            teamBuilder.Property(team => team.Capacity)
                .HasColumnName("team_capacity")
                .IsRequired();

            teamBuilder.Property(team => team.CurrentScore)
                .HasColumnName("current_score");

            teamBuilder.Property(team => team.CurrentProgressNodeId)
                .HasColumnName("current_progress_node_id");

            teamBuilder.Property(team => team.CurrentClueNodeId)
                .HasColumnName("current_clue_node_id");

            teamBuilder.Property(team => team.ReleasedClueCount)
                .HasColumnName("released_clue_count")
                .IsRequired();

            teamBuilder.Property(team => team.LastScoreCalculatedAt)
                .HasColumnName("last_score_calculated_at");

            teamBuilder.Property(team => team.JoinStatus)
                .HasColumnName("join_status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            teamBuilder.Property(team => team.TeamCode)
                .HasColumnName("team_code")
                .HasConversion(
                    teamCode => teamCode.Value,
                    value => Domain.ValueObjects.TeamCode.Create(value))
                .HasMaxLength(32)
                .IsRequired();

            teamBuilder.HasIndex(team => new { team.LiveSessionId, team.TeamCode })
                .IsUnique();

            teamBuilder.OwnsMany(team => team.Members, memberBuilder =>
            {
                memberBuilder.ToTable("live_session_team_members");
                memberBuilder.WithOwner().HasForeignKey(member => member.TeamId);

                memberBuilder.Ignore(member => member.Id);
                memberBuilder.HasKey(member => member.TeamMemberId);

                memberBuilder.Property(member => member.TeamMemberId)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                memberBuilder.Property(member => member.TeamId)
                    .HasColumnName("team_id")
                    .IsRequired();

                memberBuilder.Property(member => member.SessionParticipantId)
                    .HasColumnName("session_participant_id")
                    .IsRequired();

                memberBuilder.Property(member => member.MembershipStatus)
                    .HasColumnName("membership_status")
                    .HasConversion<string>()
                    .HasMaxLength(32)
                    .IsRequired();

                memberBuilder.Property(member => member.JoinedAt)
                    .HasColumnName("joined_at")
                    .IsRequired();

                memberBuilder.Property(member => member.LeftAt)
                    .HasColumnName("left_at");

                memberBuilder.HasIndex(member => new { member.TeamId, member.SessionParticipantId })
                    .IsUnique();
            });

            teamBuilder.Navigation(team => team.Members)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.OwnsMany(session => session.Participants, participantBuilder =>
        {
            participantBuilder.ToTable("live_session_participants");
            participantBuilder.WithOwner().HasForeignKey(participant => participant.LiveSessionId);

            participantBuilder.Ignore(participant => participant.Id);
            participantBuilder.HasKey(participant => participant.SessionParticipantId);

            participantBuilder.Property(participant => participant.SessionParticipantId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            participantBuilder.Property(participant => participant.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            participantBuilder.Property(participant => participant.ExternalIdentityId)
                .HasColumnName("external_identity_id")
                .IsRequired();

            participantBuilder.Property(participant => participant.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(200)
                .IsRequired();

            participantBuilder.Property(participant => participant.ParticipantStatus)
                .HasColumnName("participant_status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            participantBuilder.Property(participant => participant.JoinedAt)
                .HasColumnName("joined_at")
                .IsRequired();

            participantBuilder.Property(participant => participant.LastSeenAt)
                .HasColumnName("last_seen_at")
                .IsRequired();

            participantBuilder.HasIndex(participant => new { participant.LiveSessionId, participant.ExternalIdentityId })
                .IsUnique();
        });

        builder.OwnsMany(session => session.JoinContexts, joinContextBuilder =>
        {
            joinContextBuilder.ToTable("live_session_join_contexts");
            joinContextBuilder.WithOwner().HasForeignKey(joinContext => joinContext.LiveSessionId);

            joinContextBuilder.Ignore(joinContext => joinContext.Id);
            joinContextBuilder.HasKey(joinContext => joinContext.JoinContextId);

            joinContextBuilder.Property(joinContext => joinContext.JoinContextId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            joinContextBuilder.Property(joinContext => joinContext.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            joinContextBuilder.Property(joinContext => joinContext.TeamId)
                .HasColumnName("team_id");

            joinContextBuilder.Property(joinContext => joinContext.JoinTokenId)
                .HasColumnName("join_token_id");

            joinContextBuilder.Property(joinContext => joinContext.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            joinContextBuilder.Property(joinContext => joinContext.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            joinContextBuilder.Property(joinContext => joinContext.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            joinContextBuilder.Property(joinContext => joinContext.ConsumedAt)
                .HasColumnName("consumed_at");

            joinContextBuilder.HasIndex(joinContext => new { joinContext.LiveSessionId, joinContext.TeamId });
        });

        builder.Navigation(session => session.Teams)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.Participants)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.JoinContexts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.TriviaSnapshot)
            .IsRequired(false);

        builder.HasIndex(session => session.SessionCode)
            .IsUnique();
    }
}
