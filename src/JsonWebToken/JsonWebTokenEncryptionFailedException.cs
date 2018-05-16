using System;

namespace JsonWebToken
{
    /// <summary>
    /// Represents a token exception when encryption failed.
    /// </summary>
    public class JsonWebTokenEncryptionFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="JsonWebTokenEncryptionFailedException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public JsonWebTokenEncryptionFailedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonWebTokenEncryptionFailedException"/> class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The <see cref="Exception"/> that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
        public JsonWebTokenEncryptionFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
