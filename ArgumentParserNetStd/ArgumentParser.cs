using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using ArgumentParserNetStd.Exceptions;


namespace ArgumentParserNetStd
{
    /// <summary>
    /// Argument parser class
    /// </summary>
    public class ArgumentParser
    {
        #region Static members
        /// <summary>
        /// A string which enable to convert to bool value, true
        /// </summary>
        private static readonly string StringTrue;
        /// <summary>
        /// A string which enable to convert to bool value, false
        /// </summary>
        private static readonly string StringFalse;
        /// <summary>
        /// String value converter dictionary
        /// </summary>
        private static readonly Dictionary<Type, object> DefaultConverterDict;
        #endregion

        #region static ctor
        /// <summary>
        /// Initialize static members
        /// </summary>
        static ArgumentParser()
        {
            StringTrue = true.ToString();
            StringFalse = false.ToString();
            DefaultConverterDict = new Dictionary<Type, object>()
            {
                {typeof(bool), (Func<string, bool>)bool.Parse},
                {typeof(sbyte), (Func<string, sbyte>)sbyte.Parse},
                {typeof(short), (Func<string, short>)short.Parse},
                {typeof(int), (Func<string, int>)int.Parse},
                {typeof(long), (Func<string, long>)long.Parse},
                {typeof(byte), (Func<string, byte>)byte.Parse},
                {typeof(ushort), (Func<string, ushort>)ushort.Parse},
                {typeof(uint), (Func<string, uint>)uint.Parse},
                {typeof(ulong), (Func<string, ulong>)ulong.Parse},
                {typeof(float), (Func<string, float>)float.Parse},
                {typeof(double), (Func<string, double>)double.Parse},
                {typeof(char), (Func<string, char>)char.Parse},
                {typeof(string), (Func<string, string>)(s => s)},
            };
        }
        #endregion

        #region Properties
        /// <summary>Name of this program</summary>
        public string ProgName { get; set; }
        /// <summary>Rest of arguments</summary>
        public List<string> Arguments { get; }
        /// <summary>Description for this program</summary>
        public string Description { get; set; }
        /// <summary>String indent which used in <c>showUsage()</c></summary>
        public string IndentString { get; set; }
        #endregion

        #region Members
        /// <summary>Option items</summary>
        private List<OptionItem> options;
        /// <summary>Dictionary for lookup option item with short name</summary>
        private Dictionary<char, OptionItem> shortOptDict;
        /// <summary>Dictionary for lookup option item with long name</summary>
        private Dictionary<string, OptionItem> longOptDict;
        #endregion

        #region Ctors
        /// <summary>
        /// Create argument parser with default program name
        /// </summary>
        public ArgumentParser() :
            this(Environment.GetCommandLineArgs()[0])
        { }

