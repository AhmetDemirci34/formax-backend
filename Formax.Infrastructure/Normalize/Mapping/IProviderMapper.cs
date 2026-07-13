using System.Collections.Generic;

namespace Formax.Infrastructure.Normalize.Mapping;

/// <summary>
/// Bir provider'ın ham çıktısını (JSON/payload) ortak HAM modele (<typeparamref name="TRaw"/>) dönüştüren sözleşme.
///
/// Provider-özgü tek yer BURASIDIR: her provider yalnızca kendi Mapper sınıfına sahip olur.
/// Normalize Engine hiçbir provider'ı tanımaz; yalnızca ortak HAM model ile çalışır.
/// <see cref="ProviderName"/>, doğru mapper'ı seçmek için kullanılır.
///
/// Bir payload birden fazla varlık (ör. maç dizisi) içerebileceğinden LİSTE döner; boş/geçersiz payload'da
/// boş liste döner (sahte veri üretilmez).
/// </summary>
public interface IProviderMapper<TRaw>
{
    /// <summary>Bu mapper'ın işlediği provider adı.</summary>
    string ProviderName { get; }

    /// <summary>Provider ham payload'ını ortak HAM model listesine dönüştürür.</summary>
    IReadOnlyList<TRaw> Map(object? payload);
}
