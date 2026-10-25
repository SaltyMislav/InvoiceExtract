using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Anthropic;
using InvoiceExtract.App;
using Microsoft.Extensions.AI;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var history = new List<ChatMessage>();
IChatClient claude = new AnthropicClient().AsIChatClient("claude-sonnet-5-5");
var extractor = new InvoiceExtractor(claude);
IChatClient chat = claude
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

    if (entry.StartsWith("/racun "))
    {
        await ExtractInvoice(entry["/racun ".Length..].Trim().Trim('"'));
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

async Task ExtractInvoice(string path)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"Datoteka '{path}' ne postoji.");
        return;
    }

    var result = await extractor.ExtractAsync(await File.ReadAllTextAsync(path));

    Console.WriteLine(JsonSerializer.Serialize(result.Racun, new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    }));
    Console.WriteLine(result.IsValid
        ? $"Račun je valjan (pokušaja: {result.Attempts})."
        : $"Račun nije valjan nakon {result.Attempts} pokušaja:\n{string.Join('\n', result.Errors.Select(e => $"  - {e}"))}");
    Console.WriteLine($"Input token: {result.Usage.InputTokenCount}, Output token: {result.Usage.OutputTokenCount}");
}

