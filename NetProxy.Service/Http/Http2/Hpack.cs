using System.Text;

namespace NetProxy.Service.Http.Http2
{
    /// <summary>
    /// A header field as it was decoded from, and will be encoded to, an HTTP/2 header block. Names and values are kept as bytes
    ///     (not text) so that nothing is altered unless a header rule alters it.
    /// </summary>
    internal sealed class HeaderField
    {
        public byte[] Name { get; }
        public byte[] Value { get; set; }

        /// <summary>
        /// The sender marked the field as sensitive (HPACK "never indexed"). That must be kept by every hop between the sender and the receiver.
        /// </summary>
        public bool NeverIndex { get; }

        public HeaderField(byte[] name, byte[] value, bool neverIndex = false)
        {
            Name = name;
            Value = value;
            NeverIndex = neverIndex;
        }

        public HeaderField(string name, string value) : this(Encoding.Latin1.GetBytes(name), Encoding.Latin1.GetBytes(value))
        {
        }

        public string NameText => Encoding.Latin1.GetString(Name);
        public string ValueText => Encoding.Latin1.GetString(Value);
    }

    internal sealed class HpackException(string message) : Exception(message)
    {
    }

    /// <summary>
    /// HPACK Huffman decoding (RFC 7541 section 5.2). Only decoding is needed: header blocks are written without Huffman coding.
    /// </summary>
    internal static class HpackHuffman
    {
        //A binary trie: for node n, _children[2n] is where bit 0 leads and _children[2n+1] where bit 1 leads.
        //  Positive: the index of the next node (the root, 0, is never a child). Negative: a leaf, -(symbol + 1). Zero: no such code.
        private static readonly int[] _children = BuildTrie();

        private const int EndOfString = 256;

        private static int[] BuildTrie()
        {
            var children = new List<int> { 0, 0 };

            for (int symbol = 0; symbol < HpackTables.HuffmanCodes.Length; symbol++)
            {
                uint code = HpackTables.HuffmanCodes[symbol];
                int length = HpackTables.HuffmanLengths[symbol];
                int node = 0;

                for (int bit = 0; bit < length; bit++)
                {
                    int slot = node * 2 + (int)((code >> (31 - bit)) & 1);

                    if (bit == length - 1)
                    {
                        children[slot] = -(symbol + 1);
                    }
                    else
                    {
                        if (children[slot] == 0)
                        {
                            children[slot] = children.Count / 2;
                            children.Add(0);
                            children.Add(0);
                        }
                        node = children[slot];
                    }
                }
            }

            return [.. children];
        }

        public static byte[] Decode(ReadOnlySpan<byte> source)
        {
            var output = new MemoryStream(source.Length * 2);
            int node = 0;
            int bitsInSymbol = 0;
            bool allOnes = true;

            foreach (byte b in source)
            {
                for (int shift = 7; shift >= 0; shift--)
                {
                    int bit = (b >> shift) & 1;
                    int next = _children[node * 2 + bit];

                    if (next > 0)
                    {
                        node = next;
                        bitsInSymbol++;
                        allOnes &= bit == 1;
                    }
                    else if (next < 0)
                    {
                        int symbol = -next - 1;
                        if (symbol == EndOfString)
                        {
                            throw new HpackException("The Huffman data contains the end-of-string symbol.");
                        }
                        output.WriteByte((byte)symbol);
                        node = 0;
                        bitsInSymbol = 0;
                        allOnes = true;
                    }
                    else
                    {
                        throw new HpackException("The Huffman data is invalid.");
                    }
                }
            }

            //What is left over must be padding: fewer than 8 bits, all of them 1 (the start of the end-of-string code).
            if (bitsInSymbol > 7 || !allOnes)
            {
                throw new HpackException("The Huffman data has invalid padding.");
            }

            return output.ToArray();
        }
    }

    /// <summary>
    /// Decodes HTTP/2 header blocks (RFC 7541). One instance follows one direction of one connection, because the dynamic table is
    /// state that is shared by every block the sender writes. Every block must be decoded, in order, even ones that are not changed.
    /// </summary>
    internal sealed class HpackDecoder
    {
        private const int EntryOverhead = 32;
        private const int DefaultTableSize = 4096;

