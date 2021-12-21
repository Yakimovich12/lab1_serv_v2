using System;
using System.Collections.Generic;
using System.Text;

namespace SocketServer
{
    static public class ServerSettings
    {
        static ServerSettings()
        {
            RequestQueueLength = 10;
            ServerIp = "127.0.0.1";//"192.168.0.2";
            ServerPort = 8005;
            DataBufferLengthInBytes = 256;
            FileBufferLength = 1024;
        }

        static public int RequestQueueLength { get; }

        static public string ServerIp { get; }

        static public int ServerPort { get; }

        static public int DataBufferLengthInBytes { get; }

        public static int FileBufferLength { get; }
    }
}
