namespace AnemiaScanApi.Settings;

/// <summary>
/// Настройки подготовки снимка к хранению (секция <c>ImageStorage</c>).
/// Важно: это параметры ХРАНЕНИЯ, а не инференса — модели получают
/// оригинальные байты до сжатия.
/// </summary>
public class ImageStorageSettings
{
    /// <summary>
    /// Максимальная сторона сохраняемого снимка, px. Снимок конъюнктивы с
    /// телефона приходит 2-3 МБ; в GridFS на Atlas это прямые деньги и упор
    /// в лимит тарифа (P0 №9 в docs/plans/MVP_PLAN.md). 1024 px хватает,
    /// чтобы позже переобучить модель на собранных снимках.
    /// </summary>
    public int MaxDimension { get; set; } = 1024;

    /// <summary>Качество JPEG при перекодировании, 1-100.</summary>
    public int JpegQuality { get; set; } = 85;
}
