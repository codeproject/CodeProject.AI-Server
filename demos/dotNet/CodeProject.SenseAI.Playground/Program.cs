using System;
using System.Windows.Forms;

namespace CodeProject.SenseAI.Demo.Playground
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            Application.Run(new Form1());
        }
    }
}
