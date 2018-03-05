namespace Neptun.VSTestAdapterLib
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Facilitates communication using sockets
    /// </summary>
    public class SocketCommunicationManager
    {
        /// <summary>
        /// TCP Listener to host TCP channel and listen
        /// </summary>
        private TcpListener tcpListener;

        /// <summary>
        /// Binary Writer to write to channel stream
        /// </summary>
        private BinaryWriter binaryWriter;

        /// <summary>
        /// Binary reader to read from channel stream
        /// </summary>
        private BinaryReader binaryReader;

        /// <summary>
        /// Event used to maintain client connection state
        /// </summary>
        private ManualResetEvent clientConnectedEvent = new ManualResetEvent(false);

        /// <summary>
        /// Sync object for sending messages
        /// SendMessage over socket channel is NOT thread-safe
        /// </summary>
        private object sendSyncObject = new object();

        /// <summary>
        /// Stream to use read timeout
        /// </summary>
        private NetworkStream stream;

        private Socket socket;

        /// <summary>
        /// The server stream read timeout constant (in microseconds).
        /// </summary>
        private const int StreamReadTimeout = 1000 * 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketCommunicationManager"/> class.
        /// </summary>
        public SocketCommunicationManager() { }

        #region ServerMethods

        /// <summary>
        /// Host TCP Socket Server and start listening
        /// </summary>
        /// <returns></returns>
        public int HostServer()
        {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 0);
            this.tcpListener = new TcpListener(endpoint);

            this.tcpListener.Start();
            var portNumber = ((IPEndPoint)this.tcpListener.LocalEndpoint).Port;
            Console.WriteLine("Server started. Listening at port : {0}", portNumber);
            return portNumber;
        }

        /// <summary>
        /// Accepts client async
        /// </summary>
        public async Task AcceptClientAsync()
        {
            if (this.tcpListener != null)
            {
                this.clientConnectedEvent.Reset();

                var client = await this.tcpListener.AcceptTcpClientAsync();
                this.socket = client.Client;
                this.stream = client.GetStream();
                this.binaryReader = new BinaryReader(this.stream);
                this.binaryWriter = new BinaryWriter(this.stream);

                this.clientConnectedEvent.Set();

                Console.WriteLine("Accepted Client request and set the flag");
            }
        }

        /// <summary>
        /// Waits for Client Connection
        /// </summary>
        /// <param name="clientConnectionTimeout">Time to Wait for the connection</param>
        /// <returns>True if Client is connected, false otherwise</returns>
        public bool WaitForClientConnection(int clientConnectionTimeout)
        {
            return this.clientConnectedEvent.WaitOne(clientConnectionTimeout);
        }

        /// <summary>
        /// Stop Listener
        /// </summary>
        public void StopServer()
        {
            this.tcpListener?.Stop();
            this.tcpListener = null;
            this.binaryReader?.Dispose();
            this.binaryWriter?.Dispose();
        }

        #endregion

        /// <summary>
        /// Reads message from the binary reader
        /// </summary>
        /// <returns> Raw message string </returns>
        public string ReceiveRawMessage()
        {
            var rawMessage = this.binaryReader.ReadString();
            Console.WriteLine("\n=========== Receiving Message ===========");
            Console.WriteLine(rawMessage);
            return rawMessage;
        }

        /// <summary>
        /// Writes the data on socket and flushes the buffer
        /// </summary>
        /// <param name="rawMessage">message to write</param>
        public void WriteAndFlushToChannel(string rawMessage)
        {
            // Writing Message on binarywriter is not Thread-Safe
            // Need to sync one by one to avoid buffer corruption
            lock (this.sendSyncObject)
            {
                Console.WriteLine("\n=========== Sending Message ===========");
                Console.WriteLine(rawMessage);
                this.binaryWriter?.Write(rawMessage);
                this.binaryWriter?.Flush();
            }
        }
    }
}
