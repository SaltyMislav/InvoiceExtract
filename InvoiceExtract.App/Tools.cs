using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace InvoiceExtract.App;

public static class Tools
{
    private static readonly HttpClient Http = new() { BaseAddress = new Uri("https://api.hnb.hr/") };
    private static readonly CultureInfo Hr = CultureInfo.GetCultureInfo("hr-HR");

    [Description("Gets today's Croatian National Bank (HNB) middle exchange rate for EUR: how many units of the given currency 1 EUR buys.")]
    public static async Task<ExchangeRate> GetExchangeRate(
        [Description("ISO 4217 currency code other than EUR, e.g. USD, GBP, CHF")] string currency,
        CancellationToken cancellationToken)
    {
        var code = currency.Trim().ToUpperInvariant();
        using var response = await Http.GetAsync($"tecajn-eur/v3?valuta={Uri.EscapeDataString(code)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ArgumentException($"HNB does not publish an exchange rate for '{code}'.");

        var rates = await response.Content.ReadFromJsonAsync<HnbRate[]>(cancellationToken);
        var rate = rates?.FirstOrDefault()
            ?? throw new ArgumentException($"HNB returned no exchange rate for '{code}'.");

        return new ExchangeRate(
            rate.Currency,
            decimal.Parse(rate.MiddleRate, Hr),
            DateOnly.Parse(rate.Date, CultureInfo.InvariantCulture));
    }

    [Description("Gets the current local date and time, including the day of the week and UTC offset.")]
    public static string GetCurrentTime() =>
        DateTimeOffset.Now.ToString("dddd, yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
}

public record ExchangeRate(string Currency, decimal UnitsPerEur, DateOnly Date);

internal record HnbRate(
    [property: JsonPropertyName("valuta")] string Currency,
    [property: JsonPropertyName("srednji_tecaj")] string MiddleRate,
    [property: JsonPropertyName("datum_primjene")] string Date);
