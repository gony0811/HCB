using System;

namespace HCB.UI
{
    public class ErrorException : Exception
    {
        public string ErrorCode { get; }

        public ErrorException(string errorCode) : base($"Error Code: {errorCode}")
        {
            ErrorCode = errorCode;
        }

        public ErrorException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public ErrorException(string errorCode, string message, Exception innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}
