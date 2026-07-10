using umbral_backend.Domain.Entities;
using umbral_backend.Domain.Exceptions;

namespace umbral_backend.Application.UnitTests.Domain.Entities;

public class TargetTests
{
    [Fact]
    public void Create_TrimsAndStoresFields()
    {
        var target = Target.Create(" Statue ", " QR-1 ", 1, 20);

        target.Name.Should().Be("Statue");
        target.QrCode.Should().Be("QR-1");
        target.SequenceOrder.Should().Be(1);
        target.Score!.Points.Should().Be(20);
        target.IsActive.Should().BeTrue();
        target.ClueId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenNameBlank_Throws(string? name)
    {
        var act = () => Target.Create(name!, "QR-1", 1, 20);

        act.Should().Throw<TargetNameRequiredException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WhenQrCodeBlank_Throws(string? qrCode)
    {
        var act = () => Target.Create("Statue", qrCode!, 1, 20);

        act.Should().Throw<TargetQrCodeRequiredException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_WhenSequenceOrderNotPositive_Throws(int sequenceOrder)
    {
        var act = () => Target.Create("Statue", "QR-1", sequenceOrder, 20);

        act.Should().Throw<TargetSequenceOrderMustBePositiveException>();
    }

    [Fact]
    public void UpdateDetails_OverwritesFields()
    {
        var target = Target.Create("Statue", "QR-1", 1, 20);

        target.UpdateDetails("Fountain", "QR-2", 2, isActive: false, score: 30);

        target.Name.Should().Be("Fountain");
        target.QrCode.Should().Be("QR-2");
        target.SequenceOrder.Should().Be(2);
        target.Score!.Points.Should().Be(30);
        target.IsActive.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_WithoutScore_PreservesExistingScore()
    {
        var target = Target.Create("Statue", "QR-1", 1, 20);

        target.UpdateDetails("Fountain", "QR-2", 2, isActive: false);

        target.Score!.Points.Should().Be(20);
    }

    [Fact]
    public void AssociateClue_AllowsReassociatingSameClue()
    {
        var target = Target.Create("Statue", "QR-1", 1, 20);
        var substage = Substage.CreateTreasureHunt("Sub", 1);
        substage.AddTarget("Statue", "QR-1", 1, 20);
        var clue = Clue.Create("Hint", 1, "text");
        clue.Id = 5;
        substage.AddClue(clue);

        var added = substage.AddTarget("Tree", "QR-2", 2, 20);
        added.Id = 9;
        substage.AssociateClueWithTarget(9, clue);
        substage.AssociateClueWithTarget(9, clue); // idempotent

        added.ClueId.Should().Be(5);
    }
}
