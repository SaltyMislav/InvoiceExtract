using Anthropic.Models.Messages;
using InvoiceExtract.App;
using Microsoft.Extensions.AI;

namespace InvoiceExtract.Evals;

public class CallCostTests
{
    [Fact]
    public void From_WithoutCache_PricesInputAndOutput()
    {
        var usage = new UsageDetails { InputTokenCount = 1_000_000, OutputTokenCount = 100_000 };

        var cost = CallCost.From(usage, "claude-sonnet-5-5");

        Assert.Equal(1_000_000, cost.UncachedInput);
        Assert.Equal(2.00m + 1.00m, cost.Usd);
    }

    [Fact]
    public void From_SplitsInputIntoUncachedWriteAndRead()
    {
        // Adapter reports InputTokenCount = uncached + cache write + cache read.
        var usage = new UsageDetails
        {
            InputTokenCount = 1_000 + 2_000 + 7_000,
            CachedInputTokenCount = 7_000,
            AdditionalCounts = new() { [nameof(Usage.CacheCreationInputTokens)] = 2_000 },
            OutputTokenCount = 500,
        };

        var cost = CallCost.From(usage, "claude-sonnet-5-5");

        Assert.Equal((1_000, 2_000, 7_000, 500), (cost.UncachedInput, cost.CacheWrite, cost.CacheRead, cost.Output));
        // 1,000 × $2 + 2,000 × $2.50 + 7,000 × $0.20 + 500 × $10, per million tokens
        Assert.Equal(0.0134m, cost.Usd);
    }

    [Fact]
    public void From_UnknownModel_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => CallCost.From(new UsageDetails(), "gpt-4o"));
}
