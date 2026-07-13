namespace Formax.Infrastructure.Normalize.Models;

/// <summary>
/// FORMAX ortak fikstür durum modeli. Provider'ların ham durum ifadeleri buraya normalize edilir.
/// Provider-bağımsızdır; hangi ham token'ın hangi duruma karşılık geldiği generic olarak çözülür.
/// </summary>
public enum FixtureStatus
{
    Unknown = 0,
    Scheduled = 1,
    Live = 2,
    Finished = 3,
    Postponed = 4,
    Cancelled = 5
}
