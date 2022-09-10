using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {

        GoogleDriveClass Drive = new GoogleDriveClass();
        private static int numThreads = 10;
        private static string pipeName = "GoogleBridge";
        static Thread[] servers;
        public Form1()
        {
            InitializeComponent();
            PipesCreate();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        public void PipesCreate()
        {
            int i;
            servers = new Thread[numThreads];

            for (i = 0; i < numThreads; i++)
            {
                servers[i] = new Thread(ServerThread);
                servers[i].Start();
            }
        }

        private void ServerThread()
        {
            NamedPipeServerStream pipeServer =
                new NamedPipeServerStream(pipeName, PipeDirection.InOut, numThreads, PipeTransmissionMode.Message, PipeOptions.Asynchronous);

            int threadId = Thread.CurrentThread.ManagedThreadId;
            // Wait for a client to connect
            AsyncCallback asyn_connected = new AsyncCallback(Connected);
            try
            {
                pipeServer.BeginWaitForConnection(asyn_connected, pipeServer);
            }
            catch (Exception)
            {
                servers[threadId].Suspend();
                servers[threadId].Start();
            }
        }

        private void Connected(IAsyncResult pipe)
        {
            if (!pipe.IsCompleted)
                return;
            bool exit = false;
            try
            {

                NamedPipeServerStream pipeServer = (NamedPipeServerStream)pipe.AsyncState;
                try
                {
                    if (!pipeServer.IsConnected)
                        pipeServer.WaitForConnection();
                }
                catch (IOException)
                {
                    AsyncCallback asyn_connected = new AsyncCallback(Connected);
                    pipeServer.Dispose();
                    pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, numThreads, PipeTransmissionMode.Message, PipeOptions.Asynchronous);
                    pipeServer.BeginWaitForConnection(asyn_connected, pipeServer);
                    return;
                }
                while (!exit && pipeServer.IsConnected)
                {
                    // Read the request from the client. Once the client has
                    // written to the pipe its security token will be available.

                    while (pipeServer.IsConnected)
                    {
                        if (!ReadMessage(pipeServer))
                        {
                            exit = true;
                            break;
                        }
                    }
                    //Wait for a client to connect
                    AsyncCallback asyn_connected = new AsyncCallback(Connected);
                    pipeServer.Disconnect();
                    pipeServer.BeginWaitForConnection(asyn_connected, pipeServer);
                    break;
                }
            }
            finally
            {
                exit = true;
            }

        }

        private bool ReadMessage(PipeStream pipe)
        {
            if (!pipe.IsConnected)
                return false;

            byte[] arr_read = new byte[1024];
            string message = null;
            int length;
            do
            {
                length = pipe.Read(arr_read, 0, 1024);
                if (length > 0)
                    message += Encoding.Default.GetString(arr_read, 0, length);
            } while (length >= 1024 && pipe.IsConnected);
            if (message == null)
                return true;
            message = message.Substring(0, message.Length - 1);

            if (message.Trim() == "Close")
                return false;

            string result = null;
            string[] separates = { ";" };
            string[] arr_message = message.Split(separates, StringSplitOptions.RemoveEmptyEntries);
            if (arr_message[0].Trim() == "Read")
            {
                //Action action = () =>
                //    {
                try
                {
                    result = Drive.FileRead(Drive.GetFileId(arr_message[1].Trim() + GoogleDriveClass.extension));
                }
                catch (Exception e)
                {
                    result = "Error " + e.ToString();
                    Drive.Authorize();
                }
                //    };
                //if (InvokeRequired) Invoke(action); else action();
                return WriteMessage(pipe, result);
            }

            if (arr_message[0].Trim() == "Write")
            {
                try
                {
                    result = (Drive.FileUpdate(arr_message[1].Trim() + GoogleDriveClass.extension, arr_message[2].Trim()) ? "Ok" : "Error");
                }
                catch (Exception e)
                {
                    result = "Error " + e.ToString();
                    Drive.Authorize();
                }

                return WriteMessage(pipe, result);
            }
            return true;
        }

        private bool WriteMessage(PipeStream pipe, string message)
        {
            if (!pipe.IsConnected)
                return false;
            if (message == null || message.Count() == 0)
                message = "Empty";
            byte[] arr_bytes = Encoding.Default.GetBytes(message);
            try
            {
                pipe.Flush();
                pipe.Write(arr_bytes, 0, arr_bytes.Count());
                pipe.Flush();
            }
            catch (IOException)
            {
                return false;
            }
            return true;
        }
    }
}
