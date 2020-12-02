namespace ArgumentParserNetStd.Exceptions
{
    /// <summary>
    /// An exception caused in <see cref="ArgumentParser"/>
    /// <para>This exception is thrown when the omitted option name can not be resolved uniquely.</para>
    /// <para>For example, suppose that ArgumentParser can recognize long option <c>--foobarbuz</c> and <c>--foobazbar</c>.</para>
    /// <para>A command line argument <c>--foobar</c> can resolve <c>--foobarbuz</c> uniquely but <c>--foobar</c> can resolve
    /// <c>--foobarbuz</c> or <c>--foobazbar</c>.</para>
    /// </summary>
    public class ArgumentParserAmbiguousOptionException : ArgumentParserException
    {
        #region Ctors
        /// <summary>
        /// Create an exeption with an empty message
        /// </summary>
        public ArgumentParserAmbiguousOptionException()
        {
        }

        /// <summary>
        /// Create an exception with a long option name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        public ArgumentParserAmbiguousOptionException(string longOptName)
            : base("Ambiguous long option", longOptName)
        {
        }
        #endregion
    }
}
