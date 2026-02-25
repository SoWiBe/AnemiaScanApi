using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests
{
    public class PredictionRequest
    {
        [Required] public IFormFile ImageData { get; set; } = null!;
    }
}