using TipShield.Gemma.Prompts;
using Xunit;

namespace TipShield.Gemma.Tests.Prompts;

public class ClassifierPromptTests
{
    [Fact]
    public void GetSystemInstruction_ContainsRequiredElements()
    {
        var instruction = ClassifierPrompt.GetSystemInstruction("en");
        Assert.Contains("OUTPUT SCHEMA:", instruction);
        Assert.Contains("IMPORTANT SAFETY RULES:", instruction);
        Assert.Contains("FEW-SHOT EXAMPLES:", instruction);
        Assert.Contains("explanation", instruction); // from schema
    }

    [Fact]
    public void GetSystemInstruction_SetsOutputLanguage()
    {
        var instructionEn = ClassifierPrompt.GetSystemInstruction("en");
        var instructionHi = ClassifierPrompt.GetSystemInstruction("hi");

        Assert.Contains("2-4 sentences in en explaining", instructionEn);
        Assert.Contains("2-4 sentences in hi explaining", instructionHi);
    }

    [Fact]
    public void BuildUserMessage_TextOnly()
    {
        var result = ClassifierPrompt.BuildUserMessage("Hello", null);
        Assert.Contains("<message>\nHello\n</message>", result);
        Assert.DoesNotContain("<image_text>", result);
    }

    [Fact]
    public void BuildUserMessage_ImageTextOnly()
    {
        var result = ClassifierPrompt.BuildUserMessage(null, "ImgText");
        Assert.Contains("<image_text>\nImgText\n</image_text>", result);
        Assert.DoesNotContain("<message>", result);
    }

    [Fact]
    public void BuildUserMessage_Both()
    {
        var result = ClassifierPrompt.BuildUserMessage("Hello", "ImgText");
        Assert.Contains("<message>\nHello\n</message>", result);
        Assert.Contains("<image_text>\nImgText\n</image_text>", result);
    }

    [Fact]
    public void BuildUserMessage_Neither()
    {
        var result = ClassifierPrompt.BuildUserMessage(null, null);
        Assert.Equal("<message>\n[empty message]\n</message>", result);
    }
}
