using InvoiceExtract.App;
using Microsoft.Extensions.AI;

namespace InvoiceExtract.Evals;

public class InvoiceExtractorTests
{
    private const string ValidJson = """
        {"Izdavatelj":"WEBKOD d.o.o.","Oib":"69502483115","Datum":"2026-10-14",
         "Stavke":[{"Opis":"Izrada web stranice","Kolicina":1,"JedinicnaCijena":934,"Iznos":934}],
         "Pdv":233.5,"Ukupno":1167.5}
        """;

    private static readonly string WrongTotalJson = ValidJson.Replace("1167.5", "1176.5");

    [Fact]
    public async Task ExtractAsync_ValidFirstResponse_SucceedsInOneAttempt()
    {
        var chat = new FakeChatClient(Response(ValidJson));

        var result = await new InvoiceExtractor(chat).ExtractAsync("invoice text");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Attempts);
    }

    [Fact]
    public async Task ExtractAsync_InvalidThenCorrected_RetriesWithErrors()
    {
        var chat = new FakeChatClient(Response(WrongTotalJson), Response(ValidJson));

        var result = await new InvoiceExtractor(chat).ExtractAsync("invoice text");

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Attempts);
        // The retry carries the model's previous answer and the validation errors.
        Assert.Contains("failed validation", chat.Requests[1][^1].Text);
    }

    [Fact]
    public async Task ExtractAsync_AlwaysInvalid_StopsAfterMaxAttempts()
    {
        var chat = new FakeChatClient(Response(WrongTotalJson), Response(WrongTotalJson), Response(WrongTotalJson));

        var result = await new InvoiceExtractor(chat, maxAttempts: 3).ExtractAsync("invoice text");

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Attempts);
        Assert.Contains("Ukupno", Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData("length")]
    [InlineData("content_filter")]
    public async Task ExtractAsync_ResponseEndedEarly_DoesNotRetry(string finishReason)
    {
        var chat = new FakeChatClient(Response("""{"Izdavatelj":"WEBKOD d.o.""", new ChatFinishReason(finishReason)));

        var result = await new InvoiceExtractor(chat).ExtractAsync("invoice text");

        Assert.False(result.IsValid);
        Assert.Equal(1, result.Attempts);
        Assert.Single(chat.Requests);
        Assert.Contains(finishReason, Assert.Single(result.Errors));
    }

    private static ChatResponse Response(string text, ChatFinishReason? finishReason = null) =>
        new(new ChatMessage(ChatRole.Assistant, text)) { FinishReason = finishReason ?? ChatFinishReason.Stop };

    private sealed class FakeChatClient(params ChatResponse[] responses) : IChatClient
    {
        private int _next;

        public List<List<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add([.. messages]);
            return Task.FromResult(responses[_next++]);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