        /// <summary>
        /// A block that decodes to more than this is refused: indexed fields repeat a dynamic table entry for one byte each, so a
        /// small block can otherwise expand enormously.
        /// </summary>
        private const int MaxDecodedBytes = 4 * 1024 * 1024;

        private static readonly HeaderField[] _static = [.. HpackTables.Static.Select(o => new HeaderField(o.Name, o.Value))];

        private readonly List<HeaderField> _dynamic = []; //Index 0 is the newest entry.
        private readonly int _maxAllowedTableSize;
        private int _tableSize;
        private int _maxTableSize;

        /// <param name="maxAllowedTableSize">
        /// The largest dynamic table the sender may ask for. It is bounded by the receiver's SETTINGS_HEADER_TABLE_SIZE, which travels in
        /// the other direction and is not seen here, so this is a generous ceiling.
        /// </param>
        public HpackDecoder(int maxAllowedTableSize = 1024 * 1024)
        {
            _maxAllowedTableSize = maxAllowedTableSize;
            _maxTableSize = Math.Min(DefaultTableSize, maxAllowedTableSize);
        }

        public List<HeaderField> Decode(ReadOnlySpan<byte> block)
        {
            var fields = new List<HeaderField>();
            int position = 0;
            long decodedBytes = 0;

            while (position < block.Length)
            {
                byte first = block[position];
                HeaderField field;

                if ((first & 0x80) != 0)
                {
                    //Indexed header field.
                    field = Lookup(ReadInteger(block, ref position, 7));
                }
                else if ((first & 0xC0) == 0x40)
                {
                    //Literal header field with incremental indexing.
                    field = ReadLiteral(block, ref position, 6, neverIndex: false);
                    AddToDynamicTable(field);
                }
                else if ((first & 0xE0) == 0x20)
                {
                    //Dynamic table size update.
                    int size = ReadInteger(block, ref position, 5);
                    if (size > _maxAllowedTableSize)
                    {
                        throw new HpackException("The dynamic table size update is larger than allowed.");
                    }
                    _maxTableSize = size;
                    Evict(0);
                    continue;
                }
                else
                {
                    //Literal header field without indexing (0000) or never indexed (0001).
                    field = ReadLiteral(block, ref position, 4, neverIndex: (first & 0xF0) == 0x10);
                }

                decodedBytes += field.Name.Length + field.Value.Length;
                if (decodedBytes > MaxDecodedBytes)
                {
                    throw new HpackException("The header block is too large.");
                }

                fields.Add(field);
            }

            return fields;
        }

        private HeaderField ReadLiteral(ReadOnlySpan<byte> block, ref int position, int prefixBits, bool neverIndex)
        {
            int index = ReadInteger(block, ref position, prefixBits);
            var name = index == 0 ? ReadString(block, ref position) : Lookup(index).Name;
            var value = ReadString(block, ref position);
            return new HeaderField(name, value, neverIndex);
        }

        private HeaderField Lookup(int index)
        {
            if (index >= 1 && index <= _static.Length)
            {
                return _static[index - 1];
            }

            int dynamicIndex = index - _static.Length - 1;
            if (index > _static.Length && dynamicIndex < _dynamic.Count)
            {
                return _dynamic[dynamicIndex];
            }

            throw new HpackException($"The header index {index} is not valid.");
        }

        private void AddToDynamicTable(HeaderField field)
        {
            int size = field.Name.Length + field.Value.Length + EntryOverhead;

            Evict(size);

            if (size <= _maxTableSize)
            {
                _dynamic.Insert(0, field);
                _tableSize += size;
            }
        }

        /// <summary>
        /// Removes the oldest entries until there is room for <paramref name="incomingSize"/> more bytes.
        /// </summary>
        private void Evict(int incomingSize)
        {
            while (_dynamic.Count > 0 && _tableSize + incomingSize > _maxTableSize)
            {
                var oldest = _dynamic[^1];
                _tableSize -= oldest.Name.Length + oldest.Value.Length + EntryOverhead;
                _dynamic.RemoveAt(_dynamic.Count - 1);
            }
        }

