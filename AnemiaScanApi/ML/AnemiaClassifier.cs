using System.Text.Json;

using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.LLM;

using Microsoft.Extensions.ML;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace AnemiaScanApi.ML;

/// <summary>Решение классификатора по одному снимку.</summary>
/// <param name="IsAnemic">Вероятность анемии не ниже порога из карточки модели.</param>
/// <param name="AnemiaProbability">Вероятность класса <c>Low_Hb</c>, 0..1. От порога не зависит.</param>
/// <param name="ClassifierVersion">Версия модели из её карточки — пишется в скан.</param>
public sealed record ClassifierDecision(bool IsAnemic, float AnemiaProbability, string ClassifierVersion);

/// <summary>
/// Порог решения и версия классификатора — из карточки рядом с файлом модели
/// (<c>LLM/anemia_v13_conjunctiva_full.card.json</c>).
///
/// Порог живёт в карточке, а не в коде: его выбирает AnemiaScanML по
/// кросс-валидации (самый высокий порог с чувствительностью ≥ 85%), и при
/// переобучении он меняется вместе с моделью.
/// </summary>
public sealed record AnemiaClassifierCard(string Version, string PositiveClass, float Threshold)
{
    public static AnemiaClassifierCard Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Нет карточки классификатора: {path}. Без неё неизвестен порог решения — " +
                "откатываться на argmax молча нельзя.", path);

        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;

        var card = new AnemiaClassifierCard(
            root.GetProperty("version").GetString() ?? throw Invalid("version"),
            root.GetProperty("labels").GetProperty("positive").GetString() ?? throw Invalid("labels.positive"),
            (float)root.GetProperty("decision").GetProperty("threshold").GetDouble());

        if (card.Threshold is <= 0 or >= 1)
            throw new InvalidOperationException($"Порог в карточке вне (0, 1): {card.Threshold}");

        return card;

        InvalidOperationException Invalid(string field) => new($"В карточке {path} нет поля {field}");
    }
}

/// <summary>
/// Превращает выход TF-классификатора в вердикт по порогу из карточки.
///
/// ПОЧЕМУ НЕ ARGMAX. ML.NET сам выбирает класс с наибольшей вероятностью — это
/// порог 0.5, оптимальный для accuracy. Скринингу нужна чувствительность: на
/// кросс-валидации argmax находил 72% анемичных, порог 0.15 — 88%, ценой
/// специфичности (78% → 59%). Пропущенный анемичный обходится дороже ложной
/// тревоги, которая стоит человеку анализа крови.
///
/// КАКОЙ ЭЛЕМЕНТ Score — АНЕМИЯ. Порядок классов в выходе модели задаёт ML.NET
/// при обучении, и гадать о нём нельзя. Индекс берётся из имён слотов колонки
/// Score, которые хранит сама модель, — один раз, при первом обращении.
/// </summary>
public sealed class AnemiaClassifier(
    PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput> pool,
    AnemiaClassifierCard card)
{
    private readonly Lazy<int> _anemicIndex = new(() => ResolveAnemicIndex(pool, card.PositiveClass));

    public AnemiaClassifierCard Card => card;

    public ClassifierDecision Decide(AnemiaPredictionOutput output)
    {
        var scores = output.Score ?? throw new InvalidOperationException("Классификатор не вернул Score");
        var index = _anemicIndex.Value;

        if (index >= scores.Length)
            throw new InvalidOperationException($"В Score {scores.Length} элементов, ожидался индекс {index}");

        var probability = scores[index];
        return new ClassifierDecision(probability >= card.Threshold, probability, card.Version);
    }

    /// <summary>Индекс положительного класса — по именам слотов Score в схеме самой модели.</summary>
    public static int ResolveAnemicIndex(
        PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput> pool, string positiveClass)
    {
        var model = pool.GetModel(ModelName.SasModel);
        var inputSchema = new MLContext().Data.LoadFromEnumerable(Array.Empty<AnemiaInput>()).Schema;
        var scoreColumn = model.GetOutputSchema(inputSchema)["Score"];

        VBuffer<ReadOnlyMemory<char>> slotNames = default;
        scoreColumn.GetSlotNames(ref slotNames);

        var names = slotNames.DenseValues().Select(n => n.ToString()).ToList();
        var index = names.IndexOf(positiveClass);

        return index >= 0
            ? index
            : throw new InvalidOperationException(
                $"Класса {positiveClass} нет среди выходов модели: [{string.Join(", ", names)}]");
    }
}
