using ChocolArm64.Memory;
using System.Collections.Concurrent;

namespace Ryujinx.Graphics.Memory
{
    class NvGpuVmmCache
    {
        private const int PageBits = MemoryManager.PageBits;

        private const long PageSize = MemoryManager.PageSize;
        private const long PageMask = MemoryManager.PageMask;

        private ConcurrentDictionary<long, int>[] CachedPages;

        private MemoryManager _memory;

        public NvGpuVmmCache(MemoryManager memory)
        {
            _memory = memory;

            CachedPages = new ConcurrentDictionary<long, int>[1 << 20];
        }

        public bool IsRegionModified(long position, long size, NvGpuBufferType bufferType)
        {
            long va = position;

            long endAddr = (va + size + PageMask) & ~PageMask;

            long addrTruncated = va & ~PageMask;

            bool modified = _memory.IsRegionModified(addrTruncated, endAddr - addrTruncated);

            int newBuffMask = 1 << (int)bufferType;

            long cachedPagesCount = 0;

            while (va < endAddr)
            {
                long pa = _memory.GetPhysicalAddress(va);

                long page = pa >> PageBits;

                ConcurrentDictionary<long, int> dictionary = CachedPages[page];

                if (dictionary == null)
                {
                    dictionary = new ConcurrentDictionary<long, int>();

                    CachedPages[page] = dictionary;
                }
                else if (modified)
                {
                    CachedPages[page].Clear();
                }

                if (dictionary.TryGetValue(pa, out int currBuffMask))
                {
                    if ((currBuffMask & newBuffMask) != 0)
                    {
                        cachedPagesCount++;
                    }
                    else
                    {
                        dictionary[pa] |= newBuffMask;
                    }
                }
                else
                {
                    dictionary[pa] = newBuffMask;
                }

                va += PageSize;
            }

            return cachedPagesCount != (endAddr - position + PageMask) >> PageBits;
        }
    }
}