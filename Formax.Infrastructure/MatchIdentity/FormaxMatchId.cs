using System;

namespace Formax.Infrastructure.MatchIdentity;

/// <summary>
/// FORMAX'ın provider-bağımsız maç kimliği.
/// Aynı maça ait farklı provider kimlikleri tek bir <see cref="FormaxMatchId"/> altında toplanır.
/// Değer nesnesidir (value semantics).
/// </summary>
public readonly record struct FormaxMatchId
{
    public string Value { get; }

    private FormaxMatchId(string value) => Value = value;

    /// <summary>Var olan bir kimlik değerini sarmalar.</summary>
    public static FormaxMatchId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FORMAX Match ID boş olamaz.", nameof(value));

        return new FormaxMatchId(value.Trim());
    }

    /// <summary>
    /// Eşleşmeyen bir referans için yeni, benzersiz (sentetik) kimlik üretir.
    /// Deterministik türetme (takım + başlama zamanı + lig üzerinden hash) sonraki fazda eklenecek.
    /// </summary>
    public static FormaxMatchId NewSynthetic() => new($"fx_{Guid.NewGuid():N}");

    public override string ToString() => Value;
}
