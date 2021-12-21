using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace SocketServer
{
    public static class CommandProcessor
    {
        delegate void CommandHandler(string data, Socket socket);
        static private List<(string command, CommandHandler handler)> _commands;
        static CommandProcessor()
        {
            _commands = new List<(string command, CommandHandler handler)>();
            _commands.Add(("ECHO", Echo));
            _commands.Add(("TIME", Time));
            _commands.Add(("CLOSE", Close));
            _commands.Add(("ISALIVE", IsAlive));
            _commands.Add(("DOWNLOAD", Download));
            _commands.Add(("UPLOAD", Upload));
        }

        public static void ProcessStringWithCommand(string requestString, Socket requestHandler)
        {
            foreach (var element in _commands)
            {
                if (requestString.StartsWith(element.command + " ", StringComparison.InvariantCultureIgnoreCase) ||
                   String.Compare(requestString, element.command, true) == 0)
                {
                    element.handler(requestString.Remove(0, element.command.Length).TrimStart(), requestHandler);
                }
            }
        }

        private static void Echo(string data, Socket requestHandler)
        {
            Console.WriteLine(DateTime.Now.ToShortTimeString() + ": " + data);
            string message = "ваше сообщение доставлено";
            byte[] response = Encoding.Unicode.GetBytes(message);
            requestHandler.Send(response);
        }

        private static void Time(string data, Socket requestHandler)
        {
            Console.WriteLine(DateTime.Now.ToShortTimeString() + ": " + "Запрошено время на сервере");
            string message = DateTime.Now.ToString();
            byte[] response = Encoding.Unicode.GetBytes(message);
            requestHandler.Send(response);

        }

        private static void Close(string data, Socket requestHandler)
        {
            requestHandler.Shutdown(SocketShutdown.Both);
            requestHandler.Close();
            Console.WriteLine("Соединение закрыто клиентом");
        }
        //
        private static void IsAlive(string data, Socket requestHandler)
        {
            byte[] response = new byte[] { 1 };
            requestHandler.Send(response);
        }

        private static void Download(string filePath, Socket requestHandler)
        {
            var sendTimeoutMemory = requestHandler.SendTimeout;
            requestHandler.SendTimeout = 10000;

            var receiveTimeoutMemory = requestHandler.ReceiveTimeout;
            requestHandler.ReceiveTimeout = 10000;
            try
            {
                if (File.Exists(filePath))
                {
                    // размер файла
                    long length = (long)new FileInfo(filePath).Length;
                    byte[] lengthBuffer = BitConverter.GetBytes(length);

                    string message = "Передача файла...";
                    byte[] response = Encoding.Unicode.GetBytes(message);
                    requestHandler.Send(response);

                    // ждём ответ, что клиент готов
                    byte[] request = new byte[2];
                    var receivedBytesCount = requestHandler.Receive(request);
                    Console.WriteLine(message);
                    byte[] fileBuffer = new byte[ServerSettings.DataBufferLengthInBytes];
                    using (var fileStream = new FileStream(filePath, FileMode.Open))
                    {
                        if (request[0] == 1 && receivedBytesCount == 1)
                        {
                            requestHandler.Send(lengthBuffer);

                            for (int offsetInFile = 0; offsetInFile < length;)
                            {
                                var chunkSize = fileStream.Read(fileBuffer,0,ServerSettings.DataBufferLengthInBytes);

                                if (SendChunk(requestHandler, fileBuffer, chunkSize) == false)
                                {
                                    break;
                                }

                                offsetInFile += chunkSize;
                            }
                        }
                    }

                }
                else
                {
                    string message = "Файла не существует";
                    byte[] response = Encoding.Unicode.GetBytes(message);
                    requestHandler.Send(response);
                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
            finally
            {
                requestHandler.SendTimeout = sendTimeoutMemory;
                requestHandler.ReceiveTimeout = receiveTimeoutMemory;
            }
            Console.WriteLine("Завершение передачи");

        }

        private static void Upload(string filePath, Socket requestHandler)
        {

            // сервер сообщает, что готов получить файл
            byte[] response = new byte[] { 1 };
            requestHandler.Send(response);

            using (FileStream stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                byte[] fileDataBuffer = new byte[1024];
                byte[] lengthBuffer = new byte[sizeof(long)];
                requestHandler.Receive(lengthBuffer, 0, sizeof(long), SocketFlags.None);
                long fileLengthInBytes = BitConverter.ToInt64(lengthBuffer,0);

                int receivedBytesCount = 0;
                while (receivedBytesCount < fileLengthInBytes)
                {
                    var receivedBytesOnIteration = requestHandler.Receive(fileDataBuffer);

                    stream.Write(fileDataBuffer, 0, receivedBytesOnIteration);

                    receivedBytesCount += receivedBytesOnIteration;
                }
            }

        }

        private static bool SendChunk(Socket requestHandler, byte[] buffer, int size)
        {
            bool isChunkSent = false;
            while (!isChunkSent)
            {
                try
                {
                    requestHandler.Send(buffer, size, SocketFlags.None);

                    isChunkSent = true;
                    // если готов, отправляем, первым идёт lengthBuffer с длинной
                    //requestHandler.SendFile(filePath, lengthBuffer, null, TransmitFileOptions.UseDefaultWorkerThread);
                }
                catch (SocketException)
                {
                    var messageFromReceiver = new byte[sizeof(bool)];

                    try
                    {
                        Console.WriteLine("Отправка файла прервана. Ожидание запроса на продолжение от клиента...");
                        requestHandler.Receive(messageFromReceiver);
                    }
                    catch (SocketException)
                    {
                        Console.WriteLine("Отправка прервана");
                        return false;
                    }

                    var isRestoreNeeded = BitConverter.ToBoolean(messageFromReceiver,0);
                    if (isRestoreNeeded == true)
                    {
                        Console.WriteLine("Продолжение отправки");
                        continue;
                    }
                    else
                    {
                        Console.WriteLine("Клиент прекратил передачу");
                        return false;
                    }
                }
            }

            return true;

        }
    }
}


