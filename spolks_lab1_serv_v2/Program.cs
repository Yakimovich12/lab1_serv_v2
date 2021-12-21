using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.IO.Pipes;

namespace SocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Установленно соединение");

                string serializedSocket;

                using (NamedPipeClientStream pipeClient = new NamedPipeClientStream(".", args[0], PipeDirection.In))
                {
                    Console.Write("Установка соединения канала...");
                    pipeClient.Connect();

                    using (StreamReader pipeReader = new StreamReader(pipeClient))
                    {
                        serializedSocket = pipeReader.ReadLine();
                    }

                }

                var socketInfo = Deserialize(serializedSocket);
                var handler = new Socket(socketInfo);

                ProcessRequest(handler);

                Console.WriteLine("Соединение закрыто");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static SocketInformation Deserialize(string serialized)
        {
            string[] split = serialized.Split(':');
            return new SocketInformation
            {
                Options = (SocketInformationOptions)Enum.Parse(typeof(SocketInformationOptions),split[0]),
                ProtocolInformation = Convert.FromBase64String(split[1])
            };
        }


        static public Socket ConfigureSocket()
        {
            // получаем адреса для запуска сокета
            IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(ServerSettings.ServerIp), ServerSettings.ServerPort);

            // создаем сокет
            Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // связываем сокет с локальной точкой, по которой будем принимать данные
            listenSocket.Bind(ipPoint);

            // начинаем прослушивание
            listenSocket.Listen(ServerSettings.RequestQueueLength);

            return listenSocket;

        }

        static public void ProcessRequest(Socket requestHandler)
        {
            try
            {
                do
                {

                    byte[] data = new byte[ServerSettings.DataBufferLengthInBytes]; // буфер для получаемых данных

                    StringBuilder receivedString = new StringBuilder();

                    int receivedBytesCount = 0;

                    do
                    {

                        receivedBytesCount = requestHandler.Receive(data);
                        receivedString.Append(Encoding.Unicode.GetString(data, 0, receivedBytesCount));
                    }
                    while (requestHandler.Available > 0);

                    CommandProcessor.ProcessStringWithCommand(receivedString.ToString(), requestHandler);
                }
                while (requestHandler.Connected);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (requestHandler.Connected)
                {
                    requestHandler.Shutdown(SocketShutdown.Both);
                    requestHandler.Close();
                }
            }
        }
    }
}