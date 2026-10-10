using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using TipShield.Core.Models;
using TipShield.Core.TextProcessing;

namespace TipShield.Core.Rules;

public interface IRuleEngine
{
    RuleEngineResult Evaluate(string text);
    RuleEngineResult Evaluate(string text, string? normalizedImageText);
}

public sealed class RuleEngineResult
{
    public required int Score { get; init; }
    public required List<RuleHit> Hits { get; init; }
}

public sealed class RuleEngine : IRuleEngine
{
    private sealed class CompiledRule
    {
        public required Rule Definition { get; init; }
        public required Regex Regex { get; init; }
    }

    private readonly List<CompiledRule> _compiledRules = new();

    public RuleEngine(string rulesJson)
    {
        var ruleSet = JsonSerializer.Deserialize<RuleSet>(rulesJson);
        if (ruleSet?.Rules == null) return;

        foreach (var rule in ruleSet.Rules)
        {
            try
            {
                var regex = new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                _compiledRules.Add(new CompiledRule { Definition = rule, Regex = regex });
            }
            catch
            {
                // Skip invalid rules
            }
        }
    }

    public RuleEngineResult Evaluate(string text)
    {
        return Evaluate(text, null);
    }

    public RuleEngineResult Evaluate(string text, string? normalizedImageText)
    {
        var combinedText = string.IsNullOrEmpty(text) ? "" : text;
        if (!string.IsNullOrWhiteSpace(normalizedImageText))
        {
            combinedText = combinedText + " " + normalizedImageText;
        }

        var normalized = TextNormalizer.NormalizeForPatternMatching(combinedText);
        var hits = new List<RuleHit>();
        int totalScore = 0;

        foreach (var compiled in _compiledRules)
        {
            try
            {
                var match = compiled.Regex.Match(normalized);
                if (match.Success)
                {
                    hits.Add(new RuleHit
                    {
                        RuleId = compiled.Definition.Id,
                        Description = compiled.Definition.Description,
                        Weight = compiled.Definition.Weight,
                        MatchedText = match.Value,
                        StartIndex = match.Index,
                        Length = match.Length
                    });
                    totalScore += compiled.Definition.Weight;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip timeout
            }
        }

        return new RuleEngineResult
        {
            Score = Math.Min(100, totalScore),
            Hits = hits
        };
    }
}

