namespace InvoiceExtract.App;

public static class RacunValidator
{
    private const decimal Tolerance = 0.01m;

    public static IReadOnlyList<string> Validate(Racun racun)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(racun.Izdavatelj))
            errors.Add("Izdavatelj is empty.");

        if (!IsValidOib(racun.Oib))
            errors.Add($"OIB '{racun.Oib}' is not 11 digits with a valid ISO 7064 MOD 11,10 check digit.");

        if (racun.Stavke.Count == 0)
            errors.Add("The invoice has no line items.");

        var stavkeSum = racun.Stavke.Sum(s => s.Iznos);
        if (Math.Abs(stavkeSum + racun.Pdv - racun.Ukupno) > Tolerance)
            errors.Add($"Line items ({stavkeSum:0.00}) + PDV ({racun.Pdv:0.00}) = {stavkeSum + racun.Pdv:0.00}, but Ukupno is {racun.Ukupno:0.00}.");

        return errors;
    }

    // ISO 7064 MOD 11,10: the last digit is a check digit computed from the first ten.
    public static bool IsValidOib(string oib)
    {
        if (oib.Length != 11 || !oib.All(char.IsAsciiDigit))
            return false;

        var a = 10;
        for (var i = 0; i < 10; i++)
        {
            a = (oib[i] - '0' + a) % 10;
            if (a == 0) a = 10;
            a = a * 2 % 11;
        }

        var check = 11 - a;
        if (check == 10) check = 0;
        return check == oib[10] - '0';
    }
}
