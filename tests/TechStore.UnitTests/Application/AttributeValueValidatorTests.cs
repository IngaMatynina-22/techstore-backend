using TechStore.Application.Common;
using TechStore.Domain.Enums;

namespace TechStore.UnitTests.Application;

public class AttributeValueValidatorTests
{
    [Fact]
    public void Validate_RequiredMissing_Fails()
    {
        var result = AttributeValueValidator.Validate(
            AttributeDataType.Text,
            isRequired: true,
            value: null,
            optionId: null,
            validOptionIds: []);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_SelectWithForeignOption_Fails()
    {
        var result = AttributeValueValidator.Validate(
            AttributeDataType.Select,
            isRequired: true,
            value: null,
            optionId: 99,
            validOptionIds: [1, 2]);

        Assert.True(result.IsFailure);
        Assert.Contains("belong", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SelectWithValidOption_Succeeds()
    {
        var result = AttributeValueValidator.Validate(
            AttributeDataType.Select,
            isRequired: true,
            value: null,
            optionId: 2,
            validOptionIds: [1, 2]);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("maybe", false)]
    public void Validate_Boolean(string value, bool success)
    {
        var result = AttributeValueValidator.Validate(
            AttributeDataType.Boolean,
            isRequired: true,
            value,
            optionId: null,
            validOptionIds: []);

        Assert.Equal(success, result.IsSuccess);
    }

    [Fact]
    public void Validate_NumberRejectsDecimalText()
    {
        var result = AttributeValueValidator.Validate(
            AttributeDataType.Number,
            isRequired: true,
            value: "12.5",
            optionId: null,
            validOptionIds: []);

        Assert.True(result.IsFailure);
    }
}
