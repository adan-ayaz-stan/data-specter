using DataSpecter.Core.Models;
using System;
using System.Threading.Tasks;

namespace DataSpecter.Core.Interfaces
{
    public interface ISuffixArrayService
    {
        /// <summary>
        /// Generates Suffix Array and LCP Array for the given data source.
        /// </summary>
        /// <param name="dataSource">The source data.</param>
        /// <param name="progress">Optional progress reporter for indexing updates.</param>
        /// <returns>A result containing the counts, timings, and arrays.</returns>
        Task<(long saCount, TimeSpan saTime, int[] suffixArray, long lcpCount, TimeSpan lcpTime, int[] lcpArray)> GenerateAsync(BinaryDataSource dataSource, IProgress<(string stage, int current, int total, double percentage)>? progress = null);

        /// <summary>
        /// Searches for a pattern using the Suffix Array (Binary Search).
        /// </summary>
        /// <param name="dataSource">The source data.</param>
        /// <param name="suffixArray">The pre-computed Suffix Array.</param>
        /// <param name="pattern">The byte pattern to search for.</param>
        /// <returns>A list of offsets where the pattern occurs.</returns>
        Task<long[]> SearchAsync(BinaryDataSource dataSource, int[] suffixArray, byte[] pattern);
    }
}
