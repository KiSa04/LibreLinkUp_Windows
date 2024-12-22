using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Leaf.xNet;

namespace Stalker
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new StartupForm());
        }
    }
    public class FormWrapper : ApplicationContext
    {
        private Form _form;

        public FormWrapper(Form form)
        {
            _form = form;
            _form.FormClosed += (s, e) => ExitThread(); // Ensure the app closes when the GlucoseForm is closed
            _form.Show();
        }
    }

    public class StartupForm : Form
    {
        private string credentialsFile = "credentials.json";
        private string authToken;
        private string patientId;
        private string sha256Hash;

        public StartupForm()
        {
            this.Load += StartupForm_Load;
        }

        private async void StartupForm_Load(object sender, EventArgs e)
        {
            // Check if credentials exist
            if (File.Exists(credentialsFile))
            {
                var credentials = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(credentialsFile));
                string email = credentials.email.ToString();
                string pass = credentials.password.ToString();

                var isSuccess = await LoginAsync(email, pass);
                if (isSuccess)
                {
                    // Open the main form directly if login is successful
                    floating.GlucoseForm glucoseForm = new floating.GlucoseForm(authToken, sha256Hash, patientId);
                    glucoseForm.ShowDialog();

                }
                else
                {
                    ShowLoginForm();
                }
            }
            else
            {
                ShowLoginForm();
            }
        }


        private void ShowLoginForm()
        {
            LoginForm loginForm = new LoginForm();
            loginForm.FormClosed += (s, args) => this.Close();
            loginForm.ShowDialog();
        }

        private async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                Leaf.xNet.HttpRequest postReq = new Leaf.xNet.HttpRequest();
                var loginUrl = "https://api-eu.libreview.io/llu/auth/login";
                var conUrl = "https://api-eu.libreview.io/llu/connections";

                void addHeaders(Leaf.xNet.HttpRequest httpRequest, string auth, string hash)
                {
                    httpRequest.AddHeader("accept-encoding", "gzip");
                    httpRequest.AddHeader("Pragma", "no-cache");
                    httpRequest.AddHeader("connection", "Keep-Alive");
                    httpRequest.AddHeader("Sec-Fetch-Mode", "cors");
                    httpRequest.AddHeader("Sec-Fetch-Site", "cross-site");
                    httpRequest.AddHeader("sec-ch-ua-mobile", "?0");
                    httpRequest.AddHeader("Content-type", "application/json");
                    httpRequest.UserAgent = "HTTP Debugger/9.0.0.12";

                    httpRequest.AddHeader("product", "llu.android");
                    httpRequest.AddHeader("version", "4.12.0");
                    httpRequest.AddHeader("Cache-Control", "no-cache");
                    httpRequest.AddHeader("Accept-Encoding", "gzip");
                    if (auth != null)
                        httpRequest.AddHeader("Authorization", $"Bearer {auth}");
                    if (hash != null)
                        httpRequest.AddHeader("Account-Id", hash);
                }

                addHeaders(postReq, null, null);
                var requestBody = new { email = email, password = password };
                var json = JsonConvert.SerializeObject(requestBody);
                var request = postReq.Post(loginUrl, json, "application/json");

                if (request.StatusCode.ToString() == "OK")
                {
                    dynamic jsonResp = JsonConvert.DeserializeObject(request.ToString());
                    authToken = jsonResp.data.authTicket.token;
                    string input = jsonResp.data.user.id;
                    sha256Hash = ComputeSha256Hash(input);
                    postReq.ClearAllHeaders();
                    addHeaders(postReq, authToken, sha256Hash);
                    var connectionsResp = postReq.Get(conUrl);

                    if (connectionsResp.StatusCode.ToString() == "OK")
                    {
                        dynamic jsonCon = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        patientId = jsonCon.data[0].patientId;
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, authToken, sha256Hash);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
                return false;
            }
        }

        private static string ComputeSha256Hash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }

    public class LoginForm : Form
    {
        private Button loginButton;
        private TextBox emailTextBox;
        private TextBox passwordTextBox;
        private string credentialsFile = "credentials.json";
        private string authToken;
        private string patientId;
        private string sha256Hash;
        private System.Timers.Timer glucoseTimer;
        private bool _isDisposed = false;
        private bool _isLoginInProgress = false;
        private bool _isFormClosing = false;

        public LoginForm()
        {
            // Initialize components
            InitializeComponent();
            this.FormClosing += LoginForm_FormClosing;
        }

        private void InitializeComponent()
        {
            loginButton = new Button() { Text = "Login", Width = 100, Top = 80, Left = 50 };
            emailTextBox = new TextBox() { Top = 20, Left = 50, Width = 200 };
            passwordTextBox = new TextBox() { Top = 50, Left = 50, Width = 200, PasswordChar = '*' };

            loginButton.Click += LoginButton_Click;

            Controls.Add(emailTextBox);
            Controls.Add(passwordTextBox);
            Controls.Add(loginButton);

            // Basic form settings to make it look better
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Login";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                // Dispose managed resources here
                glucoseTimer?.Dispose();
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }

        private void LoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_isLoginInProgress)
            {
                e.Cancel = true;
                MessageBox.Show("Login is in progress. Please wait for it to complete.");
            }
            else
            {
                _isFormClosing = true;
            }
        }

        private async void LoginButton_Click(object sender, EventArgs e)
        {
            string email = emailTextBox.Text;
            string password = passwordTextBox.Text;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both email and password.");
                return;
            }

            _isLoginInProgress = true;
            var isSuccess = await LoginAsync(email, password);
            _isLoginInProgress = false;

            if (isSuccess && !_isFormClosing && !_isDisposed)
            {
                // Save credentials
                var credentials = new
                {
                    email = email,
                    password = password
                };
                File.WriteAllText(credentialsFile, JsonConvert.SerializeObject(credentials));

                Application.Restart();
                
            }
            else
            {
                MessageBox.Show("Login failed. Please check your credentials.");
            }
        }

        private async Task<bool> LoginAsync(string email, string password)
        {
            try
            {
                Leaf.xNet.HttpRequest postReq = new Leaf.xNet.HttpRequest();
                var loginUrl = "https://api-eu.libreview.io/llu/auth/login";
                var conUrl = "https://api-eu.libreview.io/llu/connections";

                void addHeaders(Leaf.xNet.HttpRequest httpRequest, string auth, string hash)
                {
                    httpRequest.AddHeader("accept-encoding", "gzip");
                    httpRequest.AddHeader("Pragma", "no-cache");
                    httpRequest.AddHeader("connection", "Keep-Alive");
                    httpRequest.AddHeader("Sec-Fetch-Mode", "cors");
                    httpRequest.AddHeader("Sec-Fetch-Site", "cross-site");
                    httpRequest.AddHeader("sec-ch-ua-mobile", "?0");
                    httpRequest.AddHeader("Content-type", "application/json");
                    httpRequest.UserAgent = "HTTP Debugger/9.0.0.12";

                    httpRequest.AddHeader("product", "llu.android");
                    httpRequest.AddHeader("version", "4.12.0");
                    httpRequest.AddHeader("Cache-Control", "no-cache");
                    httpRequest.AddHeader("Accept-Encoding", "gzip");
                    if (auth != null)
                        httpRequest.AddHeader("Authorization", $"Bearer {auth}");
                    if (hash != null)
                        httpRequest.AddHeader("Account-Id", hash);
                }

                addHeaders(postReq, null, null);
                var requestBody = new { email = email, password = password };
                var json = JsonConvert.SerializeObject(requestBody);
                var request = postReq.Post(loginUrl, json, "application/json");

                if (request.StatusCode.ToString() == "OK")
                {
                    dynamic jsonResp = JsonConvert.DeserializeObject(request.ToString());
                    authToken = jsonResp.data.authTicket.token;
                    string input = jsonResp.data.user.id;
                    sha256Hash = ComputeSha256Hash(input);
                    postReq.ClearAllHeaders();
                    addHeaders(postReq, authToken, sha256Hash);
                    var connectionsResp = postReq.Get(conUrl);

                    if (connectionsResp.StatusCode.ToString() == "OK")
                    {
                        dynamic jsonCon = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        patientId = jsonCon.data[0].patientId;
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, authToken, sha256Hash);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                if (!_isFormClosing && !_isDisposed)
                {
                    MessageBox.Show($"An error occurred: {ex.Message}");
                }
                return false;
            }
        }

        private static string ComputeSha256Hash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}