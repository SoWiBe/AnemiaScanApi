using System.Runtime.Serialization;

namespace AnemiaScanApi.Exceptions;

[Serializable]
public class SASException : Exception
{
    public int StatusCode { get; } = 500;
    public string ErrorCode { get; }
    public string? Details { get; }
    
    public SASException(string message) 
        : base(message)
    {
    }
    
    public SASException(string message, int statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }
    
    public SASException(string message, int statusCode, string errorCode, string? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }
    
    public SASException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
    
    public SASException(string message, int statusCode, string errorCode, string? details, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Details = details;
    }
    
    protected SASException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        StatusCode = info.GetInt32(nameof(StatusCode));
        ErrorCode = info.GetString(nameof(ErrorCode));
        Details = info.GetString(nameof(Details));
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(StatusCode), StatusCode);
        info.AddValue(nameof(ErrorCode), ErrorCode);
        info.AddValue(nameof(Details), Details);
    }
}