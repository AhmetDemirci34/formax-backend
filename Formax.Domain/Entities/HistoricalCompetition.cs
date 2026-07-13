namespace Formax.Domain.Entities
{
    /// <summary>
    /// Tarihsel veri kümesinin kanonik lig/turnuva kaydı (CSV Division kodundan çözülür).
    /// Canlı/GDP <see cref="Competition"/>'dan İZOLE — tarihsel domain kendi tablolarında yaşar,
    /// böylece 230K tarihsel maç canlı veriyi kirletmez. Division koduna göre tekilleştirilir.
    /// </summary>
    public class HistoricalCompetition
    {
        public int Id { get; set; }

        /// <summary>CSV ham division kodu (ör. "E0", "F1", "SP1"). Benzersiz.</summary>
        public string Division { get; set; } = string.Empty;

        /// <summary>Çözülmüş okunur ad (ör. "England Premier League").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Ülke (ISO/ad; resolver'dan).</summary>
        public string? Country { get; set; }

        public System.DateTime LastUpdatedUtc { get; set; }
    }
}
