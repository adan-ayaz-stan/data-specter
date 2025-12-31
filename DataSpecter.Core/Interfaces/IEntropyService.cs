using DataSpecter.Core.Models;
using System.Threading.Tasks;

namespace DataSpecter.Core.Interfaces
{
    public interface IEntropyService
    {
        /// <summary>
        /// Calculates the Shannon entropy for chunks of the file.
        /// </summary>
        /// <param name="dataSource">The source data.</param>
        /// <param name="chunkSize">Size of the window to calculate entropy for.</param>
        /// <returns>An array of entropy values (0.0 to 8.0) for each chunk.</returns>
        Task<double[]> CalculateEntropyAsync(BinaryDataSource dataSource, int chunkSize = 1024);
    }
}
