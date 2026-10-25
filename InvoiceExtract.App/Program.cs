using System.Text;
using System.Text.Json;
using Anthropic;
using InvoiceExtract.App;
using Microsoft.Extensions.AI;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var history = new List<ChatMessage>();
IChatClient chat = new AnthropicClient().AsIChatClient("claude-sonnet-5-5")
    .AsBuilder()
    .UseFunctionInvocation(configure: c => c.IncludeDetailedErrors = true)
    .Build();

var options = new ChatOptions
{
    Tools =
    [
        AIFunctionFactory.Create(Tools.GetExchangeRate),
        AIFunctionFactory.Create(Tools.GetCurrentTime),
    ],
};
while (true)
{
    Console.Write(">");
    var entry = Console.ReadLine();

    if (entry is null) break;
    if (string.IsNullOrWhiteSpace(entry))
    {
        Console.WriteLine("Upis ne smije biti prazan! Probajte ponovo.");
        continue;
    }

    history.Add(new ChatMessage(ChatRole.User, entry));
    var updates = new List<ChatResponseUpdate>();

    await foreach (var update in chat.GetStreamingResponseAsync(history, options))
    {
        foreach (var call in update.Contents.OfType<FunctionCallContent>())
            Console.WriteLine($"[alat: {call.Name}({JsonSerializer.Serialize(call.Arguments)})]");

        Console.Write(update.Text);
        updates.Add(update);
    }
    Console.WriteLine();

    var response = updates.ToChatResponse();
    history.AddMessages(response);

    Console.WriteLine($"Input token: {response.Usage?.InputTokenCount}, Output token: {response.Usage?.OutputTokenCount}");
}

