namespace F1Ticketing.Api;

// Globalni katalog valuta. Administracija uključuje ili isključuje valute
// bez brisanja istorijskih podataka o kartama.
public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = "EUR";
    public bool IsAllowed { get; set; } = true;
    public List<Ticket> Tickets { get; set; } = [];
}
