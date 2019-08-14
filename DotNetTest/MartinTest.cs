using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using MonoTests.Helpers;

namespace DotNetTest
{
	public static class MartinTest
	{
		// Ports 8 and 8887 are unassigned as per https://www.iana.org/assignments/service-names-port-numbers/service-names-port-numbers.txt
		const int UnusedPort = 8;
		const int UnusedBindablePort = 8887;

		public static void Run ()
		{
			TestHttpListener ();
		}

		static Task TestHttpClient ()
		{
			var client = new HttpClient ();
			return client.GetAsync ("http://broken-localhost:8888/");
		}

		static void TestSocket ()
		{
			var ep = new DnsEndPoint ("broken-localhost", 8888);
			var args = new SocketAsyncEventArgs (); // new ConnectEventArgs ();
			args.RemoteEndPoint = ep;

			args.Completed += (_, __) => {
				Console.Error.WriteLine ($"ON COMPLETED: {args.SocketError} {Thread.CurrentThread.ManagedThreadId}");
			};

			//			var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);
			//			socket.ConnectAsync (args);
			//			Thread.Sleep (50000);

			var result = Socket.ConnectAsync (SocketType.Stream, ProtocolType.Tcp, args);
			Console.WriteLine ($"CONNECT ASYNC: {result} {Thread.CurrentThread.ManagedThreadId}");

			Thread.Sleep (50000);

			Environment.Exit (255);

#if MARTIN_FIXME
			var cts = new CancellationTokenSource ();
			cts.CancelAfter (2500);
			var cancellationToken = cts.Token;

			using (cancellationToken.Register (s => Socket.CancelConnectAsync ((SocketAsyncEventArgs)s), args)) {
				Console.Error.WriteLine ($"X");
				await args.Builder.Task.ConfigureAwait (false);
				Console.Error.WriteLine ($"Y");
			}

			Thread.Sleep (50000);
#endif
		}

		static void TestSocket2 ()
		{
			var mre = new ManualResetEvent (false);

			var endPoint = new IPEndPoint (0,0);
			var socket = new Socket (endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);

			var socketArgs = new SocketAsyncEventArgs ();
			socketArgs.RemoteEndPoint = endPoint;
			socketArgs.Completed += (sender, e) => mre.Set ();

			var pending = socket.ConnectAsync (socketArgs);
			Console.Error.WriteLine ($"PENDING: {pending}");

			var res = mre.WaitOne (10000);
			Console.Error.WriteLine ($"RESULT: {res}");
		}

