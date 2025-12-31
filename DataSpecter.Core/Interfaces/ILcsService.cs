using DataSpecter.Core.Models;
using System.Threading.Tasks;

namespace DataSpecter.Core.Interfaces
{
    public interface ILcsService
    {
        /// <summary>
        /// Finds the Longest Common Substring between two data sources.
        /// </summary>
        /// <returns>Length of LCS, offset in source1, offset in source2</returns>
        Task<(long length, long offset1, long offset2)> FindLcsAsync(BinaryDataSource source1, BinaryDataSource source2);
    }
}
