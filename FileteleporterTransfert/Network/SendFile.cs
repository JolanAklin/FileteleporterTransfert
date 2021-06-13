﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using client;
using FileteleporterTransfert.Tools;
using System.Net.Sockets;

namespace FileteleporterTransfert.Network
{
    class SendFile
    {
        string filePath;
        private long fileLength;
        int filePos;
        public bool finished;
        private string ip;
        private TCPFileSend tcp;
        internal TCPFileSend Tcp { get => tcp;}


        public SendFile() { }

        public SendFile(string filePath, string ip)
        {
            this.filePath = filePath;
            this.ip = ip;
            filePos = 0;
            finished = false;
        }

        public SendFile(TcpClient client, bool connect)
        {
            filePos = 0;
            finished = false;
            if (connect)
            {
                tcp = new TCPFileSend();
                tcp.Connect(client);
            }
        }

        public void SendPartAsync()
        {
            Connect();
        }

        private void Connect()
        {
            SendFileTestPrepare(ip, SendAsync);
        }
        private void SendFileTestPrepare(string ip, Action canReceiveCallBack)
        {
            tcp = new TCPFileSend();
            tcp.Connect(ip, canReceiveCallBack, this);
        }

        private void SendAsync()
        {
            SendAsync(null);
        }

        private async void SendAsync(IAsyncResult asyncResult)
        {
            long timeStart = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
            await Task.Run(() => SendPart(() =>
            {
                long timeEnd = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                long timeElapsed = timeEnd - timeStart;
                if (timeElapsed == 0)
                    timeElapsed = 1;
                //EZConsole.WriteLine("SendFile", $"\n" +
                //        $"----------------------------------------\n" +
                //        $"{fileLength / 1048576} Mio transmited in {(float)timeElapsed / 1000} sec\n" +
                //        $"With a speed of {(float)(fileLength / (timeElapsed / 1000)) / 1048576} Mio/s\n" +
                //        $"----------------------------------------");

                //NetController.instance.SendData(NetController.ActionOnController.infos, new string[]
                //{
                //    $" - Raw length : {fileLength} B",
                //    $" - File length : {fileLength / 1048576} MiB",
                //    $" - Transmit time : {(float)timeElapsed / 1000} sec",
                //    $" - Transmit speed : {(float)(fileLength / (timeElapsed/1000)) / 1048576} MiB/s",

                //});

                tcp.Disconnect();
                tcp = null;
                GC.Collect();
            }));
        }

        private async void SendPart(Action callBack)
        {
            byte[] fileSmall;
            FileStream file = File.OpenRead(filePath);
            fileLength = file.Length;

            int lengthToRead = 0;
            if (fileLength < Constants.BUFFER_FOR_FILE)
                lengthToRead = (int)fileLength;
            else
                lengthToRead = Constants.BUFFER_FOR_FILE;

            Task<byte[]> readData = new Task<byte[]>(() => ReadData(file, lengthToRead));
            readData.Start();
            fileSmall = await readData;
            int length = 0;
            //int nbCalls = 0;

            while (!finished)
            {
                if(fileLength < filePos + Constants.BUFFER_FOR_FILE)
                {
                    if(fileLength == filePos)
                    {
                        finished = true;
                        callBack?.Invoke();
                        Console.WriteLine("final sent length:" + length);
                        return;
                    }
                    else
                    {
                        Console.WriteLine("pre final length:" + length);
                        lengthToRead = (int)fileLength - filePos;
                        Console.WriteLine("not buffer length:" + lengthToRead);
                    }
                }
                filePos += lengthToRead;
                length += lengthToRead;

                readData = new Task<byte[]>(() => ReadData(file, lengthToRead));
                readData.Start();

                tcp.SendDataSync(fileSmall);

                fileSmall = await readData;
            }
        }

        private byte[] ReadData(FileStream stream, int lengthToRead)
        {
            byte[] toRead = new byte[lengthToRead];
            stream.Read(toRead, 0, lengthToRead);
            return toRead;
        }

        public class TCPFileSend
        {
            public SendFile sendFile;
            public TcpClient socket;

            private NetworkStream stream;
            private byte[] receiveBuffer;
            private int dataBufferSize = Constants.BUFFER_FOR_FILE;

            private Action canReceiveCallBack;

            /// <summary>Attempts to connect to the server via TCP.</summary>
            public void Connect(string ip, Action canReceiveCallBack, SendFile sendFile)
            {
                this.canReceiveCallBack = canReceiveCallBack;
                this.sendFile = sendFile;
                socket = new TcpClient
                {
                    ReceiveBufferSize = dataBufferSize,
                    SendBufferSize = dataBufferSize
                };


                receiveBuffer = new byte[dataBufferSize];
                socket.BeginConnect(ip, Constants.SEND_FILE_PORT, ConnectCallback, socket);
            }

            public void Connect(TcpClient _socket)
            {
                socket = _socket;
                socket.ReceiveBufferSize = dataBufferSize;
                socket.SendBufferSize = dataBufferSize;

                stream = socket.GetStream();

                receiveBuffer = new byte[dataBufferSize];

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }

            /// <summary>Initializes the newly connected client's TCP-related info.</summary>
            private void ConnectCallback(IAsyncResult _result)
            {
                socket.EndConnect(_result);

                if (!socket.Connected)
                {
                    return;
                }

                stream = socket.GetStream();

                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
                canReceiveCallBack?.Invoke();
            }

            public void SendDataSync(byte[] file)
            {
                try
                {
                    if (socket != null)
                    {
                        stream.Write(file, 0, file.Length); // Send data to server
                    }
                }
                catch (Exception _ex)
                {
                    Console.WriteLine($"Error sending data to server via TCP: {_ex}");
                }
            }

            /// <summary>Reads incoming data from the stream.</summary>
            private byte[] _data;
            // pls only use this type of file stream, if use File.Open perfs will suffer
            private FileStream fileStream;
            Task t = null;
            long test = 0;
            /// <summary>Reads incoming data from the stream.</summary>
            private async void ReceiveCallback(IAsyncResult _result)
            {
                if(fileStream == null)
                    fileStream = File.OpenWrite("result1.dat");
                if (t != null)
                {
                    await t;
                }
                try
                {
                    int _byteLength = stream.EndRead(_result);
                    if (_byteLength <= 0)
                    {
                        Disconnect();
                        return;
                    }
                    GC.Collect();
                    _data = new byte[_byteLength];
                    Array.Copy(receiveBuffer, _data, _byteLength);

                    test += _byteLength;
                    Console.WriteLine(test);

                    t = new Task(() =>
                    {
                        fileStream.Write(_data, 0, _data.Length);
                    });
                    t.Start();
                    stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
                }
                catch (Exception _ex)
                {
                    Console.WriteLine($"Error receiving TCP data: {_ex}");
                    Disconnect();
                }
            }


            public void Disconnect()
            {
                stream = null;
                receiveBuffer = null;
                socket = null;
                sendFile.tcp = null;
                if(fileStream != null)
                    fileStream.Close();
                fileStream = null;
            }
        }
    }
}
