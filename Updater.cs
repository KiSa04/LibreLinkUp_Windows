using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Leaf.xNet;

public class UpdateChecker
{
    private const string VersionUrl = "https://raw.githubusercontent.com/KiSa04/LibreLinkUp_Windows/refs/heads/main/version.json";
    private const string CurrentVersion = "1.0.0"; 

    public void CheckForUpdates()
    {
        try
        {
            using (HttpRequest client = new HttpRequest())
            {
                string json = client.Get(VersionUrl).ToString();

                dynamic versionInfo = Newtonsoft.Json.JsonConvert.DeserializeObject(json);
                
                string latestVersion = versionInfo.version;
                string downloadUrl = versionInfo.url;

                if (IsNewVersionAvailable(CurrentVersion, latestVersion))
                {
                    var result = MessageBox.Show($"A new version {latestVersion} is available. Would you like to update?",
                        "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                    {
                        DownloadAndUpdate(downloadUrl);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to check for updates: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
    {
        return string.Compare(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private void DownloadAndUpdate(string downloadUrl)
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), "LibreLinkUp.msi");

        using (HttpRequest client = new HttpRequest())
        {
            var response = client.Get(downloadUrl);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    response.ToMemoryStream().CopyTo(fs);
                }

                // Launch the installer and exit the app
                Process.Start(tempFilePath);
                Environment.Exit(0);
            }
            else
            {
                MessageBox.Show("Failed to download the update.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
