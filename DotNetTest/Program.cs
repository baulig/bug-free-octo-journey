using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetTest
{
	class Program
	{
		public static void Main ()
		{
			// return TestHttpClient ();
			TestSocket ();
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
				Console.Error.WriteLine ($"ON COMPLETED: {args.SocketError}");
			};

			//			var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);
			//			socket.ConnectAsync (args);
			//			Thread.Sleep (50000);

			var result = Socket.ConnectAsync (SocketType.Stream, ProtocolType.Tcp, args);
			Console.WriteLine ($"CONNECT ASYNC: {result}");

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
	}
}