        /// <summary>
        /// Create argument parser with specified program name
        /// </summary>
        /// <param name="progName"></param>
        public ArgumentParser(string progName)
        {
            ProgName = progName;
            IndentString = "  ";
            Arguments = new List<string>();
            options = new List<OptionItem>();
            shortOptDict = new Dictionary<char, OptionItem>();
            longOptDict = new Dictionary<string, OptionItem>();
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Add one option which has both short option and long option
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="longOptName">Long option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="showUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add(char shortOptName, string longOptName, OptionType optType, string description = null, string metavar = null, string defaultValue = null)
        {
            var item = new OptionItem(shortOptName, longOptName, optType, description, metavar, defaultValue);
            options.Add(item);
            shortOptDict[shortOptName] = item;
            longOptDict[longOptName] = item;
        }

        /// <summary>
        /// Add one option which has short option only
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="showUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add(char shortOptName, OptionType optType, string description = null, string metavar = null, string defaultValue = null)
        {
            var item = new OptionItem(shortOptName, null, optType, description, metavar, defaultValue);
            options.Add(item);
            shortOptDict[shortOptName] = item;
        }

        /// <summary>
        /// Add one option which has long option only
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="showUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add(string longOptName, OptionType optType, string description = null, string metavar = null, string defaultValue = null)
        {
            var item = new OptionItem('\0', longOptName, optType, description, metavar, defaultValue);
            options.Add(item);
            longOptDict[longOptName] = item;
        }

        /// <summary>
        /// Add one option which has both short option and long option.
        /// It's possible to give default value in any type.
        /// </summary>
        /// <typeparam name="T">Default value type (Assume this type parameter infered from <c>defaultValue</c>)</typeparam>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="longOptName">Long option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="ShowUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add<T>(char shortOptName, string longOptName, OptionType optType, string description, string metavar, T defaultValue)
        {
            Add(shortOptName, longOptName, optType, description, metavar, defaultValue.ToString());
        }

        /// <summary>
        /// Add one option which has short option only.
        /// It's possible to give default value in any type.
        /// </summary>
        /// <typeparam name="T">Default value type (Assume this type parameter infered from <c>defaultValue</c>)</typeparam>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="ShowUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add<T>(char shortOptName, OptionType optType, string description, string metavar, T defaultValue)
        {
            Add(shortOptName, optType, description, metavar, defaultValue.ToString());
        }

        /// <summary>
        /// Add one option which has long option only.
        /// It's possible to give default value in any type.
        /// </summary>
        /// <typeparam name="T">Default value type (Assume this type parameter infered from <c>defaultValue</c>)</typeparam>
        /// <param name="longOptName">Long option name</param>
        /// <param name="optType">Option type which indicates whether this option require an argument or not</param>
        /// <param name="description">Description for this option</param>
        /// <param name="metavar">Name of meta variable for this option (This value is used in <see cref="showUsage()"/>)</param>
        /// <param name="defaultValue">Default value of this option</param>
        public void Add<T>(string longOptName, OptionType optType, string description, string metavar, T defaultValue)
        {
            Add(longOptName, optType, description, metavar, defaultValue.ToString());
        }

        /// <summary>
        /// Add one boolean option which has both short option and long option.
        /// This method is equivalent to <c>Add(char, string, OptionType.NoArgument, string)</c>
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="longOptName">Long option name</param>
        /// <param name="description">Description for this option</param>
        public void Add(char shortOptName, string longOptName, string description = null)
        {
            Add(shortOptName, longOptName, OptionType.NoArgument, description, null, StringFalse);
        }

        /// <summary>
        /// Add one boolean option which has short option only.
        /// This method is equivalent to <c>Add(char, OptionType.NoArgument, string)</c>
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="description">Description for this option</param>
        public void Add(char shortOptName, string description = null)
        {
            Add(shortOptName, OptionType.NoArgument, description, null, StringFalse);
        }

        /// <summary>
        /// Add one boolean option which has long option only.
        /// This method is equivalent to <c>Add(string, OptionType.NoArgument, string)</c>
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        /// <param name="description">Description for this option</param>
        public void Add(string longOptName, string description = null)
        {
            Add(longOptName, OptionType.NoArgument, description, null, StringFalse);
        }

        /// <summary>
        /// Generate default help option.
        /// </summary>
        public void AddHelp()
        {
            var item = new OptionItem('h', "help", OptionType.NoArgument, "Show help and exit this program", "", StringFalse);
            options.Add(item);
            shortOptDict['h'] = item;
            longOptDict["help"] = item;
        }

        /// <summary>
        /// Parse command-line arguments
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        public void Parse(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    if (args[i].Length == 2)
                    {
                        Arguments.AddRange(args.Skip(i));
                        return;
                    }
                    i = ParseLongOption(args, i);
                }
                else if (args[i].StartsWith("-") && args[i].Length > 1)
                {
                    i = ParseShortOption(args, i);
                }
                else
                {
                    Arguments.Add(args[i]);
                }
            }
        }

        /// <summary>
        /// Check whether option has value or not
        /// </summary>
        /// <param name="shortOptName">Short option name</param>
        /// <returns>True if an option has value, otherwise false</returns>
        public bool Exists(char shortOptName)
        {
            return shortOptDict[shortOptName].Value != null;
        }

        /// <summary>
        /// Check whether option has value or not
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        /// <returns>True if an option has value, otherwise false</returns>
        public bool Exists(string longOptName)
        {
            return longOptDict[longOptName].Value != null;
        }

        /// <summary>
        /// Get option value with short name
        /// </summary>
        /// <param name="shortOptName">Short name of option</param>
        /// <returns>Option value</returns>
        public string Get(char shortOptName)
        {
            return shortOptDict[shortOptName].Value;
        }

