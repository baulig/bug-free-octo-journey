using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DotNetTest
{
	class TestTcpClient
	{
		class Server
		{
			HttpListener listener;

			public void Start (string host)
			{
				listener = new HttpListener ();
				listener.Prefixes.Add (host);

				Task.Run (() => {
					listener.Start ();
					Console.WriteLine ("Web server running...");
					while (listener.IsListening) {
						var context = listener.GetContext ();
						var buffer = System.Text.Encoding.UTF8.GetBytes ("<HTML><BODY> Hello world!</BODY></HTML>");
						context.Response.ContentLength64 = buffer.Length;
						var output = context.Response.OutputStream;
						output.Write (buffer, 0, buffer.Length);
						output.Close ();
					}
				});
			}

			public void Stop ()
			{
				listener.Stop ();
				listener.Close ();
			}
		}

		public static void Run ()
		{
			const string url = "http://localhost:5001/";

			// Start server
			var server = new Server ();
			server.Start (url);

			Task.Delay (1000).Wait ();

			// Connect with TcpClient
			using (var tcpClient = new TcpClient ()) {
				for (int i = 0; i < 5; i++) {
					try {
						tcpClient.Connect ("localhost", 5001);
						break;
					} catch (Exception ex) {
						Console.WriteLine ($"TcpClient FAILED: {ex}");
					}

					Task.Delay (100).Wait ();
				}
			}

			// Connect with HttpClient
			using (var httpClient = new HttpClient ()) {
				try {
					for (int i = 0; i < 5; i++) {
						using (var response = httpClient.GetAsync (url, HttpCompletionOption.ResponseHeadersRead).Result) {
							if (response.StatusCode == System.Net.HttpStatusCode.OK) {
								break;
							}
						}
					}
				} catch (Exception ex) {
					Console.WriteLine ($"HttpClient FAILED: {ex}");
				}

				Task.Delay (100).Wait ();
			}

			server.Stop ();
		}
	}
}
