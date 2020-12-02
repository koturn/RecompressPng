namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// An exception caused in <see cref="ArgumentParser"/>
    /// <para>This exception is thrown when get value from an argument-required option and the value is empty.</para>
    /// </summary>
    public class ArgumentParserValueEmptyException : ArgumentParserException
    {
        #region Ctors
        /// <summary>
        /// Create an exeption with an empty message
        /// </summary>
        public ArgumentParserValueEmptyException()
        {
        }

        /// <summary>
        /// Create an exception with a short option name
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        public ArgumentParserValueEmptyException(char shortOptName)
            : base("Short option value is empty", shortOptName)
        {
        }

        /// <summary>
        /// Create an exception with a long option name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        public ArgumentParserValueEmptyException(string longOptName)
            : base("Long option value is empty", longOptName)
        {
        }
        #endregion
    }
}
