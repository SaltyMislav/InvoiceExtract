using Microsoft.Extensions.AI;

namespace InvoiceExtract.App;

public record ExtractionResult(Racun? Racun, IReadOnlyList<string> Errors, int Attempts, UsageDetails Usage)
{
    public bool IsValid => Racun is not null && Errors.Count == 0;
}

public class InvoiceExtractor(IChatClient chat, int maxAttempts = 3)
{
    private const string Instructions = """
        Extract the invoice data from the document the user sends.
        Copy every value exactly as it is printed on the invoice. Never guess or invent a value.
        """;

    public async Task<ExtractionResult> ExtractAsync(string invoiceText, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> messages =
        [
            new(ChatRole.System, Instructions),
            new(ChatRole.User, invoiceText),
        ];
        var usage = new UsageDetails();
        Racun? racun = null;
        IReadOnlyList<string> errors = [];

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await chat.GetResponseAsync<Racun>(messages, cancellationToken: cancellationToken);
            if (response.Usage is not null)
                usage.Add(response.Usage);

            if (response.TryGetResult(out var result))
            {
                racun = result;
                errors = RacunValidator.Validate(racun);
                if (errors.Count == 0)
                    return new ExtractionResult(racun, errors, attempt, usage);
            }
            else
            {
                errors = ["The response could not be parsed as the requested JSON schema."];
            }

            if (attempt == maxAttempts)
                break;

            // Show the model its own answer plus what was wrong with it, and ask for a corrected one.
            messages.AddMessages(response);
            messages.Add(new ChatMessage(ChatRole.User, $"""
                The extracted data failed validation:
                {string.Join('\n', errors.Select(e => $"- {e}"))}

                Re-check these values against the invoice and return the corrected data.
                If the invoice itself prints a value that fails a check, keep the printed value.
                """));
        }

        return new ExtractionResult(racun, errors, maxAttempts, usage);
    }
}
