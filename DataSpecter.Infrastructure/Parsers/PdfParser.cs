using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DataSpecter.Core.Interfaces;
using DataSpecter.Core.Models;

namespace DataSpecter.Infrastructure.Parsers
{
    public class PdfParser : IStructureParser
    {
        public bool CanParse(string fileName, byte[] headerBytes)
        {
            if (headerBytes.Length < 5) return false;
            // %PDF
            string head = Encoding.ASCII.GetString(headerBytes, 0, 5);
            return head.StartsWith("%PDF-");
        }

        public async Task<List<StructureItem>> ParseAsync(BinaryDataSource dataSource)
        {
            return await Task.Run(() =>
            {
                var root = new List<StructureItem>();
                
                // 1. Header
                byte[] buffer = new byte[1024];
                int read = dataSource.ReadRange(0, buffer, 0, 1024);
                string headStr = Encoding.ASCII.GetString(buffer, 0, read);
                
                // Find version
                var verMatch = Regex.Match(headStr, @"%PDF-(\d\.\d)");
                if (verMatch.Success)
                {
                    root.Add(new StructureItem("PDF Header", $"Version {verMatch.Groups[1].Value}", 0, verMatch.Length));
                }

                // 2. Scan for Objects (Simple scan in first 5MB for prototype)
                long scanLimit = Math.Min(dataSource.Length, 5 * 1024 * 1024);
                byte[] scanBuf = new byte[scanLimit];
                dataSource.ReadRange(0, scanBuf, 0, (int)scanLimit);
                string content = Encoding.ASCII.GetString(scanBuf);

                var objMatches = Regex.Matches(content, @"(\d+) (\d+) obj");
                var objectsNode = new StructureItem("PDF Objects", $"{objMatches.Count} found (in first 5MB)", 0, 0);
                
                int count = 0;
                foreach (Match m in objMatches)
                {
                    if (count++ > 50) 
                    {
                        objectsNode.Children.Add(new StructureItem("...", "More objects...", 0, 0));
                        break;
                    }
                    objectsNode.Children.Add(new StructureItem($"Object {m.Groups[1].Value}", $"Gen {m.Groups[2].Value}", m.Index, m.Length));
                }
                root.Add(objectsNode);

                // 3. Trailer?
                // Usually at the end. Read last 1KB.
                if (dataSource.Length > 1024)
                {
                    long tailOffset = dataSource.Length - 1024;
                    byte[] tailBuf = new byte[1024];
                    dataSource.ReadRange(tailOffset, tailBuf, 0, 1024);
                    string tailStr = Encoding.ASCII.GetString(tailBuf);
                    
                    int trailerIdx = tailStr.LastIndexOf("trailer");
                    if (trailerIdx >= 0)
                    {
                        root.Add(new StructureItem("Trailer", "Found", tailOffset + trailerIdx, 7));
                    }
                }
                
                return root;
            });
        }
    }
}
