using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text;
using Serilog;
using Catspaw.Properties;

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

        /// <summary>
        /// Create an Avr with the given hostanme and TCP port
        /// </summary>
        /// <param name="hostname">The Avr hostname</param>
        /// <param name="port">The Avr tcp port (default: 23)</param>
        /// <exception cref="ArgumentNullException">hostname can't be null</exception>
        /// <exception cref="ArgumentOutOfRangeException">port must be a valid port number</exception>
        public Avr(string hostname, int port = 23)
        {
            if (hostname is null) throw new ArgumentNullException(nameof(hostname));
            if (port < IPEndPoint.MinPort || port > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(port));

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

            // Initialize manual reset networkAvailable event to track network availability status
            // The event is created "signaled" if the network is up at creation time
            // Hook the callback to network availability changed event
            // The callback set or reset networkAvailable event depending of network status
            networkUp = new ManualResetEvent(NetworkInterface.GetIsNetworkAvailable());
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangeCallback;
        }

        /// <summary>Get the hostname of the AVR</summary>
        public string Hostname { get; }
        /// <summary>Get the TCP port of the AVR</summary>
        public int Port { get; }

        #region Avr network communication
        // Execute a command on avr
        // For performance reason, does not check for network or avr readiness
        // and doesn't process avr response.
        // Status of avr is unknown if an exception is thrown 
        private void Execute(in string command)
        {
            try
            {
                // Avr is a TcpClient
                using var avr = new TcpClient(AddressFamily.InterNetwork);
                avr.Connect(hostEntry.AddressList, Port);
                // Get stream, writer, reader, send command and get response
                var avrStream = avr.GetStream();
                using var avrWriter = new StreamWriter(avrStream, Encoding.ASCII, bufferSize: avr.SendBufferSize) { AutoFlush = true };
                avrWriter.WriteLine(command);
            }
            catch (Exception e)
            when (e is InvalidOperationException || e is IOException || e is SocketException)
            {
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }
        }

        // Send command to avr and get its response 
        private string Send(in string command)
        {
            string response;

            try
            {
                // Avr is a TcpClient
                using var avr = new TcpClient(AddressFamily.InterNetwork);

                // Wait for network availability event 
                // and raise exception if not signaled after default timeout
                if (!networkUp.WaitOne(Settings.Default.AvrNetworkTimeout))
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);

                // Wait for connection to Avr 
                // and raise exception if not connected after default timeout
                var connection = avr.ConnectAsync(hostEntry.AddressList, Port);
                if (!connection.Wait(Settings.Default.AvrNetworkTimeout))
                    throw new AvrException(Resources.ErrorNetworkTimeOutAvr);

                // Get stream, writer, reader, send command and get response
                var avrStream = avr.GetStream();
                using var avrWriter = new StreamWriter(avrStream, Encoding.ASCII, bufferSize: avr.SendBufferSize) { AutoFlush = true };
                using var avrReader = new StreamReader(avrStream, Encoding.ASCII, false, avr.ReceiveBufferSize);
                avrWriter.WriteLine(command);
                response = avrReader.ReadLine();
            }
            catch (Exception e)
            when (e is InvalidOperationException || e is IOException || e is SocketException)
            {
                throw new AvrException(Resources.ErrorCommunicationAvr, e);
            }

            return response;
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
