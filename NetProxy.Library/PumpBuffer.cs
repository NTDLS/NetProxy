namespace NetProxy.Library
{
    public class PumpBuffer
    {
        public byte[] Bytes { get; set; }
        public int Length { get; set; }

        public PumpBuffer(int initialSize)
        {
            Bytes = new byte[initialSize];
        }

        public void AutoResize(int maxBufferSize)
        {
            if (Length == Bytes.Length && Bytes.Length < maxBufferSize)
            {
                //If we read as much data as we could fit in the buffer, resize it a bit up to the maximum.
                int newBufferSize = (int)(Bytes.Length + (Bytes.Length * 0.20));
                if (newBufferSize > maxBufferSize)
                {
                    newBufferSize = maxBufferSize;
                }

                Bytes = new byte[newBufferSize];
            }
        }
    }
}


