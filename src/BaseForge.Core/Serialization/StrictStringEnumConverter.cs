using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaseForge.Core.Serialization;

/// <summary>
/// Enum'u JSON'da yalnızca değer ADIYLA kabul eden/yazan dönüştürücü. Standart
/// <see cref="JsonStringEnumConverter{TEnum}"/> sayıları da kabul eder; bu hem değer sırasına bağımlılığı geri
/// getirir hem de tanımsız bir sayıyı (örn. <c>99</c>) geçerliymiş gibi içeri alıp veritabanına <c>"99"</c>
/// olarak yazdırır. Burada sayılar reddedilir (400), yalnızca tanımlı adlar geçer.
/// </summary>
/// <typeparam name="TEnum">Enum tipi.</typeparam>
public sealed class StrictStringEnumConverter<TEnum> : JsonStringEnumConverter<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>Adlar olduğu gibi (isimlendirme politikası olmadan), sayısal değerler kapalı.</summary>
    public StrictStringEnumConverter()
        : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
