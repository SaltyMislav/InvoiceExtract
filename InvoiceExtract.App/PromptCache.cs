using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;

namespace InvoiceExtract.App;

public static class PromptCache
{
    // Keeps a single cache breakpoint at the end of the conversation. Each request writes the
    // whole conversation to the cache, and the next one reads it back and pays full price only
    // for what was added since. Moving the marker doesn't invalidate the cache, which matches on content.
    public static void MarkLastMessage(IList<ChatMessage> messages, bool enabled)
    {
        foreach (var content in messages.SelectMany(m => m.Contents))
            content.WithCacheControl((CacheControlEphemeral?)null);

        if (enabled && messages.Count > 0 && messages[^1].Contents.Count > 0)
            messages[^1].Contents[^1].WithCacheControl(new CacheControlEphemeral());
    }
}
