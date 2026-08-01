using System;
using System.Windows.Forms;
using Polly;

namespace Chat
{
    internal static class Program
    {
        public static bool TestBuild = true;
        public static string InstanceId = "1";

        [STAThread]
        static void Main(string[] args)
        {
            Log($"[START] Program.Main started. Command-line args count={args.Length}, Args: '{string.Join(" ", args)}'");

            try
            {
                Log($"Evaluating command-line args (args.Length = {args.Length})...");
                if (args.Length > 0)
                {
                    Log($"Setting InstanceId from args[0]: '{args[0]}'");
                    InstanceId = args[0];
                }

                Log($"Current active InstanceId: '{InstanceId}'");

                Log("Initializing WinForms configuration (ApplicationConfiguration.Initialize())...");
                ApplicationConfiguration.Initialize();
                Log("ApplicationConfiguration.Initialize() completed.");

                Log("Executing Application.Run(new Form1())...");
                Application.Run(new Form1());
                Log("Application.Run() exited normally.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Unhandled exception in Program.Main: {ex.GetType().Name} - {ex.Message}\nStack Trace:\n{ex.StackTrace}", LogLevel.Error);
                MessageBox.Show($"Kritik bir hata oluştu:\n{ex.Message}", "Kritik Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Log("[END] Program.Main exiting.");
        }
    }
}