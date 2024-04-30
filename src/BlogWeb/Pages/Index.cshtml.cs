using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using System.Text;

namespace BlogWeb.Pages
{
    static class HgCmd
    {
        private static string RunCmd(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo()
            {
                FileName = "/usr/bin/hg",
                Arguments = arguments,
                RedirectStandardOutput = true,
                WorkingDirectory = "/srv/www/waitwhat/src"
            };
            using (Process proc = new Process() { StartInfo = startInfo, })
            {
                proc.Start();
                proc.WaitForExit(5000);

                return proc.StandardOutput.ReadToEnd();
            }
        }

        public static string ReadLog()
        {
            return RunCmd("log --graph -l 15");
        }

        public static string Pull()
        {
            return RunCmd("pull");
        }

        public static string Update()
        {
            return RunCmd("update");
        }
    }

    [IgnoreAntiforgeryToken]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public string? PullLog { get; set; }
        public string? Log { get; set; }

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            Log = HgCmd.ReadLog();
        }

        public void OnPost()
        {
            Log = HgCmd.ReadLog();
            var log = new StringBuilder();

            log.Append(HgCmd.Pull());
            log.Append(HgCmd.Update());

            Engine.runFolders(Engine.OutputPath.NewOutputPath("/srv/www/waitwhat/dist"), Engine.InputPath.NewInputPath("/srv/www/waitwhat/src"));

            PullLog = log.ToString();
        }
    }
}