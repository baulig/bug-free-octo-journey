using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.Sockets.Tests;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using MonoTests.Helpers;
using System.Net.Test.Common;

namespace DotNetTest
{
	public static class MartinTest
	{
		// Ports 8 and 8887 are unassigned as per https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.txt
		const int UnusedPort = 8;
		const int UnusedBindablePort = 8887;

		static readonly ITestOutputHelper _log = new ITestOutputHelper ();

		public static async Task Run ()
		{
			await Connect_Success (IPAddress.Loopback).ConfigureAwait (false);
		}

		public static void DualModeConnectAsync_Static_DnsEndPointToHost_Helper (IPAddress listenOn, bool dualModeServer)
		{
			using (SocketServer server = new SocketServer (_log, listenOn, dualModeServer, out int port)) {
				ManualResetEvent waitHandle = new ManualResetEvent (false);
				SocketAsyncEventArgs args = new SocketAsyncEventArgs ();
				args.Completed += new EventHandler<SocketAsyncEventArgs> (AsyncCompleted);
				args.RemoteEndPoint = new DnsEndPoint ("localhost", port);
				args.UserToken = waitHandle;

				bool pending = Socket.ConnectAsync (SocketType.Stream, ProtocolType.Tcp, args);
				if (!pending)
					waitHandle.Set ();

				Assert.True (waitHandle.WaitOne (TestSettings.PassingTestTimeout), "Timed out while waiting for connection");
				if (args.SocketError != SocketError.Success) {
					throw new SocketException ((int)args.SocketError);
				}
				Assert.True (args.ConnectSocket.Connected);
				args.ConnectSocket.Dispose ();
			}
		}

		public static void DualModeConnectAsync_DnsEndPointToHost_Helper (IPAddress listenOn, bool dualModeServer)
		{
			using (Socket socket = new Socket (SocketType.Stream, ProtocolType.Tcp))
			using (SocketServer server = new SocketServer (_log, listenOn, dualModeServer, out int port)) {
				ManualResetEvent waitHandle = new ManualResetEvent (false);
				SocketAsyncEventArgs args = new SocketAsyncEventArgs ();
				args.Completed += new EventHandler<SocketAsyncEventArgs> (AsyncCompleted);
				args.RemoteEndPoint = new DnsEndPoint ("localhost", port);
				args.UserToken = waitHandle;

				bool pending = socket.ConnectAsync (args);
				if (!pending)
					waitHandle.Set ();

				Assert.True (waitHandle.WaitOne (TestSettings.PassingTestTimeout), "Timed out while waiting for connection");
				if (args.SocketError != SocketError.Success) {
					throw new SocketException ((int)args.SocketError);
				}
				Assert.True (socket.Connected);
			}
		}

		static void AsyncCompleted (object sender, SocketAsyncEventArgs e)
		{
			EventWaitHandle handle = (EventWaitHandle)e.UserToken;

			_log.WriteLine (
			    "AsyncCompleted: " + e.GetHashCode () + " SocketAsyncEventArgs with manual event " +
			    handle.GetHashCode () + " error: " + e.SocketError);

			handle.Set ();
		}

		class ITestOutputHelper
		{
			public void WriteLine (string message)
			{
				Console.WriteLine (message);
			}
		}

		class SocketServer : IDisposable
		{
			private readonly ITestOutputHelper _output;
			private Socket _server;
			private Socket _acceptedSocket;
			private EventWaitHandle _waitHandle = new AutoResetEvent (false);

			public EventWaitHandle WaitHandle {
				get { return _waitHandle; }
			}