        private static int ReadInteger(ReadOnlySpan<byte> data, ref int position, int prefixBits)
        {
            if (position >= data.Length)
            {
                throw new HpackException("The header block ended in the middle of a field.");
            }

            int mask = (1 << prefixBits) - 1;
            long value = data[position++] & mask;

            if (value < mask)
            {
                return (int)value;
            }

            int shift = 0;
            while (true)
            {
                if (position >= data.Length || shift > 28)
                {
                    throw new HpackException("The header block has an invalid integer.");
                }

                byte b = data[position++];
                value += (long)(b & 0x7F) << shift;
                shift += 7;

                if (value > int.MaxValue)
                {
                    throw new HpackException("The header block has an integer that is too large.");
                }

                if ((b & 0x80) == 0)
                {
                    return (int)value;
                }
            }
        }

        private static byte[] ReadString(ReadOnlySpan<byte> data, ref int position)
        {
            if (position >= data.Length)
            {
                throw new HpackException("The header block ended in the middle of a field.");
            }

            bool huffman = (data[position] & 0x80) != 0;
            int length = ReadInteger(data, ref position, 7);

            if (length > data.Length - position)
            {
                throw new HpackException("The header block has a string that runs past its end.");
            }

            var raw = data.Slice(position, length);
            position += length;

            return huffman ? HpackHuffman.Decode(raw) : raw.ToArray();
        }
    }

    /// <summary>
    /// Encodes HTTP/2 header blocks (RFC 7541) without using the dynamic table, so that a block never depends on an earlier one and the
    /// receiver's table (which stays empty) can never go out of step. Fields are written as static table references or plain literals.
    /// </summary>
    internal static class HpackEncoder
    {
        private static readonly Dictionary<string, int> _staticByNameAndValue = BuildStaticIndex(withValue: true);
        private static readonly Dictionary<string, int> _staticByName = BuildStaticIndex(withValue: false);

        private static Dictionary<string, int> BuildStaticIndex(bool withValue)
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < HpackTables.Static.Length; i++)
            {
                var (name, value) = HpackTables.Static[i];
                index.TryAdd(withValue ? $"{name}\0{value}" : name, i + 1); //The first (lowest) index wins.
            }
            return index;
        }

        public static byte[] Encode(IEnumerable<HeaderField> fields)
        {
            var output = new MemoryStream();

            foreach (var field in fields)
            {
                var name = field.NameText;

                if (!field.NeverIndex && _staticByNameAndValue.TryGetValue($"{name}\0{field.ValueText}", out var exact))
                {
                    WriteInteger(output, exact, 7, 0x80); //Indexed header field.
                    continue;
                }

                //Literal without indexing (0000) or never indexed (0001), with the name from the static table if it is there.
                byte flags = field.NeverIndex ? (byte)0x10 : (byte)0x00;

                if (_staticByName.TryGetValue(name, out var nameIndex))
                {
                    WriteInteger(output, nameIndex, 4, flags);
                }
                else
                {
                    WriteInteger(output, 0, 4, flags);
                    WriteString(output, field.Name);
                }

                WriteString(output, field.Value);
            }

            return output.ToArray();
        }

        private static void WriteString(MemoryStream output, byte[] value)
        {
            WriteInteger(output, value.Length, 7, 0x00); //Not Huffman coded.
            output.Write(value);
        }

        private static void WriteInteger(MemoryStream output, int value, int prefixBits, byte flags)
        {
            int mask = (1 << prefixBits) - 1;

            if (value < mask)
            {
                output.WriteByte((byte)(flags | value));
                return;
            }

            output.WriteByte((byte)(flags | mask));
            value -= mask;

            while (value >= 128)
            {
                output.WriteByte((byte)((value & 0x7F) | 0x80));
                value >>= 7;
            }

            output.WriteByte((byte)value);
        }
    }
}
