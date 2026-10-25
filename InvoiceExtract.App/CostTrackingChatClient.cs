using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace InvoiceExtract.App;

// Sits directly on top of the Anthropic client, so it sees every API call,
// including the extra round-trips that function invocation makes.
public sealed class CostTrackingChatClient(IChatClient inner, string model, Action<CallCost> onCall)
    : DelegatingChatClient(inner)
{
    public decimal TotalUsd { get; private set; }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Record(response.Usage);
        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        UsageDetails? usage = null;
        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            foreach (var content in update.Contents.OfType<UsageContent>())
                (usage ??= new UsageDetails()).Add(content.Details);

            yield return update;
        }

        Record(usage);
    }

    private void Record(UsageDetails? usage)
    {
        if (usage is null)
            return;

        var cost = CallCost.From(usage, model);
        TotalUsd += cost.Usd;
        onCall(cost);
    }
}
