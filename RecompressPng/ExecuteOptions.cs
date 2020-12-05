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
        /// Allow to output to stdout from zopflipng.dll.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Initialize all members.
        /// </summary>
        /// <param name="numberOfThreads">Number of threads for re-compressing. -1 means unlimited.</param>
        /// <param name="isOverwrite">Overwrite original files.</param>
        /// <param name="isReplaceForce">Do the replacement even if the size of the recompressed data is larger than the size of the original data.</param>
        /// <param name="verbose">Allow to output to stdout from zopflipng.dll.</param>
        public ExecuteOptions(
            int numberOfThreads = DefaultNumberOfThreads,
            bool isOverwrite = false,
            bool isReplaceForce = false,
            bool verbose = false)
        {
            NumberOfThreads = numberOfThreads;
            IsOverwrite = isOverwrite;
            IsReplaceForce = isReplaceForce;
            Verbose = verbose;
        }
    }
}
