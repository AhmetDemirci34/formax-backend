using System.Collections.Generic;

namespace Formax.Application.Services.Hero.VisualIdentity
{
    /// <summary>
    /// Tek görsel katman — düz hex değil, ÖLÇÜLÜ birim. Backend CSS ÜRETMEZ; yalnız
    /// yapılandırılmış değer verir. Frontend bunları CSS değişkenine yazıp render eder.
    /// </summary>
    public sealed class VisualLayer
    {
        public string Color { get; set; } = "";   // hex, ör. "#6CABDD"
        public double Opacity { get; set; }         // 0–1
        public double Blur { get; set; }            // px
        public double Intensity { get; set; }       // 0–1 (glow/parıltı çarpanı)
    }

    /// <summary>Gradient durağı: renk + konum (0–100).</summary>
    public sealed class VisualStop
    {
        public string Color { get; set; } = "";
        public double Position { get; set; }
    }

    /// <summary>
    /// Gradient — yalnız Angle + Stops (backend CSS üretmez). Frontend gradient'i
    /// bu yapıdan kendi render katmanında oluşturur.
    /// </summary>
    public sealed class VisualGradient
    {
        public int Angle { get; set; }
        public List<VisualStop> Stops { get; set; } = new();
    }
}
