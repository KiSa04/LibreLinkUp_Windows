using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;
using System.ComponentModel.Design;
using System.Web.ModelBinding;

namespace Stalker
{
    internal class floating
    {
        public class GlucoseForm : Form
        {
            private string authToken;
            private string sha256Hash;
            private string patientId;
            private System.Timers.Timer glucoseTimer;
            private Label glucoseLabel, avgLabel /*tirLabel*/;
            private bool isDragging = false;
            private Point mouseOffset;
            private Chart glucoseChart;
            private NotifyIcon trayIcon;
            private ContextMenu trayMenu;

            public GlucoseForm(string authToken, string sha256Hash, string patientId)
            {
                this.authToken = authToken;
                this.sha256Hash = sha256Hash;
                this.patientId = patientId;

                trayMenu = new ContextMenu();
                trayMenu.MenuItems.Add("Exit", ExitApp);

                trayIcon = new NotifyIcon()
                {
                    Icon = Properties.Resources.icon, 
                    ContextMenu = trayMenu,
                    Visible = true
                };

                trayIcon.DoubleClick += TrayIcon_DoubleClick;

                this.ShowInTaskbar = false;

                glucoseLabel = new Label()
                {
                    Text = "Glucose: Loading...",
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ImageAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Font = new System.Drawing.Font("Arial", 14)
                };

                avgLabel = new Label()
                {
                    Text = "",
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ImageAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Font = new System.Drawing.Font("Arial", 14),
                    Visible = false
                };

                /*tirLabel = new Label()
                {
                    Text = "",
                    ForeColor = System.Drawing.Color.White,
                    BackColor = System.Drawing.Color.Black,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ImageAlign = ContentAlignment.MiddleCenter,
                    AutoSize = true,
                    Font = new System.Drawing.Font("Arial", 14),
                    Visible = false
                };*/

                glucoseChart = new Chart()
                {
                    Location = new Point(0, 50),
                    Size = new Size(350, 200) 
                };

                ChartArea chartArea = new ChartArea();
                glucoseChart.ChartAreas.Add(chartArea);

                Series series = new Series
                {
                    Name = "Glucose",
                    Color = Color.Blue,
                    BorderWidth = 2,
                    IsVisibleInLegend = false
                };

                glucoseChart.Series.Add(series);
                glucoseChart.Visible = false;

                Controls.Add(glucoseChart);
                Controls.Add(glucoseLabel);
                Controls.Add(avgLabel);
                //Controls.Add(tirLabel);

                this.FormBorderStyle = FormBorderStyle.None;
                this.TopMost = true;
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new System.Drawing.Point(10, 10); 
                this.BackColor = System.Drawing.Color.Black;
                this.Opacity = 0.8;
                this.Width = 150;
                this.Height = 50;
                this.Icon = Properties.Resources.icon;

                this.MouseDown += GlucoseForm_MouseDown;
                this.MouseMove += GlucoseForm_MouseMove;
                this.MouseUp += GlucoseForm_MouseUp;
                this.MouseHover += GlucoseForm_MouseHover;
                this.MouseLeave += GlucoseForm_MouseLeave;

                glucoseLabel.Left = (this.ClientSize.Width - glucoseLabel.Width) / 2;
                glucoseLabel.Top = (this.ClientSize.Height - glucoseLabel.Height) / 2;

                avgLabel.Left = ((this.ClientSize.Width + glucoseLabel.Width) - avgLabel.Width) / 2;
                avgLabel.Top = (this.ClientSize.Height - avgLabel.Height) / 2;

                //tirLabel.Left = (this.ClientSize.Width - tirLabel.Width) / 2;
                //tirLabel.Top = (this.ClientSize.Height - tirLabel.Height) / 2;

                _ = GetLatestGlucoseValue();

                UpdateChecker updateChecker = new UpdateChecker();
                updateChecker.CheckForUpdates();
                glucoseTimer = new System.Timers.Timer();
                glucoseTimer.Interval = 60000;
                glucoseTimer.Elapsed += GlucoseTimer_Elapsed;
                glucoseTimer.Start();
            }
            private void TrayIcon_DoubleClick(object sender, EventArgs e)
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                else
                {
                    this.WindowState = FormWindowState.Minimized;
                }
            }

