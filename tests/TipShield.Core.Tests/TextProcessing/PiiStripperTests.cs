using FluentAssertions;
using TipShield.Core.TextProcessing;
using Xunit;

namespace TipShield.Core.Tests.TextProcessing;

public class PiiStripperTests
{
    [Theory]
    [InlineData("Call me at 9876543210 please.", "Call me at [REDACTED] please.")]
    [InlineData("Contact +91-9876543210 for more.", "Contact [REDACTED] for more.")]
    [InlineData("Email me at user@example.com", "Email me at [REDACTED]")]
    [InlineData("Send money to user@upi", "Send money to [REDACTED]")]
    [InlineData("Account number 12345678901", "Account number [REDACTED]")]
    [InlineData("Safe text with 123", "Safe text with 123")]
    public void StripPii_Should_ReplaceSensitiveData(string input, string expected)
    {
        PiiStripper.StripPii(input).Should().Be(expected);
    }
}

