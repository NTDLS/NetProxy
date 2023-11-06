namespace NetProxy.Hub.MessageFraming
{
    internal class FrameBuffer
    {
        /// <summary>
        /// The number of bytes in the current receive buffer.
        /// </summary>
        public int ReceiveBufferUsed = 0;
        /// <summary>
        /// The current receive buffer. May be more than one frame or even a partial frame.
        /// </summary>
        public byte[] ReceiveBuffer;

        /// <summary>
        /// The buffer used to build a full message from the frame. This will be automatically resized if its too small.
        /// </summary>
        public byte[] FrameBuilder;

        /// <summary>
        /// The length of the data currently contained in the PayloadBuilder.
        /// </summary>
        public int FrameBuilderLength = 0;

        public FrameBuffer(int framebufferSize)
        {
            ReceiveBuffer = new byte[framebufferSize];
            FrameBuilder = new byte[framebufferSize];
        }
    }
}
