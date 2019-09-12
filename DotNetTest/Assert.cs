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

		public static void False (bool condition, string message = "condition is true")
		{
			if (condition)
				Fail (message);
		}

		public static void Equal (object expected, object actual, string message = "not equal")
		{
			if (!object.Equals (expected, actual))
				Fail ($"Equal({expected},{actual}): {message}");
		}

		public static void Equal (int expected, int actual, string message = "not equal")
		{
			if (expected != actual)
				Fail ($"Equal({expected},{actual}): {message}");
		}

		public static void Equal (long expected, long actual, string message = "not equal")
		{
			if (expected != actual)
				Fail ($"Equal({expected},{actual}): {message}");
		}
	}
}
