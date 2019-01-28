using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HClient
{
    class Program
    {
        static Socket mySocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        static MemoryStream ms = new MemoryStream(new byte[256*100], 0, 256*100, true, true);
        
        static BinaryWriter writer = new BinaryWriter(ms);
        static BinaryReader reader = new BinaryReader(ms);//чтение из потока

        static void Main(string[] args)
        {
            Console.Title = "Client";
            Console.WriteLine("Подключение к серверу");

            while (!mySocket.Connected)
            {
                mySocket.Connect(@"ordashack.sytes.net", 80);
                
            }
            Console.Clear();
            Console.WriteLine("Подключенo.");
            Thread.Sleep(1000);
            Console.Clear();

            Task.Run(() => { while (true) recivePocket(); });
            while(true)
            {
                //recivePocket();
                //sendPocket();
            }

        }

        static void sendPocket()
        {
            ms.Position = 0;
            writer.Write("ok");
            mySocket.Send(ms.GetBuffer());
        }

        private static void recivePocket()
        {
            ms.Position = 0;
            mySocket.Receive(ms.GetBuffer());
            string request = reader.ReadString();
            if(request.Equals("dir"))
            {
                ms.Position = 0;

                List<string> dirList = new List<string>();//список файлов

                DirectoryInfo dr = new DirectoryInfo(@"C:\Users\Иван\Desktop\qwert");//получаем файлы
                foreach (FileInfo fi in dr.GetFiles())//получаем в список файлы
                {
                    dirList.Add(fi.ToString());                 
                }
                foreach (DirectoryInfo fi in dr.GetDirectories())//получаем папки
                {
                    dirList.Add(fi.ToString());                                      
                }

                writer.Write((Int32)dirList.Count);//записываем колличество данных
                foreach (var fi in dirList)//записываем в врайтер список
                {
                    writer.Write(fi);
                }
                
                mySocket.Send(ms.GetBuffer());//посылаем
            }
            else
            {
                ms.Position = 0;
                writer.Write((Int32)1);
                writer.Write("wrong request");
                writer.Write("-1");
                mySocket.Send(ms.GetBuffer());
            }
        }
    }
}
