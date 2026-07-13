using System;
using System.Collections.Generic;
using System.Globalization;
using Formax.Application.Interfaces;
using Formax.Application.Services.Fixtures;

namespace Formax.Application.Services.Hero.VisualIdentity
{
    /// <summary>
    /// FORMAX HeroVisualIdentityEngine — takım kimliğinden Hero atmosferini üretir.
    /// TeamColorProvider'ın yerine geçer. Seed renk tablosu + türetme kuralları; renkler
    /// ölçülü katmanlara (Color/Opacity/Blur/Intensity) dönüşür. Backend CSS ÜRETMEZ.
    /// </summary>
    public sealed class HeroVisualIdentityEngine : IHeroVisualIdentityEngine
    {
        private readonly TeamIdentityResolver _teams;

        public HeroVisualIdentityEngine(TeamIdentityResolver teams) => _teams = teams;

        // Kanonik takım → (Primary, Secondary, Accent). Kilitli §3 eşlemeleri.
        private static readonly Dictionary<string, (string P, string S, string A)> Seed =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Manchester City"] = ("#6CABDD", "#1C2C5B", "#A7D8F5"),
                ["Liverpool"] = ("#C8102E", "#7A1420", "#F1B2B8"),
                ["Barcelona"] = ("#A50044", "#004D98", "#EDBB00"),
                ["Real Madrid"] = ("#E8ECF2", "#FEBE10", "#CCA43B"),
                ["Galatasaray"] = ("#FDB912", "#A32638", "#E30A17"),
                ["Fenerbahçe"] = ("#1B458F", "#FFED00", "#4A69A8"),
                ["Inter Milan"] = ("#0068A8", "#0A0A0A", "#6CA0DC"),
                ["Juventus"] = ("#101010", "#E8E8E8", "#9A9A9A"),
            };

        private static readonly (string P, string S, string A) Neutral = ("#3A4560", "#1A1F2E", "#6E7A99");

        public HeroVisualIdentity Build(string homeTeamName, string awayTeamName)
        {
            var h = Resolve(homeTeamName);
            var a = Resolve(awayTeamName);

            return new HeroVisualIdentity
            {
                Version = "1.0",
                TeamIdentity = new TeamIdentity
                {
                    Home = new TeamColorSet { Primary = L(h.P, 1, 0, 1), Secondary = L(h.S, 1, 0, .8), Accent = L(h.A, 1, 0, .7) },
                    Away = new TeamColorSet { Primary = L(a.P, 1, 0, 1), Secondary = L(a.S, 1, 0, .8), Accent = L(a.A, 1, 0, .7) },
                },
                StadiumIdentity = new StadiumIdentity
                {
                    Background = new VisualGradient
                    {
                        Angle = 180,
                        Stops =
                        {
                            new VisualStop { Color = "#0C1526", Position = 0 },
                            new VisualStop { Color = Darken(Blend(h.S, a.S, .5), .35), Position = 50 },
                            new VisualStop { Color = "#07070C", Position = 100 },
                        },
                    },
                    TopSpotlight = L(Lighten(Blend(h.P, a.P, .5), .35), .22, 24, .8),
                    LeftBeam = L(h.P, .16, 40, .7),
                    RightBeam = L(a.P, .14, 40, .65),
                    Lighting = L(Blend(h.P, a.P, .5), .10, 30, .5),
                    Vignette = L("#000000", .55, 0, 1),
                },
                PlayerIdentity = new PlayerIdentity
                {
                    HomeGlow = L(h.P, .40, 24, .9),
                    AwayGlow = L(a.P, .40, 24, .9),
                    HomeReflection = L(h.P, .25, 12, .6),
                    AwayReflection = L(a.P, .25, 12, .6),
                },
                CTAIdentity = new CTAIdentity
                {
                    Gradient = new VisualGradient
                    {
                        Angle = 135,
                        Stops =
                        {
                            new VisualStop { Color = h.P, Position = 0 },
                            new VisualStop { Color = a.P, Position = 100 },
                        },
                    },
                    Glow = L(Blend(h.P, a.P, .5), .50, 46, 1),
                },
                AmbientIdentity = new AmbientIdentity
                {
                    Ambient = L(Blend(h.P, a.P, .5), .06, 40, .4),
                    Fog = L(Blend(h.S, a.S, .5), .12, 30, .5),
                    Particle = L(Blend(h.A, a.A, .5), .50, 4, .7),
                },
                ThemeIdentity = new ThemeIdentity
                {
                    Gradient = new VisualGradient
                    {
                        Angle = 180,
                        Stops =
                        {
                            new VisualStop { Color = Blend("#14141C", h.P, .12), Position = 0 },
                            new VisualStop { Color = "#07070C", Position = 100 },
                        },
                    },
                    Dominant = L(h.P, 1, 0, 1),
                    Accent = L(Blend(h.A, a.A, .5), 1, 0, .7),
                    ContrastText = ContrastText(h.P),
                },
            };
        }

        private (string P, string S, string A) Resolve(string teamName)
        {
            var canonical = _teams.Resolve(teamName ?? "");
            return Seed.TryGetValue(canonical, out var c) ? c : Neutral;
        }

        // ── Katman kurucu ─────────────────────────────────────────────────────────
        private static VisualLayer L(string color, double opacity, double blur, double intensity) =>
            new() { Color = color, Opacity = opacity, Blur = blur, Intensity = intensity };

        // ── Renk matematiği (backend görsel verisi üretir — CSS değil) ────────────
        private static (int R, int G, int B) Parse(string hex)
        {
            hex = (hex ?? "").TrimStart('#');
            if (hex.Length != 6) return (58, 69, 96);
            return (
                int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
                int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
                int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber));
        }

        private static string Hex(int r, int g, int b) =>
            $"#{Clamp(r):X2}{Clamp(g):X2}{Clamp(b):X2}";

        private static int Clamp(int v) => Math.Clamp(v, 0, 255);

        private static string Blend(string c1, string c2, double t)
        {
            var (r1, g1, b1) = Parse(c1);
            var (r2, g2, b2) = Parse(c2);
            return Hex(
                (int)Math.Round(r1 + (r2 - r1) * t),
                (int)Math.Round(g1 + (g2 - g1) * t),
                (int)Math.Round(b1 + (b2 - b1) * t));
        }

        private static string Lighten(string c, double amt) => Blend(c, "#FFFFFF", amt);
        private static string Darken(string c, double amt) => Blend(c, "#000000", amt);

        private static double Luminance(string hex)
        {
            var (r, g, b) = Parse(hex);
            return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
        }

        private static string ContrastText(string bg) => Luminance(bg) > 0.55 ? "#0A0E16" : "#FFFFFF";
    }
}
