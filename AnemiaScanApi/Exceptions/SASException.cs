namespace AnemiaScanApi.Exceptions
{
    public class SASException : Exception
    {
        public SASException() {}
        public SASException(string? message) : base(message) {}
         public SASException(string? message, Exception? inner = null) : base(message) {}
    }
}