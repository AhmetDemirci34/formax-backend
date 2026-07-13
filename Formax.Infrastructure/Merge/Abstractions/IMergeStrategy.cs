using System.Collections.Generic;

namespace Formax.Infrastructure.Merge.Abstractions;

/// <summary>
/// Aynı maça ait çoklu provider katkısını tek bir <typeparamref name="T"/>'ye birleştiren strateji.
///
/// Her model türü için ayrı strateji yazılır; <see cref="MergeEngine"/> uygun olanı DI'dan çözer.
/// Gerçek birleştirme kuralları (alan seçimi, eksik doldurma politikası) bu sözleşmeyi uygulayan
/// somut sınıflarda, sonraki fazda yazılacak.
/// </summary>
public interface IMergeStrategy<T>
{
    MergeResult<T> Merge(IReadOnlyList<MergeContribution<T>> contributions, MergeContext context);
}