            private void ExitApp(object sender, EventArgs e)
            {
                trayIcon.Visible = false;
                Application.Exit();
            }
            private void GlucoseForm_MouseLeave(object sender, EventArgs e)
            {
                glucoseChart.Visible = false;
                avgLabel.Visible = false;
                glucoseLabel.Text = nVal;
                this.Width = 150;
                this.Height = 50;
            }

            private void GlucoseForm_MouseHover(object sender, EventArgs e)
            {
                glucoseChart.Visible = true;
                avgLabel.Visible = true;
                nVal = glucoseLabel.Text;
                glucoseLabel.Text = exVal;
                this.Width = 350;
                this.Height = 250;
            }

            private void GlucoseTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(async () => await GetLatestGlucoseValue()));
                }
                else
                {
                    _ = GetLatestGlucoseValue();
                }
            }

            private void GlucoseForm_MouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    mouseOffset = new Point(e.X, e.Y);
                }
            }
            private void GlucoseForm_MouseMove(object sender, MouseEventArgs e)
            {
                if (isDragging)
                {
                    this.Left = e.X + this.Left - mouseOffset.X;
                    this.Top = e.Y + this.Top - mouseOffset.Y;
                }
            }
            private void GlucoseForm_MouseUp(object sender, MouseEventArgs e)
            {
                isDragging = false;
            }

            private string exVal, nVal;
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
            double max = 0;
            double min = 999;
            string con_url = "https://api.libreview.io/llu/connections";
            private async Task GetLatestGlucoseValue()
            {
                try
                {
                    Leaf.xNet.HttpRequest postReq = new Leaf.xNet.HttpRequest();
                    
                    postReq.ClearAllHeaders();
                    addHeaders(postReq, authToken, sha256Hash);

                    var connectionsResp = postReq.Get($"{con_url}/{patientId}/graph");

                    if (connectionsResp.StatusCode.ToString() == "OK")
                    {
                        dynamic JsonGraph = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        if (JsonGraph.data.region != null)
                        {
                            postReq.ClearAllHeaders();
                            addHeaders(postReq, authToken, sha256Hash);
                            con_url = $"https://api-{JsonGraph.data.region}.libreview.io/llu/connections";
                            connectionsResp = postReq.Get($"{con_url}/{patientId}/graph");
                            JsonGraph = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        }

                        var graphData = JsonGraph.data.graphData;

                       
                        var connection = JsonGraph.data.connection;

                        double max_alarm = connection.targetHigh;
                        double min_alarm = connection.targetLow;

                        double baseMaxY = max_alarm;
                        double newMaxY = Math.Max(baseMaxY, max);
                        double baseMinY = min_alarm;
                        double newMinY = Math.Min(baseMinY, min);


                        glucoseChart.ChartAreas[0].AxisY.Maximum = newMaxY;
                        glucoseChart.ChartAreas[0].AxisY.Minimum = newMinY;

                        glucoseChart.Series["Glucose"].Points.Clear();

                        string timestampFormat = "M/d/yyyy h:mm:ss tt";

                        int index = 0;
                        double glucodata = 0;
                        int offRange = 0;
                        double maxMin = connection.targetHigh;
                        double minMax = connection.targetLow;

                        foreach (var dataPoint in graphData)
                        {
                            if (DateTime.TryParseExact(dataPoint.Timestamp.ToString(), timestampFormat,
                                                        System.Globalization.CultureInfo.InvariantCulture,
                                                        System.Globalization.DateTimeStyles.None,
                                                        out DateTime timestamp))
                            {
                                double glucoseValue = dataPoint.Value;
                                if (glucoseValue > max) max = glucoseValue;
                                if (glucoseValue < min) min = glucoseValue;

                                if (glucoseValue > maxMin || glucoseValue < minMax) offRange++;

                                string measurementColor = dataPoint.MeasurementColor.ToString();
                                var chartPoint = glucoseChart.Series["Glucose"].Points.AddXY(timestamp, glucoseValue);

                                switch (measurementColor)
                                {
                                    case "1":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Green;
                                        break;
                                    case "2":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Yellow;
                                        break;
                                    case "3":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Orange;
                                        break;
                                    case "4":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Red;
                                        break;
                                    default:
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Gray;
                                        break;
                                }
                                glucodata += glucoseValue;
                                index++;
                            }
                        }

                        double avg = glucodata / index;
                        double TIR = 100 - (offRange / index * 100);

                        glucoseChart.ChartAreas["ChartArea1"].AxisX.IsMarginVisible = true;

                        StripLine stripLine = new StripLine();

                        stripLine.Interval = 0;
                        stripLine.IntervalOffset = avg;
                        stripLine.StripWidth = 2;
                        stripLine.BackColor = Color.Blue;
                        stripLine.TextAlignment = StringAlignment.Far;
                        stripLine.TextLineAlignment = StringAlignment.Center;

                        glucoseChart.ChartAreas["ChartArea1"].AxisY.StripLines.Add(stripLine);

                        string lastValue = JsonGraph.data.connection.glucoseMeasurement.Value;
                        int trendArrowValue = JsonGraph.data.connection.glucoseMeasurement.TrendArrow;
                        bool high = JsonGraph.data.connection.glucoseMeasurement.isHigh;
                        bool low = JsonGraph.data.connection.glucoseMeasurement.isLow;
                        int MeasurementColor = JsonGraph.data.connection.glucoseMeasurement.MeasurementColor;
                        string unitType = JsonGraph.data.connection.glucoseMeasurement.GlucoseUnits;

                        unitType = unitType == "1" ? "mg/dl" : "mmol/l";


                        string trendArrow = null;
                        switch (trendArrowValue)
                        {
                            case 1:
                                trendArrow = "⬇️";
                                break;
                            case 2:
                                trendArrow = "↘️";
                                break;
                            case 3: 
                                trendArrow = "➡️";
                                break;
                            case 4:  
                                trendArrow = "↗️";
                                break;
                            case 5: 
                                trendArrow = "⬆️";
                                break;
                            default:
                                break;
                        };

                        glucoseLabel.Text = $"{lastValue} {unitType} {trendArrow}";
                        avgLabel.Text = $"Average: {avg.ToString("0.")} {unitType}";
                        exVal = $"TIR: {TIR.ToString("0.")}%";

                        switch (MeasurementColor)
                        {
                            case 1:
                                glucoseLabel.ForeColor = Color.White;
                                break;
                            case 2:
                                glucoseLabel.ForeColor = Color.Yellow;
                                break;
                            case 3:
                                glucoseLabel.ForeColor = Color.Orange;
                                break;
                            case 4:
                                glucoseLabel.ForeColor = Color.Red;
                                break;
                        }

                        glucoseLabel.Left = (this.ClientSize.Width - glucoseLabel.Width) / 2;
                        //tirLabel.Left = (this.ClientSize.Width - tirLabel.Width) / 2;

                        glucoseChart.Series["Glucose"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Point;

                        glucoseChart.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm"; 
                        glucoseChart.ChartAreas[0].AxisX.IntervalType = System.Windows.Forms.DataVisualization.Charting.DateTimeIntervalType.Hours;
                        glucoseChart.ChartAreas[0].AxisX.Interval = 1;

                        /*if(high)
                            glucoseLabel.ForeColor = Color.Yellow;
                        else if (low)
                            glucoseLabel.ForeColor = Color.Red;
                        else
                            glucoseLabel.ForeColor = Color.White;*/

                    }
                }
                catch (Exception ex)
                {
                    glucoseLabel.Text = $"Failed to fetch glucose: {ex.Message}";
                }
            }
        }
    }
}

