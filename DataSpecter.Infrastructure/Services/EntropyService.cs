using System;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Services
{
    public class EntropyService : IEntropyService
    {
        public async Task<double[]> CalculateEntropyAsync(BinaryDataSource dataSource, int chunkSize = 1024)
        {
            long length = dataSource.Length;
            int numChunks = (int)((length + chunkSize - 1) / chunkSize);
            double[] entropyValues = new double[numChunks];

            // Limit memory usage - process in chunks, maybe parallelize later
            // For now, simple sequential async implementation
            return await Task.Run(() =>
            {
                byte[] buffer = new byte[chunkSize];
                
                for (int i = 0; i < numChunks; i++)
                {
                    long offset = (long)i * chunkSize;
                    int read = dataSource.ReadRange(offset, buffer, 0, chunkSize);
                    
                    if (read > 0)
                    {
                        entropyValues[i] = CalculateShannonEntropy(buffer, read);
                    }
                }

                return entropyValues;
            });
        }

        private double CalculateShannonEntropy(byte[] buffer, int count)
        {
            if (count == 0) return 0.0;

            int[] frequencies = new int[256];
            for (int i = 0; i < count; i++)
            {
                frequencies[buffer[i]]++;
            }

            double entropy = 0.0;
            double log2 = Math.Log(2);

            for (int i = 0; i < 256; i++)
            {
                if (frequencies[i] > 0)
                {
                    double p = (double)frequencies[i] / count;
                    entropy -= p * (Math.Log(p) / log2);
                }
            }

            return entropy;
        }
    }
}
