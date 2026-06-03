using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Enums;

namespace umbral_backend.SessionOperations.UnitTests.Domain.Entities;

public sealed class TeamMemberTests
{
    [Fact]
    public void Remove_MarksMembershipRemoved()
    {
        var member = TeamMember.Assign(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        member.Remove(DateTimeOffset.UtcNow.AddMinutes(1));

        member.MembershipStatus.Should().Be(TeamMembershipStatus.Removed);
        member.LeftAt.Should().NotBeNull();
    }
}
