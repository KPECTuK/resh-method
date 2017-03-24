namespace _Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;

    public static class SimpleZip
    {
        internal class Inflater
        {
            private const int DECODE_HEADER = 0;

            private const int DECODE_DICT = 1;

            private const int DECODE_BLOCKS = 2;

            private const int DECODE_STORED_LEN1 = 3;

            private const int DECODE_STORED_LEN2 = 4;

            private const int DECODE_STORED = 5;

            private const int DECODE_DYN_HEADER = 6;

            private const int DECODE_HUFFMAN = 7;

            private const int DECODE_HUFFMAN_LENBITS = 8;

            private const int DECODE_HUFFMAN_DIST = 9;

            private const int DECODE_HUFFMAN_DISTBITS = 10;

            private const int DECODE_CHKSUM = 11;

            private const int FINISHED = 12;

            private static readonly int[] CPLENS = new int[]
            {
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                13,
                15,
                17,
                19,
                23,
                27,
                31,
                35,
                43,
                51,
                59,
                67,
                83,
                99,
                115,
                131,
                163,
                195,
                227,
                258
            };

            private static readonly int[] CPLEXT = new int[]
            {
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                1,
                1,
                1,
                1,
                2,
                2,
                2,
                2,
                3,
                3,
                3,
                3,
                4,
                4,
                4,
                4,
                5,
                5,
                5,
                5,
                0
            };

            private static readonly int[] CPDIST = new int[]
            {
                1,
                2,
                3,
                4,
                5,
                7,
                9,
                13,
                17,
                25,
                33,
                49,
                65,
                97,
                129,
                193,
                257,
                385,
                513,
                769,
                1025,
                1537,
                2049,
                3073,
                4097,
                6145,
                8193,
                12289,
                16385,
                24577
            };

            private static readonly int[] CPDEXT = new int[]
            {
                0,
                0,
                0,
                0,
                1,
                1,
                2,
                2,
                3,
                3,
                4,
                4,
                5,
                5,
                6,
                6,
                7,
                7,
                8,
                8,
                9,
                9,
                10,
                10,
                11,
                11,
                12,
                12,
                13,
                13
            };

            private int mode;

            private int neededBits;

            private int repLength;

            private int repDist;

            private int uncomprLen;

            private bool isLastBlock;

            private SimpleZip.StreamManipulator input;

            private SimpleZip.OutputWindow outputWindow;

            private SimpleZip.InflaterDynHeader dynHeader;

            private SimpleZip.InflaterHuffmanTree litlenTree;

            private SimpleZip.InflaterHuffmanTree distTree;

            public Inflater(byte[] bytes)
            {
                this.input = new SimpleZip.StreamManipulator();
                this.outputWindow = new SimpleZip.OutputWindow();
                this.mode = 2;
                this.input.SetInput(bytes, 0, bytes.Length);
            }

            private bool DecodeHuffman()
            {
                int i = this.outputWindow.GetFreeSpace();
                while(i >= 258)
                {
                    int symbol;
                    switch(this.mode)
                    {
                        case 7:
                            while(((symbol = this.litlenTree.GetSymbol(this.input)) & -256) == 0)
                            {
                                this.outputWindow.Write(symbol);
                                if(--i < 258)
                                {
                                    return true;
                                }
                            }
                            if(symbol >= 257)
                            {
                                this.repLength = SimpleZip.Inflater.CPLENS[symbol - 257];
                                this.neededBits = SimpleZip.Inflater.CPLEXT[symbol - 257];
                                goto IL_B7;
                            }
                            if(symbol < 0)
                            {
                                return false;
                            }
                            this.distTree = null;
                            this.litlenTree = null;
                            this.mode = 2;
                            return true;
                        case 8:
                            goto IL_B7;
                        case 9:
                            goto IL_106;
                        case 10:
                            break;
                        default:
                            continue;
                    }
                IL_138:
                    if(this.neededBits > 0)
                    {
                        this.mode = 10;
                        int num = this.input.PeekBits(this.neededBits);
                        if(num < 0)
                        {
                            return false;
                        }
                        this.input.DropBits(this.neededBits);
                        this.repDist += num;
                    }
                    this.outputWindow.Repeat(this.repLength, this.repDist);
                    i -= this.repLength;
                    this.mode = 7;
                    continue;
                IL_106:
                    symbol = this.distTree.GetSymbol(this.input);
                    if(symbol < 0)
                    {
                        return false;
                    }
                    this.repDist = SimpleZip.Inflater.CPDIST[symbol];
                    this.neededBits = SimpleZip.Inflater.CPDEXT[symbol];
                    goto IL_138;
                IL_B7:
                    if(this.neededBits > 0)
                    {
                        this.mode = 8;
                        int num2 = this.input.PeekBits(this.neededBits);
                        if(num2 < 0)
                        {
                            return false;
                        }
                        this.input.DropBits(this.neededBits);
                        this.repLength += num2;
                    }
                    this.mode = 9;
                    goto IL_106;
                }
                return true;
            }

            private bool Decode()
            {
                switch(this.mode)
                {
                    case 2:
                        {
                            if(this.isLastBlock)
                            {
                                this.mode = 12;
                                return false;
                            }
                            int num = this.input.PeekBits(3);
                            if(num < 0)
                            {
                                return false;
                            }
                            this.input.DropBits(3);
                            if((num & 1) != 0)
                            {
                                this.isLastBlock = true;
                            }
                            switch(num >> 1)
                            {
                                case 0:
                                    this.input.SkipToByteBoundary();
                                    this.mode = 3;
                                    break;
                                case 1:
                                    this.litlenTree = SimpleZip.InflaterHuffmanTree.defLitLenTree;
                                    this.distTree = SimpleZip.InflaterHuffmanTree.defDistTree;
                                    this.mode = 7;
                                    break;
                                case 2:
                                    this.dynHeader = new SimpleZip.InflaterDynHeader();
                                    this.mode = 6;
                                    break;
                            }
                            return true;
                        }
                    case 3:
                        if((this.uncomprLen = this.input.PeekBits(16)) < 0)
                        {
                            return false;
                        }
                        this.input.DropBits(16);
                        this.mode = 4;
                        break;
                    case 4:
                        break;
                    case 5:
                        goto IL_137;
                    case 6:
                        if(!this.dynHeader.Decode(this.input))
                        {
                            return false;
                        }
                        this.litlenTree = this.dynHeader.BuildLitLenTree();
                        this.distTree = this.dynHeader.BuildDistTree();
                        this.mode = 7;
                        goto IL_1BB;
                    case 7:
                    case 8:
                    case 9:
                    case 10:
                        goto IL_1BB;
                    case 11:
                        return false;
                    case 12:
                        return false;
                    default:
                        return false;
                }
                int num2 = this.input.PeekBits(16);
                if(num2 < 0)
                {
                    return false;
                }
                this.input.DropBits(16);
                this.mode = 5;
            IL_137:
                int num3 = this.outputWindow.CopyStored(this.input, this.uncomprLen);
                this.uncomprLen -= num3;
                if(this.uncomprLen == 0)
                {
                    this.mode = 2;
                    return true;
                }
                return !this.input.IsNeedingInput;
            IL_1BB:
                return this.DecodeHuffman();
            }

            public int Inflate(byte[] buf, int offset, int len)
            {
                int num = 0;
                while(true)
                {
                    if(this.mode != 11)
                    {
                        int num2 = this.outputWindow.CopyOutput(buf, offset, len);
                        offset += num2;
                        num += num2;
                        len -= num2;
                        if(len == 0)
                        {
                            break;
                        }
                    }
                    if(!this.Decode() && (this.outputWindow.GetAvailable() <= 0 || this.mode == 11))
                    {
                        return num;
                    }
                }
                return num;
            }
        }

        internal class StreamManipulator
        {
            private byte[] window;

            private int window_start;

            private int window_end;

            private uint buffer;

            private int bits_in_buffer;

            public int AvailableBits
            {
                get
                {
                    return this.bits_in_buffer;
                }
            }

            public int AvailableBytes
            {
                get
                {
                    return this.window_end - this.window_start + (this.bits_in_buffer >> 3);
                }
            }

            public bool IsNeedingInput
            {
                get
                {
                    return this.window_start == this.window_end;
                }
            }

            public int PeekBits(int n)
            {
                if(this.bits_in_buffer < n)
                {
                    if(this.window_start == this.window_end)
                    {
                        return -1;
                    }
                    this.buffer |= (uint)((uint)((int)(this.window[this.window_start++] & 255) | (int)(this.window[this.window_start++] & 255) << 8) << this.bits_in_buffer);
                    this.bits_in_buffer += 16;
                }
                return (int)((ulong)this.buffer & (ulong)((long)((1 << n) - 1)));
            }

            public void DropBits(int n)
            {
                this.buffer >>= n;
                this.bits_in_buffer -= n;
            }

            public void SkipToByteBoundary()
            {
                this.buffer >>= (this.bits_in_buffer & 7);
                this.bits_in_buffer &= -8;
            }

            public int CopyBytes(byte[] output, int offset, int length)
            {
                int num = 0;
                while(this.bits_in_buffer > 0 && length > 0)
                {
                    output[offset++] = (byte)this.buffer;
                    this.buffer >>= 8;
                    this.bits_in_buffer -= 8;
                    length--;
                    num++;
                }
                if(length == 0)
                {
                    return num;
                }
                int num2 = this.window_end - this.window_start;
                if(length > num2)
                {
                    length = num2;
                }
                Array.Copy(this.window, this.window_start, output, offset, length);
                this.window_start += length;
                if((this.window_start - this.window_end & 1) != 0)
                {
                    this.buffer = (uint)(this.window[this.window_start++] & 255);
                    this.bits_in_buffer = 8;
                }
                return num + length;
            }

            public void Reset()
            {
                this.buffer = (uint)(this.window_start = (this.window_end = (this.bits_in_buffer = 0)));
            }

            public void SetInput(byte[] buf, int off, int len)
            {
                if(this.window_start < this.window_end)
                {
                    throw new InvalidOperationException();
                }
                int num = off + len;
                if(0 > off || off > num || num > buf.Length)
                {
                    throw new ArgumentOutOfRangeException();
                }
                if((len & 1) != 0)
                {
                    this.buffer |= (uint)((uint)(buf[off++] & 255) << this.bits_in_buffer);
                    this.bits_in_buffer += 8;
                }
                this.window = buf;
                this.window_start = off;
                this.window_end = num;
            }
        }

        internal class OutputWindow
        {
            private const int WINDOW_SIZE = 32768;

            private const int WINDOW_MASK = 32767;

            private byte[] window = new byte[32768];

            private int windowEnd;

            private int windowFilled;

            public void Write(int abyte)
            {
                if(this.windowFilled++ == 32768)
                {
                    throw new InvalidOperationException();
                }
                this.window[this.windowEnd++] = (byte)abyte;
                this.windowEnd &= 32767;
            }

            private void SlowRepeat(int repStart, int len, int dist)
            {
                while(len-- > 0)
                {
                    this.window[this.windowEnd++] = this.window[repStart++];
                    this.windowEnd &= 32767;
                    repStart &= 32767;
                }
            }

            public void Repeat(int len, int dist)
            {
                if((this.windowFilled += len) > 32768)
                {
                    throw new InvalidOperationException();
                }
                int num = this.windowEnd - dist & 32767;
                int num2 = 32768 - len;
                if(num > num2 || this.windowEnd >= num2)
                {
                    this.SlowRepeat(num, len, dist);
                    return;
                }
                if(len <= dist)
                {
                    Array.Copy(this.window, num, this.window, this.windowEnd, len);
                    this.windowEnd += len;
                    return;
                }
                while(len-- > 0)
                {
                    this.window[this.windowEnd++] = this.window[num++];
                }
            }

            public int CopyStored(SimpleZip.StreamManipulator input, int len)
            {
                len = Math.Min(Math.Min(len, 32768 - this.windowFilled), input.AvailableBytes);
                int num = 32768 - this.windowEnd;
                int num2;
                if(len > num)
                {
                    num2 = input.CopyBytes(this.window, this.windowEnd, num);
                    if(num2 == num)
                    {
                        num2 += input.CopyBytes(this.window, 0, len - num);
                    }
                }
                else
                {
                    num2 = input.CopyBytes(this.window, this.windowEnd, len);
                }
                this.windowEnd = (this.windowEnd + num2 & 32767);
                this.windowFilled += num2;
                return num2;
            }

            public void CopyDict(byte[] dict, int offset, int len)
            {
                if(this.windowFilled > 0)
                {
                    throw new InvalidOperationException();
                }
                if(len > 32768)
                {
                    offset += len - 32768;
                    len = 32768;
                }
                Array.Copy(dict, offset, this.window, 0, len);
                this.windowEnd = (len & 32767);
            }

            public int GetFreeSpace()
            {
                return 32768 - this.windowFilled;
            }

            public int GetAvailable()
            {
                return this.windowFilled;
            }

            public int CopyOutput(byte[] output, int offset, int len)
            {
                int num = this.windowEnd;
                if(len > this.windowFilled)
                {
                    len = this.windowFilled;
                }
                else
                {
                    num = (this.windowEnd - this.windowFilled + len & 32767);
                }
                int num2 = len;
                int num3 = len - num;
                if(num3 > 0)
                {
                    Array.Copy(this.window, 32768 - num3, output, offset, num3);
                    offset += num3;
                    len = num;
                }
                Array.Copy(this.window, num - len, output, offset, len);
                this.windowFilled -= num2;
                if(this.windowFilled < 0)
                {
                    throw new InvalidOperationException();
                }
                return num2;
            }

            public void Reset()
            {
                this.windowFilled = (this.windowEnd = 0);
            }
        }

        internal class InflaterHuffmanTree
        {
            private const int MAX_BITLEN = 15;

            private short[] tree;

            public static readonly SimpleZip.InflaterHuffmanTree defLitLenTree;

            public static readonly SimpleZip.InflaterHuffmanTree defDistTree;

            static InflaterHuffmanTree()
            {
                byte[] array = new byte[288];
                int i = 0;
                while(i < 144)
                {
                    array[i++] = 8;
                }
                while(i < 256)
                {
                    array[i++] = 9;
                }
                while(i < 280)
                {
                    array[i++] = 7;
                }
                while(i < 288)
                {
                    array[i++] = 8;
                }
                SimpleZip.InflaterHuffmanTree.defLitLenTree = new SimpleZip.InflaterHuffmanTree(array);
                array = new byte[32];
                i = 0;
                while(i < 32)
                {
                    array[i++] = 5;
                }
                SimpleZip.InflaterHuffmanTree.defDistTree = new SimpleZip.InflaterHuffmanTree(array);
            }

            public InflaterHuffmanTree(byte[] codeLengths)
            {
                this.BuildTree(codeLengths);
            }

            private void BuildTree(byte[] codeLengths)
            {
                int[] array = new int[16];
                int[] array2 = new int[16];
                for(int i = 0; i < codeLengths.Length; i++)
                {
                    int num = (int)codeLengths[i];
                    if(num > 0)
                    {
                        array[num]++;
                    }
                }
                int num2 = 0;
                int num3 = 512;
                for(int j = 1; j <= 15; j++)
                {
                    array2[j] = num2;
                    num2 += array[j] << 16 - j;
                    if(j >= 10)
                    {
                        int num4 = array2[j] & 130944;
                        int num5 = num2 & 130944;
                        num3 += num5 - num4 >> 16 - j;
                    }
                }
                this.tree = new short[num3];
                int num6 = 512;
                for(int k = 15; k >= 10; k--)
                {
                    int num7 = num2 & 130944;
                    num2 -= array[k] << 16 - k;
                    int num8 = num2 & 130944;
                    for(int l = num8; l < num7; l += 128)
                    {
                        this.tree[(int)SimpleZip.DeflaterHuffman.BitReverse(l)] = (short)(-num6 << 4 | k);
                        num6 += 1 << k - 9;
                    }
                }
                for(int m = 0; m < codeLengths.Length; m++)
                {
                    int num9 = (int)codeLengths[m];
                    if(num9 != 0)
                    {
                        num2 = array2[num9];
                        int num10 = (int)SimpleZip.DeflaterHuffman.BitReverse(num2);
                        if(num9 <= 9)
                        {
                            do
                            {
                                this.tree[num10] = (short)(m << 4 | num9);
                                num10 += 1 << num9;
                            }
                            while(num10 < 512);
                        }
                        else
                        {
                            int num11 = (int)this.tree[num10 & 511];
                            int num12 = 1 << (num11 & 15);
                            num11 = -(num11 >> 4);
                            do
                            {
                                this.tree[num11 | num10 >> 9] = (short)(m << 4 | num9);
                                num10 += 1 << num9;
                            }
                            while(num10 < num12);
                        }
                        array2[num9] = num2 + (1 << 16 - num9);
                    }
                }
            }

            public int GetSymbol(SimpleZip.StreamManipulator input)
            {
                int num;
                if((num = input.PeekBits(9)) >= 0)
                {
                    int num2;
                    if((num2 = (int)this.tree[num]) >= 0)
                    {
                        input.DropBits(num2 & 15);
                        return num2 >> 4;
                    }
                    int num3 = -(num2 >> 4);
                    int n = num2 & 15;
                    if((num = input.PeekBits(n)) >= 0)
                    {
                        num2 = (int)this.tree[num3 | num >> 9];
                        input.DropBits(num2 & 15);
                        return num2 >> 4;
                    }
                    int availableBits = input.AvailableBits;
                    num = input.PeekBits(availableBits);
                    num2 = (int)this.tree[num3 | num >> 9];
                    if((num2 & 15) <= availableBits)
                    {
                        input.DropBits(num2 & 15);
                        return num2 >> 4;
                    }
                    return -1;
                }
                else
                {
                    int availableBits2 = input.AvailableBits;
                    num = input.PeekBits(availableBits2);
                    int num2 = (int)this.tree[num];
                    if(num2 >= 0 && (num2 & 15) <= availableBits2)
                    {
                        input.DropBits(num2 & 15);
                        return num2 >> 4;
                    }
                    return -1;
                }
            }
        }

        internal class InflaterDynHeader
        {
            private const int LNUM = 0;

            private const int DNUM = 1;

            private const int BLNUM = 2;

            private const int BLLENS = 3;

            private const int LENS = 4;

            private const int REPS = 5;

            private static readonly int[] repMin = new int[]
            {
                3,
                3,
                11
            };

            private static readonly int[] repBits = new int[]
            {
                2,
                3,
                7
            };

            private byte[] blLens;

            private byte[] litdistLens;

            private SimpleZip.InflaterHuffmanTree blTree;

            private int mode;

            private int lnum;

            private int dnum;

            private int blnum;

            private int num;

            private int repSymbol;

            private byte lastLen;

            private int ptr;

            private static readonly int[] BL_ORDER = new int[]
            {
                16,
                17,
                18,
                0,
                8,
                7,
                9,
                6,
                10,
                5,
                11,
                4,
                12,
                3,
                13,
                2,
                14,
                1,
                15
            };

            public bool Decode(SimpleZip.StreamManipulator input)
            {
                while(true)
                {
                    switch(this.mode)
                    {
                        case 0:
                            this.lnum = input.PeekBits(5);
                            if(this.lnum < 0)
                            {
                                return false;
                            }
                            this.lnum += 257;
                            input.DropBits(5);
                            this.mode = 1;
                            goto IL_61;
                        case 1:
                            goto IL_61;
                        case 2:
                            goto IL_B9;
                        case 3:
                            break;
                        case 4:
                            goto IL_1A8;
                        case 5:
                            goto IL_1DE;
                        default:
                            continue;
                    }
                IL_13B:
                    while(this.ptr < this.blnum)
                    {
                        int num = input.PeekBits(3);
                        if(num < 0)
                        {
                            return false;
                        }
                        input.DropBits(3);
                        this.blLens[SimpleZip.InflaterDynHeader.BL_ORDER[this.ptr]] = (byte)num;
                        this.ptr++;
                    }
                    this.blTree = new SimpleZip.InflaterHuffmanTree(this.blLens);
                    this.blLens = null;
                    this.ptr = 0;
                    this.mode = 4;
                IL_1A8:
                    int symbol;
                    while(((symbol = this.blTree.GetSymbol(input)) & -16) == 0)
                    {
                        this.litdistLens[this.ptr++] = (this.lastLen = (byte)symbol);
                        if(this.ptr == this.num)
                        {
                            return true;
                        }
                    }
                    if(symbol < 0)
                    {
                        return false;
                    }
                    if(symbol >= 17)
                    {
                        this.lastLen = 0;
                    }
                    this.repSymbol = symbol - 16;
                    this.mode = 5;
                IL_1DE:
                    int n = SimpleZip.InflaterDynHeader.repBits[this.repSymbol];
                    int num2 = input.PeekBits(n);
                    if(num2 < 0)
                    {
                        return false;
                    }
                    input.DropBits(n);
                    num2 += SimpleZip.InflaterDynHeader.repMin[this.repSymbol];
                    while(num2-- > 0)
                    {
                        this.litdistLens[this.ptr++] = this.lastLen;
                    }
                    if(this.ptr == this.num)
                    {
                        return true;
                    }
                    this.mode = 4;
                    continue;
                IL_B9:
                    this.blnum = input.PeekBits(4);
                    if(this.blnum < 0)
                    {
                        return false;
                    }
                    this.blnum += 4;
                    input.DropBits(4);
                    this.blLens = new byte[19];
                    this.ptr = 0;
                    this.mode = 3;
                    goto IL_13B;
                IL_61:
                    this.dnum = input.PeekBits(5);
                    if(this.dnum < 0)
                    {
                        return false;
                    }
                    this.dnum++;
                    input.DropBits(5);
                    this.num = this.lnum + this.dnum;
                    this.litdistLens = new byte[this.num];
                    this.mode = 2;
                    goto IL_B9;
                }
                return false;
            }

            public SimpleZip.InflaterHuffmanTree BuildLitLenTree()
            {
                byte[] array = new byte[this.lnum];
                Array.Copy(this.litdistLens, 0, array, 0, this.lnum);
                return new SimpleZip.InflaterHuffmanTree(array);
            }

            public SimpleZip.InflaterHuffmanTree BuildDistTree()
            {
                byte[] array = new byte[this.dnum];
                Array.Copy(this.litdistLens, this.lnum, array, 0, this.dnum);
                return new SimpleZip.InflaterHuffmanTree(array);
            }
        }

        internal class Deflater
        {
            private const int IS_FLUSHING = 4;

            private const int IS_FINISHING = 8;

            private const int BUSY_STATE = 16;

            private const int FLUSHING_STATE = 20;

            private const int FINISHING_STATE = 28;

            private const int FINISHED_STATE = 30;

            private int state = 16;

            private long totalOut;

            private SimpleZip.DeflaterPending pending;

            private SimpleZip.DeflaterEngine engine;

            public long TotalOut
            {
                get
                {
                    return this.totalOut;
                }
            }

            public bool IsFinished
            {
                get
                {
                    return this.state == 30 && this.pending.IsFlushed;
                }
            }

            public bool IsNeedingInput
            {
                get
                {
                    return this.engine.NeedsInput();
                }
            }

            public Deflater()
            {
                this.pending = new SimpleZip.DeflaterPending();
                this.engine = new SimpleZip.DeflaterEngine(this.pending);
            }

            public void Finish()
            {
                this.state |= 12;
            }

            public void SetInput(byte[] buffer)
            {
                this.engine.SetInput(buffer);
            }

            public int Deflate(byte[] output)
            {
                int num = 0;
                int num2 = output.Length;
                int num3 = num2;
                while(true)
                {
                    int num4 = this.pending.Flush(output, num, num2);
                    num += num4;
                    this.totalOut += (long)num4;
                    num2 -= num4;
                    if(num2 == 0 || this.state == 30)
                    {
                        goto IL_E2;
                    }
                    if(!this.engine.Deflate((this.state & 4) != 0, (this.state & 8) != 0))
                    {
                        if(this.state == 16)
                        {
                            break;
                        }
                        if(this.state == 20)
                        {
                            for(int i = 8 + (-this.pending.BitCount & 7); i > 0; i -= 10)
                            {
                                this.pending.WriteBits(2, 10);
                            }
                            this.state = 16;
                        }
                        else if(this.state == 28)
                        {
                            this.pending.AlignToByte();
                            this.state = 30;
                        }
                    }
                }
                return num3 - num2;
            IL_E2:
                return num3 - num2;
            }
        }

        internal class DeflaterHuffman
        {
            public class Tree
            {
                public short[] freqs;

                public byte[] length;

                public int minNumCodes;

                public int numCodes;

                private short[] codes;

                private int[] bl_counts;

                private int maxLength;

                private SimpleZip.DeflaterHuffman dh;

                public Tree(SimpleZip.DeflaterHuffman dh, int elems, int minCodes, int maxLength)
                {
                    this.dh = dh;
                    this.minNumCodes = minCodes;
                    this.maxLength = maxLength;
                    this.freqs = new short[elems];
                    this.bl_counts = new int[maxLength];
                }

                public void WriteSymbol(int code)
                {
                    this.dh.pending.WriteBits((int)this.codes[code] & 65535, (int)this.length[code]);
                }

                public void SetStaticCodes(short[] stCodes, byte[] stLength)
                {
                    this.codes = stCodes;
                    this.length = stLength;
                }

                public void BuildCodes()
                {
                    int[] array = new int[this.maxLength];
                    int num = 0;
                    this.codes = new short[this.freqs.Length];
                    for(int i = 0; i < this.maxLength; i++)
                    {
                        array[i] = num;
                        num += this.bl_counts[i] << 15 - i;
                    }
                    for(int j = 0; j < this.numCodes; j++)
                    {
                        int num2 = (int)this.length[j];
                        if(num2 > 0)
                        {
                            this.codes[j] = SimpleZip.DeflaterHuffman.BitReverse(array[num2 - 1]);
                            array[num2 - 1] += 1 << 16 - num2;
                        }
                    }
                }

                private void BuildLength(int[] childs)
                {
                    this.length = new byte[this.freqs.Length];
                    int num = childs.Length / 2;
                    int num2 = (num + 1) / 2;
                    int num3 = 0;
                    for(int i = 0; i < this.maxLength; i++)
                    {
                        this.bl_counts[i] = 0;
                    }
                    int[] array = new int[num];
                    array[num - 1] = 0;
                    for(int j = num - 1; j >= 0; j--)
                    {
                        if(childs[2 * j + 1] != -1)
                        {
                            int num4 = array[j] + 1;
                            if(num4 > this.maxLength)
                            {
                                num4 = this.maxLength;
                                num3++;
                            }
                            array[childs[2 * j]] = (array[childs[2 * j + 1]] = num4);
                        }
                        else
                        {
                            int num5 = array[j];
                            this.bl_counts[num5 - 1]++;
                            this.length[childs[2 * j]] = (byte)array[j];
                        }
                    }
                    if(num3 == 0)
                    {
                        return;
                    }
                    int num6 = this.maxLength - 1;
                    while(true)
                    {
                        if(this.bl_counts[--num6] != 0)
                        {
                            do
                            {
                                this.bl_counts[num6]--;
                                this.bl_counts[++num6]++;
                                num3 -= 1 << this.maxLength - 1 - num6;
                            }
                            while(num3 > 0 && num6 < this.maxLength - 1);
                            if(num3 <= 0)
                            {
                                break;
                            }
                        }
                    }
                    this.bl_counts[this.maxLength - 1] += num3;
                    this.bl_counts[this.maxLength - 2] -= num3;
                    int num7 = 2 * num2;
                    for(int num8 = this.maxLength; num8 != 0; num8--)
                    {
                        int k = this.bl_counts[num8 - 1];
                        while(k > 0)
                        {
                            int num9 = 2 * childs[num7++];
                            if(childs[num9 + 1] == -1)
                            {
                                this.length[childs[num9]] = (byte)num8;
                                k--;
                            }
                        }
                    }
                }

                public void BuildTree()
                {
                    int num = this.freqs.Length;
                    int[] array = new int[num];
                    int i = 0;
                    int num2 = 0;
                    for(int j = 0; j < num; j++)
                    {
                        int num3 = (int)this.freqs[j];
                        if(num3 != 0)
                        {
                            int num4 = i++;
                            int num5;
                            while(num4 > 0 && (int)this.freqs[array[num5 = (num4 - 1) / 2]] > num3)
                            {
                                array[num4] = array[num5];
                                num4 = num5;
                            }
                            array[num4] = j;
                            num2 = j;
                        }
                    }
                    while(i < 2)
                    {
                        int num6 = (num2 < 2) ? (++num2) : 0;
                        array[i++] = num6;
                    }
                    this.numCodes = Math.Max(num2 + 1, this.minNumCodes);
                    int num7 = i;
                    int[] array2 = new int[4 * i - 2];
                    int[] array3 = new int[2 * i - 1];
                    int num8 = num7;
                    for(int k = 0; k < i; k++)
                    {
                        int num9 = array[k];
                        array2[2 * k] = num9;
                        array2[2 * k + 1] = -1;
                        array3[k] = (int)this.freqs[num9] << 8;
                        array[k] = k;
                    }
                    do
                    {
                        int num10 = array[0];
                        int num11 = array[--i];
                        int num12 = 0;
                        int l;
                        for(l = 1; l < i; l = l * 2 + 1)
                        {
                            if(l + 1 < i && array3[array[l]] > array3[array[l + 1]])
                            {
                                l++;
                            }
                            array[num12] = array[l];
                            num12 = l;
                        }
                        int num13 = array3[num11];
                        while((l = num12) > 0 && array3[array[num12 = (l - 1) / 2]] > num13)
                        {
                            array[l] = array[num12];
                        }
                        array[l] = num11;
                        int num14 = array[0];
                        num11 = num8++;
                        array2[2 * num11] = num10;
                        array2[2 * num11 + 1] = num14;
                        int num15 = Math.Min(array3[num10] & 255, array3[num14] & 255);
                        num13 = (array3[num11] = array3[num10] + array3[num14] - num15 + 1);
                        num12 = 0;
                        for(l = 1; l < i; l = num12 * 2 + 1)
                        {
                            if(l + 1 < i && array3[array[l]] > array3[array[l + 1]])
                            {
                                l++;
                            }
                            array[num12] = array[l];
                            num12 = l;
                        }
                        while((l = num12) > 0 && array3[array[num12 = (l - 1) / 2]] > num13)
                        {
                            array[l] = array[num12];
                        }
                        array[l] = num11;
                    }
                    while(i > 1);
                    this.BuildLength(array2);
                }

                public int GetEncodedLength()
                {
                    int num = 0;
                    for(int i = 0; i < this.freqs.Length; i++)
                    {
                        num += (int)(this.freqs[i] * (short)this.length[i]);
                    }
                    return num;
                }

                public void CalcBLFreq(SimpleZip.DeflaterHuffman.Tree blTree)
                {
                    int num = -1;
                    int i = 0;
                    while(i < this.numCodes)
                    {
                        int num2 = 1;
                        int num3 = (int)this.length[i];
                        int num4;
                        int num5;
                        if(num3 == 0)
                        {
                            num4 = 138;
                            num5 = 3;
                        }
                        else
                        {
                            num4 = 6;
                            num5 = 3;
                            if(num != num3)
                            {
                                short[] expr_3B_cp_0 = blTree.freqs;
                                int expr_3B_cp_1 = num3;
                                expr_3B_cp_0[expr_3B_cp_1] += 1;
                                num2 = 0;
                            }
                        }
                        num = num3;
                        i++;
                        while(i < this.numCodes && num == (int)this.length[i])
                        {
                            i++;
                            if(++num2 >= num4)
                            {
                                break;
                            }
                        }
                        if(num2 < num5)
                        {
                            short[] expr_8A_cp_0 = blTree.freqs;
                            int expr_8A_cp_1 = num;
                            expr_8A_cp_0[expr_8A_cp_1] += (short)num2;
                        }
                        else if(num != 0)
                        {
                            short[] expr_AB_cp_0 = blTree.freqs;
                            int expr_AB_cp_1 = 16;
                            expr_AB_cp_0[expr_AB_cp_1] += 1;
                        }
                        else if(num2 <= 10)
                        {
                            short[] expr_CD_cp_0 = blTree.freqs;
                            int expr_CD_cp_1 = 17;
                            expr_CD_cp_0[expr_CD_cp_1] += 1;
                        }
                        else
                        {
                            short[] expr_EA_cp_0 = blTree.freqs;
                            int expr_EA_cp_1 = 18;
                            expr_EA_cp_0[expr_EA_cp_1] += 1;
                        }
                    }
                }

                public void WriteTree(SimpleZip.DeflaterHuffman.Tree blTree)
                {
                    int num = -1;
                    int i = 0;
                    while(i < this.numCodes)
                    {
                        int num2 = 1;
                        int num3 = (int)this.length[i];
                        int num4;
                        int num5;
                        if(num3 == 0)
                        {
                            num4 = 138;
                            num5 = 3;
                        }
                        else
                        {
                            num4 = 6;
                            num5 = 3;
                            if(num != num3)
                            {
                                blTree.WriteSymbol(num3);
                                num2 = 0;
                            }
                        }
                        num = num3;
                        i++;
                        while(i < this.numCodes && num == (int)this.length[i])
                        {
                            i++;
                            if(++num2 >= num4)
                            {
                                break;
                            }
                        }
                        if(num2 < num5)
                        {
                            while(num2-- > 0)
                            {
                                blTree.WriteSymbol(num);
                            }
                        }
                        else if(num != 0)
                        {
                            blTree.WriteSymbol(16);
                            this.dh.pending.WriteBits(num2 - 3, 2);
                        }
                        else if(num2 <= 10)
                        {
                            blTree.WriteSymbol(17);
                            this.dh.pending.WriteBits(num2 - 3, 3);
                        }
                        else
                        {
                            blTree.WriteSymbol(18);
                            this.dh.pending.WriteBits(num2 - 11, 7);
                        }
                    }
                }
            }

            private const int BUFSIZE = 16384;

            private const int LITERAL_NUM = 286;

            private const int DIST_NUM = 30;

            private const int BITLEN_NUM = 19;

            private const int REP_3_6 = 16;

            private const int REP_3_10 = 17;

            private const int REP_11_138 = 18;

            private const int EOF_SYMBOL = 256;

            private static readonly int[] BL_ORDER;

            private static readonly byte[] bit4Reverse;

            private SimpleZip.DeflaterPending pending;

            private SimpleZip.DeflaterHuffman.Tree literalTree;

            private SimpleZip.DeflaterHuffman.Tree distTree;

            private SimpleZip.DeflaterHuffman.Tree blTree;

            private short[] d_buf;

            private byte[] l_buf;

            private int last_lit;

            private int extra_bits;

            private static readonly short[] staticLCodes;

            private static readonly byte[] staticLLength;

            private static readonly short[] staticDCodes;

            private static readonly byte[] staticDLength;

            public static short BitReverse(int toReverse)
            {
                return (short)((int)SimpleZip.DeflaterHuffman.bit4Reverse[toReverse & 15] << 12 | (int)SimpleZip.DeflaterHuffman.bit4Reverse[toReverse >> 4 & 15] << 8 | (int)SimpleZip.DeflaterHuffman.bit4Reverse[toReverse >> 8 & 15] << 4 | (int)SimpleZip.DeflaterHuffman.bit4Reverse[toReverse >> 12]);
            }

            static DeflaterHuffman()
            {
                SimpleZip.DeflaterHuffman.BL_ORDER = new int[]
                {
                    16,
                    17,
                    18,
                    0,
                    8,
                    7,
                    9,
                    6,
                    10,
                    5,
                    11,
                    4,
                    12,
                    3,
                    13,
                    2,
                    14,
                    1,
                    15
                };
                SimpleZip.DeflaterHuffman.bit4Reverse = new byte[]
                {
                    0,
                    8,
                    4,
                    12,
                    2,
                    10,
                    6,
                    14,
                    1,
                    9,
                    5,
                    13,
                    3,
                    11,
                    7,
                    15
                };
                SimpleZip.DeflaterHuffman.staticLCodes = new short[286];
                SimpleZip.DeflaterHuffman.staticLLength = new byte[286];
                int i = 0;
                while(i < 144)
                {
                    SimpleZip.DeflaterHuffman.staticLCodes[i] = SimpleZip.DeflaterHuffman.BitReverse(48 + i << 8);
                    SimpleZip.DeflaterHuffman.staticLLength[i++] = 8;
                }
                while(i < 256)
                {
                    SimpleZip.DeflaterHuffman.staticLCodes[i] = SimpleZip.DeflaterHuffman.BitReverse(256 + i << 7);
                    SimpleZip.DeflaterHuffman.staticLLength[i++] = 9;
                }
                while(i < 280)
                {
                    SimpleZip.DeflaterHuffman.staticLCodes[i] = SimpleZip.DeflaterHuffman.BitReverse(-256 + i << 9);
                    SimpleZip.DeflaterHuffman.staticLLength[i++] = 7;
                }
                while(i < 286)
                {
                    SimpleZip.DeflaterHuffman.staticLCodes[i] = SimpleZip.DeflaterHuffman.BitReverse(-88 + i << 8);
                    SimpleZip.DeflaterHuffman.staticLLength[i++] = 8;
                }
                SimpleZip.DeflaterHuffman.staticDCodes = new short[30];
                SimpleZip.DeflaterHuffman.staticDLength = new byte[30];
                for(i = 0; i < 30; i++)
                {
                    SimpleZip.DeflaterHuffman.staticDCodes[i] = SimpleZip.DeflaterHuffman.BitReverse(i << 11);
                    SimpleZip.DeflaterHuffman.staticDLength[i] = 5;
                }
            }

            public DeflaterHuffman(SimpleZip.DeflaterPending pending)
            {
                this.pending = pending;
                this.literalTree = new SimpleZip.DeflaterHuffman.Tree(this, 286, 257, 15);
                this.distTree = new SimpleZip.DeflaterHuffman.Tree(this, 30, 1, 15);
                this.blTree = new SimpleZip.DeflaterHuffman.Tree(this, 19, 4, 7);
                this.d_buf = new short[16384];
                this.l_buf = new byte[16384];
            }

            public void Init()
            {
                this.last_lit = 0;
                this.extra_bits = 0;
            }

            private int Lcode(int len)
            {
                if(len == 255)
                {
                    return 285;
                }
                int num = 257;
                while(len >= 8)
                {
                    num += 4;
                    len >>= 1;
                }
                return num + len;
            }

            private int Dcode(int distance)
            {
                int num = 0;
                while(distance >= 4)
                {
                    num += 2;
                    distance >>= 1;
                }
                return num + distance;
            }

            public void SendAllTrees(int blTreeCodes)
            {
                this.blTree.BuildCodes();
                this.literalTree.BuildCodes();
                this.distTree.BuildCodes();
                this.pending.WriteBits(this.literalTree.numCodes - 257, 5);
                this.pending.WriteBits(this.distTree.numCodes - 1, 5);
                this.pending.WriteBits(blTreeCodes - 4, 4);
                for(int i = 0; i < blTreeCodes; i++)
                {
                    this.pending.WriteBits((int)this.blTree.length[SimpleZip.DeflaterHuffman.BL_ORDER[i]], 3);
                }
                this.literalTree.WriteTree(this.blTree);
                this.distTree.WriteTree(this.blTree);
            }

            public void CompressBlock()
            {
                for(int i = 0; i < this.last_lit; i++)
                {
                    int num = (int)(this.l_buf[i] & 255);
                    int num2 = (int)this.d_buf[i];
                    if(num2-- != 0)
                    {
                        int num3 = this.Lcode(num);
                        this.literalTree.WriteSymbol(num3);
                        int num4 = (num3 - 261) / 4;
                        if(num4 > 0 && num4 <= 5)
                        {
                            this.pending.WriteBits(num & (1 << num4) - 1, num4);
                        }
                        int num5 = this.Dcode(num2);
                        this.distTree.WriteSymbol(num5);
                        num4 = num5 / 2 - 1;
                        if(num4 > 0)
                        {
                            this.pending.WriteBits(num2 & (1 << num4) - 1, num4);
                        }
                    }
                    else
                    {
                        this.literalTree.WriteSymbol(num);
                    }
                }
                this.literalTree.WriteSymbol(256);
            }

            public void FlushStoredBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
            {
                this.pending.WriteBits(lastBlock ? 1 : 0, 3);
                this.pending.AlignToByte();
                this.pending.WriteShort(storedLength);
                this.pending.WriteShort(~storedLength);
                this.pending.WriteBlock(stored, storedOffset, storedLength);
                this.Init();
            }

            public void FlushBlock(byte[] stored, int storedOffset, int storedLength, bool lastBlock)
            {
                short[] expr_15_cp_0 = this.literalTree.freqs;
                int expr_15_cp_1 = 256;
                expr_15_cp_0[expr_15_cp_1] += 1;
                this.literalTree.BuildTree();
                this.distTree.BuildTree();
                this.literalTree.CalcBLFreq(this.blTree);
                this.distTree.CalcBLFreq(this.blTree);
                this.blTree.BuildTree();
                int num = 4;
                for(int i = 18; i > num; i--)
                {
                    if(this.blTree.length[SimpleZip.DeflaterHuffman.BL_ORDER[i]] > 0)
                    {
                        num = i + 1;
                    }
                }
                int num2 = 14 + num * 3 + this.blTree.GetEncodedLength() + this.literalTree.GetEncodedLength() + this.distTree.GetEncodedLength() + this.extra_bits;
                int num3 = this.extra_bits;
                for(int j = 0; j < 286; j++)
                {
                    num3 += (int)(this.literalTree.freqs[j] * (short)SimpleZip.DeflaterHuffman.staticLLength[j]);
                }
                for(int k = 0; k < 30; k++)
                {
                    num3 += (int)(this.distTree.freqs[k] * (short)SimpleZip.DeflaterHuffman.staticDLength[k]);
                }
                if(num2 >= num3)
                {
                    num2 = num3;
                }
                if(storedOffset >= 0 && storedLength + 4 < num2 >> 3)
                {
                    this.FlushStoredBlock(stored, storedOffset, storedLength, lastBlock);
                    return;
                }
                if(num2 == num3)
                {
                    this.pending.WriteBits(2 + (lastBlock ? 1 : 0), 3);
                    this.literalTree.SetStaticCodes(SimpleZip.DeflaterHuffman.staticLCodes, SimpleZip.DeflaterHuffman.staticLLength);
                    this.distTree.SetStaticCodes(SimpleZip.DeflaterHuffman.staticDCodes, SimpleZip.DeflaterHuffman.staticDLength);
                    this.CompressBlock();
                    this.Init();
                    return;
                }
                this.pending.WriteBits(4 + (lastBlock ? 1 : 0), 3);
                this.SendAllTrees(num);
                this.CompressBlock();
                this.Init();
            }

            public bool IsFull()
            {
                return this.last_lit >= 16384;
            }

            public bool TallyLit(int lit)
            {
                this.d_buf[this.last_lit] = 0;
                this.l_buf[this.last_lit++] = (byte)lit;
                short[] expr_39_cp_0 = this.literalTree.freqs;
                expr_39_cp_0[lit] += 1;
                return this.IsFull();
            }

            public bool TallyDist(int dist, int len)
            {
                this.d_buf[this.last_lit] = (short)dist;
                this.l_buf[this.last_lit++] = (byte)(len - 3);
                int num = this.Lcode(len - 3);
                short[] expr_46_cp_0 = this.literalTree.freqs;
                int expr_46_cp_1 = num;
                expr_46_cp_0[expr_46_cp_1] += 1;
                if(num >= 265 && num < 285)
                {
                    this.extra_bits += (num - 261) / 4;
                }
                int num2 = this.Dcode(dist - 1);
                short[] expr_95_cp_0 = this.distTree.freqs;
                int expr_95_cp_1 = num2;
                expr_95_cp_0[expr_95_cp_1] += 1;
                if(num2 >= 4)
                {
                    this.extra_bits += num2 / 2 - 1;
                }
                return this.IsFull();
            }
        }

        internal class DeflaterEngine
        {
            private const int MAX_MATCH = 258;

            private const int MIN_MATCH = 3;

            private const int WSIZE = 32768;

            private const int WMASK = 32767;

            private const int HASH_SIZE = 32768;

            private const int HASH_MASK = 32767;

            private const int HASH_SHIFT = 5;

            private const int MIN_LOOKAHEAD = 262;

            private const int MAX_DIST = 32506;

            private const int TOO_FAR = 4096;

            private int ins_h;

            private short[] head;

            private short[] prev;

            private int matchStart;

            private int matchLen;

            private bool prevAvailable;

            private int blockStart;

            private int strstart;

            private int lookahead;

            private byte[] window;

            private byte[] inputBuf;

            private int totalIn;

            private int inputOff;

            private int inputEnd;

            private SimpleZip.DeflaterPending pending;

            private SimpleZip.DeflaterHuffman huffman;

            public DeflaterEngine(SimpleZip.DeflaterPending pending)
            {
                this.pending = pending;
                this.huffman = new SimpleZip.DeflaterHuffman(pending);
                this.window = new byte[65536];
                this.head = new short[32768];
                this.prev = new short[32768];
                this.blockStart = (this.strstart = 1);
            }

            private void UpdateHash()
            {
                this.ins_h = ((int)this.window[this.strstart] << 5 ^ (int)this.window[this.strstart + 1]);
            }

            private int InsertString()
            {
                int num = (this.ins_h << 5 ^ (int)this.window[this.strstart + 2]) & 32767;
                short num2 = this.prev[this.strstart & 32767] = this.head[num];
                this.head[num] = (short)this.strstart;
                this.ins_h = num;
                return (int)num2 & 65535;
            }

            private void SlideWindow()
            {
                Array.Copy(this.window, 32768, this.window, 0, 32768);
                this.matchStart -= 32768;
                this.strstart -= 32768;
                this.blockStart -= 32768;
                for(int i = 0; i < 32768; i++)
                {
                    int num = (int)this.head[i] & 65535;
                    this.head[i] = (short)((num >= 32768) ? (num - 32768) : 0);
                }
                for(int j = 0; j < 32768; j++)
                {
                    int num2 = (int)this.prev[j] & 65535;
                    this.prev[j] = (short)((num2 >= 32768) ? (num2 - 32768) : 0);
                }
            }

            public void FillWindow()
            {
                if(this.strstart >= 65274)
                {
                    this.SlideWindow();
                }
                while(this.lookahead < 262 && this.inputOff < this.inputEnd)
                {
                    int num = 65536 - this.lookahead - this.strstart;
                    if(num > this.inputEnd - this.inputOff)
                    {
                        num = this.inputEnd - this.inputOff;
                    }
                    Array.Copy(this.inputBuf, this.inputOff, this.window, this.strstart + this.lookahead, num);
                    this.inputOff += num;
                    this.totalIn += num;
                    this.lookahead += num;
                }
                if(this.lookahead >= 3)
                {
                    this.UpdateHash();
                }
            }

            private bool FindLongestMatch(int curMatch)
            {
                int num = 128;
                int num2 = 128;
                short[] array = this.prev;
                int num3 = this.strstart;
                int num4 = this.strstart + this.matchLen;
                int num5 = Math.Max(this.matchLen, 2);
                int num6 = Math.Max(this.strstart - 32506, 0);
                int num7 = this.strstart + 258 - 1;
                byte b = this.window[num4 - 1];
                byte b2 = this.window[num4];
                if(num5 >= 8)
                {
                    num >>= 2;
                }
                if(num2 > this.lookahead)
                {
                    num2 = this.lookahead;
                }
                do
                {
                    if(this.window[curMatch + num5] == b2 && this.window[curMatch + num5 - 1] == b && this.window[curMatch] == this.window[num3] && this.window[curMatch + 1] == this.window[num3 + 1])
                    {
                        int num8 = curMatch + 2;
                        num3 += 2;
                        while(this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && this.window[++num3] == this.window[++num8] && num3 < num7)
                        {
                        }
                        if(num3 > num4)
                        {
                            this.matchStart = curMatch;
                            num4 = num3;
                            num5 = num3 - this.strstart;
                            if(num5 >= num2)
                            {
                                break;
                            }
                            b = this.window[num4 - 1];
                            b2 = this.window[num4];
                        }
                        num3 = this.strstart;
                    }
                }
                while((curMatch = ((int)array[curMatch & 32767] & 65535)) > num6 && --num != 0);
                this.matchLen = Math.Min(num5, this.lookahead);
                return this.matchLen >= 3;
            }

            private bool DeflateSlow(bool flush, bool finish)
            {
                if(this.lookahead < 262 && !flush)
                {
                    return false;
                }
                while(this.lookahead >= 262 || flush)
                {
                    if(this.lookahead == 0)
                    {
                        if(this.prevAvailable)
                        {
                            this.huffman.TallyLit((int)(this.window[this.strstart - 1] & 255));
                        }
                        this.prevAvailable = false;
                        this.huffman.FlushBlock(this.window, this.blockStart, this.strstart - this.blockStart, finish);
                        this.blockStart = this.strstart;
                        return false;
                    }
                    if(this.strstart >= 65274)
                    {
                        this.SlideWindow();
                    }
                    int num = this.matchStart;
                    int num2 = this.matchLen;
                    if(this.lookahead >= 3)
                    {
                        int num3 = this.InsertString();
                        if(num3 != 0 && this.strstart - num3 <= 32506 && this.FindLongestMatch(num3) && this.matchLen <= 5 && this.matchLen == 3 && this.strstart - this.matchStart > 4096)
                        {
                            this.matchLen = 2;
                        }
                    }
                    if(num2 >= 3 && this.matchLen <= num2)
                    {
                        this.huffman.TallyDist(this.strstart - 1 - num, num2);
                        num2 -= 2;
                        do
                        {
                            this.strstart++;
                            this.lookahead--;
                            if(this.lookahead >= 3)
                            {
                                this.InsertString();
                            }
                        }
                        while(--num2 > 0);
                        this.strstart++;
                        this.lookahead--;
                        this.prevAvailable = false;
                        this.matchLen = 2;
                    }
                    else
                    {
                        if(this.prevAvailable)
                        {
                            this.huffman.TallyLit((int)(this.window[this.strstart - 1] & 255));
                        }
                        this.prevAvailable = true;
                        this.strstart++;
                        this.lookahead--;
                    }
                    if(this.huffman.IsFull())
                    {
                        int num4 = this.strstart - this.blockStart;
                        if(this.prevAvailable)
                        {
                            num4--;
                        }
                        bool flag = finish && this.lookahead == 0 && !this.prevAvailable;
                        this.huffman.FlushBlock(this.window, this.blockStart, num4, flag);
                        this.blockStart += num4;
                        return !flag;
                    }
                }
                return true;
            }

            public bool Deflate(bool flush, bool finish)
            {
                bool flag;
                do
                {
                    this.FillWindow();
                    bool flush2 = flush && this.inputOff == this.inputEnd;
                    flag = this.DeflateSlow(flush2, finish);
                }
                while(this.pending.IsFlushed && flag);
                return flag;
            }

            public void SetInput(byte[] buffer)
            {
                this.inputBuf = buffer;
                this.inputOff = 0;
                this.inputEnd = buffer.Length;
            }

            public bool NeedsInput()
            {
                return this.inputEnd == this.inputOff;
            }
        }

        internal class DeflaterPending
        {
            protected byte[] buf = new byte[65536];

            private int start;

            private int end;

            private uint bits;

            private int bitCount;

            public int BitCount
            {
                get
                {
                    return this.bitCount;
                }
            }

            public bool IsFlushed
            {
                get
                {
                    return this.end == 0;
                }
            }

            public void WriteShort(int s)
            {
                this.buf[this.end++] = (byte)s;
                this.buf[this.end++] = (byte)(s >> 8);
            }

            public void WriteBlock(byte[] block, int offset, int len)
            {
                Array.Copy(block, offset, this.buf, this.end, len);
                this.end += len;
            }

            public void AlignToByte()
            {
                if(this.bitCount > 0)
                {
                    this.buf[this.end++] = (byte)this.bits;
                    if(this.bitCount > 8)
                    {
                        this.buf[this.end++] = (byte)(this.bits >> 8);
                    }
                }
                this.bits = 0u;
                this.bitCount = 0;
            }

            public void WriteBits(int b, int count)
            {
                this.bits |= (uint)((uint)b << this.bitCount);
                this.bitCount += count;
                if(this.bitCount >= 16)
                {
                    this.buf[this.end++] = (byte)this.bits;
                    this.buf[this.end++] = (byte)(this.bits >> 8);
                    this.bits >>= 16;
                    this.bitCount -= 16;
                }
            }

            public int Flush(byte[] output, int offset, int length)
            {
                if(this.bitCount >= 8)
                {
                    this.buf[this.end++] = (byte)this.bits;
                    this.bits >>= 8;
                    this.bitCount -= 8;
                }
                if(length > this.end - this.start)
                {
                    length = this.end - this.start;
                    Array.Copy(this.buf, this.start, output, offset, length);
                    this.start = 0;
                    this.end = 0;
                }
                else
                {
                    Array.Copy(this.buf, this.start, output, offset, length);
                    this.start += length;
                }
                return length;
            }
        }

        internal class ZipStream : MemoryStream
        {
            public void WriteShort(int value)
            {
                this.WriteByte((byte)(value & 255));
                this.WriteByte((byte)(value >> 8 & 255));
            }

            public void WriteInt(int value)
            {
                this.WriteShort(value);
                this.WriteShort(value >> 16);
            }

            public int ReadShort()
            {
                return this.ReadByte() | this.ReadByte() << 8;
            }

            public int ReadInt()
            {
                return this.ReadShort() | this.ReadShort() << 16;
            }

            public ZipStream()
            {
            }

            public ZipStream(byte[] buffer) : base(buffer, false)
            {
            }
        }

        public static string ExceptionMessage;

        private static bool PublicKeysMatch(Assembly executingAssembly, Assembly callingAssembly)
        {
            byte[] publicKey = executingAssembly.GetName().GetPublicKey();
            byte[] publicKey2 = callingAssembly.GetName().GetPublicKey();
            if(publicKey2 == null != (publicKey == null))
            {
                return false;
            }
            if(publicKey2 != null)
            {
                for(int i = 0; i < publicKey2.Length; i++)
                {
                    if(publicKey2[i] != publicKey[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static ICryptoTransform GetAesTransform(byte[] key, byte[] iv, bool decrypt)
        {
            ICryptoTransform result;
            using(SymmetricAlgorithm symmetricAlgorithm = new RijndaelManaged())
            {
                result = (decrypt ? symmetricAlgorithm.CreateDecryptor(key, iv) : symmetricAlgorithm.CreateEncryptor(key, iv));
            }
            return result;
        }

        private static ICryptoTransform GetDesTransform(byte[] key, byte[] iv, bool decrypt)
        {
            ICryptoTransform result;
            using(DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider())
            {
                result = (decrypt ? dESCryptoServiceProvider.CreateDecryptor(key, iv) : dESCryptoServiceProvider.CreateEncryptor(key, iv));
            }
            return result;
        }

        public static byte[] Unzip(byte[] buffer)
        {
            Assembly callingAssembly = Assembly.GetCallingAssembly();
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            if(callingAssembly != executingAssembly && !SimpleZip.PublicKeysMatch(executingAssembly, callingAssembly))
            {
                return null;
            }
            SimpleZip.ZipStream zipStream = new SimpleZip.ZipStream(buffer);
            byte[] array = new byte[0];
            int num = zipStream.ReadInt();
            if(num != 67324752)
            {
                int num2 = num >> 24;
                num -= num2 << 24;
                if(num == 8223355)
                {
                    if(num2 == 1)
                    {
                        int num3 = zipStream.ReadInt();
                        array = new byte[num3];
                        int num5;
                        for(int i = 0; i < num3; i += num5)
                        {
                            int num4 = zipStream.ReadInt();
                            num5 = zipStream.ReadInt();
                            byte[] array2 = new byte[num4];
                            zipStream.Read(array2, 0, array2.Length);
                            SimpleZip.Inflater inflater = new SimpleZip.Inflater(array2);
                            inflater.Inflate(array, i, num5);
                        }
                    }
                    if(num2 == 2)
                    {
                        byte[] key = new byte[]
                        {
                            154,
                            41,
                            66,
                            148,
                            125,
                            6,
                            16,
                            238
                        };
                        byte[] iv = new byte[]
                        {
                            223,
                            25,
                            36,
                            206,
                            144,
                            228,
                            102,
                            121
                        };
                        using(ICryptoTransform desTransform = SimpleZip.GetDesTransform(key, iv, true))
                        {
                            byte[] buffer2 = desTransform.TransformFinalBlock(buffer, 4, buffer.Length - 4);
                            array = SimpleZip.Unzip(buffer2);
                        }
                    }
                    if(num2 != 3)
                    {
                        goto IL_26B;
                    }
                    byte[] key2 = new byte[]
                    {
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1,
                        1
                    };
                    byte[] iv2 = new byte[]
                    {
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        2
                    };
                    using(ICryptoTransform aesTransform = SimpleZip.GetAesTransform(key2, iv2, true))
                    {
                        byte[] buffer3 = aesTransform.TransformFinalBlock(buffer, 4, buffer.Length - 4);
                        array = SimpleZip.Unzip(buffer3);
                        goto IL_26B;
                    }
                }
                throw new FormatException("Unknown Header");
            }
            short num6 = (short)zipStream.ReadShort();
            int num7 = zipStream.ReadShort();
            int num8 = zipStream.ReadShort();
            if(num != 67324752 || num6 != 20 || num7 != 0 || num8 != 8)
            {
                throw new FormatException("Wrong Header Signature");
            }
            zipStream.ReadInt();
            zipStream.ReadInt();
            zipStream.ReadInt();
            int num9 = zipStream.ReadInt();
            int num10 = zipStream.ReadShort();
            int num11 = zipStream.ReadShort();
            if(num10 > 0)
            {
                byte[] buffer4 = new byte[num10];
                zipStream.Read(buffer4, 0, num10);
            }
            if(num11 > 0)
            {
                byte[] buffer5 = new byte[num11];
                zipStream.Read(buffer5, 0, num11);
            }
            byte[] array3 = new byte[zipStream.Length - zipStream.Position];
            zipStream.Read(array3, 0, array3.Length);
            SimpleZip.Inflater inflater2 = new SimpleZip.Inflater(array3);
            array = new byte[num9];
            inflater2.Inflate(array, 0, array.Length);
        IL_26B:
            zipStream.Close();
            zipStream = null;
            return array;
        }

        public static byte[] Zip(byte[] buffer)
        {
            return SimpleZip.Zip(buffer, 1, null, null);
        }

        public static byte[] ZipAndEncrypt(byte[] buffer, byte[] key, byte[] iv)
        {
            return SimpleZip.Zip(buffer, 2, key, iv);
        }

        public static byte[] ZipAndAES(byte[] buffer, byte[] key, byte[] iv)
        {
            return SimpleZip.Zip(buffer, 3, key, iv);
        }

        private static byte[] Zip(byte[] buffer, int version, byte[] key, byte[] iv)
        {
            byte[] result;
            try
            {
                SimpleZip.ZipStream zipStream = new SimpleZip.ZipStream();
                if(version == 0)
                {
                    SimpleZip.Deflater deflater = new SimpleZip.Deflater();
                    DateTime now = DateTime.Now;
                    long num = (long)((ulong)((now.Year - 1980 & 127) << 25 | now.Month << 21 | now.Day << 16 | now.Hour << 11 | now.Minute << 5 | (int)((uint)now.Second >> 1)));
                    uint[] array = new uint[]
                    {
                        0u,
                        1996959894u,
                        3993919788u,
                        2567524794u,
                        124634137u,
                        1886057615u,
                        3915621685u,
                        2657392035u,
                        249268274u,
                        2044508324u,
                        3772115230u,
                        2547177864u,
                        162941995u,
                        2125561021u,
                        3887607047u,
                        2428444049u,
                        498536548u,
                        1789927666u,
                        4089016648u,
                        2227061214u,
                        450548861u,
                        1843258603u,
                        4107580753u,
                        2211677639u,
                        325883990u,
                        1684777152u,
                        4251122042u,
                        2321926636u,
                        335633487u,
                        1661365465u,
                        4195302755u,
                        2366115317u,
                        997073096u,
                        1281953886u,
                        3579855332u,
                        2724688242u,
                        1006888145u,
                        1258607687u,
                        3524101629u,
                        2768942443u,
                        901097722u,
                        1119000684u,
                        3686517206u,
                        2898065728u,
                        853044451u,
                        1172266101u,
                        3705015759u,
                        2882616665u,
                        651767980u,
                        1373503546u,
                        3369554304u,
                        3218104598u,
                        565507253u,
                        1454621731u,
                        3485111705u,
                        3099436303u,
                        671266974u,
                        1594198024u,
                        3322730930u,
                        2970347812u,
                        795835527u,
                        1483230225u,
                        3244367275u,
                        3060149565u,
                        1994146192u,
                        31158534u,
                        2563907772u,
                        4023717930u,
                        1907459465u,
                        112637215u,
                        2680153253u,
                        3904427059u,
                        2013776290u,
                        251722036u,
                        2517215374u,
                        3775830040u,
                        2137656763u,
                        141376813u,
                        2439277719u,
                        3865271297u,
                        1802195444u,
                        476864866u,
                        2238001368u,
                        4066508878u,
                        1812370925u,
                        453092731u,
                        2181625025u,
                        4111451223u,
                        1706088902u,
                        314042704u,
                        2344532202u,
                        4240017532u,
                        1658658271u,
                        366619977u,
                        2362670323u,
                        4224994405u,
                        1303535960u,
                        984961486u,
                        2747007092u,
                        3569037538u,
                        1256170817u,
                        1037604311u,
                        2765210733u,
                        3554079995u,
                        1131014506u,
                        879679996u,
                        2909243462u,
                        3663771856u,
                        1141124467u,
                        855842277u,
                        2852801631u,
                        3708648649u,
                        1342533948u,
                        654459306u,
                        3188396048u,
                        3373015174u,
                        1466479909u,
                        544179635u,
                        3110523913u,
                        3462522015u,
                        1591671054u,
                        702138776u,
                        2966460450u,
                        3352799412u,
                        1504918807u,
                        783551873u,
                        3082640443u,
                        3233442989u,
                        3988292384u,
                        2596254646u,
                        62317068u,
                        1957810842u,
                        3939845945u,
                        2647816111u,
                        81470997u,
                        1943803523u,
                        3814918930u,
                        2489596804u,
                        225274430u,
                        2053790376u,
                        3826175755u,
                        2466906013u,
                        167816743u,
                        2097651377u,
                        4027552580u,
                        2265490386u,
                        503444072u,
                        1762050814u,
                        4150417245u,
                        2154129355u,
                        426522225u,
                        1852507879u,
                        4275313526u,
                        2312317920u,
                        282753626u,
                        1742555852u,
                        4189708143u,
                        2394877945u,
                        397917763u,
                        1622183637u,
                        3604390888u,
                        2714866558u,
                        953729732u,
                        1340076626u,
                        3518719985u,
                        2797360999u,
                        1068828381u,
                        1219638859u,
                        3624741850u,
                        2936675148u,
                        906185462u,
                        1090812512u,
                        3747672003u,
                        2825379669u,
                        829329135u,
                        1181335161u,
                        3412177804u,
                        3160834842u,
                        628085408u,
                        1382605366u,
                        3423369109u,
                        3138078467u,
                        570562233u,
                        1426400815u,
                        3317316542u,
                        2998733608u,
                        733239954u,
                        1555261956u,
                        3268935591u,
                        3050360625u,
                        752459403u,
                        1541320221u,
                        2607071920u,
                        3965973030u,
                        1969922972u,
                        40735498u,
                        2617837225u,
                        3943577151u,
                        1913087877u,
                        83908371u,
                        2512341634u,
                        3803740692u,
                        2075208622u,
                        213261112u,
                        2463272603u,
                        3855990285u,
                        2094854071u,
                        198958881u,
                        2262029012u,
                        4057260610u,
                        1759359992u,
                        534414190u,
                        2176718541u,
                        4139329115u,
                        1873836001u,
                        414664567u,
                        2282248934u,
                        4279200368u,
                        1711684554u,
                        285281116u,
                        2405801727u,
                        4167216745u,
                        1634467795u,
                        376229701u,
                        2685067896u,
                        3608007406u,
                        1308918612u,
                        956543938u,
                        2808555105u,
                        3495958263u,
                        1231636301u,
                        1047427035u,
                        2932959818u,
                        3654703836u,
                        1088359270u,
                        936918000u,
                        2847714899u,
                        3736837829u,
                        1202900863u,
                        817233897u,
                        3183342108u,
                        3401237130u,
                        1404277552u,
                        615818150u,
                        3134207493u,
                        3453421203u,
                        1423857449u,
                        601450431u,
                        3009837614u,
                        3294710456u,
                        1567103746u,
                        711928724u,
                        3020668471u,
                        3272380065u,
                        1510334235u,
                        755167117u
                    };
                    uint num2 = 4294967295u;
                    uint num3 = num2;
                    int num4 = 0;
                    int num5 = buffer.Length;
                    while(--num5 >= 0)
                    {
                        num3 = (array[(int)((UIntPtr)((num3 ^ (uint)buffer[num4++]) & 255u))] ^ num3 >> 8);
                    }
                    num3 ^= num2;
                    zipStream.WriteInt(67324752);
                    zipStream.WriteShort(20);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(8);
                    zipStream.WriteInt((int)num);
                    zipStream.WriteInt((int)num3);
                    long position = zipStream.Position;
                    zipStream.WriteInt(0);
                    zipStream.WriteInt(buffer.Length);
                    byte[] bytes = Encoding.UTF8.GetBytes("{data}");
                    zipStream.WriteShort(bytes.Length);
                    zipStream.WriteShort(0);
                    zipStream.Write(bytes, 0, bytes.Length);
                    deflater.SetInput(buffer);
                    while(!deflater.IsNeedingInput)
                    {
                        byte[] array2 = new byte[512];
                        int num6 = deflater.Deflate(array2);
                        if(num6 <= 0)
                        {
                            break;
                        }
                        zipStream.Write(array2, 0, num6);
                    }
                    deflater.Finish();
                    while(!deflater.IsFinished)
                    {
                        byte[] array3 = new byte[512];
                        int num7 = deflater.Deflate(array3);
                        if(num7 <= 0)
                        {
                            break;
                        }
                        zipStream.Write(array3, 0, num7);
                    }
                    long totalOut = deflater.TotalOut;
                    zipStream.WriteInt(33639248);
                    zipStream.WriteShort(20);
                    zipStream.WriteShort(20);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(8);
                    zipStream.WriteInt((int)num);
                    zipStream.WriteInt((int)num3);
                    zipStream.WriteInt((int)totalOut);
                    zipStream.WriteInt(buffer.Length);
                    zipStream.WriteShort(bytes.Length);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(0);
                    zipStream.WriteInt(0);
                    zipStream.WriteInt(0);
                    zipStream.Write(bytes, 0, bytes.Length);
                    zipStream.WriteInt(101010256);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(0);
                    zipStream.WriteShort(1);
                    zipStream.WriteShort(1);
                    zipStream.WriteInt(46 + bytes.Length);
                    zipStream.WriteInt((int)((long)(30 + bytes.Length) + totalOut));
                    zipStream.WriteShort(0);
                    zipStream.Seek(position, SeekOrigin.Begin);
                    zipStream.WriteInt((int)totalOut);
                }
                else if(version == 1)
                {
                    zipStream.WriteInt(25000571);
                    zipStream.WriteInt(buffer.Length);
                    byte[] array4;
                    for(int i = 0; i < buffer.Length; i += array4.Length)
                    {
                        array4 = new byte[Math.Min(2097151, buffer.Length - i)];
                        Buffer.BlockCopy(buffer, i, array4, 0, array4.Length);
                        long position2 = zipStream.Position;
                        zipStream.WriteInt(0);
                        zipStream.WriteInt(array4.Length);
                        SimpleZip.Deflater deflater2 = new SimpleZip.Deflater();
                        deflater2.SetInput(array4);
                        while(!deflater2.IsNeedingInput)
                        {
                            byte[] array5 = new byte[512];
                            int num8 = deflater2.Deflate(array5);
                            if(num8 <= 0)
                            {
                                break;
                            }
                            zipStream.Write(array5, 0, num8);
                        }
                        deflater2.Finish();
                        while(!deflater2.IsFinished)
                        {
                            byte[] array6 = new byte[512];
                            int num9 = deflater2.Deflate(array6);
                            if(num9 <= 0)
                            {
                                break;
                            }
                            zipStream.Write(array6, 0, num9);
                        }
                        long position3 = zipStream.Position;
                        zipStream.Position = position2;
                        zipStream.WriteInt((int)deflater2.TotalOut);
                        zipStream.Position = position3;
                    }
                }
                else
                {
                    if(version == 2)
                    {
                        zipStream.WriteInt(41777787);
                        byte[] array7 = SimpleZip.Zip(buffer, 1, null, null);
                        using(ICryptoTransform desTransform = SimpleZip.GetDesTransform(key, iv, false))
                        {
                            byte[] array8 = desTransform.TransformFinalBlock(array7, 0, array7.Length);
                            zipStream.Write(array8, 0, array8.Length);
                            goto IL_44F;
                        }
                    }
                    if(version == 3)
                    {
                        zipStream.WriteInt(58555003);
                        byte[] array9 = SimpleZip.Zip(buffer, 1, null, null);
                        using(ICryptoTransform aesTransform = SimpleZip.GetAesTransform(key, iv, false))
                        {
                            byte[] array10 = aesTransform.TransformFinalBlock(array9, 0, array9.Length);
                            zipStream.Write(array10, 0, array10.Length);
                        }
                    }
                }
            IL_44F:
                zipStream.Flush();
                zipStream.Close();
                result = zipStream.ToArray();
            }
            catch(Exception ex)
            {
                SimpleZip.ExceptionMessage = "ERR 2003: " + ex.Message;
                throw;
            }
            return result;
        }
    }
}
