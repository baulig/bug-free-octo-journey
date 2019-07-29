//
// Program.cs
//
// Author:
//       Martin Baulig <mabaul@microsoft.com>
//
// Copyright (c) 2019 Microsoft Corporation
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SocketTest
{
	class MainClass
	{
		public static async Task Main ()
		{
			var ep = new DnsEndPoint ("localhost", 8888);
			var args = new ConnectEventArgs ();
			args.RemoteEndPoint = ep;

			args.Completed += (_, __) => {
				Console.Error.WriteLine ($"ON COMPLETED: {args.SocketError}");
			};

//			var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);
//			socket.ConnectAsync (args);
//			Thread.Sleep (50000);

			var result = Socket.ConnectAsync (SocketType.Stream, ProtocolType.Tcp, args);
			Console.WriteLine ($"CONNECT ASYNC: {result}");

			var cts = new CancellationTokenSource ();
			cts.CancelAfter (2500);
			var cancellationToken = cts.Token;

			using (cancellationToken.Register (s => Socket.CancelConnectAsync ((SocketAsyncEventArgs)s), args)) {
				Console.Error.WriteLine ($"X");
				await args.Builder.Task.ConfigureAwait (false);
				Console.Error.WriteLine ($"Y");
			}

			Thread.Sleep (50000);
		}

		class MyArgs : SocketAsyncEventArgs
		{
			int completed;

			protected override void OnCompleted (SocketAsyncEventArgs e)
			{
				Console.Error.WriteLine ($"MY COMPLETED!");
				if (Interlocked.Increment (ref completed) != 1)
					throw new InvalidTimeZoneException ("I LIVE ON THE MOON!");
			}
		}

		sealed class ConnectEventArgs : SocketAsyncEventArgs
		{
			public AsyncTaskMethodBuilder Builder { get; private set; }
			public CancellationToken CancellationToken { get; private set; }

			public void Initialize (CancellationToken cancellationToken)
			{
				CancellationToken = cancellationToken;
				var b = new AsyncTaskMethodBuilder ();
				var ignored = b.Task; // force initialization
				Builder = b;
			}

			public void Clear () => CancellationToken = default;

			protected override void OnCompleted (SocketAsyncEventArgs _)
			{
				switch (SocketError) {
					case SocketError.Success:
						Builder.SetResult ();
						break;

					case SocketError.OperationAborted:
					case SocketError.ConnectionAborted:
						if (CancellationToken.IsCancellationRequested) {
							Builder.SetException (new OperationCanceledException ());
							break;
						}
						goto default;

					default:
						Builder.SetException (new SocketException ((int)SocketError));
						break;
				}
			}
		}


	}
}
