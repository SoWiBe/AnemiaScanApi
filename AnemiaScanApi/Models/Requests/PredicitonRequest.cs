using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Models.Requests
{
    public class PredictionRequest
    {
        [Required] public IFormFile ImageData { get; set; } = null!;
    }
}