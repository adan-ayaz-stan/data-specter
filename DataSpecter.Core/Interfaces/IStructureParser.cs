using DataSpecter.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataSpecter.Core.Interfaces
{
    public interface IStructureParser
    {
        bool CanParse(string fileName, byte[] headerBytes);
        Task<List<StructureItem>> ParseAsync(BinaryDataSource dataSource);
    }
}
