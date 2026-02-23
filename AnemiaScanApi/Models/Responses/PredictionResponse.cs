namespace AnemiaScanApi.Models.Responses;

public record PredictionResponse(string Prediction, float Score, float Confidence);