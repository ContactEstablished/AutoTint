using AutoTint.Models;

namespace AutoTint.Tests;

public class TintPresetTests
{
    [Theory]
    [InlineData("black")]
    [InlineData("warm")]
    [InlineData("grey")]
    public void KnownIdsRoundTrip(string id)
    {
        Assert.Equal(id, TintPreset.FromId(id).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chartreuse")]
    public void UnknownIdsFallBackToBlack(string? id)
    {
        // A hand-edited or older settings file must not stop the app from starting.
        Assert.Equal("black", TintPreset.FromId(id).Id);
    }
}
