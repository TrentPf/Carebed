using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.MessageEnvelope;
using System.Collections.Generic;
using System.Linq;
using Carebed.Managers;
using Carebed.Modules;
using Carebed.Infrastructure.Message.SensorMessages;

namespace Carebed
{
    partial class MainDashboard
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// A reference to the event bus for publishing and subscribing to events.
        /// </summary>
        private readonly IEventBus _eventBus;

        // start/stop sensors toggle button
        private System.Windows.Forms.Button toggleSensorsButton;
        private System.Windows.Forms.TextBox transportLogTextBox;

        // new UI controls for sensors overview and history
        private System.Windows.Forms.ListView sensorsListView;
        private System.Windows.Forms.DataGridView historyGridView;
        private System.Windows.Forms.NumericUpDown refreshIntervalUpDown;
        private System.Windows.Forms.Label refreshIntervalLabel;
        private System.Windows.Forms.Timer refreshTimer;

        // in-memory sensor history storage
        private readonly Dictionary<string, List<SensorData<object>>> _sensorHistory = new();
        private readonly object _historyLock = new();

        // sensor manager
        private IManager _sensorManager;
        private bool _sensorsRunning = false;

        /// <summary>
        /// Constructor for MainDashboard that accepts an IEventBus instance.
        /// </summary>
        public MainDashboard(IEventBus eventBus, IManager sensorManager)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _sensorManager = sensorManager ?? throw new ArgumentNullException(nameof(sensorManager));

