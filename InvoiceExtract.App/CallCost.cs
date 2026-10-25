using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace InvoiceExtract.App;

// USD per million tokens, cache writes at the 5-minute TTL rate.
// Haiku 5.5 prices apply to prompts up to 100K tokens.
public record ModelPricing(decimal Input, decimal Output, decimal CacheWrite, decimal CacheRead)
{
    public static readonly IReadOnlyDictionary<string, ModelPricing> ByModel = new Dictionary<string, ModelPricing>
    {
        ["claude-haiku-5-5"] = new(0.10m, 0.50m, 0.125m, 0.01m),
        ["claude-sonnet-5-5"] = new(2.00m, 10.00m, 2.50m, 0.20m),
        ["claude-opus-5-5"] = new(4.00m, 20.00m, 5.00m, 0.20m),
    };
}

public record CallCost(long UncachedInput, long CacheWrite, long CacheRead, long Output, decimal Usd)
{
    public static CallCost From(UsageDetails usage, string model)
    {
        var pricing = ModelPricing.ByModel[model];

        // The Anthropic adapter reports InputTokenCount as uncached + cache write + cache read.
        var read = usage.CachedInputTokenCount ?? 0;
        var write = usage.AdditionalCounts?.TryGetValue(nameof(Usage.CacheCreationInputTokens), out var w) == true ? w : 0;
        var uncached = (usage.InputTokenCount ?? 0) - read - write;
        var output = usage.OutputTokenCount ?? 0;

        var usd = (uncached * pricing.Input + write * pricing.CacheWrite + read * pricing.CacheRead + output * pricing.Output) / 1_000_000m;
        return new CallCost(uncached, write, read, output, usd);
    }

    public override string ToString() =>
        $"Input token: {UncachedInput}, cache write: {CacheWrite}, cache read: {CacheRead}, Output token: {Output}, trošak: ${Usd:0.000000}";
}
