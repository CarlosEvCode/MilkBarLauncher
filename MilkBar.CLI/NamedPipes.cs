using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace MilkBar.CLI
{
    public static class NamedPipes
    {
        private static NamedPipeServerStream? _server;
        public static bool Online { get; private set; } = false;

        public static void StartServer(int timeoutSeconds = 15)
        {
            _server = new NamedPipeServerStream(
                "languageConnectionPipe",
                PipeDirection.InOut,
                2,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );

            IAsyncResult asyncResult = _server.BeginWaitForConnection(null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutSeconds * 1000))
            {
                _server.Close();
                Online = false;
                throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for Cemu DLL connection.");
            }

            _server.EndWaitForConnection(asyncResult);
            Online = true;
        }

        public static void Disconnect()
        {
            try
            {
                if (_server != null && Online)
                {
                    _server.Disconnect();
                    _server.Close();
                }
            }
            catch
            {
                // Suppress on shutdown
            }
            finally
            {
                Online = false;
            }
        }

        public static bool SendInstruction(string instruction)
        {
            byte[] buff = Encoding.UTF8.GetBytes(instruction + ";[END]");
            return SendInstruction(buff);
        }

        public static bool SendInstruction(byte[] instruction)
        {
            if (_server == null || !Online)
            {
                return false;
            }

            try
            {
                _server.Write(instruction, 0, instruction.Length);
                _server.Flush();

                string response = ReceiveResponse();
                return response.Contains("Succeeded", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pipe Error] {ex.Message}");
                Disconnect();
                return false;
            }
        }

        public static string ReceiveResponse()
        {
            if (_server == null || !Online)
            {
                return string.Empty;
            }

            byte[] buff = new byte[2048];
            try
            {
                int read = _server.Read(buff, 0, buff.Length);
                if (read <= 0) return string.Empty;
                return Encoding.UTF8.GetString(buff, 0, read);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
