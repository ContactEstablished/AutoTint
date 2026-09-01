using AutoTint.Services;

namespace AutoTint.Tests;

public class StartupCommandTests
{
    private const string Installed =
        @"C:\Users\sam\AppData\Local\Programs\AutoTint\AutoTint.exe";

    [Fact]
    public void CommandIsQuoted()
    {
        // An unquoted path through a folder with a space is read by Windows as a command
        // plus arguments, and the app silently fails to start at logon.
        Assert.Equal('"', StartupCommand.For(Installed)[0]);
        Assert.EndsWith("\"", StartupCommand.For(Installed), System.StringComparison.Ordinal);
    }

    [Fact]
    public void PathWithSpacesSurvivesQuoting()
    {
        const string spaced = @"C:\Program Files\Auto Tint\AutoTint.exe";

        Assert.Equal("\"" + spaced + "\"", StartupCommand.For(spaced));
    }

    [Fact]
    public void AlreadyQuotedPathsAreNotDoubleQuoted()
    {
        Assert.Equal("\"" + Installed + "\"", StartupCommand.For("\"" + Installed + "\""));
    }

    [Fact]
    public void RecognisesItsOwnValue()
    {
        Assert.True(StartupCommand.PointsAt(StartupCommand.For(Installed), Installed));
    }

    [Theory]
    [InlineData("\"C:\\Users\\sam\\AppData\\Local\\Programs\\AutoTint\\AutoTint.exe\"")]
    [InlineData("C:\\Users\\sam\\AppData\\Local\\Programs\\AutoTint\\AutoTint.exe")]
    [InlineData("  \"C:\\Users\\sam\\AppData\\Local\\Programs\\AutoTint\\AutoTint.exe\"  ")]
    [InlineData("c:\\users\\sam\\appdata\\local\\programs\\autotint\\autotint.exe")]
    public void MatchesRegardlessOfQuotingSpacingOrCase(string registryValue)
    {
        // The value may have been written by an older build, by the installer, or by hand.
        Assert.True(StartupCommand.PointsAt(registryValue, Installed));
    }

    [Fact]
    public void DoesNotMatchADifferentCopyOfTheApp()
    {
        // This is what tells the app its logon entry is stale and needs repointing.
        const string elsewhere = @"D:\portable\AutoTint\AutoTint.exe";

        Assert.False(StartupCommand.PointsAt(StartupCommand.For(elsewhere), Installed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void MissingOrEmptyValuesDoNotCount(string? registryValue)
    {
        Assert.False(StartupCommand.PointsAt(registryValue, Installed));
    }
}