			public SocketServer (ITestOutputHelper output, IPAddress address, bool dualMode, out int port)
			{
				_output = output;

				if (dualMode) {
					_server = new Socket (SocketType.Stream, ProtocolType.Tcp);
				} else {
					_server = new Socket (address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				}

				port = _server.BindToAnonymousPort (address);
				_server.Listen (1);

				IPAddress remoteAddress = address.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any;
				EndPoint remote = new IPEndPoint (remoteAddress, 0);
				SocketAsyncEventArgs e = new SocketAsyncEventArgs ();
				e.RemoteEndPoint = remote;
				e.Completed += new EventHandler<SocketAsyncEventArgs> (Accepted);
				e.UserToken = _waitHandle;

				_server.AcceptAsync (e);
			}

			private void Accepted (object sender, SocketAsyncEventArgs e)
			{
				EventWaitHandle handle = (EventWaitHandle)e.UserToken;
				_output.WriteLine (
				    "Accepted: " + e.GetHashCode () + " SocketAsyncEventArgs with manual event " +
				    handle.GetHashCode () + " error: " + e.SocketError);

				_acceptedSocket = e.AcceptSocket;

				handle.Set ();
			}

			public void Dispose ()
			{
				try {
					_server.Dispose ();
					if (_acceptedSocket != null)
						_acceptedSocket.Dispose ();
				} catch (Exception) { }
			}
		}

		class SocketClient
		{
			private IPAddress _connectTo;
			private Socket _serverSocket;
			private int _port;
			private readonly ITestOutputHelper _output;

			private EventWaitHandle _waitHandle = new AutoResetEvent (false);
			public EventWaitHandle WaitHandle {
				get { return _waitHandle; }
			}

			public SocketError Error {
				get;
				private set;
			}

			public SocketClient (ITestOutputHelper output, Socket serverSocket, IPAddress connectTo, int port)
			{
				_output = output;
				_connectTo = connectTo;
				_serverSocket = serverSocket;
				_port = port;
				Error = SocketError.Success;

				Task.Run (() => ConnectClient (null));
			}

			private void ConnectClient (object state)
			{
				try {
					Socket socket = new Socket (_connectTo.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

					SocketAsyncEventArgs e = new SocketAsyncEventArgs ();
					e.Completed += new EventHandler<SocketAsyncEventArgs> (Connected);
					e.RemoteEndPoint = new IPEndPoint (_connectTo, _port);
					e.UserToken = _waitHandle;

					if (!socket.ConnectAsync (e)) {
						Connected (socket, e);
					}
				} catch (SocketException ex) {
					Error = ex.SocketErrorCode;
					Thread.Sleep (TestSettings.FailingTestTimeout); // Give the other end a chance to call Accept().
					_serverSocket.Dispose (); // Cancels the test
					_waitHandle.Set ();
				}
			}
			private void Connected (object sender, SocketAsyncEventArgs e)
			{
				EventWaitHandle handle = (EventWaitHandle)e.UserToken;
				_output.WriteLine (
				    "Connected: " + e.GetHashCode () + " SocketAsyncEventArgs with manual event " +
				    handle.GetHashCode () + " error: " + e.SocketError);

				Error = e.SocketError;
				if (Error != SocketError.Success) {
					Thread.Sleep (TestSettings.FailingTestTimeout); // Give the other end a chance to call Accept().
					_serverSocket.Dispose (); // Cancels the test
				}
				handle.Set ();
			}
		}

		public static async Task Connect_Success (IPAddress listenAt)
		{
			Debug.WriteLine ($"HELLO WORLD!");
			int port;
			using (SocketTestServer.SocketTestServerFactory (SocketImplementationType.Async, listenAt, out port)) {
				using (Socket client = new Socket (listenAt.AddressFamily, SocketType.Stream, ProtocolType.Tcp)) {
					client.ForceNonBlocking (true);
					var endPoint = new IPEndPoint (listenAt, port);
					client.Connect (endPoint);
					//					Task connectTask = ConnectAsync (client, new IPEndPoint (listenAt, port));
					//					await connectTask;
					Console.Error.WriteLine ($"CONNECT SUCCESS: {client.Connected}");
					Assert.True (client.Connected);
				}
			}

			Console.Error.WriteLine ($"TEST DONE!");

			Task ConnectAsync (Socket s, EndPoint endPoint) =>
				Task.Run (() => { s.ForceNonBlocking (true); s.Connect (endPoint); });
		}
	}
}
