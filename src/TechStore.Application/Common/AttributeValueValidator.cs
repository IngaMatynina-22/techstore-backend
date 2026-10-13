using TechStore.Domain.Enums;

namespace TechStore.Application.Common;

public static class AttributeValueValidator
{
    public static Result Validate(
        AttributeDataType dataType,
        bool isRequired,
        string? value,
        int? optionId,
        IReadOnlyCollection<int> validOptionIds)
    {
        var hasValue = !string.IsNullOrWhiteSpace(value);
        var hasOption = optionId.HasValue;

        if (isRequired && !hasValue && !hasOption)
        {
            return Result.Failure("Attribute value is required.");
        }

        if (!hasValue && !hasOption)
        {
            return Result.Success();
        }

        return dataType switch
        {
            AttributeDataType.Select => ValidateSelect(optionId, validOptionIds),
            AttributeDataType.Boolean => ValidateBoolean(value, optionId),
            AttributeDataType.Number => ValidateNumber(value, optionId, integerOnly: true),
            AttributeDataType.Decimal => ValidateNumber(value, optionId, integerOnly: false),
            AttributeDataType.Text => ValidateText(value, optionId),
            _ => Result.Failure("Unsupported attribute data type.")
        };
    }

    private static Result ValidateSelect(int? optionId, IReadOnlyCollection<int> validOptionIds)
    {
        if (!optionId.HasValue)
        {
            return Result.Failure("Select attributes require an option.");
        }

        if (!validOptionIds.Contains(optionId.Value))
        {
            return Result.Failure("Selected option does not belong to this attribute definition.");
        }

        return Result.Success();
    }

    private static Result ValidateBoolean(string? value, int? optionId)
    {
        if (optionId.HasValue)
        {
            return Result.Failure("Boolean attributes must use a text/number value, not an option.");
        }

        if (!bool.TryParse(value, out _))
        {
            return Result.Failure("Boolean attribute value must be true or false.");
        }

        return Result.Success();
    }

    private static Result ValidateNumber(string? value, int? optionId, bool integerOnly)
    {
        if (optionId.HasValue)
        {
            return Result.Failure("Numeric attributes must not use an option id.");
        }

        if (integerOnly)
        {
            return int.TryParse(value, out _)
                ? Result.Success()
                : Result.Failure("Number attribute value must be an integer.");
        }

        return decimal.TryParse(value, out _)
            ? Result.Success()
            : Result.Failure("Decimal attribute value must be a number.");
    }

    private static Result ValidateText(string? value, int? optionId)
    {
        if (optionId.HasValue)
        {
            return Result.Failure("Text attributes must not use an option id.");
        }

        return string.IsNullOrWhiteSpace(value)
            ? Result.Failure("Text attribute value cannot be empty.")
            : Result.Success();
    }
}
