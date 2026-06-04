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


    public class VisionException : ErrorException
    {
        public VisionException(string errorCode) : base(errorCode)
        {
        }

        public VisionException(string errorCode, string message) : base(errorCode, message)
        {
        }

        public VisionException(string errorCode, string message, Exception innerException) : base(errorCode, message, innerException)
        {
        }
    }

    public class PmacException : ErrorException
    {
        public PmacException(string errorCode) : base(errorCode)
        {
        }

        public PmacException(string errorCode, string message) : base(errorCode, message)
        {
        }

        public PmacException(string errorCode, string message, Exception innerException) : base(errorCode, message, innerException)
        {
        }
    }

    public class DBException : ErrorException
    {
        public DBException(string errorCode) : base(errorCode)
        {
        }

        public DBException(string errorCode, string message) : base(errorCode, message)
        {
        }

        public DBException(string errorCode, string message, Exception innerException) : base(errorCode, message, innerException)
        {
        }
    }


    public static class PmacErrorCode
    {
        public static string SERVO_OFF = "E0031"; 
        public static string RUNNING = "E0032";
        public static string IO_EXCEPTION = "E0033";
        public static string HEAD_VAC= "E0034";
    }

    public static class DBErrorCode
    {
        public static string NOT_FOUND = "D0001";
    }

    public static class VisionErrorCode
    {
        public static string DISCONNECTED = "V0001";    // 비전 연결 끊김
        public static string COMMUNICATION_ERROR= "V0002";    // 비전 연결 끊김
        public static string MEASUREMENT_FAIL = "V0003";    // 비전 측정 실패
        public static string AF_FAIL = "V0004";    // AF 실패
        
    }
}
