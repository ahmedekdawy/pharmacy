using Pharmacy.Application.Inventory;
using Pharmacy.Domain.Inventory;

namespace Pharmacy.UnitTests;

public class ShortageTests
{
    [Fact]
    public void Requires_a_product_or_an_item_name()
    {
        var validator = new CreateShortageCommandValidator();
        Assert.False(validator.Validate(new CreateShortageCommand(null, "  ")).IsValid);
        Assert.True(validator.Validate(new CreateShortageCommand(null, "Augmentin 1g")).IsValid);
        Assert.True(validator.Validate(new CreateShortageCommand(Guid.NewGuid(), null)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_must_be_positive(decimal quantity)
    {
        Assert.False(new CreateShortageCommandValidator().Validate(new CreateShortageCommand(null, "X", quantity)).IsValid);
    }

    [Theory]
    [InlineData("open", "Open")]
    [InlineData(" Ordered ", "Ordered")]
    [InlineData("RECEIVED", "Received")]
    [InlineData("done", null)]
    public void Status_is_normalized(string input, string? expected)
    {
        Assert.Equal(expected, ShortageStatuses.Normalize(input));
    }

    [Fact]
    public void Update_rejects_unknown_status()
    {
        var result = new UpdateShortageCommandValidator().Validate(new UpdateShortageCommand(Guid.NewGuid(), 1, "Done", null, null, null));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Only_open_and_ordered_are_active()
    {
        Assert.True(ShortageStatuses.IsActive(ShortageStatuses.Open));
        Assert.True(ShortageStatuses.IsActive(ShortageStatuses.Ordered));
        Assert.False(ShortageStatuses.IsActive(ShortageStatuses.Received));
        Assert.False(ShortageStatuses.IsActive(ShortageStatuses.Cancelled));
    }
}
