using System;
using System.IO;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Services
{
    public class FileService : IFileService
    {
        public BinaryDataSource OpenFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("File not found", path);
            return new BinaryDataSource(path);
        }

        public async Task SaveIndexAsync(string originalFilePath, int[] sa, int[] lcp)
        {
            string idxPath = originalFilePath + ".idx";
            await Task.Run(() =>
            {
                using (var fs = new FileStream(idxPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    // Version header
                    bw.Write(1); 
                    
                    // Length check
                    bw.Write(sa.Length);
                    
                    // Write SA
                    foreach (var val in sa) bw.Write(val);
                    
                    // Write LCP
                    foreach (var val in lcp) bw.Write(val);
                }
            });
        }

        public async Task<(int[] sa, int[] lcp)?> LoadIndexAsync(string originalFilePath)
        {
            string idxPath = originalFilePath + ".idx";
            if (!File.Exists(idxPath)) return null;

            return await Task.Run(() =>
            {
                try
                {
                    using (var fs = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var br = new BinaryReader(fs))
                    {
                        int version = br.ReadInt32();
                        if (version != 1) return ((int[] sa, int[] lcp)?)null; 

                        int len = br.ReadInt32();
                        
                        int[] sa = new int[len];
                        for(int i=0; i<len; i++) sa[i] = br.ReadInt32();
                        
                        int[] lcp = new int[len];
                        for(int i=0; i<len; i++) lcp[i] = br.ReadInt32();
                        
                        return ((int[] sa, int[] lcp)?)(sa, lcp);
                    }
                }
                catch
                {
                    // Corrupt index or read error, ignore and force rebuild
                    return ((int[] sa, int[] lcp)?)null;
                }
            });
        }
    }
}