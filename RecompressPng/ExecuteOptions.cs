using System;


namespace RecompressPng
{
    /// <summary>
    /// Option values class for execution.
    /// </summary>
    public class ExecuteOptions : ICloneable
    {
        /// <summary>
        /// Default value for <see cref="NumberOfThreads"/>.
        /// </summary>
        public const int DefaultNumberOfThreads = -1;

        /// <summary>
        /// Number of threads for re-compressing. -1 means unlimited.
        /// </summary>
        public int NumberOfThreads { get; set; }
        /// <summary>
        /// Overwrite original files.
        /// </summary>
        public bool IsOverwrite { get; set; }
        /// <summary>
        /// Do the replacement even if the size of the recompressed data is larger than the size of the original data.
        /// </summary>
        public bool IsReplaceForce { get; set; }
        /// <summary>
        /// Keep timestamp of original file.
        /// </summary>
        public bool IsKeepTimestamp { get; set; }
        /// <summary>
        /// Size of each data part of IDAT chunk.
        /// </summary>
        public int IdatSize { get; set; }
        /// <summary>
        /// <para>Format string of <see cref="System.DateTime"/> for value of Creation Time of tEXt chunk.</para>
        /// <para><c>null</c> or empty string means don't add Creation Time.</para>
        /// </summary>
        public string TextCreationTimeFormat { get; set; }
        /// <summary>
        /// Whether add tIME chunk or not.
        /// </summary>
        public bool IsAddTimeChunk { get; set; }
        /// <summary>
        /// Don't save any files, just see the console output.
        /// </summary>
        public bool IsDryRun { get; set; }
        /// <summary>
        /// Count target PNG files and exit this program.
        /// </summary>
        public bool IsCountOnly { get; set; }
        /// <summary>
        /// Allow to output to stdout from zopflipng.dll.
        /// </summary>
        public bool Verbose { get; set; }
        /// <summary>
        /// Compare two images which is original image and re-compressed image.
        /// </summary>
        public bool IsVerifyImage { get; set; }
        /// <summary>
        /// True if <see cref="IdatSize"/> is positive or <see cref="TextCreationTimeFormat"/> is not null or empty string
        /// or <see cref="IsAddTimeChunk"/> is <c>true</c>.
        /// </summary>
        public bool IsModifyPng
        {
            get { return IdatSize > 0 || !string.IsNullOrEmpty(TextCreationTimeFormat) || IsAddTimeChunk; }
        }
        /// <summary>
        /// True if <see cref="IsOverwrite"/> is false and <see cref="IsDryRun"/> is false.
        /// </summary>
        public bool IsCreateNewFile
        {
            get { return !IsOverwrite && !IsDryRun; }
        }

        /// <summary>
        /// Initialize all members.
        /// </summary>
        /// <param name="numberOfThreads">Number of threads for re-compressing. -1 means unlimited.</param>
        /// <param name="isOverwrite">Overwrite original files.</param>
        /// <param name="isReplaceForce">Do the replacement even if the size of the recompressed data is larger than the size of the original data.</param>
        /// <param name="isKeepTimestamp">Keep timestamp of original file.</param>
        /// <param name="idatSize">Size of each data part of IDAT chunk.</param>
        /// <param name="textCreationTimeFormat">Format string of <see cref="System.DateTime"/> for value of Creation Time of tEXt chunk.</param>
        /// <param name="isAddTimeChunk">Whether add tIME chunk or not.</param>
        /// <param name="isDryRun">Don't save any files, just see the console output.</param>
        /// <param name="isCountOnly">Count target PNG files and exit this program.</param>
        /// <param name="verbose">Allow to output to stdout from zopflipng.dll.</param>
        /// <param name="isVerifyImage">Compare two images which is original image and re-compressed image.</param>
        public ExecuteOptions(
            int numberOfThreads = DefaultNumberOfThreads,
            bool isOverwrite = false,
            bool isReplaceForce = false,
            bool isKeepTimestamp = true,
            int idatSize = -1,
            string textCreationTimeFormat = null,
            bool isAddTimeChunk = false,
            bool isDryRun = false,
            bool isCountOnly = false,
            bool verbose = false,
            bool isVerifyImage = true)
        {
            NumberOfThreads = numberOfThreads;
            IsOverwrite = isOverwrite;
            IsReplaceForce = isReplaceForce;
            IsKeepTimestamp = isKeepTimestamp;
            IdatSize = idatSize;
            TextCreationTimeFormat = textCreationTimeFormat;
            IsAddTimeChunk = isAddTimeChunk;
            IsDryRun = isDryRun;
            IsCountOnly = isCountOnly;
            Verbose = verbose;
            IsVerifyImage = isVerifyImage;
        }

        /// <summary>
        /// Clone this instance.
        /// </summary>
        /// <returns>Cloned instance.</returns>
        public object Clone()
        {
            return new ExecuteOptions(
                NumberOfThreads,
                IsOverwrite,
                IsReplaceForce,
                IsKeepTimestamp,
                IdatSize,
                TextCreationTimeFormat,
                IsAddTimeChunk,
                IsDryRun,
                IsCountOnly,
                Verbose,
                IsVerifyImage);
        }
    }
}
