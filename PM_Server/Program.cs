using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace PM_Server
{
    class Program
    {
        static Socket socket;
        static IPAddress serverAddress;
        static int serverPort;
        static IPEndPoint serverEndPoint;

        static byte[] buffer;
        static List<EndPoint> clientEndPoints;
        static ConcurrentQueue<byte[]> concurrentQueue;
        //static BlockingCollection<byte[]> blockingCollection;

        static void Main(string[] args)
        {
            //Init
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverAddress = IPAddress.Parse("127.0.0.1");
            serverPort = 12345;
            serverEndPoint = new IPEndPoint(serverAddress, serverPort);
            socket.Bind(serverEndPoint);
            Console.WriteLine("Server available at: {0}\n", serverPort);

            buffer = new byte[1024];
            clientEndPoints = new List<EndPoint>();
            concurrentQueue = new ConcurrentQueue<byte[]>();
            //blockingCollection = new BlockingCollection<byte[]>();

            while (true)
            {
                EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int receivedBytes = socket.ReceiveFrom(buffer, ref clientEndPoint);

                //Received data
                byte code;
                string message;
                byte a, r, g, b;
                int thickness;
                double x, y;
                string output = "";

                BinaryFormatter formatter = new BinaryFormatter();
                using (var ms = new System.IO.MemoryStream(buffer, 0, receivedBytes))
                {
                    int id = clientEndPoints.FindIndex(x => x.Equals(clientEndPoint));
                    code = (byte)formatter.Deserialize(ms);

                    if (code == 0x00)
                    {
                        message = (string)formatter.Deserialize(ms);

                        if (message == "connect")
                        {
                            byte[] data = null;
                            using (var ms2 = new System.IO.MemoryStream())
                            {
                                formatter.Serialize(ms2, code);
                                formatter.Serialize(ms2, serverPort);
                                data = ms2.ToArray();
                            }
                            socket.SendTo(data, clientEndPoint);

                            clientEndPoints.Add(clientEndPoint);
                            output += '\n' + clientEndPoint.ToString() + " connected";
                        }
                        else if (message == "disconnect")
                        {
                            clientEndPoints.Remove(clientEndPoint);
                            output += '\n' + clientEndPoint.ToString() + " disconnected";
                        }
                    }
                    else if (code == 0x01)
                    {
                        a = (byte)formatter.Deserialize(ms);
                        r = (byte)formatter.Deserialize(ms);
                        g = (byte)formatter.Deserialize(ms);
                        b = (byte)formatter.Deserialize(ms);
                        thickness = (int)formatter.Deserialize(ms);

                        x = (double)formatter.Deserialize(ms);
                        y = (double)formatter.Deserialize(ms);

                        output += '\n' + clientEndPoint.ToString() + " has started drawing";

                        byte[] data = null;
                        using (var ms2 = new System.IO.MemoryStream())
                        {
                            formatter.Serialize(ms2, code);
                            formatter.Serialize(ms2, a);
                            formatter.Serialize(ms2, r);
                            formatter.Serialize(ms2, g);
                            formatter.Serialize(ms2, b);
                            formatter.Serialize(ms2, thickness);
                            formatter.Serialize(ms2, x);
                            formatter.Serialize(ms2, y);
                            formatter.Serialize(ms2, (byte)id);
                            data = ms2.ToArray();
                        }
                        concurrentQueue.Enqueue(data);

                        //Get from queue and send
                        byte[] newData = null;
                        concurrentQueue.TryDequeue(out newData);
                        clientEndPoints.ForEach(client => socket.SendTo(newData, client));
                    }
                    else if (code == 0x02)
                    {
                        x = (double)formatter.Deserialize(ms);
                        y = (double)formatter.Deserialize(ms);

                        byte[] data = null;
                        using (var ms2 = new System.IO.MemoryStream())
                        {
                            formatter.Serialize(ms2, code);
                            formatter.Serialize(ms2, x);
                            formatter.Serialize(ms2, y);
                            formatter.Serialize(ms2, (byte)id);
                            data = ms2.ToArray();
                        }
                        concurrentQueue.Enqueue(data);

                        //Get from queue and send
                        byte[] newData = null;
                        concurrentQueue.TryDequeue(out newData);
                        clientEndPoints.ForEach(client => socket.SendTo(newData, client));
                    }
                    else if (code == 0x03)
                    {
                        output += '\n' + clientEndPoint.ToString() + " has stopped drawing";

                        byte[] data = null;
                        using (var ms2 = new System.IO.MemoryStream())
                        {
                            formatter.Serialize(ms2, code);
                            formatter.Serialize(ms2, (byte)id);
                            data = ms2.ToArray();
                        }
                        concurrentQueue.Enqueue(data);

                        //Get from queue and send
                        byte[] newData = null;
                        concurrentQueue.TryDequeue(out newData);
                        clientEndPoints.ForEach(client => socket.SendTo(newData, client));
                    }
                }
                Console.Write(output);
            }
            socket.Close();
        }
    }
}