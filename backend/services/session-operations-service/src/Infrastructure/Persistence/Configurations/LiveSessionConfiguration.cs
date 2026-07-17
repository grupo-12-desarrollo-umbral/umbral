using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using umbral_backend.Domain.Entities;

namespace umbral_backend.Infrastructure.Persistence.Configurations;

public sealed class LiveSessionConfiguration : IEntityTypeConfiguration<LiveSession>
{
    public void Configure(EntityTypeBuilder<LiveSession> builder)
    {
        builder.ToTable("live_sessions");

        // Optimistic concurrency over Postgres's xmin system column — no stored column, no data
        // migration. Guards the principal row only: state transitions, timer fields and the substage
        // reveal window. An owned-collection insert (an answer, a scan) touches no principal column,
        // emits no UPDATE, and is therefore guarded by that collection's unique index instead — see
        // the TriviaAnswerSubmissions and TreasureEvidenceSubmissions blocks below.
        //
        // Mapped by hand because Npgsql dropped UseXminAsConcurrencyToken() in v9; this is the
        // mapping that helper used to generate. xmin already exists on every table as a system
        // column, so the migration must NOT emit an AddColumn for it.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Ignore(session => session.Id);
        builder.Ignore(session => session.CreatedBy);
        builder.Ignore(session => session.LastModifiedBy);

        builder.HasKey(session => session.LiveSessionId);

        builder.Property(session => session.LiveSessionId)
            .HasColumnName("id")
            .ValueGeneratedNever();

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

        builder.Property(session => session.ActiveQuestionIndex)
            .HasColumnName("active_question_index");

        builder.Property(session => session.ActiveSubstageId)
            .HasColumnName("active_substage_id");

        builder.Property<TimeSpan>("_questionTimerTotalDuration")
            .HasColumnName("question_timer_total_duration")
            .IsRequired();

        builder.Property<TimeSpan>("_questionTimerRemainingDuration")
            .HasColumnName("question_timer_remaining_duration")
            .IsRequired();

        builder.Property<DateTimeOffset?>("_questionTimerAdvancingSince")
            .HasColumnName("question_timer_advancing_since");

        builder.Property<DateTimeOffset?>("_questionTimerExpiredAt")
            .HasColumnName("question_timer_expired_at");

        // The mission deadline (MaximumTime), seeded once at start and frozen while paused. Columns
        // renamed from substage_timer_* in AddMissionTimerColumns: same machinery, mission-wide budget.
        builder.Property<TimeSpan>("_missionTimerTotalDuration")
            .HasColumnName("mission_timer_total_duration")
            .IsRequired();

        builder.Property<TimeSpan>("_missionTimerRemainingDuration")
            .HasColumnName("mission_timer_remaining_duration")
            .IsRequired();

        builder.Property<DateTimeOffset?>("_missionTimerAdvancingSince")
            .HasColumnName("mission_timer_advancing_since");

        builder.Property<DateTimeOffset?>("_missionTimerExpiredAt")
            .HasColumnName("mission_timer_expired_at");

        // Post-close reveal window (HU-35): deadline for the deferred next-question activation, and the
        // captured next-question index (null => advance substage) applied when the reveal completes.
        builder.Property<DateTimeOffset?>("_questionRevealUntil")
            .HasColumnName("question_reveal_until");

        builder.Property<int?>("_pendingNextQuestionIndex")
            .HasColumnName("pending_next_question_index");

        // Substage-end ranking reveal (D-3): deadline for the advance/finish the reveal defers. Unlike
        // the mission timer this needs no remaining/frozen pair — it is an absolute deadline that a
        // pause does not extend (a paused session is not ticked at all).
        builder.Property<DateTimeOffset?>("_substageRevealUntil")
            .HasColumnName("substage_reveal_until");

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
        });

        builder.OwnsOne(session => session.MaximumTime, maximumTimeBuilder =>
        {
            maximumTimeBuilder.Property(maximumTime => maximumTime.Minutes)
                .HasColumnName("maximum_time_minutes")
                .IsRequired();
        });

        builder.OwnsOne(session => session.MissionRuntimeSnapshot, snapshotBuilder =>
        {
            snapshotBuilder.ToTable("live_session_mission_runtime_snapshots");
            snapshotBuilder.WithOwner().HasForeignKey("live_session_id");

            snapshotBuilder.Ignore(snapshot => snapshot.Id);

            snapshotBuilder.Property<Guid>("live_session_id")
                .HasColumnName("live_session_id");

            snapshotBuilder.HasKey("live_session_id");

            snapshotBuilder.Property(snapshot => snapshot.MissionRuntimeSnapshotId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            snapshotBuilder.Property(snapshot => snapshot.SourceMissionId)
                .HasColumnName("source_mission_id")
                .IsRequired();

            snapshotBuilder.Property(snapshot => snapshot.MissionTitle)
                .HasColumnName("mission_title")
                .HasMaxLength(200)
                .IsRequired();

            snapshotBuilder.OwnsOne(snapshot => snapshot.MaximumTime, maximumTimeBuilder =>
            {
                maximumTimeBuilder.Property(maximumTime => maximumTime.Minutes)
                    .HasColumnName("maximum_time_minutes")
                    .IsRequired();
            });

            snapshotBuilder.OwnsMany(snapshot => snapshot.StageSnapshots, stageBuilder =>
            {
                stageBuilder.ToTable("live_session_mission_runtime_snapshot_stages");
                stageBuilder.WithOwner().HasForeignKey("mission_runtime_snapshot_live_session_id");

                stageBuilder.Property<Guid>("mission_runtime_snapshot_live_session_id")
                    .HasColumnName("live_session_id");

                stageBuilder.Property(stage => stage.StageSnapshotId)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                stageBuilder.HasKey(stage => stage.StageSnapshotId);

                stageBuilder.Property(stage => stage.Title)
                    .HasColumnName("title")
                    .HasMaxLength(200)
                    .IsRequired();

                stageBuilder.Property(stage => stage.SequenceOrder)
                    .HasColumnName("sequence_order")
                    .IsRequired();

                stageBuilder.HasIndex("mission_runtime_snapshot_live_session_id", nameof(Domain.ValueObjects.StageSnapshot.SequenceOrder))
                    .IsUnique();

                stageBuilder.OwnsMany(stage => stage.SubstageSnapshots, substageBuilder =>
                {
                    substageBuilder.ToTable("live_session_mission_runtime_snapshot_substages");
                    substageBuilder.WithOwner().HasForeignKey("stage_snapshot_id");

                    substageBuilder.Property<Guid>("stage_snapshot_id")
                        .HasColumnName("stage_snapshot_id");

                    substageBuilder.Property(substage => substage.SubstageSnapshotId)
                        .HasColumnName("id")
                        .ValueGeneratedNever();

                    substageBuilder.HasKey(substage => substage.SubstageSnapshotId);

                    substageBuilder.Property(substage => substage.Title)
                        .HasColumnName("title")
                        .HasMaxLength(200)
                        .IsRequired();

                    substageBuilder.Property(substage => substage.SequenceOrder)
                        .HasColumnName("sequence_order")
                        .IsRequired();

                    substageBuilder.Property(substage => substage.PlayMode)
                        .HasColumnName("play_mode")
                        .HasConversion<string>()
                        .HasMaxLength(32)
                        .IsRequired();

                    substageBuilder.HasIndex("stage_snapshot_id", nameof(Domain.ValueObjects.SubstageSnapshot.SequenceOrder))
                        .IsUnique();
                });

                stageBuilder.Navigation(stage => stage.SubstageSnapshots)
                    .UsePropertyAccessMode(PropertyAccessMode.Field);
            });

            snapshotBuilder.OwnsMany(snapshot => snapshot.TargetSnapshots, targetBuilder =>
            {
                targetBuilder.ToTable("live_session_mission_runtime_snapshot_targets");
                targetBuilder.WithOwner().HasForeignKey("mission_runtime_snapshot_live_session_id");

                targetBuilder.Property<Guid>("mission_runtime_snapshot_live_session_id")
                    .HasColumnName("live_session_id");

                targetBuilder.Property(target => target.TargetSnapshotId)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                targetBuilder.HasKey(target => target.TargetSnapshotId);

                targetBuilder.Property(target => target.SubstageSnapshotId)
                    .HasColumnName("substage_snapshot_id")
                    .IsRequired();

                targetBuilder.Property(target => target.Name)
                    .HasColumnName("name")
                    .HasMaxLength(200)
                    .IsRequired();

                targetBuilder.Property(target => target.QrCode)
                    .HasColumnName("qr_code")
                    .HasMaxLength(200)
                    .IsRequired();

                targetBuilder.Property(target => target.SequenceOrder)
                    .HasColumnName("sequence_order")
                    .IsRequired();

                targetBuilder.Property(target => target.IsActive)
                    .HasColumnName("is_active")
                    .IsRequired();

                targetBuilder.Property(target => target.Score)
                    .HasColumnName("score")
                    .IsRequired();

                targetBuilder.Property(target => target.Latitude)
                    .HasColumnName("latitude")
                    .IsRequired();

                targetBuilder.Property(target => target.Longitude)
                    .HasColumnName("longitude")
                    .IsRequired();

                targetBuilder.Property(target => target.ClueText)
                    .HasColumnName("clue_text")
                    .HasMaxLength(4000);

                targetBuilder.Property(target => target.ClueVisibilityPolicy)
                    .HasColumnName("clue_visibility_policy")
                    .HasMaxLength(128);

                targetBuilder.HasIndex("mission_runtime_snapshot_live_session_id", nameof(Domain.ValueObjects.TargetSnapshot.QrCode))
                    .IsUnique();

                targetBuilder.HasIndex(
                        "mission_runtime_snapshot_live_session_id",
                        nameof(Domain.ValueObjects.TargetSnapshot.SubstageSnapshotId),
                        nameof(Domain.ValueObjects.TargetSnapshot.SequenceOrder))
                    .IsUnique();
            });

            snapshotBuilder.OwnsMany(snapshot => snapshot.TriviaQuestionSnapshots, questionBuilder =>
            {
                questionBuilder.ToTable("live_session_mission_runtime_snapshot_trivia_questions");
                questionBuilder.WithOwner().HasForeignKey("mission_runtime_snapshot_live_session_id");

                questionBuilder.Property<int>("id")
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd();

                questionBuilder.Property<Guid>("mission_runtime_snapshot_live_session_id")
                    .HasColumnName("live_session_id");

                questionBuilder.HasKey("id");

                questionBuilder.Property(question => question.SubstageSnapshotId)
                    .HasColumnName("substage_snapshot_id")
                    .IsRequired();

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

                questionBuilder.HasIndex(
                        "mission_runtime_snapshot_live_session_id",
                        nameof(Domain.ValueObjects.TriviaQuestionSnapshot.SubstageSnapshotId),
                        nameof(Domain.ValueObjects.TriviaQuestionSnapshot.SequenceOrder))
                    .IsUnique();

                questionBuilder.OwnsMany(question => question.Options, optionBuilder =>
                {
                    optionBuilder.ToTable("live_session_mission_runtime_snapshot_trivia_options");
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

            snapshotBuilder.OwnsMany(snapshot => snapshot.ClueSnapshots, clueBuilder =>
            {
                clueBuilder.ToTable("live_session_mission_runtime_snapshot_clues");
                clueBuilder.WithOwner().HasForeignKey("mission_runtime_snapshot_live_session_id");

                clueBuilder.Property<Guid>("mission_runtime_snapshot_live_session_id")
                    .HasColumnName("live_session_id");

                clueBuilder.Property(clue => clue.ClueSnapshotId)
                    .HasColumnName("id")
                    .ValueGeneratedNever();

                clueBuilder.HasKey(clue => clue.ClueSnapshotId);

                clueBuilder.Property(clue => clue.SubstageSnapshotId)
                    .HasColumnName("substage_snapshot_id")
                    .IsRequired();

                clueBuilder.Property(clue => clue.Text)
                    .HasColumnName("text")
                    .HasMaxLength(4000)
                    .IsRequired();

                clueBuilder.Property(clue => clue.VisibilityPolicy)
                    .HasColumnName("visibility_policy")
                    .HasMaxLength(128)
                    .IsRequired();

                clueBuilder.Property(clue => clue.SequenceOrder)
                    .HasColumnName("sequence_order")
                    .IsRequired();

                clueBuilder.HasIndex(
                        "mission_runtime_snapshot_live_session_id",
                        nameof(Domain.ValueObjects.ClueSnapshot.SubstageSnapshotId),
                        nameof(Domain.ValueObjects.ClueSnapshot.SequenceOrder))
                    .IsUnique();
            });

            snapshotBuilder.Navigation(snapshot => snapshot.StageSnapshots)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            snapshotBuilder.Navigation(snapshot => snapshot.TargetSnapshots)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            snapshotBuilder.Navigation(snapshot => snapshot.TriviaQuestionSnapshots)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            snapshotBuilder.Navigation(snapshot => snapshot.ClueSnapshots)
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

            teamBuilder.Property(team => team.ReferenceTeamId)
                .HasColumnName("reference_team_id");

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

            teamBuilder.HasIndex(team => new { team.LiveSessionId, team.ReferenceTeamId })
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

                // Filtered on Active because a membership row is per-stint history, not per-participant:
                // ReleaseParticipant marks the row Removed (keeping left_at) and AssignParticipant adds a
                // fresh one, so rejoining a team leaves several rows behind for the same pair. The domain
                // invariant is only that at most one of them is Active — an unfiltered unique index made a
                // legitimate rejoin fail with 23505.
                memberBuilder.HasIndex(member => new { member.TeamId, member.SessionParticipantId })
                    .IsUnique()
                    .HasFilter("membership_status = 'Active'");

                // The authoritative "one active team per participant" guard (Finding 3). The index above
                // only forbids two active rows for the *same* team+participant pair, so nothing at the row
                // level stopped a participant holding an active membership in two *different* teams —
                // Team.AssignParticipant checks only its own team, and LiveSession.SelectTeam's release-then-
                // assign ordering is the sole thing keeping it to one. Serialising every aggregate write
                // makes concurrent switches sequential, but this filtered index is the last-line invariant
                // that survives a bypass or future regression: a second Active row for one participant, in
                // any team, collides. Removed rows are excluded so a legitimate switch/rejoin still writes.
                memberBuilder.HasIndex(member => member.SessionParticipantId)
                    .IsUnique()
                    .HasFilter("membership_status = 'Active'")
                    .HasDatabaseName("ux_team_member_one_active_team_per_participant");
            });

            teamBuilder.Navigation(team => team.Members)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Append-only per-team clue-release trace (HU-26). LiveSession exposes only the field-only
        // navigation `_clueReleaseRecords` (read via GetClueReleaseRecords()), so the owned collection is
        // mapped by field name — the same way the runtime Teams collection is loaded, so GetByIdAsync
        // hydrates the release records back into the aggregate and the in-memory duplicate/no-leak guards
        // still see prior releases after a reload. ClueReleaseRecord derives from BaseEntity (no audit
        // columns), so the only base ceremony to strip is BaseEntity.Id.
        builder.OwnsMany<ClueReleaseRecord>("_clueReleaseRecords", releaseBuilder =>
        {
            releaseBuilder.ToTable(
                "live_session_clue_releases",
                tableBuilder => tableBuilder.HasCheckConstraint(
                    "CK_live_session_clue_releases_exactly_one_subject",
                    "(target_id IS NOT NULL) <> (clue_id IS NOT NULL)"));
            releaseBuilder.WithOwner().HasForeignKey(release => release.LiveSessionId);

            releaseBuilder.Ignore(release => release.Id);
            releaseBuilder.HasKey(release => release.ClueReleaseRecordId);

            releaseBuilder.Property(release => release.ClueReleaseRecordId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            releaseBuilder.Property(release => release.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            releaseBuilder.Property(release => release.TeamId)
                .HasColumnName("team_id")
                .IsRequired();

            releaseBuilder.Property(release => release.TargetId)
                .HasColumnName("target_id");

            releaseBuilder.Property(release => release.ClueId)
                .HasColumnName("clue_id");

            releaseBuilder.Property(release => release.ReleaseMode)
                .HasColumnName("release_mode")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            releaseBuilder.Property(release => release.ReleasedByUserId)
                .HasColumnName("released_by_user_id");

            releaseBuilder.Property(release => release.ReleasedAt)
                .HasColumnName("released_at")
                .IsRequired();

            // Spec §ClueReleaseRecord L696 forbids releasing the same clue twice to one team in one
            // session. The target-backed and clue-snapshot-backed subjects therefore need independent
            // filtered indexes: PostgreSQL otherwise treats nullable keys as distinct.
            releaseBuilder.HasIndex(release => new
                {
                    release.LiveSessionId,
                    release.TeamId,
                    release.TargetId,
                })
                .IsUnique()
                .HasFilter("target_id IS NOT NULL");

            releaseBuilder.HasIndex(release => new
                {
                    release.LiveSessionId,
                    release.TeamId,
                    release.ClueId,
                })
                .IsUnique()
                .HasFilter("clue_id IS NOT NULL");
        });

        // Append-only operator-authored clues targeted to individual teams (HU-28). The aggregate exposes
        // the collection only through GetOperativeClues(), so map and hydrate its backing field directly,
        // mirroring ClueReleaseRecord. OperativeClue derives from BaseEntity rather than
        // BaseAuditableEntity: ignore the inherited integer Id and map only the explicit authorship/time
        // fields below so no aggregate audit columns leak into the owned-child table.
        builder.OwnsMany<OperativeClue>("_operativeClues", operativeClueBuilder =>
        {
            operativeClueBuilder.ToTable("live_session_operative_clues");
            operativeClueBuilder.WithOwner().HasForeignKey(clue => clue.LiveSessionId);

            operativeClueBuilder.Ignore(clue => clue.Id);
            operativeClueBuilder.HasKey(clue => clue.OperativeClueId);

            operativeClueBuilder.Property(clue => clue.OperativeClueId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            operativeClueBuilder.Property(clue => clue.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            operativeClueBuilder.Property(clue => clue.TeamId)
                .HasColumnName("team_id")
                .IsRequired();

            operativeClueBuilder.Property(clue => clue.ClueText)
                .HasColumnName("clue_text")
                .HasMaxLength(500)
                .IsRequired();

            operativeClueBuilder.Property(clue => clue.CreatedByUserId)
                .HasColumnName("created_by_user_id")
                .IsRequired();

            operativeClueBuilder.Property(clue => clue.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
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

            // Presence leases (Finding 5): one row per live socket the participant holds, keyed on the
            // SignalR ConnectionId. Owned/hydrated by field like the other aggregate children so a
            // reload rebuilds the connection set and DropConnection's decrement guard sees prior sockets
            // after an xmin-conflict retry. ParticipantConnection derives from BaseEntity, so the only
            // base ceremony to strip is BaseEntity.Id.
            participantBuilder.OwnsMany(participant => participant.Connections, connectionBuilder =>
            {
                connectionBuilder.ToTable("live_session_participant_connections");
                connectionBuilder.WithOwner().HasForeignKey(connection => connection.SessionParticipantId);

                connectionBuilder.Ignore(connection => connection.Id);
                connectionBuilder.HasKey(connection => connection.ConnectionId);

                connectionBuilder.Property(connection => connection.ConnectionId)
                    .HasColumnName("connection_id")
                    .HasMaxLength(200)
                    .ValueGeneratedNever();

                connectionBuilder.Property(connection => connection.SessionParticipantId)
                    .HasColumnName("session_participant_id")
                    .IsRequired();

                connectionBuilder.Property(connection => connection.OpenedAt)
                    .HasColumnName("opened_at")
                    .IsRequired();

                connectionBuilder.Property(connection => connection.LastSeenAt)
                    .HasColumnName("last_seen_at")
                    .IsRequired();
            });

            participantBuilder.Navigation(participant => participant.Connections)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
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

        // Accepted trivia answers: the base EvidenceSubmission fields + the TriviaAnswerSubmission
        // specialization, owned by the session aggregate. Only the concrete leaf is mapped
        // (EvidenceSubmission is abstract and never surfaces as its own table), so its inherited
        // columns flatten onto this one table. TriviaAnswerSubmission derives from BaseEntity (NOT
        // BaseAuditableEntity), so it carries no created_by/updated_by/created_at/updated_at audit
        // columns — the only base-class ceremony to strip is BaseEntity.Id, ignored exactly as the
        // Teams/Participants/JoinContexts children do.
        builder.OwnsMany(session => session.TriviaAnswerSubmissions, answerBuilder =>
        {
            answerBuilder.ToTable("live_session_trivia_answer_submissions");
            answerBuilder.WithOwner().HasForeignKey(answer => answer.LiveSessionId);

            answerBuilder.Ignore(answer => answer.Id);
            answerBuilder.HasKey(answer => answer.EvidenceSubmissionId);

            answerBuilder.Property(answer => answer.EvidenceSubmissionId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            answerBuilder.Property(answer => answer.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            answerBuilder.Property(answer => answer.TeamId)
                .HasColumnName("team_id")
                .IsRequired();

            answerBuilder.Property(answer => answer.ActiveSubstageId)
                .HasColumnName("active_substage_id")
                .IsRequired();

            answerBuilder.Property(answer => answer.SubmissionType)
                .HasColumnName("submission_type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            answerBuilder.Property(answer => answer.SubmittedByParticipantId)
                .HasColumnName("submitted_by_participant_id");

            answerBuilder.Property(answer => answer.SubmittedAt)
                .HasColumnName("submitted_at")
                .IsRequired();

            answerBuilder.Property(answer => answer.ValidationState)
                .HasColumnName("validation_state")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            answerBuilder.Property(answer => answer.RejectionReason)
                .HasColumnName("rejection_reason")
                .HasConversion<string>()
                .HasMaxLength(256);

            answerBuilder.Property(answer => answer.QuestionSequenceOrder)
                .HasColumnName("question_sequence_order")
                .IsRequired();

            answerBuilder.Property(answer => answer.SelectedOptionSequenceOrder)
                .HasColumnName("selected_option_sequence_order")
                .IsRequired();

            answerBuilder.Property(answer => answer.IsCorrect)
                .HasColumnName("is_correct")
                .IsRequired();

            answerBuilder.Property(answer => answer.ScoreValue)
                .HasColumnName("score_value")
                .IsRequired();

            // First-write-wins enforced at the DB boundary: exactly one accepted answer per team per
            // snapshotted trivia question. Mirrors the domain's EnsureFirstAnswerWins composite key
            // (team + substage snapshot + question sequence order), scoped to the owning session.
            answerBuilder.HasIndex(answer => new
                {
                    answer.LiveSessionId,
                    answer.TeamId,
                    answer.ActiveSubstageId,
                    answer.QuestionSequenceOrder,
                })
                .IsUnique();
        });

        // Append-only per-transition audit trail (bd_umbral_entity_spec.md:453-480): one SessionEvent
        // per valid state change, capturing actor + reason. SessionEvent derives from BaseEntity (NOT
        // BaseAuditableEntity), so it carries no created_by/updated_by/created_at/updated_at columns —
        // the only base-class ceremony to strip is BaseEntity.Id, ignored exactly as the other children
        // do. Never updated after insert; mirrors the TriviaAnswerSubmissions OwnsMany block.
        builder.OwnsMany(session => session.SessionEvents, eventBuilder =>
        {
            eventBuilder.ToTable("live_session_events");
            eventBuilder.WithOwner().HasForeignKey(sessionEvent => sessionEvent.LiveSessionId);

            eventBuilder.Ignore(sessionEvent => sessionEvent.Id);
            eventBuilder.HasKey(sessionEvent => sessionEvent.SessionEventId);

            eventBuilder.Property(sessionEvent => sessionEvent.SessionEventId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            eventBuilder.Property(sessionEvent => sessionEvent.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            eventBuilder.Property(sessionEvent => sessionEvent.OccurredAt)
                .HasColumnName("occurred_at")
                .IsRequired();

            eventBuilder.Property(sessionEvent => sessionEvent.ActorType)
                .HasColumnName("actor_type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            eventBuilder.Property(sessionEvent => sessionEvent.ActorId)
                .HasColumnName("actor_id");

            eventBuilder.Property(sessionEvent => sessionEvent.EventType)
                .HasColumnName("event_type")
                .HasMaxLength(64)
                .IsRequired();

            // Wide enough to hold "{previous}→{current}: {reason}"; reason mirrors StateReason (500).
            eventBuilder.Property(sessionEvent => sessionEvent.PayloadSummary)
                .HasColumnName("payload_summary")
                .HasMaxLength(600)
                .IsRequired();

            eventBuilder.Property(sessionEvent => sessionEvent.CorrelationId)
                .HasColumnName("correlation_id")
                .IsRequired();
        });

        builder.Navigation(session => session.Teams)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation("_clueReleaseRecords")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.Participants)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.JoinContexts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Treasure (QR/target) evidence submissions: base EvidenceSubmission umbrella + concrete
        // TreasureEvidenceSubmission specialization, owned by the session aggregate. TreasureEvidenceSubmission
        // derives from BaseEntity (NOT BaseAuditableEntity), so it carries no created_by/updated_by/
        // created_at/updated_at audit columns — the only base-class ceremony to strip is BaseEntity.Id.
        builder.OwnsMany(session => session.TreasureEvidenceSubmissions, treasureBuilder =>
        {
            treasureBuilder.ToTable("live_session_treasure_evidence_submissions");
            treasureBuilder.WithOwner().HasForeignKey(treasure => treasure.LiveSessionId);

            treasureBuilder.Ignore(treasure => treasure.Id);
            treasureBuilder.HasKey(treasure => treasure.EvidenceSubmissionId);

            treasureBuilder.Property(treasure => treasure.EvidenceSubmissionId)
                .HasColumnName("id")
                .ValueGeneratedNever();

            treasureBuilder.Property(treasure => treasure.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.TeamId)
                .HasColumnName("team_id")
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.ActiveSubstageId)
                .HasColumnName("active_substage_id")
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.SubmissionType)
                .HasColumnName("submission_type")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.SubmittedByParticipantId)
                .HasColumnName("submitted_by_participant_id");

            treasureBuilder.Property(treasure => treasure.SubmittedAt)
                .HasColumnName("submitted_at")
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.ValidationState)
                .HasColumnName("validation_state")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.RejectionReason)
                .HasColumnName("rejection_reason")
                .HasConversion<string>()
                .HasMaxLength(256);

            treasureBuilder.Property(treasure => treasure.ScannedValue)
                .HasColumnName("scanned_value")
                .HasMaxLength(200)
                .IsRequired();

            treasureBuilder.Property(treasure => treasure.TargetSnapshotId)
                .HasColumnName("target_snapshot_id");

            treasureBuilder.Property(treasure => treasure.ResolutionRejectionReason)
                .HasColumnName("resolution_rejection_reason")
                .HasConversion<string>()
                .HasMaxLength(64);

            // First-resolution-wins enforced at the DB boundary: exactly one *accepted* resolution per
            // team per snapshotted target. Mirrors DetermineTargetResolutionRejection's alreadyResolved
            // check, which two concurrent scans of the same QR can both pass — read-then-Add is not
            // atomic — double-counting the target.
            //
            // The Accepted filter is load-bearing, not an optimisation: a rejected scan keeps the
            // target_snapshot_id it resolved to, so a team re-scanning an already-resolved code writes
            // repeated rejected rows that an unfiltered index would collide on.
            treasureBuilder.HasIndex(treasure => new
                {
                    treasure.LiveSessionId,
                    treasure.TeamId,
                    treasure.TargetSnapshotId,
                })
                .IsUnique()
                .HasFilter("validation_state = 'Accepted'")
                .HasDatabaseName("ux_treasure_evidence_accepted_target");

            // Redeclares the FK index the conventions would otherwise drop as redundant: they see the
            // unique index above starting with live_session_id and treat it as covering this prefix,
            // but it is partial, so it cannot serve the unfiltered by-session load that hydrates an
            // aggregate's submissions.
            treasureBuilder.HasIndex(treasure => treasure.LiveSessionId);
        });

        builder.Navigation(session => session.TreasureEvidenceSubmissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.TriviaAnswerSubmissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.SessionEvents)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(session => session.MissionRuntimeSnapshot)
            .IsRequired();

        builder.HasIndex(session => session.SessionCode)
            .IsUnique();
    }
}
