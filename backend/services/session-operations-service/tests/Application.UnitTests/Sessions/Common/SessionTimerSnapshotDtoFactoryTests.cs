using umbral_backend.Application.Dtos.Sessions;
using umbral_backend.Application.Sessions.Common;
using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;
using umbral_backend.Domain.Services;

namespace umbral_backend.Application.UnitTests.Sessions.Common;

public sealed class SessionTimerSnapshotDtoFactoryTests
{
    private static readonly DateTimeOffset Now = LiveSessionTestFactory.TriviaQuestionActivatedAt;

    [Fact]
    public void Create_WhileQuestionActive_HasNoAwaitingRevealSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var dto = Build(session, Now.AddSeconds(5));

        dto.ActiveQuestion.Should().NotBeNull();
        dto.AwaitingRevealQuestionSequenceOrder.Should().BeNull();
    }

    [Fact]
    public void Create_DuringRevealWindow_MidSubstage_ExposesJustClosedSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 2);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var dto = Build(session, Now.AddSeconds(32));

        // The just-closed question is history (no active window) but its result is being revealed.
        dto.ActiveQuestion.Should().BeNull();
        dto.AwaitingRevealQuestionSequenceOrder.Should().Be(1);
    }

    [Fact]
    public void Create_DuringRevealWindow_LastQuestion_ExposesJustClosedSequenceOrder()
    {
        var session = CreateActiveTrivia(questionCount: 1);
        var nextQuestionIndex = new SequentialQuestionActivationStrategy().Next(session);
        nextQuestionIndex.Should().BeNull("the substage's last question just closed");
        session.CloseActiveQuestionForReveal(Now.AddSeconds(30), TimeSpan.FromSeconds(5), nextQuestionIndex);

        var dto = Build(session, Now.AddSeconds(32));

        dto.AwaitingRevealQuestionSequenceOrder.Should().Be(1);
    }

    [Fact]
    public void Create_DuringCloseToAdvanceGap_HasNoAwaitingRevealSequenceOrder()
    {
        // A plain close (no reveal window) leaves nothing awaiting reveal — the field stays null.
        var session = CreateActiveTrivia(questionCount: 2);
        session.CloseActiveQuestion(Now.AddSeconds(30));

        var dto = Build(session, Now.AddSeconds(31));

        dto.AwaitingRevealQuestionSequenceOrder.Should().BeNull();
    }

    private static SessionTimerSnapshotDto Build(LiveSession session, DateTimeOffset observedAt) =>
        SessionTimerSnapshotDtoFactory.Create(
            session,
            teamId: null,
            session.GetAuthoritativeSessionTimerSnapshot(observedAt));

    private static LiveSession CreateActiveTrivia(int questionCount)
    {
        var session = LiveSessionTestFactory.CreateScheduledTrivia(questionCount: questionCount);
        session.AssociateTeam(Guid.NewGuid(), "Alpha", "A-01", 4);
        var policy = new SessionStateTransitionPolicy();
        session.MoveTo(SessionState.Preparing, Now.AddMinutes(-1), policy);
        session.MoveTo(SessionState.Active, Now.AddSeconds(-30), policy);
        session.ActivateQuestion(0, Now);
        return session;
    }
}
