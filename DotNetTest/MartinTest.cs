using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.Sockets.Tests;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using MonoTests.Helpers;
using System.Net.Test.Common;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices.WindowsRuntime;

namespace DotNetTest
{
	public static class MartinTest
	{
		// Ports 8 and 8887 are unassigned as per https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.txt
		const int UnusedPort = 8;
		const int UnusedBindablePort = 8887;

		static readonly ITestOutputHelper _log = new ITestOutputHelper ();

		public static Task Run ()
		{
			JoinMulticastGroup3_IPv6 ();
			return Task.CompletedTask;
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
					// Task connectTask = ConnectAsync (client, new IPEndPoint (listenAt, port));
					// await connectTask;
					Console.Error.WriteLine ($"CONNECT SUCCESS: {client.Connected}");
					Assert.True (client.Connected);

					client.Shutdown (SocketShutdown.Both);
				}
			}

			Console.Error.WriteLine ($"TEST DONE!");

			Task ConnectAsync (Socket s, EndPoint endPoint) =>
				Task.Run (() => { s.ForceNonBlocking (true); s.Connect (endPoint); });
		}

		public static async Task ReadWrite_Byte_Success ()
		{
			await RunWithConnectedNetworkStreamsAsync (async (server, client) =>
			{
				for (byte i = 0; i < 2; i++) {
					Task<int> read = Task.Run (() => client.ReadByte ());
					Task write = Task.Run (() => server.WriteByte (i));
					await Task.WhenAll (read, write);
					Assert.Equal (i, await read);
				}
				Console.Error.WriteLine ("TEST DONE");
			});
		}

		public static async Task Exit_During_Read ()
		{
			await RunWithConnectedNetworkStreamsAsync (async (server, client) => {
				for (byte i = 0; i < 2; i++) {
					Task<int> read = Task.Run (() => client.ReadByte ());
					Task sleep = Task.Delay (TimeSpan.FromSeconds (5));
					sleep.ContinueWith (_ => {
						Environment.Exit (255);
					});
					await Task.WhenAll (read, sleep);
					Assert.Equal (i, await read);
				}
				Console.Error.WriteLine ("TEST DONE");
			});
		}

		public static async Task Ctor_SocketFileAccess_CanReadAndWrite ()
		{
			using (Socket listener = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
			using (Socket client = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				listener.Bind (new IPEndPoint (IPAddress.Loopback, 0));
				listener.Listen (1);

				Task<Socket> acceptTask = listener.AcceptAsync ();
				await Task.WhenAll (acceptTask, client.ConnectAsync (new IPEndPoint (IPAddress.Loopback, ((IPEndPoint)listener.LocalEndPoint).Port)));
				using (Socket server = await acceptTask) {
					for (int i = 0; i < 2; i++) // Verify closing the streams doesn't close the sockets
					{
						using (var serverStream = new NetworkStream (server, FileAccess.Write))
						using (var clientStream = new NetworkStream (client, FileAccess.Read)) {
							Assert.True (serverStream.CanWrite && !serverStream.CanRead);
							Assert.True (!clientStream.CanWrite && clientStream.CanRead);
							Assert.False (serverStream.CanSeek && clientStream.CanSeek);
							Assert.True (serverStream.CanTimeout && clientStream.CanTimeout);

							// Verify Read and Write on both streams
							byte[] buffer = new byte[2];

							var task = clientStream.ReadAsync (buffer, 1, 1);

							await serverStream.WriteAsync (new byte[] { (byte)'a' }, 0, 1);
							Assert.Equal (1, await task);
							Assert.Equal (0, buffer[0]);
							Assert.Equal ('a', (char)buffer[1]);

							// Assert.Throws<InvalidOperationException> (() => { serverStream.BeginRead (buffer, 0, 1, null, null); });
							// Assert.Throws<InvalidOperationException> (() => { clientStream.BeginWrite (buffer, 0, 1, null, null); });

							// Assert.Throws<InvalidOperationException> (() => { serverStream.ReadAsync (buffer, 0, 1); });
							// Assert.Throws<InvalidOperationException> (() => { clientStream.WriteAsync (buffer, 0, 1); });
						}
					}
				}
			}
		}

		public static void TestFailedConnection ()
		{
			try {
				WebRequest.Create ("http://127.0.0.1:0/non-existant.txt").GetResponse ();
				Assert.Fail ("Should have raised an exception");
			} catch (Exception e) {
				Assert.True (e is WebException, "Got " + e.GetType ().Name + ": " + e.Message);
				//#if NET_2_0 e.Message == "Unable to connect to the remote server"
				//#if NET_1_1 e.Message == "The underlying connection was closed: Unable to connect to the remote server."

				Assert.Equal (WebExceptionStatus.ConnectFailure, ((WebException)e).Status);

				//#if !NET_1_1 (this is not true in .NET 1.x)
				Assert.True (e.InnerException != null);
				// Assert.True (e.InnerException is Socks.SocketException, "InnerException should be SocketException");
				//e.Message == "The requested address is not valid in its context 127.0.0.1:0"
				//#endif
			}
		}

		public static void TestBeginConnectWithError ()
		{
			var endpoint = new IPEndPoint (IPAddress.Loopback, 0);
			var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			var result = socket.BeginConnect (endpoint, AsyncCallback, null);
			Console.Error.WriteLine ($"BEGIN CONNECT DONE: {result}");

			Thread.Sleep (TimeSpan.FromSeconds (10));

			void AsyncCallback (IAsyncResult asyncResult)
			{
				Console.Error.WriteLine ($"ASYNC CALLBACK: {asyncResult}");
			}

		}

		public static void DualModeConnect_IPAddressListToHost_Success (IPAddress[] connectTo, IPAddress listenOn, bool dualModeServer)
		{
			using (Socket socket = new Socket (SocketType.Stream, ProtocolType.Tcp))
			using (SocketServer server = new SocketServer (_log, listenOn, dualModeServer, out int port)) {
				socket.Connect (connectTo, port);
				Assert.True (socket.Connected);
			}
		}

		public static async Task Receive0ByteReturns_WhenPeerDisconnects ()
		{
			using (Socket listener = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
			using (Socket client = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
				listener.Bind (new IPEndPoint (IPAddress.Loopback, 0));
				listener.Listen (1);

				Task<Socket> acceptTask = AcceptAsync (listener);
				await Task.WhenAll (
				    acceptTask,
				    ConnectAsync (client, new IPEndPoint (IPAddress.Loopback, ((IPEndPoint)listener.LocalEndPoint).Port)));

				using (Socket server = await acceptTask) {
					// Have the client do a 0-byte receive.  No data is available, so this should pend.
					Task<int> receive = ReceiveAsync (client, new ArraySegment<byte> (Array.Empty<byte> ()));
					Assert.False (receive.IsCompleted, $"Task should not have been completed, was {receive.Status}");

					// Disconnect the client
					server.Shutdown (SocketShutdown.Both);
					server.Close ();

					// The client should now wake up
					Assert.Equal (0, await receive);
				}
			}
		}

		public static Task<Socket> AcceptAsync (Socket s) =>
		    InvokeAsync (s, e => e.AcceptSocket, e => s.AcceptAsync (e));
		public static Task<Socket> AcceptAsync (Socket s, Socket acceptSocket) =>
		    InvokeAsync (s, e => e.AcceptSocket, e => {
			    e.AcceptSocket = acceptSocket;
			    return s.AcceptAsync (e);
		    });
		public static Task ConnectAsync (Socket s, EndPoint endPoint) =>
		    InvokeAsync (s, e => true, e => {
			    e.RemoteEndPoint = endPoint;
			    return s.ConnectAsync (e);
		    });
		public static Task<int> ReceiveAsync (Socket s, ArraySegment<byte> buffer) =>
		    InvokeAsync (s, e => e.BytesTransferred, e => {
			    e.SetBuffer (buffer.Array, buffer.Offset, buffer.Count);
			    return s.ReceiveAsync (e);
		    });
		public static Task<int> ReceiveAsync (Socket s, IList<ArraySegment<byte>> bufferList) =>
		    InvokeAsync (s, e => e.BytesTransferred, e => {
			    e.BufferList = bufferList;
			    return s.ReceiveAsync (e);
		    });

		private static Task<TResult> InvokeAsync<TResult> (
		        Socket s,
		        Func<SocketAsyncEventArgs, TResult> getResult,
		        Func<SocketAsyncEventArgs, bool> invoke)
		{
			var tcs = new TaskCompletionSource<TResult> ();
			var saea = new SocketAsyncEventArgs ();
			EventHandler<SocketAsyncEventArgs> handler = (_, e) =>
			{
				if (e.SocketError == SocketError.Success)
					tcs.SetResult (getResult (e));
				else
					tcs.SetException (new SocketException ((int)e.SocketError));
				saea.Dispose ();
			};
			saea.Completed += handler;
			if (!invoke (saea))
				handler (s, saea);
			return tcs.Task;
		}

		static async Task RunWithConnectedNetworkStreamsAsync (Func<NetworkStream, NetworkStream, Task> func,
			    FileAccess serverAccess = FileAccess.ReadWrite, FileAccess clientAccess = FileAccess.ReadWrite)
		{
			var listener = new TcpListener (IPAddress.Loopback, 0);
			try {
				listener.Start (1);
				var clientEndpoint = (IPEndPoint)listener.LocalEndpoint;

				using (var client = new TcpClient (clientEndpoint.AddressFamily)) {
					Task<TcpClient> remoteTask = listener.AcceptTcpClientAsync ();
					Task clientConnectTask = client.ConnectAsync (clientEndpoint.Address, clientEndpoint.Port);

					await Task.WhenAll (remoteTask, clientConnectTask);

					using (TcpClient remote = remoteTask.Result)
					using (NetworkStream serverStream = new NetworkStream (remote.Client, serverAccess, ownsSocket: true))
					using (NetworkStream clientStream = new NetworkStream (client.Client, clientAccess, ownsSocket: true)) {
						await func (serverStream, clientStream);
					}
				}
			} finally {
				listener.Stop ();
			}
		}

		public static async Task ReadWrite_Array_Success ()
		{
			await RunWithConnectedNetworkStreamsAsync ((server, client) =>
			{
				var clientData = new byte[] { 42 };
				client.Write (clientData, 0, clientData.Length);

				var serverData = new byte[clientData.Length];
				Assert.Equal (serverData.Length, server.Read (serverData, 0, serverData.Length));

				Assert.Equal (clientData, serverData);

				client.Flush (); // nop

				return Task.CompletedTask;
			});
		}

		public static void BeginGetRequestStream_Body_NotAllowed ()
		{
			using (SocketResponder responder = new SocketResponder (out var ep, s => EchoRequestHandler (s))) {
				string url = "http://" + ep.ToString () + "/test/";
				HttpWebRequest request;

				request = (HttpWebRequest)WebRequest.Create (url);
				request.Method = "GET";

				try {
					var result = request.BeginGetRequestStream (null, null);
					request.EndGetRequestStream (result);
					Assert.Fail ("#A1");
				} catch (ProtocolViolationException ex) {
					// Cannot send a content-body with this
					// verb-type
					Assert.IsNull (ex.InnerException, "#A2");
					Assert.IsNotNull (ex.Message, "#A3");
				}

				request = (HttpWebRequest)WebRequest.Create (url);
				request.Method = "HEAD";

				try {
					var res = request.BeginGetRequestStream (null, null);
					request.EndGetRequestStream (res);
					Assert.Fail ("#B1");
				} catch (ProtocolViolationException ex) {
					// Cannot send a content-body with this
					// verb-type
					Assert.IsNull (ex.InnerException, "#B2");
					Assert.IsNotNull (ex.Message, "#B3");
				}

				Console.Error.WriteLine ($"TEST DONE");
			}

			Console.Error.WriteLine ($"TEST DONE #1");
		}

		internal static byte[] EchoRequestHandler (Socket socket)
		{
			Console.Error.WriteLine ($"ERH!");
			MemoryStream ms = new MemoryStream ();
			byte[] buffer = new byte[4096];
			try {
				int bytesReceived = socket.Receive (buffer);
				Console.Error.WriteLine ($"ERH #1: {bytesReceived}");
				while (bytesReceived > 0) {
					Console.Error.WriteLine ($"ERH #1a: {bytesReceived}");
					ms.Write (buffer, 0, bytesReceived);
					// We don't check for Content-Length or anything else here, so we give the client a little time to write
					// after sending the headers
					Console.Error.WriteLine ($"ERH #2: {bytesReceived} {socket.Available}");
					Thread.Sleep (200);
					if (socket.Available > 0) {
						bytesReceived = socket.Receive (buffer);
					} else {
						bytesReceived = 0;
					}
					Console.Error.WriteLine ($"ERH #3: {bytesReceived}");
				}

				Console.Error.WriteLine ($"ERH #4");

				ms.Flush ();
				ms.Position = 0;
				StreamReader sr = new StreamReader (ms, Encoding.UTF8);
				string request = sr.ReadToEnd ();

				StringWriter sw = new StringWriter ();
				sw.WriteLine ("HTTP/1.1 200 OK");
				sw.WriteLine ("Content-Type: text/xml");
				sw.WriteLine ("Content-Length: " + request.Length.ToString (CultureInfo.InvariantCulture));
				sw.WriteLine ();
				sw.Write (request);
				sw.Flush ();

				Console.Error.WriteLine ($"ERH DONE!");

				return Encoding.UTF8.GetBytes (sw.ToString ());
			} catch (Exception ex) {
				Console.Error.WriteLine ($"ERH EX: {ex}");
				throw;
			}
		}

		public static void DisposeDuringAccept ()
		{
			var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Bind (new IPEndPoint (IPAddress.Loopback, 8888));
			socket.Listen (1);

			ThreadPool.QueueUserWorkItem (_ => {
				Console.WriteLine ($"LISTENING");
				try {
					var accepted = socket.Accept ();
					Console.WriteLine ($"ACCEPTED: {accepted}");
				} catch (Exception ex) {
					Console.WriteLine ($"ACCEPT EX: {ex}");
				}
			});

			Thread.Sleep (1500);

			socket.Dispose ();

			Console.WriteLine ($"DISPOSE DONE!");
 
			Thread.Sleep (TimeSpan.FromSeconds (15));
		}

		public static void JoinMulticastGroup3_IPv6 ()
		{
			IPAddress mcast_addr = IPAddress.Parse ("ff02::1");

			using (UdpClient client = new UdpClient (new IPEndPoint (IPAddress.IPv6Any, 0))) {
				client.JoinMulticastGroup (mcast_addr, 0);
			}

			using (UdpClient client = new UdpClient (new IPEndPoint (IPAddress.IPv6Any, 0))) {
				client.JoinMulticastGroup (mcast_addr, 255);
			}

			Console.Error.WriteLine ("TEST DONE");
		}

	}
}
