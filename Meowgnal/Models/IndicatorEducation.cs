using System.Collections.Generic;

namespace Meowgnal.Models;

/// <summary>
/// Educational content for a single technical indicator.
/// Written in plain, friendly English — like explaining to a non-trader friend.
/// </summary>
public sealed class IndicatorEducation
{
    /// <summary>Must match exactly one IndicatorInfo.Type in the registry.</summary>
    public string IndicatorId { get; set; } = "";

    /// <summary>One simple paragraph: what this indicator does, no jargon.</summary>
    public string WhatIsIt { get; set; } = "";

    /// <summary>Which market conditions (trending / ranging / volatile) suit it best.</summary>
    public string WhenToUse { get; set; } = "";

    /// <summary>2–3 other indicators that pair well with this one, each with a short reason.</summary>
    public List<string> BestPairedWith { get; set; } = new();

    /// <summary>2–3 common mistakes beginners make with this indicator.</summary>
    public List<string> CommonTraps { get; set; } = new();

    /// <summary>The recommended default parameters and a short "why".</summary>
    public string RecommendedDefaultParams { get; set; } = "";
}