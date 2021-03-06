using System;
using System.Net.Sockets;
using System.Threading;
using FileteleporterTransfert.Tools;

namespace FileteleporterTransfert
{
    class Program
    {
        private static bool isRunning = false;

        static void Main(string[] args)
        {
            EZConsole.AddHeader("Server", "[SERVER]", ConsoleColor.DarkRed, ConsoleColor.White);
            EZConsole.AddHeader("Client", "[CLIENT]", ConsoleColor.Cyan, ConsoleColor.White);
            EZConsole.AddHeader("ThreadManager", "[THREADMANAGER]", ConsoleColor.Red, ConsoleColor.Red);
            EZConsole.AddHeader("NetController", "[NETCONTROLLER]", ConsoleColor.Blue, ConsoleColor.White);
            EZConsole.AddHeader("handle", "[HANDLENETCONTROLLER]", ConsoleColor.Magenta, ConsoleColor.White);
            EZConsole.AddHeader("error", "[ERROR]", ConsoleColor.Red, ConsoleColor.Red);

            NetController netController = new NetController("127.0.0.1", 56236, 56235);


            Console.Title = "Game Server";
            isRunning = true;

            Thread mainThread = new Thread(new ThreadStart(MainThread));
            mainThread.Start();

            server.Server.Start(50, 26950);

            client.Client client = new client.Client("127.0.0.1", "test");
            client.ConnectToServer();
        }

        private static void MainThread()
        {
            EZConsole.WriteLine($"Main thread started. Running at {Constants.TICKS_PER_SEC} ticks per second.", ConsoleColor.Green);
            DateTime _nextLoop = DateTime.Now;

            while (isRunning)
            {
                while (_nextLoop < DateTime.Now)
                {
                    // If the time for the next loop is in the past, aka it's time to execute another tick
                    GameLogic.Update(); // Execute game logic

                    _nextLoop = _nextLoop.AddMilliseconds(Constants.MS_PER_TICK); // Calculate at what point in time the next tick should be executed

                    if (_nextLoop > DateTime.Now)
                    {
                        // If the execution time for the next tick is in the future, aka the server is NOT running behind
                        Thread.Sleep(_nextLoop - DateTime.Now); // Let the thread sleep until it's needed again.
                    }
                }
            }
        }
    }
}