		static void TestSocket3 ()
		{
			Socket serverSocket = null;
			Socket clientSocket;
			ManualResetEvent readyEvent;
			ManualResetEvent mainEvent;
			Exception error = null;

			readyEvent = new ManualResetEvent (false);
			mainEvent = new ManualResetEvent (false);

			ThreadPool.QueueUserWorkItem (_ => DoWork ());
			readyEvent.WaitOne ();

			if (error != null)
				throw error;

			clientSocket = new Socket (
				AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			clientSocket.Connect (serverSocket.LocalEndPoint);
			clientSocket.NoDelay = true;
		
			void DoWork ()
			{
				try {
					serverSocket = new Socket (
						AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					serverSocket.Bind (new IPEndPoint (IPAddress.Loopback, 0));
					serverSocket.Listen (1);

					var async = new SocketAsyncEventArgs ();
					async.Completed += (s,ce) => OnAccepted (ce);

					if (!serverSocket.AcceptAsync (async))
						OnAccepted (async);
				} catch (Exception ex) {
					error = ex;
				} finally {
					readyEvent.Set ();
				}
			}

			void OnAccepted (SocketAsyncEventArgs args)
			{
				var acceptSocket = args.AcceptSocket;

				try {
					var header = new byte [4];
					acceptSocket.Receive (header);
					if ((header [0] != 0x12) || (header [1] != 0x34) ||
						(header [2] != 0x56) || (header [3] != 0x78))
						throw new InvalidOperationException ();
				} catch (Exception ex) {
					error = ex;
					return;
				}

				var recvAsync = new SocketAsyncEventArgs ();
				recvAsync.Completed += (sender, innerArgs) => OnReceived (innerArgs);
				recvAsync.SetBuffer (new byte [4], 0, 4);
				if (!acceptSocket.ReceiveAsync (recvAsync))
					OnReceived (recvAsync);

				mainEvent.Set ();
			}

			void OnReceived (SocketAsyncEventArgs args)
			{
				if (args.SocketError != SocketError.Success)
					error = new SocketException ((int) args.SocketError);
				else if (args.Buffer [0] != 0x9a)
					error = new InvalidOperationException ();

				mainEvent.Set ();
			}

			var buffer = new byte [] { 0x12, 0x34, 0x56, 0x78 };
			var m = new ManualResetEvent (false);
			var e = new SocketAsyncEventArgs ();
			e.SetBuffer (buffer, 0, buffer.Length);
			e.Completed += (s,o) => {
				Console.Error.WriteLine ($"COMPLETED: {o.SocketError}");
				if (o.SocketError != SocketError.Success)
					error = new SocketException ((int)o.SocketError);
				m.Set ();
			};
			bool res = clientSocket.SendAsync (e);
			Console.Error.WriteLine ($"SEND ASYNC: {res}");
			if (res) {
				if (!m.WaitOne (1500))
					Assert.Fail ("Timeout #1");
			}

			if (!mainEvent.WaitOne (1500))
				Assert.Fail ("Timeout #2");
			if (error != null)
				throw error;

			m.Reset ();
			mainEvent.Reset ();

			buffer [0] = 0x9a;
			buffer [1] = 0xbc;
			buffer [2] = 0xde;
			buffer [3] = 0xff;
			res = clientSocket.SendAsync (e);
			Console.Error.WriteLine ($"SEND ASYNC #1: {res}");
			if (res) {
				if (!m.WaitOne (1500))
					Assert.Fail ("Timeout #3");
			}

			if (!mainEvent.WaitOne (1500))
				Assert.Fail ("Timeout #4");
			if (error != null)
				throw error;
		}

		static void TestSocket4 ()
		{
			var endPoint = new DnsEndPoint ("localhost", 8080, AddressFamily.Unspecified);

			var args = new SocketAsyncEventArgs ();
			args.Completed += Args_Completed;
			args.RemoteEndPoint = endPoint;

//			var pending = Socket.ConnectAsync (SocketType.Stream, ProtocolType.Tcp, args);
//			Console.Error.WriteLine ($"PENDING: {pending}");
//			Thread.Sleep (25000);
//			return;

			var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);
			socket.Connect (endPoint);
		}

		private static void Args_Completed (object sender, SocketAsyncEventArgs e)
		{
			Console.Error.WriteLine ($"COMPLETED CALLBACK: {sender} {e.SocketError} {e.ConnectByNameError}");
		}

		static void TestHttpListener ()
		{
			HttpListener listener = NetworkHelpers.CreateAndStartHttpListener ("http://localhost:", out var port, "/", out var uri);

			IPEndPoint expectedIpEndPoint = CreateListenerRequest (listener, uri);

			var first = CreateListenerRequest (listener, uri);
			var second = CreateListenerRequest (listener, uri);

			Console.Error.WriteLine ($"TEST HTTP LISTENER: {expectedIpEndPoint} {first} {second}");
		}

		static IPEndPoint CreateListenerRequest (HttpListener listener, string uri)
		{
			IPEndPoint ipEndPoint = null;
			var mre = new ManualResetEventSlim ();
			listener.BeginGetContext (result => {
				ipEndPoint = ListenerCallback (result);
				mre.Set ();
			}, listener);

			var request = (HttpWebRequest)WebRequest.Create (uri);
			request.Method = "POST";
			request.KeepAlive = true;

			// We need to write something
			request.GetRequestStream ().Write (new byte[] { (byte)'a' }, 0, 1);
			request.GetRequestStream ().Dispose ();

			// Send request, socket is created or reused.
			var response = request.GetResponse ();

//			using (var rs = response.GetResponseStream ())
//			using (var reader = new StreamReader (rs)) {
//				reader.ReadToEnd ();
// n			}

			// Close response so socket can be reused.
			response.Close ();

			mre.Wait ();

			return ipEndPoint;
		}

		static IPEndPoint ListenerCallback (IAsyncResult result)
		{
			var listener = (HttpListener)result.AsyncState;
			var context = listener.EndGetContext (result);
			var clientEndPoint = context.Request.RemoteEndPoint;

			// Get a response stream and write the response to it.
			context.Response.ContentLength64 = 0;
			context.Response.Close ();

			return clientEndPoint;
		}

	}
}
