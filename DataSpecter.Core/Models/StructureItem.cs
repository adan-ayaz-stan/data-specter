using System.Collections.Generic;

namespace DataSpecter.Core.Models
{
    public class StructureItem
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public long Offset { get; set; }
        public long Length { get; set; }
        public List<StructureItem> Children { get; set; } = new List<StructureItem>();

        public StructureItem(string name, string value, long offset, long length)
        {
            Name = name;
            Value = value;
            Offset = offset;
            Length = length;
        }
    }
}
