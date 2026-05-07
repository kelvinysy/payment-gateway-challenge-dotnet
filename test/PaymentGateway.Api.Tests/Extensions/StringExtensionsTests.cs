using PaymentGateway.Api.Extensions;

namespace PaymentGateway.Api.Tests.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("9q238yurq893h4gq89o34hyfr1234")]
    [InlineData("13572345")]
    [InlineData("+-/*")]
    public void ToCardNumberLastFour_ReturnsLastFour(string input)
    {
        // Arrange
        var expected = input.Substring(input.Length - 4, 4);
        
        // Act
        var result = input.ToCardNumberLastFour();
        
        // Assert
        Assert.Equal(expected, result);
    }
}