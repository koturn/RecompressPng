namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// This exception is throwed when an argument is given to a non-argument-required option.
    /// </summary>
    public class ArgumentParserDoesNotTakeArgumentException : ArgumentParserException
    {
        #region Ctors
        /// <summary>
        /// Create an exeption with an empty message
        /// </summary>
        public ArgumentParserDoesNotTakeArgumentException()
        {
        }

        /// <summary>
        /// Create an exception with a long option name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        public ArgumentParserDoesNotTakeArgumentException(string longOptName, string value)
            : base("An argument is given to non-argument required long option", longOptName + "=" + value)
        {
        }
        #endregion
    }
}
