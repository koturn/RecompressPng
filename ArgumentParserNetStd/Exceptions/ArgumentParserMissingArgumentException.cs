namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// An exception caused in <see cref="ArgumentParser"/>
    /// <para>This exception is thrown when detect an argument for argument-required option is not found.</para>
    /// </summary>
    public class ArgumentParserMissingArgumentException : ArgumentParserException
    {
        #region Ctors
        /// <summary>
        /// Create an exeption with an empty message
        /// </summary>
        public ArgumentParserMissingArgumentException()
        {
        }

        /// <summary>
        /// Create an exception with a short option name
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        public ArgumentParserMissingArgumentException(char shortOptName)
            : base("Missing argument of short option", shortOptName)
        {
        }

        /// <summary>
        /// Create an exception with a long option name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        public ArgumentParserMissingArgumentException(string longOptName)
            : base("Missing argument of long option", longOptName)
        {
        }
        #endregion
    }
}
