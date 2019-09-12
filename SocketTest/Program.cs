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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

//
// Add the following to your /etc/hosts:
//
// 0.0.0.0 broken-localhost
//
// On the Mac, after editing the /etc/hosts, you also need to do
//   sudo killall -HUP mDNSResponder
//


namespace SocketTest
{
	class MainClass
	{
#if !FIXME
		public static Task Main ()
		{
			Debug.Listeners.Add (new TextWriterTraceListener (Console.Out));
			Debug.WriteLine ($"MAIN!");
			return DotNetTest.MartinTest.Run ();
		}
#else
		public static void Main ()
		{
			var asm = typeof (Socket).Assembly;
			var type = asm.GetType ("System.Net.Internals.MartinTest");
			var method = type.GetMethod ("Run");
			method.Invoke (null, null);
		}
#endif
	}
}
