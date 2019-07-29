using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DotNetTest
{
	class Program
	{
		public static Task Main ()
		{
			return TestHttpClient ();
		}

		static Task TestHttpClient ()
		{
			var client = new HttpClient ();
			return client.GetAsync ("http://broken-localhost:8888/");
		}

	}
}
