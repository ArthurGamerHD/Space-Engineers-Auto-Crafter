using System;
using System.Collections.Generic;
using System.Text;

namespace IngameScript
{
    public static class HuffmanDecompressor
    {
        // Unpack 15 payload bits from each char in order (MSB-first within the 15-bit payload).
        static string Unpack15(List<char> chars)
        {
            StringBuilder bits = new StringBuilder();
            foreach (char c in chars)
            {
                int v = c;
                // convert to 16-bit binary string (MSB first)
                string b = Convert.ToString(v, 2).PadLeft(16, '0');
                // Our payload is bits 14..0 (i.e. characters b[1]..b[15] ), because bit 15 (b[0]) is unused.
                bits.Append(b.Substring(1, 15));
            }

            return bits.ToString();
        }

        public static string Decompress(List<char> DictChars, List<char> DataChars)
        {
            if ((DictChars == null || DictChars.Count == 0) && (DataChars == null || DataChars.Count == 0))
                return string.Empty;

            string dictBits = Unpack15(DictChars ?? new List<char>());
            string dataBits = Unpack15(DataChars ?? new List<char>());

            // Rebuild dictionary: parse until consumed
            // Format: [16-bit symbol][8-bit length][N-bit code]...
            var decodeMap = new Dictionary<string, char>();
            int i = 0;
            while (i + 24 <= dictBits.Length) // need at least 16 + 8 bits
            {
                string symbolBits = dictBits.Substring(i, 16);
                int symVal = Convert.ToInt32(symbolBits, 2);
                char symbol = (char)symVal;
                i += 16;

                int codeLength = Convert.ToInt32(dictBits.Substring(i, 8), 2);
                i += 8;

                if (codeLength < 0 || i + codeLength > dictBits.Length)
                {
                    MyLog.Log(LogLevel.Error, "Decompress Error: invalid code length: " + dictBits.Substring(i));
                    break;
                }

                string code = dictBits.Substring(i, codeLength);
                i += codeLength;
                
                if (!decodeMap.ContainsKey(code))
                    decodeMap[code] = symbol;
            }


            StringBuilder output = new StringBuilder();
            StringBuilder current = new StringBuilder();

            for (int pos = 0; pos < dataBits.Length; pos++)
            {
                char bit = dataBits[pos];
                if (bit != '0' && bit != '1') continue;
                current.Append(bit);
                string curStr = current.ToString();
                char decoded;
                if (decodeMap.TryGetValue(curStr, out decoded))
                {
                    output.Append(decoded);
                    current.Clear();
                }
            }

            return output.ToString();
        }

        // DeOffset: Unset 16 bit of characters from 0 to 32 
        public static string DeOffset(string line)
        {
            var sb = new StringBuilder(line);
            for (var index = 0; index < 32; index++)
                sb = sb.Replace(((char)(index + 32768)).ToString(), ((char)index).ToString());
            return sb.ToString();
        }
    }
}