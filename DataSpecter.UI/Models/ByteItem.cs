namespace DataSpecter.UI.Models
{
    public class ByteItem
    {
        public byte Value { get; set; }
        public long Offset { get; set; }
        public bool IsHighlighted { get; set; }

        public ByteItem(byte value, long offset, bool isHighlighted = false)
        {
            Value = value;
            Offset = offset;
            IsHighlighted = isHighlighted;
        }
    }
}
