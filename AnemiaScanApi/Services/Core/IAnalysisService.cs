namespace AnemiaScanApi.Services.Core;

/// <summary>
/// Interface for ML analysis service operations.
/// </summary>
/// <typeparam name="T">Enum representing the type of condition to analyze.</typeparam>
/// <typeparam name="TType">Type of the condition enum.</typeparam>
public interface IAnalysisService<T, TType> where T : Enum where TType : struct
{
    /// <summary>
    /// Analyzes an image for a specific condition.
    /// </summary>
    /// <param name="image">Image data for analysis.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Prediction result for the specified condition.</returns>
    Task<T> AnalyzeAsync(byte[] image, CancellationToken cancellationToken = default);
}