namespace BaseForge.Core.CQRS;

/// <summary>Sayfalanmış bir liste sonucu: bu sayfadaki kayıtlar + toplam sayı + sayfa bilgisi.</summary>
public sealed class PagedResult<T>
{
    /// <summary>Bu sayfadaki kayıtlar.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Filtre/arama sonrası, sayfalamadan önceki toplam kayıt sayısı.</summary>
    public required int TotalCount { get; init; }

    /// <summary>1 tabanlı geçerli sayfa numarası.</summary>
    public required int Page { get; init; }

    /// <summary>Sayfa başına kayıt sayısı.</summary>
    public required int PageSize { get; init; }

    /// <summary>Toplam sayfa sayısı.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
