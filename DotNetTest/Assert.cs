using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetTest
{
	public static class Assert
	{
		public static void Fail (string message)
		{
			throw new NotSupportedException ($"ASSERTION FAILED: {message}");
		}

		public static void True (bool condition, string message = "condition is false")
		{
			if (!condition)
				Fail (message);
		}
	}
}