            InitializeComponent();
        }

        /// <summary>
        /// A override for the OnLoad event to perform additional initialization.
        /// </summary>
        /// <remarks> Can be used to subscribe to events or perform other setup tasks. </remarks>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _eventBus.Subscribe<SensorTelemetryMessage>(HandleSensorData);

            // Do NOT start the sensor manager automatically. User must press Start.
            // _sensorManager.Start(); // removed to prevent automatic sensor start

            // start timer
            _sensorsRunning = false;
            RunOnUiThread(() => toggleSensorsButton.Text = _sensorsRunning ? "Stop Sensors" : "Start Sensors");

            // start timer for UI refresh (keeps UI responsive even when sensors are stopped)
            refreshTimer?.Start();
        }

        /// <summary>
        /// Handler for incoming SensorData messages.
        /// </summary>
        /// <param name="envelope"></param>
        private void HandleSensorData(MessageEnvelope<SensorTelemetryMessage> envelope)
        {
            // fast guard: if form is closing/disposed, ignore the update
            if (IsDisposed || Disposing || !_sensorsRunning) return;

            // store in history, then schedule UI update via timer to reduce UI churn
            var source = envelope.Payload.SensorID ?? "Unknown";
            var timestamp = envelope.Timestamp != default ? envelope.Timestamp : DateTime.Now;
            var value = envelope.Payload.Data.Value;

            lock (_historyLock)
            {
                // Check to see if an entry for this sensor already exists
                if (!_sensorHistory.TryGetValue(source, out var list))
                {
                    // An entry does not exist, create a new list for this sensor
                    list = new List<SensorData<object>>();
                    _sensorHistory[source] = list;
                }

                list.Add(envelope.Payload.Data);

                // cap history to last 5000 entries per sensor to avoid unbounded growth
                if (list.Count > 5000)
                    list.RemoveRange(0, list.Count - 5000);
            }

            // update transport log quickly on UI thread
            RunOnUiThread(() =>
            {
                try
                {
                    transportLogTextBox?.AppendText($"{DateTime.Now:HH:mm:ss.fff} Received: {value:F2} from {source}\r\n");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"UI update failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Helper to run an action on the UI thread.
        /// </summary>
        /// <param name="action"></param>
        private void RunOnUiThread(Action action)
        {
            if (action == null) return;
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        /// <summary>
        /// Unsubscribe from event bus when the form is closed.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // stop sensors if running
            try
            {
                _sensorManager.Stop();
                _sensorsRunning = false;
            }
            catch { }

            // stop timer first
            try
            {
                refreshTimer?.Stop();
                refreshTimer?.Dispose();
            }
            catch { }

            _eventBus.Unsubscribe<SensorTelemetryMessage>(HandleSensorData);
            base.OnFormClosed(e);
        }

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // toggleSensorsButton - start/stop sensors
            this.toggleSensorsButton = new System.Windows.Forms.Button();
            this.toggleSensorsButton.Name = "toggleSensorsButton";
            this.toggleSensorsButton.Text = "Start Sensors";
            this.toggleSensorsButton.Size = new System.Drawing.Size(140, 30);
            this.toggleSensorsButton.Location = new System.Drawing.Point(12, 12);
            this.toggleSensorsButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.toggleSensorsButton.Click += toggleSensorsButton_Click;

            // transportLogTextBox - multi-line text box to show send/receive traces
            this.transportLogTextBox = new System.Windows.Forms.TextBox();
            this.transportLogTextBox.Name = "transportLogTextBox";
            this.transportLogTextBox.Multiline = true;
            this.transportLogTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.transportLogTextBox.ReadOnly = true;
            this.transportLogTextBox.Size = new System.Drawing.Size(760, 120);
            this.transportLogTextBox.Location = new System.Drawing.Point(12, 320);
            this.transportLogTextBox.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            // sensorsListView - shows list of sensors and latest value
            this.sensorsListView = new System.Windows.Forms.ListView();
            this.sensorsListView.Name = "sensorsListView";
            this.sensorsListView.View = System.Windows.Forms.View.Details;
            this.sensorsListView.FullRowSelect = true;
            this.sensorsListView.MultiSelect = false;
            this.sensorsListView.Size = new System.Drawing.Size(255, 260);
            this.sensorsListView.Location = new System.Drawing.Point(12, 50);
            this.sensorsListView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Bottom;
            this.sensorsListView.Columns.Add("Sensor", 120);
            this.sensorsListView.Columns.Add("Last Value", 80);
            this.sensorsListView.Columns.Add("Count", 50);
            this.sensorsListView.SelectedIndexChanged += sensorsListView_SelectedIndexChanged;

            // historyGridView - shows timestamped values for selected sensor
            this.historyGridView = new System.Windows.Forms.DataGridView();
            this.historyGridView.Name = "historyGridView";
            this.historyGridView.ReadOnly = true;
            this.historyGridView.AllowUserToAddRows = false;
            this.historyGridView.AllowUserToDeleteRows = false;
            this.historyGridView.RowHeadersVisible = false;
            this.historyGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.historyGridView.Size = new System.Drawing.Size(500, 260);
            this.historyGridView.Location = new System.Drawing.Point(280, 50);
            this.historyGridView.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right | System.Windows.Forms.AnchorStyles.Bottom;

            // setup columns for historyGridView
            if (this.historyGridView.Columns.Count == 0)
            {
                this.historyGridView.Columns.Add("Timestamp", "Timestamp");
                this.historyGridView.Columns.Add("Value", "Value");
            }

            // refresh interval controls
            this.refreshIntervalLabel = new System.Windows.Forms.Label();
            this.refreshIntervalLabel.Text = "Refresh (ms):";
            this.refreshIntervalLabel.Location = new System.Drawing.Point(12, 380);
            this.refreshIntervalLabel.AutoSize = true;
            this.refreshIntervalLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;

            this.refreshIntervalUpDown = new System.Windows.Forms.NumericUpDown();
            this.refreshIntervalUpDown.Minimum = 200;
            this.refreshIntervalUpDown.Maximum = 60000;
            this.refreshIntervalUpDown.Value = 1000;
            this.refreshIntervalUpDown.Increment = 200;
            this.refreshIntervalUpDown.Location = new System.Drawing.Point(90, 376);
            this.refreshIntervalUpDown.Size = new System.Drawing.Size(80, 22);
            this.refreshIntervalUpDown.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left;
            this.refreshIntervalUpDown.ValueChanged += (s, e) =>
            {
                if (refreshTimer != null)
                    refreshTimer.Interval = (int)refreshIntervalUpDown.Value;
            };

            // timer to refresh UI at configured interval
            this.refreshTimer = new System.Windows.Forms.Timer(this.components);
            this.refreshTimer.Interval = (int)this.refreshIntervalUpDown.Value;
            this.refreshTimer.Tick += RefreshTimer_Tick;

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "Sensor Dashboard";

            // Add controls to the form
            this.Controls.Add(this.toggleSensorsButton);
            this.Controls.Add(this.transportLogTextBox);
            this.Controls.Add(this.sensorsListView);
            this.Controls.Add(this.historyGridView);
            this.Controls.Add(this.refreshIntervalLabel);
            this.Controls.Add(this.refreshIntervalUpDown);
        }

        #endregion

        /// <summary>
        /// Button to toggle starting/stopping sensors
        /// </summary>
        private void toggleSensorsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_sensorsRunning)
                {
                    _sensorManager.Stop();
                    _sensorsRunning = false;
                    toggleSensorsButton.Text = "Start Sensors";
                    transportLogTextBox?.AppendText($"{DateTime.Now:HH:mm:ss.fff} Sensors stopped by user\r\n");
                }
                else
                {
                    _sensorManager.Start();
                    _sensorsRunning = true;
                    toggleSensorsButton.Text = "Stop Sensors";
                    transportLogTextBox?.AppendText($"{DateTime.Now:HH:mm:ss.fff} Sensors started by user\r\n");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Toggle sensors failed: {ex}");
                MessageBox.Show($"Failed to toggle sensors: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Timer tick handler - refreshes the sensors list and currently selected sensor history.
        /// </summary>
        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            // update sensors list
            List<(string Sensor, DateTime LastTime, double LastValue, int Count)> snapshot;
            lock (_historyLock)
            {
                snapshot = _sensorHistory.Select(kvp =>
                {
                    var list = kvp.Value;
                    var last = list.LastOrDefault();
                    return (Sensor: kvp.Key, LastTime: last.CreatedAt, LastValue: last.Value, Count: list.Count);
                }).ToList();
            }

            RunOnUiThread(() =>
            {
                try
                {
                    // update sensorsListView while preserving selection
                    string? selectedKey = null;
                    if (sensorsListView.SelectedItems.Count > 0)
                        selectedKey = sensorsListView.SelectedItems[0].Text;

                    sensorsListView.BeginUpdate();
                    sensorsListView.Items.Clear();
                    foreach (var item in snapshot.OrderBy(s => s.Sensor))
                    {
                        var lvi = new System.Windows.Forms.ListViewItem(item.Sensor);
                        lvi.SubItems.Add(item.LastValue.ToString("F2"));
                        lvi.SubItems.Add(item.Count.ToString());
                        sensorsListView.Items.Add(lvi);

                        if (item.Sensor == selectedKey)
                            lvi.Selected = true;
                    }
                    sensorsListView.EndUpdate();

                    // update history for selected sensor
                    if (!string.IsNullOrEmpty(selectedKey))
                        UpdateHistoryGrid(selectedKey);
                    else if (sensorsListView.Items.Count > 0 && sensorsListView.SelectedItems.Count == 0)
                    {
                        // auto-select first
                        sensorsListView.Items[0].Selected = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Refresh failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Update the history grid for the given sensor key.
        /// </summary>
        private void UpdateHistoryGrid(string sensorKey)
        {
            List<(DateTime Timestamp, double Value)> items;
            lock (_historyLock)
            {
                if (!_sensorHistory.TryGetValue(sensorKey, out items))
                    items = new List<(DateTime, double)>();
                else
                    items = new List<(DateTime, double)>(items);
            }

            historyGridView.SuspendLayout();
            historyGridView.Rows.Clear();
            foreach (var it in items.OrderByDescending(i => i.Timestamp).Take(200))
            {
                historyGridView.Rows.Add(it.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"), it.Value.ToString("F2"));
            }
            historyGridView.ResumeLayout();
        }

        /// <summary>
        /// Handler when the selected sensor changes - update history view.
        /// </summary>
        private void sensorsListView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sensorsListView.SelectedItems.Count == 0) return;
            var key = sensorsListView.SelectedItems[0].Text;
            UpdateHistoryGrid(key);
        }
    }
}
