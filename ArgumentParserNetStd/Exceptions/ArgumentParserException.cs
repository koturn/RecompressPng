using System;


namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// An exception caused in <see cref="ArgumentParser"/>
    /// </summary>
    public class ArgumentParserException : Exception
    {
        #region Ctors
        /// <summary>
        /// Create an exeption
        /// </summary>
        public ArgumentParserException()
        {
        }

        /// <summary>
        /// Create an exception with a spcecified message
        /// </summary>
        /// <param name="message">Exception message</param>
        public ArgumentParserException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Create an exception with a specified message and a source exception
        /// </summary>
        /// <param name="message">Exception message</param>
        /// <param name="inner">Source exception</param>
        public ArgumentParserException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Create an exception with a message with short option
        /// </summary>
        /// <param name="message">Exception message</param>
        public ArgumentParserException(string message, char shortOptName)
            : base(message + ": -" + shortOptName)
        {
        }

        /// <summary>
        /// Create an exception with a message with long option
        /// </summary>
        /// <param name="message">Exception message</param>
        public ArgumentParserException(string message, string longOptName)
            : base(message + ": --" + longOptName)
        {
        }
        #endregion
    }
}
