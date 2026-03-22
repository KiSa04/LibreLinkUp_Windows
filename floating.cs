using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows.Forms;
using System.Drawing;
using System.Windows.Forms.DataVisualization.Charting;

namespace LibreLinkUp_Windows
{
    public class floating
    {
        public class GlucoseForm : Form
        {
            private string authToken;
            private string sha256Hash;
            private string patientId;
            private System.Timers.Timer glucoseTimer;
            private Label glucoseLabel, avgLabel;
            private bool isDragging, isHover = false;
            private Point mouseOffset;
            private Chart glucoseChart;
            private NotifyIcon trayIcon;
            private ContextMenuStrip trayMenu;
            private static int URGENT_HIGH = 250;
            private static int URGENT_LOW = 55;

            public GlucoseForm(string authToken, string sha256Hash, string patientId)
            {
                this.authToken = authToken;
                this.sha256Hash = sha256Hash;
                this.patientId = patientId;

                trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("Exit", null, ExitApp);

                trayIcon = new NotifyIcon()
                {
                    Icon = Properties.Resources.icon,
                    ContextMenuStrip = trayMenu,
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

                glucoseChart.MouseLeave += GlucoseForm_MouseLeave;

                glucoseLabel.Left = (this.ClientSize.Width - glucoseLabel.Width) / 2;
                glucoseLabel.Top = (this.ClientSize.Height - glucoseLabel.Height) / 2;

                avgLabel.Left = ((this.ClientSize.Width + glucoseLabel.Width) - avgLabel.Width) / 2;
                avgLabel.Top = (this.ClientSize.Height - avgLabel.Height) / 2;

                //tirLabel.Left = (this.ClientSize.Width - tirLabel.Width) / 2;
                //tirLabel.Top = (this.ClientSize.Height - tirLabel.Height) / 2;

                _ = GetLatestGlucoseValue();

                // UpdateChecker updateChecker = new UpdateChecker();
                // updateChecker.CheckForUpdates();
                glucoseTimer = new System.Timers.Timer();
                glucoseTimer.Interval = 60000;
                glucoseTimer.Elapsed += GlucoseTimer_Elapsed;
                glucoseTimer.Start();

                this.Deactivate += (s, e) => HandleMouseExit();
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
                try
                {
                    // Properly dispose the tray icon and its menu
                    if (trayIcon != null)
                    {
                        trayIcon.Visible = false;

                        if (trayIcon.ContextMenuStrip != null)
                        {
                            trayIcon.ContextMenuStrip.Dispose();
                            trayIcon.ContextMenuStrip = null;
                        }

                        trayIcon.Dispose();
                        trayIcon = null;
                    }

                    // Exit application cleanly
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error while exiting: {ex.Message}", "Exit Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            private void ExitAppOld(object sender, EventArgs e)
            {
                trayIcon.Visible = false;
                Application.Exit();
            }
            private void GlucoseForm_MouseLeave(object sender, EventArgs e)
            {
                HandleMouseExit();
            }

            private bool isHovering = false;
            private Color nValColor = Color.White;
            private void HandleMouseExit()
            {
                if (!isHovering) return;

                if (!glucoseLabel.ClientRectangle.Contains(glucoseLabel.PointToClient(System.Windows.Forms.Cursor.Position)) &&
                    !glucoseChart.ClientRectangle.Contains(glucoseChart.PointToClient(System.Windows.Forms.Cursor.Position)) &&
                    !this.ClientRectangle.Contains(this.PointToClient(System.Windows.Forms.Cursor.Position)))
                {
                    isHovering = false;
                    glucoseChart.Visible = false;
                    avgLabel.Visible = false;
                    glucoseLabel.Text = nVal;
                    glucoseLabel.ForeColor = nValColor;
                    this.Width = 150;
                    this.Height = 50;
                }
            }

            private void GlucoseForm_MouseHover(object sender, EventArgs e)
            {
                isHovering = true;
                glucoseChart.Visible = true;
                avgLabel.Visible = true;
                glucoseLabel.Text = exVal;
                glucoseLabel.ForeColor = Color.White;
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

            double max = 0;
            double min = 999;
            string con_url = "https://api.libreview.io/llu/connections";
            private async Task GetLatestGlucoseValue()
            {
                try
                {
                    Leaf.xNet.HttpRequest postReq = new Leaf.xNet.HttpRequest();

                    postReq.ClearAllHeaders();
                    Utils.addHeaders(postReq, authToken, sha256Hash);

                    var connectionsResp = postReq.Get($"{con_url}/{patientId}/graph");

                    if (connectionsResp.StatusCode.ToString() == "OK")
                    {
                        dynamic JsonGraph = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        if (JsonGraph.data.region != null)
                        {
                            postReq.ClearAllHeaders();
                            Utils.addHeaders(postReq, authToken, sha256Hash);
                            con_url = $"https://api-{JsonGraph.data.region}.libreview.io/llu/connections";
                            connectionsResp = postReq.Get($"{con_url}/{patientId}/graph");
                            JsonGraph = JsonConvert.DeserializeObject(connectionsResp.ToString());
                        }

                        var graphData = JsonGraph.data.graphData;

                        var connection = JsonGraph.data.connection;
                        
                        double max_alarm = connection.patientDevice.hl;
                        double min_alarm = connection.patientDevice.ll;
                        double targetHigh = connection.targetHigh;
                        double targetLow = connection.targetLow;

                        //double lastValuei = Math.Round(Convert.ToDouble(connection.glucoseMeasurement.Value) * 18, 0);
                        string lastValue = connection.glucoseMeasurement.Value; //Convert.ToString(lastValuei); //

                        string unitType = connection.glucoseMeasurement.GlucoseUnits;

                        unitType = unitType == "1" ? "mg/dl" : "mmol/l";

                        double localGlucoseConversion = 1;
                        if (unitType == "mmol/l")
                        {
                            localGlucoseConversion = 1.0 / 18.0;
                            max_alarm = Math.Round(max_alarm * localGlucoseConversion, 1);
                            min_alarm = Math.Round(min_alarm * localGlucoseConversion, 1);
                            targetHigh = Math.Round(targetHigh * localGlucoseConversion, 1);
                            targetLow = Math.Round(targetLow * localGlucoseConversion, 1);
                            lastValue = double.Parse(lastValue).ToString("0.0");
                        }

                        glucoseChart.Series["Glucose"].Points.Clear();

                        glucoseChart.ChartAreas[0].AxisY.StripLines.Clear();

                        string timestampFormat = "M/d/yyyy h:mm:ss tt";

                        int index = 0;
                        double glucodata = 0;
                        int offRange = 0;

                        double newMaxY = Math.Max(targetHigh, Math.Round(max_alarm));
                        double newMinY = Math.Min(targetLow, Math.Round(min_alarm));

                        DateTime.TryParseExact(connection.glucoseMeasurement.Timestamp.ToString(), timestampFormat,
                                                        System.Globalization.CultureInfo.InvariantCulture,
                                                        System.Globalization.DateTimeStyles.None,
                                                        out DateTime lastTimestamp);
                        int trendArrowValue = connection.glucoseMeasurement.TrendArrow;
                        bool high = connection.glucoseMeasurement.isHigh;
                        bool low = connection.glucoseMeasurement.isLow;
                        int MeasurementColor = connection.glucoseMeasurement.MeasurementColor;
                        

                        string trendArrow = null;

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

                                if (glucoseValue > targetHigh || glucoseValue < targetLow) offRange++;

                                string measurementColor = dataPoint.MeasurementColor.ToString();
                                var chartPoint = glucoseChart.Series["Glucose"].Points.AddXY(timestamp, glucoseValue);

                                switch (measurementColor)
                                {
                                    case "1":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.Green;
                                        break;
                                    case "2":
                                        glucoseChart.Series["Glucose"].Points[index].Color = Color.FromArgb(255, 200, 0);
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
                                } // 2:10:43 Last graphed data -> At 2:44:00RT | 2:25:43 data posted
                                  // ~18 min API delivery delay + ~5-10 min sensor lag = ~23-28 min total delay
                                  // between actual glucose and what appears on the API response.
                                  // Color of label must be moved.
                                glucodata += glucoseValue;
                                index++;
                            }
                        }

                        // Add current value to chart
                        {
                            double lastValueV = Convert.ToDouble(lastValue);

                            if (lastValueV > max) max = lastValueV;
                            if (lastValueV < min) min = lastValueV;
                            var chartPointCurrent = glucoseChart.Series["Glucose"].Points.AddXY(lastTimestamp, lastValue);
                            glucoseChart.Series["Glucose"].Points[index].MarkerStyle = MarkerStyle.Star5;
                            glucoseChart.Series["Glucose"].Points[index].MarkerBorderColor = Color.Black;
                            glucoseChart.Series["Glucose"].Points[index].MarkerSize = 12;
                            if (lastValueV > targetHigh || lastValueV < targetLow) offRange++;

                            if (lastValueV < URGENT_LOW * localGlucoseConversion)
                            {
                                glucoseChart.Series["Glucose"].Points[index].Color = Color.Red;
                                glucoseLabel.ForeColor = Color.Red;
                            }
                            else if (lastValueV < targetLow)
                            {
                                glucoseChart.Series["Glucose"].Points[index].Color = Color.FromArgb(255, 200, 0);
                                glucoseLabel.ForeColor = Color.FromArgb(255, 200, 0);
                            }
                            else if (lastValueV > URGENT_HIGH * localGlucoseConversion)
                            {
                                glucoseChart.Series["Glucose"].Points[index].Color = Color.Red;
                                glucoseLabel.ForeColor = Color.Red;
                            }
                            else if (lastValueV > targetHigh)
                            {
                                glucoseChart.Series["Glucose"].Points[index].Color = Color.Orange;
                                glucoseLabel.ForeColor = Color.Orange;
                            }
                            else
                            {
                                glucoseChart.Series["Glucose"].Points[index].Color = Color.Green;
                                glucoseLabel.ForeColor = Color.White;
                            }
                            glucodata += lastValueV;
                            index++;
                        }

                        double gentleBuffer = 25 * localGlucoseConversion;
                        double sharpDropBuffer = 35 * localGlucoseConversion;
                        double sharpRiseBuffer = 50 * localGlucoseConversion;

                        switch (trendArrowValue)
                        {
                            case 1:
                                trendArrow = "⬇️";
                                if (Convert.ToDouble(lastValue) <= (Convert.ToDouble(connection.targetLow) * localGlucoseConversion) + sharpDropBuffer)
                                {
                                    glucoseLabel.ForeColor = Color.Red;
                                    lastValue.Insert(0, "⚠️");
                                }
                                break;
                            case 2:
                                trendArrow = "↘️";
                                if (Convert.ToDouble(lastValue) <= (Convert.ToDouble(connection.targetLow) * localGlucoseConversion) + gentleBuffer)
                                {
                                    glucoseLabel.ForeColor = Color.Red;
                                    lastValue.Insert(0, "⚠️");
                                }
                                break;
                            case 3:
                                trendArrow = "➡️";
                                break;
                            case 4:
                                trendArrow = "↗️";
                                if (Convert.ToDouble(lastValue) >= (Convert.ToDouble(connection.targetHigh) * localGlucoseConversion) + gentleBuffer)
                                {
                                    glucoseLabel.ForeColor = Color.FromArgb(255, 200, 0);
                                    lastValue.Insert(0, "⚠️");
                                }
                                break;
                            case 5:
                                trendArrow = "⬆️";
                                if (Convert.ToDouble(lastValue) >= (Convert.ToDouble(connection.targetHigh) * localGlucoseConversion) + sharpRiseBuffer)
                                {
                                    glucoseLabel.ForeColor = Color.Orange;
                                    lastValue.Insert(0, "⚠️");
                                }
                                break;
                            default:
                                break;
                        }
                        ;

                        newMaxY = Math.Max(newMaxY, Math.Ceiling(max));
                        newMinY = Math.Min(newMinY, Math.Floor(min));
                        double avg = glucodata / index;
                        double TIR = 100 - ((offRange * 100) / index);
                        
                        double interval = GetInterval(newMinY, newMaxY, unitType);
                        (double normalizedMinY, double normalizedMaxY) = NormalizeChart(newMinY, newMaxY, interval, unitType);

                        glucoseChart.ChartAreas[0].AxisY.LabelStyle.Format = "";
                        glucoseChart.ChartAreas[0].AxisY.Interval = interval;
                        glucoseChart.ChartAreas[0].AxisY.Maximum = normalizedMaxY;
                        glucoseChart.ChartAreas[0].AxisY.Minimum = normalizedMinY;

                        double axisRange = normalizedMaxY - normalizedMinY;
                        double stripWidth = Math.Round((axisRange / glucoseChart.Size.Height) * 3,1);


                        glucoseChart.ChartAreas["ChartArea1"].AxisX.IsMarginVisible = true;

                        StripLine stripLine = new StripLine();

                        stripLine.Interval = 0;
                        stripLine.IntervalOffset = avg;
                        stripLine.StripWidth = stripWidth;
                        stripLine.BackColor = Color.Blue;
                        stripLine.TextAlignment = StringAlignment.Far;
                        stripLine.TextLineAlignment = StringAlignment.Center;

                        glucoseChart.ChartAreas["ChartArea1"].AxisY.StripLines.Add(stripLine);

                        double stripRange;

                        // Low Stripline
                        if (min_alarm <= Math.Round(newMinY,0))
                        {
                            stripRange = Math.Abs(min_alarm - normalizedMinY);
                            if (stripRange >= 1) stripRange = 0;
                        }
                        else
                        {
                            stripRange = 0;
                        }
                        stripLine = new StripLine();

                        stripLine.Interval = 0;
                        stripLine.IntervalOffset = min_alarm + stripRange;
                        stripLine.StripWidth = stripWidth;
                        stripLine.BackColor = Color.Red;
                        stripLine.TextAlignment = StringAlignment.Far;
                        stripLine.TextLineAlignment = StringAlignment.Center;

                        glucoseChart.ChartAreas["ChartArea1"].AxisY.StripLines.Add(stripLine);

                        // High Stripline
                        if (max_alarm >= newMaxY)
                        {
                            stripRange = Math.Abs(max_alarm - newMaxY);
                            if (stripRange >= 1)
                                stripRange = 0;
                            else
                                stripRange += (2.7 * localGlucoseConversion);
                        }
                        else
                        {
                            stripRange = 0;
                        }
                        stripLine = new StripLine();

                        stripLine.Interval = 0;
                        stripLine.IntervalOffset = max_alarm - stripRange;
                        stripLine.StripWidth = stripWidth;
                        stripLine.BackColor = Color.Orange;
                        stripLine.TextAlignment = StringAlignment.Far;
                        stripLine.TextLineAlignment = StringAlignment.Center;

                        glucoseChart.ChartAreas["ChartArea1"].AxisY.StripLines.Add(stripLine);
                        
                        glucoseLabel.Text = $"{lastValue} {unitType} {trendArrow}";
                        nVal = glucoseLabel.Text;
                        nValColor = glucoseLabel.ForeColor;

                        if (unitType == "mg/dl")
                        {
                            avgLabel.Text = $"Average: {avg.ToString("0.")} {unitType}";
                        }
                        else
                        {
                            avgLabel.Text = $"Average: {avg.ToString("0.0")} {unitType}";
                        }
                        exVal = $"TIR: {TIR.ToString("0.")}%";

                        if (avgLabel.Visible == false)
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

            double GetInterval(double min, double max, string _unitType)
            {
                double range;

                if (_unitType == "mg/dl")
                {
                    range = (Math.Ceiling(max / 5) * 5) - (Math.Floor(min / 5) * 5);
                    for (int i = 5; i < range; i++) // Find a good divisible candidate
                    {
                        if (range % i == 0 && (range / i) <= 7)
                        {
                            return i;
                        }
                    }
                    return 10;
                }
                else
                {
                    range = Math.Round(max, 0) - Math.Round(min, 0);
                    int rangeInt = (int)Math.Round(range * 10);
                    for (int i = 1; i < rangeInt; i++) // Find a good divisible candidate
                    {
                        double interval = i / 10.0;
                        if (rangeInt % i == 0 && (rangeInt / i) <= 7)
                        {
                            return interval;
                        }
                    }
                    return 1.0;
                }
            }

            private (double _min, double _max) NormalizeChart(double _min, double _max, double _interval, string _units)
            {
                double newMin = _min;
                double newMax = _max;

                if (_units == "mg/dl")
                {
                    newMin = (Math.Floor(_min / 5) * 5);
                    newMax = (Math.Ceiling(_max / 5) * 5);
                }
                else
                {
                    newMin = Math.Round(_min, 0);
                    newMax = Math.Round(_max, 0);
                }

                return (newMin, newMax);
            }
        }
    }
}

