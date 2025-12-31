using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataSpecter.UI.Models
{
    public class HexRow
    {
        public long Offset { get; set; }
        public List<ByteItem> Items { get; set; } = new List<ByteItem>();
        
        public string AsciiText
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var item in Items)
                {
                    // Convert byte to ASCII, show '.' for non-printable characters
                    char c = (item.Value >= 32 && item.Value <= 126) ? (char)item.Value : '.';
                    sb.Append(c);
                }
                return sb.ToString();
            }
        }

        public HexRow(long offset, IEnumerable<ByteItem> items)
        {
            Offset = offset;
            Items.AddRange(items);
        }
    }
}
