using DataSpecter.Core.Models;
using System.Threading.Tasks;

namespace DataSpecter.Core.Interfaces
{
    /// <summary>
    /// Service for fuzzy hashing (SSDEEP/CTPH) and file similarity comparison.
    /// </summary>
    public interface IFuzzyHashService
    {
        /// <summary>
        /// Computes the fuzzy hash (SSDEEP) of a file.
        /// </summary>
        /// <param name="dataSource">The binary data source.</param>
        /// <returns>The fuzzy hash string.</returns>
        Task<string> ComputeFuzzyHashAsync(BinaryDataSource dataSource);

        /// <summary>
        /// Compares two fuzzy hashes and returns a similarity score (0-100).
        /// </summary>
        /// <param name="hash1">First fuzzy hash.</param>
        /// <param name="hash2">Second fuzzy hash.</param>
        /// <returns>Similarity score from 0 (completely different) to 100 (identical).</returns>
        int CompareFuzzyHashes(string hash1, string hash2);

        /// <summary>
        /// Computes byte-level similarity between two files using LCP arrays.
        /// </summary>
        /// <param name="dataSource1">First file data source.</param>
        /// <param name="dataSource2">Second file data source.</param>
        /// <param name="suffixArray1">Suffix array of first file.</param>
        /// <param name="lcpArray1">LCP array of first file.</param>
        /// <returns>Similarity metrics including percentage and common substrings.</returns>
        Task<SimilarityResult> ComputeByteLevelSimilarityAsync(
            BinaryDataSource dataSource1,
            BinaryDataSource dataSource2,
            int[]? suffixArray1 = null,
            int[]? lcpArray1 = null);
    }

    /// <summary>
    /// Result of byte-level similarity comparison.
    /// </summary>
    public class SimilarityResult
    {
        /// <summary>
        /// Percentage of bytes in common (0.0 to 100.0).
        /// </summary>
        public double SimilarityPercentage { get; set; }

        /// <summary>
        /// Length of the longest common substring.
        /// </summary>
        public int LongestCommonSubstringLength { get; set; }

        /// <summary>
        /// Offset in file1 of the longest common substring.
        /// </summary>
        public long LongestCommonSubstringOffset1 { get; set; }

        /// <summary>
        /// Offset in file2 of the longest common substring.
        /// </summary>
        public long LongestCommonSubstringOffset2 { get; set; }

        /// <summary>
        /// Total bytes analyzed.
        /// </summary>
        public long TotalBytesAnalyzed { get; set; }

        /// <summary>
        /// Number of common chunks found.
        /// </summary>
        public int CommonChunksCount { get; set; }

        /// <summary>
        /// Calculation time in milliseconds.
        /// </summary>
        public long CalculationTimeMs { get; set; }
    }
}
