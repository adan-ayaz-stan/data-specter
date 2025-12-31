using DataSpecter.Core.Models;

namespace DataSpecter.Core.Interfaces
{
    public interface IFileService
    {
        /// <summary>
        /// Reads a file into a BinaryDataSource.
        /// </summary>
        BinaryDataSource OpenFile(string path);
        
        /// <summary>
        /// Saves the index (SA + LCP) to a file.
        /// </summary>
        Task SaveIndexAsync(string originalFilePath, int[] sa, int[] lcp);
        
        /// <summary>
        /// Loads the index (SA + LCP) if it exists.
        /// </summary>
        Task<(int[] sa, int[] lcp)?> LoadIndexAsync(string originalFilePath);
    }
}