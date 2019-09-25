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
			Console.Error.WriteLine ("MAIN");
			MartinTest.Run ();
			Console.Error.WriteLine ("MAIN DONE");
		}
	}
}
