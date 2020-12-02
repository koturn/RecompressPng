namespace ArgumentParserNetStd
{
    /// <summary>
    /// One option item
    /// </summary>
    public class OptionItem
    {
        #region Properties
        /// <summary>
        /// Short option name
        /// </summary>
        public char ShortOptName { get; private set; }
        /// <summary>
        /// Long option name
        /// </summary>
        public string LongOptName { get; private set; }
        /// <summary>
        /// Description for this option
        /// </summary>
        public string Description { get; private set; }
        /// <summary>
        /// Name of meta variable for option parameter
        /// </summary>
        public string Metavar { get; private set; }
        /// <summary>
        /// Option type
        /// </summary>
        public OptionType OptType { get; private set; }
        /// <summary>
        /// Value of this option
        /// </summary>
        public string Value { get; set; }
        #endregion

        #region Ctors
        /// <summary>Create one option item</summary>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="longOptName">Long option name</param>
        /// <param name="optType">Option type</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for option parameter</param>
        /// <param name="defaultValue">Default value of this option</param>
        public OptionItem(char shortOptName, string longOptName, OptionType optType, string description = "", string metavar = "", string defaultValue = "")
        {
            ShortOptName = shortOptName;
            LongOptName = longOptName;
            OptType = optType;
            Description = description;
            Metavar = metavar;
            Value = defaultValue;
        }
        #endregion
    }


    /// <summary>
    /// This enumeration indicates whether an option requires an argument or not
    /// </summary>
    public enum OptionType
    {
        /// <summary>Mean that the option doesn't require an argument</summary>
        NoArgument,
        /// <summary>Mean that the option requires an argument</summary>
        RequiredArgument,
        /// <summary>
        /// Mean that the option may or may not requires an argument.
        /// In short option, this constant is equivalent to RequiredArgument.
        /// But in long option, you don't have to give argument the option.
        /// <c>--option</c>, <c>--option=arg</c>
        /// </summary>
        OptionalArgument
    }
}
