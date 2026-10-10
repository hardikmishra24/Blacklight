using FluentAssertions;
using TipShield.Core.Logging;
using Xunit;

namespace TipShield.Core.Tests.Logging;

public class LogRedactorTests
{
    [Fact]
    public void Redact_Should_MaskPhoneNumbers()
    {
        LogRedactor.Redact("Call 9876543210").Should().Be("Call [REDACTED]");
    }

    [Fact]
    public void Redact_Should_MaskApiKeys()
    {
        LogRedactor.Redact("Bearer abcdefghijklmnopqrstuvwxyz").Should().Be("Bearer [REDACTED_TOKEN]");
    }

    [Fact]
    public void Redact_Should_KeepNormalText()
    {
        LogRedactor.Redact("Safe message").Should().Be("Safe message");
    }
}

