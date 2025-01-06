using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Leaf.xNet;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.ComponentModel.Design;
using System.Reflection;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace Stalker
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AddAppToStartup();
            Application.Run(new StartupForm());
        }
        private static void AddAppToStartup()
        {
            string appPath = Application.ExecutablePath;

            // The registry key where startup applications are registered
            string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryKey, true))
            {
                // Add the application to the registry
                key.SetValue("Stalker", appPath);
            }
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
        private string credentialsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kisa",
            "LibreLinkUp",
            "credentials.json"
        );

        private string authToken;
        private string patientId;
        private string sha;

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
                    floating.GlucoseForm glucoseForm = new floating.GlucoseForm(authToken, sha, patientId);
                    FormClosingEventHandler glucoseFormCloseEvent = (s, args) => this.Close();
                    glucoseForm.FormClosing += glucoseFormCloseEvent;
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
                var loginUrl = "https://api.libreview.io/llu/auth/login";
                var conUrl = "https://api.libreview.io/llu/connections";

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
                string region = null;

                if (request.StatusCode.ToString() == "OK")
                {
                    string requested = null;
                    bool actual = false;
                    dynamic JsonLogin = JsonConvert.DeserializeObject(request.ToString());
                    if (JsonLogin.data.region != null)
                    {
                        region = $"-{JsonLogin.data.region}";
                        loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                        conUrl = $"https://api{region}.libreview.io/llu/connections";
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, null, null);
                        requested = postReq.Post(loginUrl, json, "application/json").ToString();
                    }
                    else
                        actual = true;

                    string input = null;
                    if (requested != null || actual == true)
                    {
                        if (requested != null)
                        {
                            dynamic jsonResp = JsonConvert.DeserializeObject(requested);
                            authToken = jsonResp.data.authTicket.token;
                            input = jsonResp.data.user.id;

                        }
                        else if (actual == true)
                        {
                            authToken = JsonLogin.data.authTicket.token;
                            input = JsonLogin.data.user.id;
                        }
                        sha = input;
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, authToken, sha);
                        var connectionsResp = postReq.Get(conUrl);

                        if (connectionsResp.StatusCode.ToString() == "OK")
                        {
                            dynamic jsonCon = JsonConvert.DeserializeObject(connectionsResp.ToString());
                            patientId = jsonCon.data[0].patientId;
                            postReq.ClearAllHeaders();
                            addHeaders(postReq, authToken, sha);
                            return true;
                        }
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
    }

    public class LoginForm : Form
    {
        private Button loginButton;
        private TextBox emailTextBox;
        private TextBox passwordTextBox;
        private string credentialsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kisa",
            "LibreLinkUp",
            "credentials.json"
        );
        private string authToken;
        private string patientId;
        private string sha;
        private System.Timers.Timer glucoseTimer;
        private bool _isDisposed = false;
        private bool _isLoginInProgress = false;
        private bool _isFormClosing = false;
        private int borderRadius = 17;
        private int borderSize = 0;
        private Color borderColor = Color.FromArgb(128, 128, 255);
        private const int ButtonSize = 12; 
        private const int ButtonPadding = 10; 

        public LoginForm()
        {
            // Initialize components
            InitializeComponent();
            this.FormClosing += LoginForm_FormClosing;
            this.Icon = Properties.Resources.icon;
        }
        private Image LoadEmbeddedImage(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Resource '{resourceName}' not found.");
                return Image.FromStream(stream);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000; // <--- Minimize borderless form from taskbar
                return cp;
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float curveSize = radius * 2F;

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, curveSize, curveSize, 180, 90);
            path.AddArc(rect.Right - curveSize, rect.Y, curveSize, curveSize, 270, 90);
            path.AddArc(rect.Right - curveSize, rect.Bottom - curveSize, curveSize, curveSize, 0, 90);
            path.AddArc(rect.X, rect.Bottom - curveSize, curveSize, curveSize, 90, 90);
            path.CloseFigure();
            return path;
        }
        private void FormRegionAndBorder(Form form, float radius, Graphics graph, Color borderColor, float borderSize)
        {
            if (this.WindowState != FormWindowState.Minimized)
            {
                using (GraphicsPath roundPath = GetRoundedPath(form.ClientRectangle, radius))
                using (Pen penBorder = new Pen(borderColor, borderSize))
                using (Matrix transform = new Matrix())
                {
                    graph.SmoothingMode = SmoothingMode.AntiAlias;
                    form.Region = new Region(roundPath);
                    if (borderSize >= 1)
                    {
                        Rectangle rect = form.ClientRectangle;
                        float scaleX = 1.0F - ((borderSize + 1) / rect.Width);
                        float scaleY = 1.0F - ((borderSize + 1) / rect.Height);

                        transform.Scale(scaleX, scaleY);
                        transform.Translate(borderSize / 1.6F, borderSize / 1.6F);

                        graph.Transform = transform;
                        graph.DrawPath(penBorder, roundPath);
                    }
                }
            }
        }
        private void ControlRegionAndBorder(Control control, float radius, Graphics graph, Color borderColor)
        {
            using (GraphicsPath roundPath = GetRoundedPath(control.ClientRectangle, radius))
            using (Pen penBorder = new Pen(borderColor, 1))
            {
                graph.SmoothingMode = SmoothingMode.AntiAlias;
                control.Region = new Region(roundPath);
                graph.DrawPath(penBorder, roundPath);
            }
        }
        private void DrawPath(Rectangle rect, Graphics graph, Color color)
        {
            using (GraphicsPath roundPath = GetRoundedPath(rect, borderRadius))
            using (Pen penBorder = new Pen(color, 3))
            {
                graph.DrawPath(penBorder, roundPath);
            }
        }
        private struct FormBoundsColors
        {
            public Color TopLeftColor;
            public Color TopRightColor;
            public Color BottomLeftColor;
            public Color BottomRightColor;
        }
        private FormBoundsColors GetFormBoundsColors()
        {
            var fbColor = new FormBoundsColors();
            using (var bmp = new Bitmap(1, 1))
            using (Graphics graph = Graphics.FromImage(bmp))
            {
                Rectangle rectBmp = new Rectangle(0, 0, 1, 1);

                //Top Left
                rectBmp.X = this.Bounds.X - 1;
                rectBmp.Y = this.Bounds.Y;
                graph.CopyFromScreen(rectBmp.Location, Point.Empty, rectBmp.Size);
                fbColor.TopLeftColor = bmp.GetPixel(0, 0);

                //Top Right
                rectBmp.X = this.Bounds.Right;
                rectBmp.Y = this.Bounds.Y;
                graph.CopyFromScreen(rectBmp.Location, Point.Empty, rectBmp.Size);
                fbColor.TopRightColor = bmp.GetPixel(0, 0);

                //Bottom Left
                rectBmp.X = this.Bounds.X;
                rectBmp.Y = this.Bounds.Bottom;
                graph.CopyFromScreen(rectBmp.Location, Point.Empty, rectBmp.Size);
                fbColor.BottomLeftColor = bmp.GetPixel(0, 0);

                //Bottom Right
                rectBmp.X = this.Bounds.Right;
                rectBmp.Y = this.Bounds.Bottom;
                graph.CopyFromScreen(rectBmp.Location, Point.Empty, rectBmp.Size);
                fbColor.BottomRightColor = bmp.GetPixel(0, 0);
            }
            return fbColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rectForm = this.ClientRectangle;
            int mWidht = rectForm.Width / 2;
            int mHeight = rectForm.Height / 2;
            var fbColors = GetFormBoundsColors();
            DrawPath(rectForm, e.Graphics, fbColors.TopLeftColor);
            Rectangle rectTopRight = new Rectangle(mWidht, rectForm.Y, mWidht, mHeight);
            int buttonY = 10; // Vertical position for the buttons
            int buttonX = this.Width - ButtonSize - ButtonPadding; // Horizontal position (right-aligned)


            FormRegionAndBorder(this, borderRadius, e.Graphics, borderColor, borderSize);
            DrawButton(e.Graphics, buttonX, buttonY, Color.Red);   
            DrawButton(e.Graphics, buttonX - ButtonSize - 5, buttonY, Color.Green); 
        }

        private void DrawButton(Graphics graphics, int x, int y, Color color)
        {
            using (Brush brush = new SolidBrush(color))
            {
                graphics.FillEllipse(brush, x, y, ButtonSize, ButtonSize); // Draw a circle (button)
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            // Check if the click is inside any of the buttons
            int buttonY = 10; // Vertical position of the buttons
            int buttonX = this.Width - ButtonSize - ButtonPadding; // Horizontal position (right-aligned)

            if (IsPointInsideButton(e.X, e.Y, buttonX, buttonY))
            {
                this.Close();
            }
            // Minimize button
            else if (IsPointInsideButton(e.X, e.Y, buttonX - ButtonSize - 5, buttonY))
            {
                this.WindowState = FormWindowState.Minimized;
            }
            // Maximize button
            else if (IsPointInsideButton(e.X, e.Y, buttonX - 2 * (ButtonSize + 5), buttonY))
            {
                this.WindowState = (this.WindowState == FormWindowState.Normal) ? FormWindowState.Maximized : FormWindowState.Normal;
            }
        }

        private bool IsPointInsideButton(int mouseX, int mouseY, int buttonX, int buttonY)
        {
            return mouseX >= buttonX && mouseX <= buttonX + ButtonSize &&
                   mouseY >= buttonY && mouseY <= buttonY + ButtonSize;
        }


        private void InitializeComponent()
        {
            // PictureBox for the glucometer image

            // PictureBox for the LibreLinkUp icon
            PictureBox libreLinkUpPictureBox = new PictureBox()
            {
                Top = 50,
                Left = 50,
                Width = 250,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Image = LoadEmbeddedImage("Stalker.LLU-Logo.png")
            };

            // Labels
            Label emailLabel = new Label()
            {
                Text = "Email",
                Top = 110,
                Left = 50,
                Width = 250,
                Font = new Font("Arial", 10, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Label passwordLabel = new Label()
            {
                Text = "Password",
                Top = 170,
                Left = 50,
                Width = 250,
                Font = new Font("Arial", 10, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Input fields
            emailTextBox = new TextBox()
            {
                Top = 135,
                Left = 50,
                Width = 250,
                Font = new Font("Arial", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            passwordTextBox = new TextBox()
            {
                Top = 195,
                Left = 50,
                Width = 250,
                Font = new Font("Arial", 10),
                PasswordChar = '•',
                BorderStyle = BorderStyle.FixedSingle
            };

            // Login button
            loginButton = new Button()
            {
                Text = "Login",
                Width = 250,
                Height = 35,
                Top = 250,
                Left = 50,
                BackColor = Color.FromArgb(45, 137, 239),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            loginButton.FlatAppearance.BorderSize = 0;
            loginButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 120, 200);
            loginButton.Click += LoginButton_Click;

            // Form settings
            this.Text = "Login";
            this.ClientSize = new Size(350, 320);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            

            // Add controls to form
            //Controls.Add(glucometerPictureBox);
            Controls.Add(libreLinkUpPictureBox);
            //Controls.Add(headerLabel);
            Controls.Add(emailLabel);
            Controls.Add(emailTextBox);
            Controls.Add(passwordLabel);
            Controls.Add(passwordTextBox);
            Controls.Add(loginButton);
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
                string directoryPath = Path.GetDirectoryName(credentialsFile);

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

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
                string region = "";
                var loginUrl = $"https://api.libreview.io/llu/auth/login";
                var conUrl = $"https://api.libreview.io/llu/connections";

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
                var loginResp = postReq.Post(loginUrl, json, "application/json");
                string request = null;
                if (((int)loginResp.StatusCode) == 200)
                {
                    dynamic JsonLogin = JsonConvert.DeserializeObject(loginResp.ToString());
                    bool actual = false;
                    if (JsonLogin.data.region != null)
                    {
                        region = $"-{JsonLogin.data.region}";
                        loginUrl = $"https://api{region}.libreview.io/llu/auth/login";
                        conUrl = $"https://api{region}.libreview.io/llu/connections";
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, null, null);
                        request = postReq.Post(loginUrl, json, "application/json").ToString();
                    }
                    else
                        actual = true;

                    string input = null;
                    if (request != null || actual == true)
                    {
                        if (request != null)
                        {
                            dynamic jsonResp = JsonConvert.DeserializeObject(request);
                            authToken = jsonResp.data.authTicket.token;
                            input = jsonResp.data.user.id;

                        }
                        else if (actual == true)
                        {
                            authToken = JsonLogin.data.authTicket.token;
                            input = JsonLogin.data.user.id;
                        }
                        sha = input;
                        postReq.ClearAllHeaders();
                        addHeaders(postReq, authToken, sha);
                        var connectionsResp = postReq.Get(conUrl);

                        if (connectionsResp.StatusCode.ToString() == "OK")
                        {
                            dynamic jsonCon = JsonConvert.DeserializeObject(connectionsResp.ToString());
                            patientId = jsonCon.data[0].patientId;
                            postReq.ClearAllHeaders();
                            addHeaders(postReq, authToken, sha);
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                if (!_isFormClosing && !_isDisposed)
                {
                }
                return false;
            }
        }
    }
}
