using System.ComponentModel;

namespace InvoiceExtract.App;

[Description("Data extracted from a Croatian invoice (račun). All amounts are in EUR.")]
public record Racun(
    [property: Description("Name of the company or person that issued the invoice")] string Izdavatelj,
    [property: Description("Issuer's OIB (Croatian personal identification number), 11 digits")] string Oib,
    [property: Description("Invoice issue date")] DateOnly Datum,
    [property: Description("Invoice line items, in the order they appear")] IReadOnlyList<Stavka> Stavke,
    [property: Description("Total PDV (VAT) amount")] decimal Pdv,
    [property: Description("Total amount payable, including PDV")] decimal Ukupno);

public record Stavka(
    [property: Description("Line item description")] string Opis,
    [property: Description("Quantity")] decimal Kolicina,
    [property: Description("Unit price without PDV")] decimal JedinicnaCijena,
    [property: Description("Line amount without PDV")] decimal Iznos);
