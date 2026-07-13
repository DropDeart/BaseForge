namespace BaseForge.Core.CQRS;

/// <summary>
/// Sayfalama, sıralama ve arama parametrelerini taşıyan ortak "list" sorgu tabanı. Üretilen
/// <c>List{Entity}Query</c> sınıfları bundan türer; <c>[FromQuery]</c> ile querystring'den bağlanır.
/// </summary>
public abstract class PagedRequest
{
    private int _page = 1;
    private int _pageSize = 20;

    /// <summary>1 tabanlı sayfa numarası. 1'den küçük değerler 1'e sabitlenir.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Sayfa başına kayıt sayısı. 1-100 aralığına sabitlenir.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch { < 1 => 1, > 100 => 100, _ => value };
    }

    /// <summary>
    /// Dynamic LINQ sıralama ifadesi (örn. <c>"Name desc"</c>, <c>"CreatedAt asc, Name"</c>).
    /// Boşsa entity'nin varsayılan sıralaması (<c>CreatedAt desc</c>) kullanılır.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Serbest metin arama; üretilen handler entity'nin string/text alanlarında ILIKE ile arar.
    /// Boşsa arama uygulanmaz.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>Sayfaya göre atlanacak kayıt sayısı (0 tabanlı).</summary>
    public int Skip => (Page - 1) * PageSize;
}
