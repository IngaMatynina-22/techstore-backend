using TechStore.Application.Common;

namespace TechStore.UnitTests.Application;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Smart Phones", "smart-phones")]
    [InlineData("  USB-C Cables  ", "usb-c-cables")]
    [InlineData("A---B", "a-b")]
    public void Generate_NormalizesValue(string input, string expected)
    {
        Assert.Equal(expected, SlugHelper.Generate(input));
    }

    [Theory]
    [InlineData("smart-phones", true)]
    [InlineData("Invalid Slug", false)]
    [InlineData("", false)]
    public void IsValid_ChecksPattern(string slug, bool expected)
    {
        Assert.Equal(expected, SlugHelper.IsValid(slug));
    }
}
