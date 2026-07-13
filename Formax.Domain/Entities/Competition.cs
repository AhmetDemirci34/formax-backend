namespace Formax.Domain.Entities
{
    /// <summary>
    /// Kanonik turnuva/lig kaydı. GDP fikstürlerinden ada göre tekilleştirilerek beslenir
    /// (Team ile aynı pattern: <see cref="Match"/> içinden çözülür). Sezon burada tutulmaz —
    /// sezon maça özgüdür, competition ise sezonlar-üstü tek kayıttır.
    /// </summary>
    public class Competition
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Country { get; set; }

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
