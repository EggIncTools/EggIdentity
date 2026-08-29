using EggIdentity.StyleVerify;

namespace EggIdentity.StyleVerify.Tests;

public class StyleValueComparerTests {
    [Theory]
    [InlineData("16px", "16px")]
    [InlineData("16px", "16.2px")]
    [InlineData("16.4px", "16px")]
    [InlineData("flex", "flex")]
    public void AreEquivalent_WithinToleranceOrIdentical_ReturnsTrue(string oldValue, string newValue) {
        Assert.True(StyleValueComparer.AreEquivalent(oldValue, newValue));
    }

    [Theory]
    [InlineData("16px", "20px")]
    [InlineData("16px", "16rem")]
    [InlineData("flex", "grid")]
    [InlineData("16px", "flex")]
    public void AreEquivalent_OutsideToleranceOrDifferentUnitOrKind_ReturnsFalse(string oldValue, string newValue) {
        Assert.False(StyleValueComparer.AreEquivalent(oldValue, newValue));
    }

    [Fact]
    public void AreEquivalent_CustomTolerance_Honored() {
        Assert.True(StyleValueComparer.AreEquivalent("10px", "11px", numericTolerance: 1.5));
        Assert.False(StyleValueComparer.AreEquivalent("10px", "11px", numericTolerance: 0.5));
    }
}