        /// <summary>
        /// Get option value with short name and convert the value using default premitive type converter
        /// </summary>
        /// <typeparam name="T">Converted type (Assume premitive type and string only)</typeparam>
        /// <param name="shortOptName">Short option name</param>
        /// <returns>Converted option value</returns>
        /// <exception cref="ArgumentParserValueEmptyException">Throw if option value is empty</exception>
        public T Get<T>(char shortOptName)
        {
            var value = shortOptDict[shortOptName].Value;
            if (value == null)
            {
                throw new ArgumentParserValueEmptyException(shortOptName);
            }
            return ((Func<string, T>)DefaultConverterDict[typeof(T)])(value);
        }

        /// <summary>
        /// Get option value with short name and convert the value using specified type converter
        /// </summary>
        /// <typeparam name="T">Converted type (This type parameter is infered from <c>convert</c>)</typeparam>
        /// <param name="shortOptName">Short option name</param>
        /// <param name="convert">String value converter</param>
        /// <returns>Converted option value</returns>
        public T Get<T>(char shortOptName, Func<string, T> convert)
        {
            return convert(shortOptDict[shortOptName].Value);
        }

        /// <summary>
        /// Get option value with long name
        /// </summary>
        /// <param name="longOptName">Long option name</param>
        /// <returns>Option value</returns>
        public string Get(string longOptName)
        {
            return longOptDict[longOptName].Value;
        }

        /// <summary>
        /// Get option value with long name and convert the value using default premitive type converter
        /// </summary>
        /// <typeparam name="T">Converted type (Assume premitive type and string only)</typeparam>
        /// <param name="longOptName">Long option name</param>
        /// <returns>Converted option value</returns>
        /// <exception cref="ArgumentParserValueEmptyException">Throw if option value is empty</exception>
        public T Get<T>(string longOptName)
        {
            var value = longOptDict[longOptName].Value;
            if (value == null)
            {
                throw new ArgumentParserValueEmptyException(longOptName);
            }
            return ((Func<string, T>)DefaultConverterDict[typeof(T)])(value);
        }

        /// <summary>
        /// Get option value with long name and convert the value using specified type converter
        /// </summary>
        /// <typeparam name="T">Converted type (This type parameter is infered from <c>convert</c>)</typeparam>
        /// <param name="longOptName">Long option name</param>
        /// <param name="convert">String value converter</param>
        /// <returns>Converted option value</returns>
        public T Get<T>(string longOptName, Func<string, T> convert)
        {
            return convert(longOptDict[longOptName].Value);
        }

        /// <summary>
        /// Show usage using <c>Console.Out</c>
        /// </summary>
        public void ShowUsage()
        {
            ShowUsage(Console.Out);
        }

