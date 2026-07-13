namespace Formax.Application.Services.Hero.VisualIdentity
{
    /// <summary>Bir takımın renk çekirdeği (Primary/Secondary/Accent — her biri ölçülü katman).</summary>
    public sealed class TeamColorSet
    {
        public VisualLayer Primary { get; set; } = new();
        public VisualLayer Secondary { get; set; } = new();
        public VisualLayer Accent { get; set; } = new();
    }

    /// <summary>Takım renk kimliği (ev + deplasman).</summary>
    public sealed class TeamIdentity
    {
        public TeamColorSet Home { get; set; } = new();
        public TeamColorSet Away { get; set; } = new();
    }

    /// <summary>Stadyum / arka plan ışık kimliği.</summary>
    public sealed class StadiumIdentity
    {
        public VisualGradient Background { get; set; } = new();
        public VisualLayer TopSpotlight { get; set; } = new();
        public VisualLayer LeftBeam { get; set; } = new();
        public VisualLayer RightBeam { get; set; } = new();
        public VisualLayer Lighting { get; set; } = new();
        public VisualLayer Vignette { get; set; } = new();
    }

    /// <summary>Oyuncu arkası glow + yansıma kimliği (takım renklerinden).</summary>
    public sealed class PlayerIdentity
    {
        public VisualLayer HomeGlow { get; set; } = new();
        public VisualLayer AwayGlow { get; set; } = new();
        public VisualLayer HomeReflection { get; set; } = new();
        public VisualLayer AwayReflection { get; set; } = new();
    }

    /// <summary>CTA buton kimliği (iki takım renginin birleşimi).</summary>
    public sealed class CTAIdentity
    {
        public VisualGradient Gradient { get; set; } = new();
        public VisualLayer Glow { get; set; } = new();
    }

    /// <summary>Atmosfer kimliği: ambient ışık + sis + partikül (takım renklerini taşır).</summary>
    public sealed class AmbientIdentity
    {
        public VisualLayer Ambient { get; set; } = new();
        public VisualLayer Fog { get; set; } = new();
        public VisualLayer Particle { get; set; } = new();
    }

    /// <summary>Genel tema kimliği (Mood YOK).</summary>
    public sealed class ThemeIdentity
    {
        public VisualGradient Gradient { get; set; } = new();
        public VisualLayer Dominant { get; set; } = new();
        public VisualLayer Accent { get; set; } = new();
        /// <summary>Metin okunabilirliği — backend hesaplar; frontend hesaplamaz.</summary>
        public string ContrastText { get; set; } = "#FFFFFF";
    }

    /// <summary>
    /// FORMAX HeroVisualIdentityEngine çıktısı — bir maçın TÜM görsel atmosferi.
    /// Backend CSS üretmez; yalnız yapılandırılmış görsel veri. Frontend render eder.
    /// </summary>
    public sealed class HeroVisualIdentity
    {
        public TeamIdentity TeamIdentity { get; set; } = new();
        public StadiumIdentity StadiumIdentity { get; set; } = new();
        public PlayerIdentity PlayerIdentity { get; set; } = new();
        public CTAIdentity CTAIdentity { get; set; } = new();
        public AmbientIdentity AmbientIdentity { get; set; } = new();
        public ThemeIdentity ThemeIdentity { get; set; } = new();

        /// <summary>İleride sürüm yönetimi için.</summary>
        public string Version { get; set; } = "1.0";
    }
}
