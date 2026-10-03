using System.Net;
using AnemiaScanApi.Common.Constants;
using AnemiaScanApi.Common.LLM;
using AnemiaScanApi.Common.Requests;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Exceptions;
using AnemiaScanApi.ML;
using Microsoft.Extensions.ML;

namespace AnemiaScanApi.Services;

public class PredictionService(
    PredictionEnginePool<AnemiaInput, AnemiaPredictionOutput> predictionEnginePool,
    AnemiaClassifier classifier,
    ILogger<PredictionService> logger) : BaseService<PredictionService>(logger), IPredictionService
{
    public async Task<ClassifierDecision> PredictAnemiaAsync(PredictionRequest request, CancellationToken cancellationToken)
    {
        if (request.ImageData is null) throw new SASException("Image data is null", (int)HttpStatusCode.BadRequest);
        var tempPath = Path.GetTempFileName();

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await request.ImageData.CopyToAsync(stream, cancellationToken);
            }
            
            await Task.Delay(50, cancellationToken);

            var input = new AnemiaInput { ImagePath = tempPath };
            var prediction = predictionEnginePool.Predict(ModelName.SasModel, input);

            if (prediction is null) throw new SASException(ExceptionMessage.PredictionFail);

            // Вердикт — по порогу из карточки модели, а не argmax ML.NET.
            return classifier.Decide(prediction);
        }
        catch (Exception ex)
        {
            throw new SASException(ex.Message, ex);
        }
        finally 
        {
            await SafeDeleteFileAsync(tempPath);
        }
    }

    private async Task SafeDeleteFileAsync(string filePath)
    {
        var maxRetries = 5;
        var delayMs = 100;
    
        for (var i = 0; i < maxRetries; i++)
        {
            try 
            { 
                if (File.Exists(filePath)) File.Delete(filePath);

                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs);
                delayMs *= 2;
            }
        }
        
        Logger.LogError($"Не удалось удалить временный файл: {filePath}");
    }
}