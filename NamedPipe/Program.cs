using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;

namespace NamedPipe
{
   internal class Program
   {
      private const string _PIPE_NAME = "NamedPipe";
      private const int _MAX_SERVER_INSTANCES = 1;
      private const int _CLIENT_CONNECT_TIMEOUT_MS = 2000;
      private const int _TASK_SHUTDOWN_TIMEOUT_MS = 2000;
      private const string _EXIT_SEQUENCE = "QUIT";

      static void Main(string[] args)
      {
         ConcurrentQueue<string> inputQueue = new ConcurrentQueue<string>();
         BlockingCollection<string> blockingQueue = new BlockingCollection<string>(inputQueue);

         Task serverTask = Task.Run(() => Server());
         Task.Delay(1000).Wait();
         Task clientTask = Task.Run(() => Client(blockingQueue));

         bool isDone = false;

         while (!isDone)
         {
            string input = Console.ReadLine();

            if (string.IsNullOrEmpty(input))
            {
               continue;
            }

            // Send input to client
            blockingQueue.Add(input);

            if (input.Equals(_EXIT_SEQUENCE, StringComparison.InvariantCultureIgnoreCase))
            {
               isDone = true;
            }
         }

         if (!clientTask.Wait(_TASK_SHUTDOWN_TIMEOUT_MS))
         {
            Console.WriteLine("Client failed to shut down within timeout");
         }
         
         if (!serverTask.Wait(_TASK_SHUTDOWN_TIMEOUT_MS))
         {
            Console.WriteLine("Server failed to shut down within timeout");
         }   
      }

      static void Server() 
      {
         bool isDone = false;
         NamedPipeServerStream serverPipe = null;
         StreamReader serverReader = null;
         StreamWriter serverWriter = null;

         try
         {
            serverPipe = new NamedPipeServerStream(_PIPE_NAME, PipeDirection.InOut, _MAX_SERVER_INSTANCES);
            serverReader = new StreamReader(serverPipe);
            serverWriter = new StreamWriter(serverPipe);

            while (!isDone)
            {
               if (serverPipe.IsConnected)
               {
                  string clientMsg = serverReader.ReadLine();

                  if (string.IsNullOrEmpty(clientMsg))
                  {
                     continue;
                  }

                  if (clientMsg.Equals(_EXIT_SEQUENCE, StringComparison.InvariantCultureIgnoreCase))
                  {
                     isDone = true;
                     Console.WriteLine($"Server: Exiting...");
                  }
                  else
                  {
                     Console.WriteLine($"Server: Received '{clientMsg}'");
                     serverWriter.WriteLine(string.Join("", clientMsg.Reverse()));
                     serverWriter.Flush();
                  }
               }
               else
               {
                  Console.WriteLine("Server: Waiting for client connection...");
                  serverPipe.WaitForConnection();
                  Console.WriteLine("Server: Connected");
               }
            }
         }
         finally
         {
            // Do not need as serverWriter will displose base stream: serverReader?.Dispose();
            serverWriter?.Dispose();
            // Do not need as serverWriter will displose base stream: serverPipe?.Dispose();
         }
      }

      static void Client(BlockingCollection<string> inputQueue)
      {
         bool isDone = false;
         NamedPipeClientStream clientPipe = null;
         StreamReader clientReader = null;
         StreamWriter clientWriter = null;

         try
         {
            clientPipe = new NamedPipeClientStream(".", _PIPE_NAME, PipeDirection.InOut);
            clientReader = new StreamReader(clientPipe);
            clientWriter = new StreamWriter(clientPipe);

            Console.WriteLine("Client: Waiting for server connection...");
            clientPipe.Connect(_CLIENT_CONNECT_TIMEOUT_MS);
            Console.WriteLine("Client: Connected");

            while (!isDone)
            {
               string input = inputQueue.Take();

               if (string.IsNullOrEmpty(input))
               {
                  continue;
               }

               clientWriter.WriteLine(input);
               clientWriter.Flush();

               if (input.Equals(_EXIT_SEQUENCE, StringComparison.InvariantCultureIgnoreCase))
               {
                  isDone = true;
                  Console.WriteLine($"Client: Exiting...");
               }
               else
               {
                  string serverMsg = clientReader.ReadLine();
                  Console.WriteLine($"Client: Received '{serverMsg}'");
               }
            }
         }
         catch (TimeoutException)
         {
            Console.WriteLine("Client failed to connect to server within timeout");
         }
         finally
         {
            if (clientPipe != null && clientPipe.IsConnected)
            {
               // Do not need as clientWriter will displose base stream: clientReader?.Dispose();
               clientWriter?.Dispose();
               // Do not need as clientWriter will displose base stream: clientPipe?.Dispose();
            }
         }
      }
   }
}
