﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Kilo.Networking
{
    public class SocketListener
    {
        private bool isRunning;
        private TcpListener listener;
        private TraceSource trace = new TraceSource("Kilo.Networking.SocketListener");
        ConcurrentBag<SocketHandler> handlers = new ConcurrentBag<SocketHandler>();

        public event MessageEventHandler MessageReceived;

        public IPEndPoint EndPoint { get; private set; }

        /// <summary>
        /// Listen for clients on a port
        /// </summary>
        public void Listen(int port)
        {
            this.Listen(new IPEndPoint(IPAddress.Loopback, port));
        }

        /// <summary>
        /// Listen for clients on an address and a port
        /// </summary>
        public void Listen(IPAddress address, int port)
        {
            this.Listen(new IPEndPoint(address, port));
        }

        /// <summary>
        /// Listen for clients on an endpoint
        /// </summary>
        public void Listen(IPEndPoint endpoint)
        {
            this.EndPoint = endpoint;

            trace.TraceEvent(TraceEventType.Verbose, 0, $"Starting listener on { endpoint }");

            this.listener = listener = new TcpListener(endpoint);
            listener.Start();

            this.isRunning = true;
            listener.BeginAcceptTcpClient(this.OnAcceptClient, listener);

            trace.TraceEvent(TraceEventType.Information, 0, $"Waiting for clients on {endpoint}");       
        }

        /// <summary>
        /// Called when a client has connected, in order to accept the connection
        /// </summary>
        private void OnAcceptClient(IAsyncResult result)
        {
            var listener = (TcpListener)result.AsyncState;
            TcpClient client;

            try
            {
                client = listener.EndAcceptTcpClient(result);
            }
            catch (ObjectDisposedException)
            {
                this.trace.TraceEvent(TraceEventType.Warning, 0, "ObjectDisposedException encountered, listener could be in an exiting state. Not listening for any more clients");
                this.isRunning = false;
                return;
            }

            trace.TraceEvent(TraceEventType.Information, 0, $"Accepted client from {client.Client.RemoteEndPoint}");

            if (!this.isRunning)
                return;

            // Process the client
            ThreadPool.QueueUserWorkItem(state => 
            {
                trace.TraceEvent(TraceEventType.Verbose, 0, "Starting client processor thread");

                using (var handler = new SocketHandler(client))
                {
                    handler.MessageReceived += (s, a) =>
                    {
                        this.OnMessageReceived(handler, a.Message);                                            
                    };

                    this.handlers.Add(handler);

                    handler.Receive(cancelToken: new CancelToken());

                    trace.TraceEvent(TraceEventType.Information, 0, "Connection to client closed");
                }
            });

            if (this.isRunning)
            {
                trace.TraceEvent(TraceEventType.Information, 0, "Waiting for more clients");
                listener.BeginAcceptTcpClient(this.OnAcceptClient, listener);
            }
        }

        /// <summary>
        /// Stops the listener
        /// </summary>
        public void Stop()
        {
            trace.TraceEvent(TraceEventType.Information, 0, "Stopping");

            if (this.listener != null)
            {
                this.listener.Stop();
            }

            var availableHandlers = this.handlers.ToList();

            foreach (var handler in availableHandlers)
            {
                handler.Dispose();
            }

            this.handlers = null;

            this.isRunning = false;
        }

        protected virtual void OnMessageReceived(SocketHandler handler, ISocketMessage message)
        {
            trace.TraceEvent(TraceEventType.Verbose, 0, $"Received { message.MessageTypeId } ({ message.MessageLength } bytes): {message.GetType()}");

            this.MessageReceived?.Invoke(this, new SocketMessageEventArgs(message, handler));
        }
    }
}
