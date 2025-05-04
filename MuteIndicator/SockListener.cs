using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MuteIndicator
{
    public static class ErrorLogging
    {
        private static bool _firstCall = true;
        private const string FileName = "Stuff.log";
        private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), FileName);

        public static void WriteException(Exception ex)
        {
            var filePath = FilePath;
            if (_firstCall && File.Exists(filePath)) TryDelete(filePath);
            _firstCall = false;

            StringBuilder sb = new StringBuilder();
            sb.Append(ex.Message);
            sb.Append(Environment.NewLine);
            sb.Append(ex.StackTrace);
            sb.Append(Environment.NewLine);
            File.AppendAllText(filePath, sb.ToString());
            return;

            void TryDelete(string path)
            {
                try { File.Delete(path); }
                catch (Exception _)
                {
                    /* ignored */
                }
            }
        }
    }

    // State object for reading client data asynchronously
    public class StateObject
    {
        // Size of receive buffer.
        public const int BufferSize = 1024;

        // Receive buffer.
        public byte[] buffer = new byte[BufferSize];

        // Received data string.
        public StringBuilder sb = new StringBuilder();

        // Client socket.
        public Socket workSocket = null;
    }

    public class AsynchronousSocketListener
    {
        // Thread signal.
        public static ManualResetEvent allDone = new(false);
        private static Socket listener;
        private static bool stop;

        public static void StopListening()
        {
            stop = true;

            if (listener == null)
                return;

            listener.Close();
            listener.Dispose();
        }

        public static void StartListening()
        {
            // Establish the local endpoint for the socket.
            // The DNS name of the computer
            // running the listener is "host.contoso.com".
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            // Create a TCP/IP socket.
            listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (!stop)
                {
                    // Set the event to nonsignaled state.
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections.
                    //Debug.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    // Wait until a connection is made before continuing.
                    allDone.WaitOne(100);
                }
            }
            catch (Exception e) { ErrorLogging.WriteException(e); }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            if (stop)
                return;

            // Signal the main thread to continue.
            allDone.Set();

            // Get the socket that handles the client request.
            listener = (Socket) ar.AsyncState;
            var handler = listener.EndAccept(ar);

            // Create the state object.
            var state = new StateObject { workSocket = handler };
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            const string eof = "<EOF>";

            try
            {
                var content = string.Empty;

                // Retrieve the state object and the handler socket
                // from the asynchronous state object.
                var state = (StateObject) ar.AsyncState;
                var handler = state.workSocket;

                // Check if the socket is still connected before proceeding.
                // if (state is null || handler is null || !IsSocketConnected(handler))
                // {
                //     Debug.WriteLine("Socket is no longer connected. Closing...");
                //     handler?.Close();
                //     return;
                // }

                // Read data from the client socket.
                var bytesRead = handler.EndReceive(ar);

                // Check if the client sent any data.
                if (bytesRead <= 0)
                {
                    // No bytes were read; the client may have closed the connection.
                    Debug.WriteLine("No data read from socket. Closing...");
                    handler.Close();
                }
                else
                {
                    // Append received data to the StringBuilder.
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    content = state.sb.ToString();

                    // Check for the <EOF> tag to indicate the end of the message.

                    if (content.IndexOf(eof, StringComparison.Ordinal) > -1)
                    {
                        // Complete message received. Display on the console.
                        Debug.WriteLine("Read {0} bytes from socket. \nData: {1}", content.Length, content);
                        SimpleMessageHandler.ParseAndFire(content);

                        // Close the socket safely after processing.
                        handler.Shutdown(SocketShutdown.Both);
                        handler.Close();
                    }
                    else
                    {
                        // Not all data received; continue reading.
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, ReadCallback, state);
                    }
                }
            }
            catch (SocketException ex)
            {
                // Handle socket-related exceptions, such as unexpected disconnections.
                Debug.WriteLine("SocketException: {0}", ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                // Handle cases when the socket is disposed before this callback.
                Debug.WriteLine("ObjectDisposedException: {0}", ex.Message);
            }
            catch (Exception ex)
            {
                // Handle any other unexpected exceptions.
                Debug.WriteLine("Unexpected exception in ReadCallback: {0}", ex.Message);
            }
        }

        // Helper method to check if the socket is still connected.
        private static bool IsSocketConnected(Socket socket)
        {
            try { return !(socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0); }
            catch (SocketException) { return false; }
        }
    }
}