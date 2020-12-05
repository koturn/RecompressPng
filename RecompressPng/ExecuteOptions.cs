namespace RecompressPng
{
    /// <summary>
    /// Option values class for execution.
    /// </summary>
    public class ExecuteOptions
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
        /// Initialize all members.
        /// </summary>
        /// <param name="numberOfThreads">Number of threads for re-compressing. -1 means unlimited.</param>
        /// <param name="isOverwrite">Overwrite original files.</param>
        /// <param name="isReplaceForce">Do the replacement even if the size of the recompressed data is larger than the size of the original data.</param>
        /// <param name="isCountOnly">Count target PNG files and exit this program.</param>
        /// <param name="verbose">Allow to output to stdout from zopflipng.dll.</param>
        /// <param name="isVerifyImage">Compare two images which is original image and re-compressed image.</param>
        public ExecuteOptions(
            int numberOfThreads = DefaultNumberOfThreads,
            bool isOverwrite = false,
            bool isReplaceForce = false,
            bool isCountOnly = false,
            bool verbose = false,
            bool isVerifyImage = true)
        {
            NumberOfThreads = numberOfThreads;
            IsOverwrite = isOverwrite;
            IsReplaceForce = isReplaceForce;
            IsCountOnly = isCountOnly;
            Verbose = verbose;
            IsVerifyImage = isVerifyImage;
        }
    }
}