        /// <summary>
        /// Show usage using specified <c>TextWriter</c>
        /// </summary>
        /// <param name="writer">TextWriter to output message</param>
        public void ShowUsage(TextWriter writer)
        {
            if (Description != null)
            {
                writer.WriteLine(Description + Environment.NewLine);
            }
            writer.WriteLine(
                "[Usage]" + Environment.NewLine
                + ProgName + " [Options ...] [Arguments ...]" + Environment.NewLine + Environment.NewLine
                + "[Options]");
            foreach (var item in options)
            {
                writer.Write(IndentString);
                if (item.LongOptName == null)
                {
                    ShowShortOptionDescription(writer, item);
                }
                else if (item.ShortOptName == '\0')
                {
                    ShowLongOptionDescription(writer, item);
                }
                else
                {
                    ShowShortOptionDescription(writer, item);
                    writer.Write(", ");
                    ShowLongOptionDescription(writer, item);
                }
                writer.WriteLine(Environment.NewLine + IndentString + IndentString + item.Description);
            }
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Parse one short option
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <param name="idx">Current index of parsing</param>
        /// <returns>Parse finished index (<c>idx</c> or <c>idx + 1</c>)</returns>
        /// <exception cref="ArgumentParserUnknownOptionException">Throw if unknown option is specified</exception>
        /// <exception cref="ArgumentParserMissingArgumentException">Throw if argument-required option value is not found</exception>
        private int ParseShortOption(string[] args, int idx)
        {
            var arg = args[idx];
            for (int i = 1; i < arg.Length; i++)
            {
                var shortOptName = arg[i];
                if (!shortOptDict.ContainsKey(shortOptName))
                {
                    throw new ArgumentParserUnknownOptionException(shortOptName);
                }
                var item = shortOptDict[shortOptName];
                if (item.OptType == OptionType.NoArgument)
                {
                    item.Value = StringTrue;
                }
                else if (i == arg.Length - 1)
                {
                    if (idx + 1 >= args.Length)
                    {
                        throw new ArgumentParserMissingArgumentException(shortOptName);
                    }
                    item.Value = args[idx + 1];
                    return idx + 1;
                }
                else
                {
                    item.Value = arg.Substring(i + 1);
                    return idx;
                }
            }
            return idx;
        }

        /// <summary>
        /// Parse one long option
        /// </summary>
        /// <param name="args">Command-line arguments</param>
        /// <param name="idx">Current index of parsing</param>
        /// <returns>Parse finished index (<c>idx</c> or <c>idx + 1</c>)</returns>
        /// <exception cref="ArgumentParserUnknownOptionException">Throw if unknown option is specified</exception>
        /// <exception cref="ArgumentParserMissingArgumentException">Throw if argument-required option value is not found</exception>
        /// <exception cref="ArgumentParserAmbiguousOptionException">Throw if unknown a command-line is now resolve to one long option uniquely</exception>
        /// <exception cref="ArgumentParserDoesNotTakeArgumentException">Throw if non argument-required option is given an argument</exception>
        private int ParseLongOption(string[] args, int idx)
        {
            string longOptName, value;
            SplitFirstPos(args[idx].Substring(2), '=', out longOptName, out value);
            var items = longOptDict.Where(pair => pair.Key.StartsWith(longOptName)).Select(pair => pair.Value).ToArray();
            if (items.Length == 0)
            {
                throw new ArgumentParserUnknownOptionException(longOptName);
            }
            else if (items.Length > 1)
            {
                throw new ArgumentParserAmbiguousOptionException(longOptName);
            }
            var item = items[0];
            switch (item.OptType)
            {
                case OptionType.NoArgument:
                    if (value != null)
                    {
                        throw new ArgumentParserDoesNotTakeArgumentException(longOptName, value);
                    }
                    item.Value = StringTrue;
                    return idx;
                case OptionType.OptionalArgument:
                    item.Value = (value == null ? StringTrue : value);
                    return idx;
                case OptionType.RequiredArgument:
                    if (value == null)
                    {
                        if (idx + 1 >= args.Length)
                        {
                            throw new ArgumentParserMissingArgumentException(longOptName);
                        }
                        item.Value = args[idx + 1];
                        return idx + 1;
                    }
                    else
                    {
                        item.Value = value;
                        return idx;
                    }
                default:
                    return -1;
            }
        }

        /// <summary>
        /// Split string at the first position of <c>ch</c>
        /// </summary>
        /// <param name="str">Target string</param>
        /// <param name="ch">Separator character</param>
        /// <param name="first">First part of separated string. If target string doesn't have a character <c>ch</c>, store <c>str</c> to this variable</param>
        /// <param name="second">Second part of separated string. If target string doesn't have a character <c>ch</c>, store <c>null</c> to this variable</param>
        private void SplitFirstPos(string str, char ch, out string first, out string second)
        {
            var pos = str.IndexOf(ch);
            if (pos == -1)
            {
                first = str;
                second = null;
            }
            else
            {
                first = str.Substring(0, pos);
                second = str.Substring(pos + 1);
            }
        }

        /// <summary>
        /// Show description of a short option
        /// </summary>
        /// <param name="writer"><c>TextWriter</c> instance to output</param>
        /// <param name="item">Option item of short option</param>
        private void ShowShortOptionDescription(TextWriter writer, OptionItem item)
        {
            writer.Write("-" + item.ShortOptName);
            if (item.OptType != OptionType.NoArgument)
            {
                writer.Write(" " + item.Metavar);
            }
        }

        /// <summary>
        /// Show description of a long option
        /// </summary>
        /// <param name="writer"><c>TextWriter</c> instance to output</param>
        /// <param name="item">Option item of long option</param>
        private void ShowLongOptionDescription(TextWriter writer, OptionItem item)
        {
            writer.Write("--" + item.LongOptName);
            switch (item.OptType)
            {
                case OptionType.OptionalArgument:
                    writer.Write("[=" + item.Metavar + "]");
                    break;
                case OptionType.RequiredArgument:
                    writer.Write("=" + item.Metavar);
                    break;
            }
        }
        #endregion
    }
}
