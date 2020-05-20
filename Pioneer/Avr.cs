using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text;
using System.Security;
using Catspaw.Properties;
using System.Diagnostics;
using System.Globalization;

namespace Catspaw.Pioneer
{
    /// <summary>
    /// Implement the Avr
    /// </summary>
    public partial class Avr : IDisposable
    {
        // Manually reset event to track avr availability status
        private readonly ManualResetEvent networkUp;
        // Avr Ip host 
        private readonly IPHostEntry hostEntry;
        private TcpClient avr;
        private NetworkStream avrStream;
        private StreamWriter avrWriter;
        private StreamReader avrReader;

        /// <summary>
        /// Create an Avr with the given hostanme and TCP port
        /// </summary>
        /// <param name="hostname">The Avr hostname</param>
        /// <param name="port">The Avr tcp port (default: 23)</param>
        /// <exception cref="ArgumentNullException">hostname can't be null</exception>
        /// <exception cref="ArgumentOutOfRangeException">port must be a valid port number</exception>
        /// <exception cref="AvrException">Communication error with the Avr</exception>
        public Avr(string hostname, int port = 23)
        {
            if (hostname is null) throw new ArgumentNullException(nameof(hostname));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));

            // Initialize manual reset networkAvailable event to track network availability status
            // The event is created "signaled" if the network is up at creation time
            // Hook the callback to network availability changed event
            // The callback set or reset networkAvailable event depending of network status
            networkUp = new ManualResetEvent(NetworkInterface.GetIsNetworkAvailable());
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangeCallback;

            // Hostname and port for the TcpClient
            Hostname = hostname;
            Port = port;

            // Get Ip Addresses of Avr
            try
            {
                hostEntry = Dns.GetHostEntry(hostname);
            }
            catch (Exception e)
            when (e is SocketException || e is ArgumentException)
            {
                throw new AvrException(Resources.ErrorIPAvr, e);
            }

            Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Constructor connect avr");
            // Connect to the avr
            ConnectAvr();
        }

        /// <summary>Get the hostname of the AVR</summary>
        public string Hostname { get; }
        /// <summary>Get the TCP port of the AVR</summary>
        public int Port { get; }

        #region Avr network communication
        // Send a command to avr without processing avr answer
        private void Send(string command)
        {
            try
            {
                avrWriter.WriteLine(command);
            }
            catch (IOException e)
            {
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
        }

        // Execute command on avr and get its response 
        private string Exec(string command)
        {
            string response;

            try
            {
                avrWriter.WriteLine(command);
                response = avrReader.ReadLine();
            }
            catch (IOException e)
            {
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }

            return response;
        }

        // Connect to avr through TcpClient and create writer and reader
        private void ConnectAvr()
        {
            Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Create TcpClient");
            // Avr is a TcpClient
            avr = new TcpClient(AddressFamily.InterNetwork);

            try
            {
                Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Wait for network");
                // Wait for network availability event 
                // and raise exception if not signaled after default timeout
                if (!networkUp.WaitOne(Settings.Default.AvrNetworkTimeout))
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);

                Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Wait for connection");
                // Wait for connection to Avr 
                // and raise exception if not connected after default timeout
                var connection = avr.ConnectAsync(hostEntry.AddressList, Port);
                if (!connection.Wait(Settings.Default.AvrNetworkTimeout))
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);

                Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Create stream, reader & writer");
                // Get stream, writer, reader, send command and get response
                avrStream = avr.GetStream();
                avrWriter = new StreamWriter(avrStream, Encoding.ASCII, bufferSize: avr.SendBufferSize) { AutoFlush = true };
                avrReader = new StreamReader(avrStream, Encoding.ASCII, false, avr.ReceiveBufferSize);
            }
            catch (Exception e)
            when (e is InvalidOperationException || e is SocketException || e is SecurityException)
            {
                Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Connect exception");
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
        }

        // Close and dispose connection, stream, writer and reader  
        private void CloseAvr()
        {
            Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Close avr");
            avrReader?.Close();
            avrWriter?.Close();
            avrStream?.Close();
            avr?.Close();
        }

        // Close and dispose connection, stream, writer and reader 
        // Then reconnect to avr
        private void ResetAvr()
        {
            Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Reset avr");
            CloseAvr();
            ConnectAvr();
        }

        // Signal or unsignal the network availability event when the network availability change
        private void NetworkAvailabilityChangeCallback(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable) networkUp.Set();
            else networkUp.Reset();
        }
        #endregion

        #region IDisposable Support
        // Avoid redundant calls
        private bool disposedValue = false;

        /// <summary>
        /// Do dispose the resources if disposing is true
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Debug.WriteLine(DateTime.Now.ToString("G", CultureInfo.InvariantCulture) + ": Dispose avr");
                    CloseAvr();
                    networkUp.Close();
                }
                disposedValue = true;
            }
        }

        /// <summary>
        /// Implement IDisposable interface
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
