using InvoiceExtract.App;

namespace InvoiceExtract.Evals;

public class RacunValidatorTests
{
    private static Racun ValidRacun() => new(
        "WEBKOD d.o.o.",
        "69502483115",
        new DateOnly(2026, 10, 14),
        [
            new Stavka("Izrada web stranice", 1, 800.00m, 800.00m),
            new Stavka("Hosting (12 mjeseci)", 12, 9.50m, 114.00m),
            new Stavka("Domena .hr", 1, 20.00m, 20.00m),
        ],
        233.50m,
        1167.50m);

    [Theory]
    [InlineData("69502483115")]
    [InlineData("45287051629")]
    public void IsValidOib_AcceptsCorrectCheckDigit(string oib) =>
        Assert.True(RacunValidator.IsValidOib(oib));

    [Theory]
    [InlineData("69502483116")] // wrong check digit
    [InlineData("6950248311")]  // too short
    [InlineData("695024831150")] // too long
    [InlineData("6950248311A")] // not a digit
    [InlineData("")]
    public void IsValidOib_RejectsInvalid(string oib) =>
        Assert.False(RacunValidator.IsValidOib(oib));

    [Fact]
    public void Validate_ConsistentRacun_HasNoErrors() =>
        Assert.Empty(RacunValidator.Validate(ValidRacun()));

    [Fact]
    public void Validate_TotalMismatch_ReportsError()
    {
        var racun = ValidRacun() with { Ukupno = 1176.50m };

        var error = Assert.Single(RacunValidator.Validate(racun));
        Assert.Contains("Ukupno", error);
    }

    [Fact]
    public void Validate_ToleratesOneCentRounding() =>
        Assert.Empty(RacunValidator.Validate(ValidRacun() with { Ukupno = 1167.51m }));

    [Fact]
    public void Validate_EmptyRacun_ReportsEveryProblem()
    {
        var racun = new Racun("", "123", new DateOnly(2026, 10, 14), [], 0m, 10m);

        Assert.Equal(4, RacunValidator.Validate(racun).Count);
    }
}
