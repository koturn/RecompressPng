namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// An exception caused in <see cref="ArgumentParser"/>
    /// <para>This exception is thrown when detect unknown option.</para>
    /// </summary>
    public class ArgumentParserUnknownOptionException : ArgumentParserException
    {
        #region Ctors
        /// <summary>
        /// Create an exeption with an empty message
        /// </summary>
        public ArgumentParserUnknownOptionException()
        {
        }

        /// <summary>
        /// Create an exception with a short option name
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        public ArgumentParserUnknownOptionException(char shortOptName)
            : base("Unknown short option", shortOptName)
        {
        }

        /// <summary>
        /// Create an exception with a long option name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        public ArgumentParserUnknownOptionException(string longOptName)
            : base("Unknown long option", longOptName)
        {
        }
        #endregion
    }
}
