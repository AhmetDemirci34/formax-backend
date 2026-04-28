namespace Formax.API.Contracts.Live
{
    public sealed class UpsertLiveOynanmaRequest
    {
        /// <summary>Home / Away / Draw / None</summary>
        public string Side { get; set; } = "None";

        /// <summary>0-100</summary>
        public int Intensity { get; set; }

        /// <summary>0-100 (opsiyonel)</summary>
        public int OddsMove { get; set; } = 50;

        /// <summary>0-100 (opsiyonel)</summary>
        public int MediaTrend { get; set; } = 50;
    }
}
