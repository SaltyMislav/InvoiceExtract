using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Anthropic;
using InvoiceExtract.App;
using Microsoft.Extensions.AI;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

const string model = "claude-sonnet-5-5";

var history = new List<ChatMessage>();
var cacheEnabled = true;
var claude = new CostTrackingChatClient(new AnthropicClient().AsIChatClient(model), model, PrintCallCost);
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

    if (entry == "/cache")
    {
        cacheEnabled = !cacheEnabled;
        Console.WriteLine($"Prompt caching je {(cacheEnabled ? "uključen" : "isključen")}.");
        continue;
    }

    history.Add(new ChatMessage(ChatRole.User, entry));
    PromptCache.MarkLastMessage(history, cacheEnabled);
    var updates = new List<ChatResponseUpdate>();

    await foreach (var update in chat.GetStreamingResponseAsync(history, options))
    {
        foreach (var call in update.Contents.OfType<FunctionCallContent>())
            Console.WriteLine($"[alat: {call.Name}({JsonSerializer.Serialize(call.Arguments)})]");

        Console.Write(update.Text);
        updates.Add(update);
    }

    history.AddMessages(updates.ToChatResponse());

    Console.WriteLine($"Ukupni trošak sesije: ${claude.TotalUsd:0.000000}");
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
    Console.WriteLine($"Trošak ekstrakcije: ${CallCost.From(result.Usage, model).Usd:0.000000}");
}

static void PrintCallCost(CallCost cost)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"\n[poziv] {cost}");
    Console.ResetColor();
}
