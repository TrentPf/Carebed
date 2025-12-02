using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Managers;
using Carebed.Models.Sensors;
using Carebed.Infrastructure.Message.AlertMessages;
using Carebed.Infrastructure.Message.Actuator;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carebed.Infrastructure.Message.UI;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.LoggerMessages;

namespace Carebed.UI
{
    public partial class MainDashboard : Form 
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Fields and Properties
        
        // A reference to the event bus for publishing and subscribing to events.        
        private readonly IEventBus _eventBus;

        // A single AlertViewModel instance for databinding
        private AlertViewModel alertViewModel = new AlertViewModel();

        // A databinding source for alert banner
        private BindingSource alertBindingSource = new BindingSource();

        // Flag to indicate if alerts are paused
        private bool alertsPaused = false;

        // List to store all global messages for logging
        private List<IMessageEnvelope> allGlobalMessages = new();

        // Sensor alert handlers
        private Action<MessageEnvelope<AlertActionMessage<SensorTelemetryMessage>>>? _alertHandlerSensorTelemetry;
        private Action<MessageEnvelope<AlertActionMessage<SensorStatusMessage>>>? _alertHandlerSensorStatus;
        private Action<MessageEnvelope<AlertActionMessage<SensorErrorMessage>>>? _alertHandlerSensorError;

        // Actuator alert handlers
        private Action<MessageEnvelope<AlertActionMessage<ActuatorTelemetryMessage>>>? _alertHandlerActuatorTelemetry;
        private Action<MessageEnvelope<AlertActionMessage<ActuatorStatusMessage>>>? _alertHandlerActuatorStatus;
        private Action<MessageEnvelope<AlertActionMessage<ActuatorErrorMessage>>>? _alertHandlerActuatorError;

        // Sensor grid for displaying telemetry data
        private DataGridView sensorGridView;
        private Dictionary<string, DataGridViewRow> sensorRows = new();

        // Alert Clear Ack handler
        private Action<MessageEnvelope<AlertClearAckMessage>>? _alertClearAckHandler;

        // Logger Command Ack handler
        private Action<MessageEnvelope<LoggerCommandAckMessage>>? _loggerCommandAckHandler;

        // Sensor Command Ack handler
        private Action<MessageEnvelope<SensorCommandAckMessage>>? _sensorCommandAckHandler;

        // Global log message handler
        private Action<IMessageEnvelope> _globalLogHandler;

        #endregion

        #region Windows Forms Elements

        #region Alert Banner

        private TableLayoutPanel alertBannerLayout = new TableLayoutPanel();
        private Panel alertBanner = new Panel();
        private Label alertBannerLabel = new Label();
        private PictureBox alertBannerIcon = new PictureBox();

        private TableLayoutPanel alertBannerTable;
        private Label alertBannerTimeTitle;
        private Label alertBannerSourceTitle;
        private Label alertBannerValueTitle;
        private Label alertBannerTimeValue;
        private Label alertBannerSourceValue;
        private Label alertBannerValueValue;
        private Label alertBannerSeverityTitle;
        private Label alertBannerSeverityValue;

        // Background colour for the alert banner
        private Color NoAlertsActiveColour = Color.Green;
        private Color ActiveAlertsColour = Color.Orange;
        private Color SevereAlertColour = Color.DarkRed;

        // Icons for alert banner
        Image NoActiveAlertsIcon = SystemIcons.Information.ToBitmap();
        Image AlertsActiveIcon = SystemIcons.Warning.ToBitmap();
        Image SevereAlertsIcon = SystemIcons.Error.ToBitmap();
        #endregion
        
        #region Tabs and Viewport

        private Panel tabsPanel;        

        private Button vitalsTabButton;
        private Button actuatorsTabButton;
        private Button logsTabButton;
        private Button settingsTabButton;

        private Panel mainViewportPanel;

        #endregion

        #region Alert Log Panel
        private ListView alertListView = new ListView();
        private Panel alertLogPanel = new Panel();
        private Label alertSourceLabel = new Label();
        private Label alertLabel = new Label();
        private Label alertCountLabel = new Label();

        private Panel pauseStatusIndicator;
        private Label pauseStatusLabel;

        private TableLayoutPanel alertLogContainer;
        private Button clearAlertsButton;
        private Button pauseAlertsButton;
        #endregion

        #region Logs Viewer Section
        private DataGridView logGridView;
        private string? logFilePath;
        //private long lastLogFileLength = 0;
        //private bool logFileErrorShown = false;
        private ComboBox logTypeFilterComboBox;
        private Button applyLogFilterButton;
        private List<string> allLogLines = new();
        private string currentLogTypeFilter = "All";
        private Button liveLogsButton;
        private Button showLogFileButton;
        private Button refreshLogFileButton;
        private bool showingFileLog = false;
        private FlowLayoutPanel logButtonPanel; // Add this field
        private Guid? selectedMessageId = null;
        private const int MaxLogMessages = 1000; // Set your desired limit

        #endregion        

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Constructor for MainDashboard that accepts an IEventBus instance.
        /// </summary>
        public MainDashboard(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            InitializeComponent();
            InitializeAlertBanner();            
            InitializeTabsPanel();
            InitializeMainViewportPanel();
            InitializeSensorGrid();
            InitializeAlertLogPanel();
            InitializeLogViewer();

            // Subscribe to single-click selection
            alertListView.MouseUp += AlertListView_MouseUp;

            // Setup the Alert Banner click event handlers
            AttachAlertBannerClickHandlers(alertBanner);

            // Set z-order: 0 = topmost, higher = further back
            this.Controls.SetChildIndex(alertBanner, this.Controls.Count - 1); // Topmost
            this.Controls.SetChildIndex(tabsPanel, this.Controls.Count - 2);
            this.Controls.SetChildIndex(mainViewportPanel, this.Controls.Count - 3);
            this.Controls.SetChildIndex(alertLogContainer, this.Controls.Count - 4);
        }

        /// <summary>
        /// Setup the alert banner UI components.
        /// </summary>
        private void InitializeAlertBanner()
        {
            // Create the table for the banner
            alertBannerTable = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 2,
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
                Padding = new Padding(8, 4, 8, 4)
            };
            alertBannerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            alertBannerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            alertBannerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            alertBannerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            alertBannerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Try 36 or 40
            alertBannerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Column titles
            alertBannerTimeTitle = new Label
            {
                Text = "Time:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.WhiteSmoke,
                AutoSize = false
              
            };
            alertBannerSourceTitle = new Label
            {
                Text = "Source:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.WhiteSmoke,
                AutoSize = false
            };
            alertBannerValueTitle = new Label
            {
                Text = "Value:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.WhiteSmoke,
                AutoSize = false
            };
            alertBannerSeverityTitle = new Label
            {
                Text = "Severity:",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.WhiteSmoke,
                AutoSize = false
            };

            // Value labels
            alertBannerTimeValue = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.White
            };
            alertBannerSourceValue = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.White
            };
            alertBannerValueValue = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.White
            };
            alertBannerSeverityValue = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = Color.White
            };

            // Add to table
            alertBannerTable.Controls.Add(alertBannerTimeTitle, 0, 0);
            alertBannerTable.Controls.Add(alertBannerSourceTitle, 1, 0);
            alertBannerTable.Controls.Add(alertBannerValueTitle, 2, 0);
            alertBannerTable.Controls.Add(alertBannerSeverityTitle, 3, 0);
            alertBannerTable.Controls.Add(alertBannerTimeValue, 0, 1);
            alertBannerTable.Controls.Add(alertBannerSourceValue, 1, 1);
            alertBannerTable.Controls.Add(alertBannerValueValue, 2, 1);
            alertBannerTable.Controls.Add(alertBannerSeverityValue, 3, 1);

            // Setup alert banner panel
            alertBanner.Name = "AlertBanner";
            alertBanner.Height = 100;
            alertBanner.Dock = DockStyle.Top;
            alertBanner.BackColor = NoAlertsActiveColour;
            alertBanner.Controls.Clear();
            alertBanner.Controls.Add(alertBannerTable);

            alertBannerTable.Height = 100;

            // Add banner to form
            this.Controls.Add(alertBanner);
        }

        /// <summary>
        /// Setup the alert log panel UI components.
        /// </summary>
        private void InitializeAlertLogPanel()
        {
            // Create a label for the alert log title
            var alertLogTitleLabel = new Label
            {
                Text = "Alert Log",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(4, 0, 0, 4),
                Height = 24
            };

            // Create a TableLayoutPanel to stack the title and the ListView
            var alertLogContentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                BackColor = Color.WhiteSmoke,
            };
            alertLogContentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Title height
            alertLogContentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // ListView takes the rest

            // Create a bordered panel for the ListView
            var alertListViewBorderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0),
                BackColor = Color.White
            };

            // Configure alert count label (optional, if you want to show it)
            alertCountLabel.Dock = DockStyle.Top;
            alertCountLabel.TextAlign = ContentAlignment.MiddleLeft;
            alertCountLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            alertCountLabel.Text = "0";

            alertListView.Dock = DockStyle.Fill;
            alertListView.View = View.Details;
            alertListView.FullRowSelect = true;
            alertListView.Columns.Clear();
            alertListView.Columns.Add("Count", 80);
            alertListView.Columns.Add("Time", 160);
            alertListView.Columns.Add("Source", 180);
            alertListView.Columns.Add("Alert", 350);
            alertListView.Columns.Add("Severity", 100);

            // Add the ListView to the bordered panel
            alertListViewBorderPanel.Controls.Add(alertListView);

            // Add the title label and bordered ListView panel to the layout
            alertLogContentLayout.Controls.Add(alertLogTitleLabel, 0, 0);
            alertLogContentLayout.Controls.Add(alertListViewBorderPanel, 0, 1);

            // Alert log panel setup
            alertLogPanel.Name = "AlertLogPanel";
            alertLogPanel.Dock = DockStyle.Fill;
            alertLogPanel.Padding = new Padding(0);
            alertLogPanel.BackColor = Color.WhiteSmoke;
            alertLogPanel.Controls.Clear();
            alertLogPanel.Controls.Add(alertLogContentLayout);

            // Pause and Clear buttons
            clearAlertsButton = new Button
            {
                Text = "Clear",
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 48),
                Margin = new Padding(8)
            };
            clearAlertsButton.FlatAppearance.BorderSize = 0;
            clearAlertsButton.Click += ClearAlertsButton_Click;

            pauseAlertsButton = new Button
            {
                Text = "Pause",
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(72, 48),
                Margin = new Padding(8)
            };
            pauseAlertsButton.FlatAppearance.BorderSize = 0;
            pauseAlertsButton.Click += (s, e) =>
            {
                PauseAlertsButton_Click(s, e);
                UpdatePauseStatusIndicator();
            };

            // Status label for clear button
            var clearStatusLabel = new Label
            {
                Text = "Clear Alerts",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0)
            };

            // Status label and indicator for pause button
            pauseStatusLabel = new Label
            {
                Text = "Alerts On",
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0)
            };
            pauseStatusIndicator = new Panel
            {
                Size = new Size(16, 16),
                BackColor = Color.Green,
                Margin = new Padding(0, 0, 8, 0)
            };

            // Row for clear button and label
            var clearRowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 8)
            };
            clearAlertsButton.Anchor = AnchorStyles.Left;
            clearStatusLabel.Anchor = AnchorStyles.Left;
            clearStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            clearRowPanel.Controls.Add(clearAlertsButton);
            clearRowPanel.Controls.Add(clearStatusLabel);

            // Row for pause button, label, and indicator
            var pauseRowPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };
            pauseAlertsButton.Anchor = AnchorStyles.Left;
            pauseStatusLabel.Anchor = AnchorStyles.Left;
            pauseStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            pauseStatusIndicator.Anchor = AnchorStyles.Left;
            pauseRowPanel.Controls.Add(pauseAlertsButton);
            pauseRowPanel.Controls.Add(pauseStatusLabel);
            pauseRowPanel.Controls.Add(pauseStatusIndicator);

            // Main button panel (vertical stack)
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                Padding = new Padding(16, 16, 16, 16),
                WrapContents = false
            };
            buttonPanel.Controls.Add(clearRowPanel);
            buttonPanel.Controls.Add(pauseRowPanel);

            // Alert log panel setup (add buttonPanel to the right)
            alertLogPanel.Controls.Clear();
            alertLogPanel.Controls.Add(alertLogContentLayout);
            alertLogPanel.Controls.Add(buttonPanel);

            // Container for log and buttons
            alertLogContainer = new TableLayoutPanel
            {
                Name = "AlertLogContainer",
                Dock = DockStyle.Bottom,
                Height = 220,
                Padding = new Padding(4),
                BackColor = Color.WhiteSmoke,
                ColumnCount = 1,
                RowCount = 1,
            };
            alertLogContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            alertLogContainer.Controls.Add(alertLogPanel, 0, 0);

            // Add container to form
            this.Controls.Add(alertLogContainer);
        }

        /// <summary>
        /// Setup the tabs panel UI components.
        /// </summary>
        private void InitializeTabsPanel()
        {
            tabsPanel = new Panel
            {
                Name = "TabsPanel",
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.LightGray
            };

            vitalsTabButton = new Button
            {
                Text = "Vitals",
                Width = 120,
                Dock = DockStyle.Left
            };
            actuatorsTabButton = new Button
            {
                Text = "Actuators",
                Width = 120,
                Dock = DockStyle.Left
            };
            logsTabButton = new Button
            {
                Text = "Logs",
                Width = 120,
                Dock = DockStyle.Left
            };
            settingsTabButton = new Button
            {
                Text = "Settings",
                Width = 120,
                Dock = DockStyle.Left
            };

            // Add buttons to panel (reverse order for DockStyle.Left)
            tabsPanel.Controls.Add(settingsTabButton);
            tabsPanel.Controls.Add(logsTabButton);
            tabsPanel.Controls.Add(actuatorsTabButton);
            tabsPanel.Controls.Add(vitalsTabButton);

            this.Controls.Add(tabsPanel);
        }

        /// <summary>
        /// Setup the main viewport panel UI components.
        /// </summary>
        private void InitializeMainViewportPanel()
        {
            mainViewportPanel = new Panel
            {
                Name = "MainViewportPanel",
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            this.Controls.Add(mainViewportPanel);
        }

        private void InitializeLogViewer()
        {
            logGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.Black,
                ForeColor = Color.LightGreen,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.Black,      // cell background
                    ForeColor = Color.LightGreen, // cell text
                    SelectionBackColor = Color.DarkGreen,
                    SelectionForeColor = Color.White
                }
            };
            logGridView.Columns.Add("MessageId", "MessageId");
            logGridView.Columns.Add("Type", "Type");
            logGridView.Columns.Add("Timestamp", "Timestamp");
            logGridView.Columns.Add("Source", "Source");
            logGridView.Columns.Add("Message", "Message");
            
            // Make MessageId column hidden
            logGridView.Columns["MessageId"].Visible = false;

            logTypeFilterComboBox = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Populate with all MessageOrigins enum values
            logTypeFilterComboBox.Items.Add("All");
            
            foreach (var origin in Enum.GetValues(typeof(MessageOrigins)))
                logTypeFilterComboBox.Items.Add(origin.ToString());

            logTypeFilterComboBox.SelectedIndex = 0;

            applyLogFilterButton = new Button
            {
                Text = "Apply Filter",
                Width = 100
            };            

            showLogFileButton = new Button
            {
                Text = "Show Log File",
                Width = 100
            };           

            refreshLogFileButton = new Button
            {
                Text = "Refresh",
                Width = 100,
                Visible = false
            };

            var logButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(4),
                WrapContents = false
            };

            // Store reference for use in LogsTabButton_Click
            this.logButtonPanel = logButtonPanel;
        }

        /// <summary>
        /// A override for the OnLoad event to perform additional initialization.
        /// </summary>
        /// <remarks> Can be used to subscribe to events or perform other setup tasks. </remarks>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Bind the label text to AlertMessage.Text
            AlertViewModel emptyBinding = new AlertViewModel()
            {
                AlertText = "",
                IsCritical = false
            };
            
            // Setup data binding for alert banner
            alertBindingSource.DataSource = alertViewModel;
            alertLabel.DataBindings.Add("Text", alertBindingSource, "AlertText");

            // Sensor message handlers
            _alertHandlerSensorTelemetry = HandleAlertActionForSensor<SensorTelemetryMessage>;
            _alertHandlerSensorStatus = HandleAlertActionForSensor<SensorStatusMessage>;
            _alertHandlerSensorError = HandleAlertActionForSensor<SensorErrorMessage>;

            // Actuator message handlers
            _alertHandlerActuatorTelemetry = HandleAlertActionForActuator<ActuatorTelemetryMessage>;
            _alertHandlerActuatorStatus = HandleAlertActionForActuator<ActuatorStatusMessage>;
            _alertHandlerActuatorError = HandleAlertActionForActuator<ActuatorErrorMessage>;

            // Register alert clear ack event handler
            _alertClearAckHandler = OnAlertClearAck;

            // Register logger command ack handler
            _loggerCommandAckHandler = OnLoggerCommandAck;

            // Register sensor command ack handler
            _sensorCommandAckHandler = OnSensorCommandAck;

            // Register global log message handler
            _globalLogHandler = OnGlobalLogMessage;

            // Register sensor handlers with event bus
            _eventBus.Subscribe(_alertHandlerSensorTelemetry);
            _eventBus.Subscribe(_alertHandlerSensorStatus);
            _eventBus.Subscribe(_alertHandlerSensorError);

            // Register actuator handlers with event bus
            _eventBus.Subscribe(_alertHandlerActuatorTelemetry);
            _eventBus.Subscribe(_alertHandlerActuatorStatus);
            _eventBus.Subscribe(_alertHandlerActuatorError);

            // Subscribe to sensor telemetry for the grid
            _eventBus.Subscribe<SensorTelemetryMessage>(HandleSensorTelemetry);

            // Register alert clear ack handler with event bus
            _eventBus.Subscribe(_alertClearAckHandler);

            // Register the logger ack message handler with event bus
            _eventBus.Subscribe(_loggerCommandAckHandler);

            // Register to the global messages so we can log them
            _eventBus.SubscribeToGlobalMessages(_globalLogHandler);

            // Attach tab button click handlers
            vitalsTabButton.Click += VitalsTabButton_Click;
            actuatorsTabButton.Click += ActuatorsTabButton_Click;
            logsTabButton.Click += LogsTabButton_Click;
            settingsTabButton.Click += SettingsTabButton_Click;

            // Log viewer event handlers
            logGridView.CellDoubleClick += LogGridView_CellDoubleClick;
            applyLogFilterButton.Click += ApplyLogFilterButton_Click;
            //showLogFileButton.Click += (s, e) => ShowLogFile();
            //refreshLogFileButton.Click += (s, e) => RefreshLogFile();
            logGridView.SelectionChanged += logGridView_SelectionChanged;

            logTypeFilterComboBox.SelectedIndexChanged += LogTypeFilterComboBox_SelectedIndexChanged;
        }



        private void LogTypeFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {

            ApplyLogFilter();
        }

        #endregion

        #region Event Handlers

        private void LogGridView_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= logGridView.Rows.Count)
                return;

            var row = logGridView.Rows[e.RowIndex];
            var type = row.Cells["Type"].Value?.ToString() ?? "";
            var timestamp = row.Cells["Timestamp"].Value?.ToString() ?? "";
            var source = row.Cells["Source"].Value?.ToString() ?? "";
            var message = row.Cells["Message"].Value?.ToString() ?? "";

            string details = $"Type: {type}\nTime: {timestamp}\nSource: {source}\nMessage:\n{message}";

            ShowAlertPopup(details, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void logGridView_SelectionChanged(object? sender, EventArgs e)
        {
            if (logGridView.SelectedRows.Count > 0)
            {
                var row = logGridView.SelectedRows[0];
                if (row.Cells["MessageId"].Value is Guid id)
                    selectedMessageId = id;
            }
        }

        private void OnGlobalLogMessage(IMessageEnvelope envelope)
        {
            RunOnUiThread(() =>
            {
                allGlobalMessages.Add(envelope);

                // Limit the number of messages held
                if (allGlobalMessages.Count > MaxLogMessages)
                    allGlobalMessages.RemoveAt(0); // Remove oldest

                string selectedOrigin = logTypeFilterComboBox.SelectedItem?.ToString() ?? "All";
                if (selectedOrigin == "All" || envelope.MessageOrigin.ToString() == selectedOrigin)
                {
                    logGridView.Rows.Add(
                        envelope.MessageId,
                        envelope.MessageOrigin.ToString(),
                        envelope.MessageType.ToString(),
                        envelope.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        envelope.Payload?.ToString() ?? ""
                    );
                }
            });
        }

        private void ApplyLogFilterButton_Click(object? sender, EventArgs e)
        {
            // Get the selected filter type from the ComboBox
            currentLogTypeFilter = logTypeFilterComboBox.SelectedItem?.ToString() ?? "All";
            ApplyLogFilter();
        }

        private void OnSensorCommandAck(MessageEnvelope<SensorCommandAckMessage> envelope)
        {
            throw new NotImplementedException();
        }

        private void OnLoggerCommandAck(MessageEnvelope<LoggerCommandAckMessage> envelope)
        {
            // Use switch statement to handle the different command types
            switch (envelope.Payload.CommandType)
            {
                case LoggerCommands.Start:
                    if (envelope.Payload.IsAcknowledged)
                    {
                        // Update UI to reflect that logging has started
                        RunOnUiThread(() =>
                        {
                            // Example: Change a label or button state
                            // loggingStatusLabel.Text = "Logging Started";
                        });
                    }
                    break;
                case LoggerCommands.Stop:
                    if (envelope.Payload.IsAcknowledged)
                    {
                        // Update UI to reflect that logging has stopped
                        RunOnUiThread(() =>
                        {
                            // Example: Change a label or button state
                            // loggingStatusLabel.Text = "Logging Stopped";
                        });
                    }
                    break;
                case LoggerCommands.AdjustLogFilePath:
                    if (envelope.Payload.IsAcknowledged)
                    {
                        // Update UI to reflect new log file path
                        RunOnUiThread(() =>
                        {
                            // Example: Update a label with the new file path
                            // logFilePathLabel.Text = "New Log File Path Set";
                        });
                    }
                    break;
                case LoggerCommands.GetLogFilePath:
                    if (envelope.Payload.IsAcknowledged)
                    {
                        // Update UI to display the current log file path
                        RunOnUiThread(() =>
                        {
                            logFilePath = envelope.Payload.Metadata?["FilePath"];
                            //LoadLogFile(); // Custom method to load and display t
                            //logFileTimer.Start();
                            //logRichTextBox.Text = $"Log File Path: {envelope.Payload.Metadata["FilePath"]}";

                        });
                    }
                    break;
                default:
                    // Handle unknown command types if necessary
                    break;
            }
        }

        /// <summary>
        /// Handler for when the user clicks the AlertBanner.
        /// </summary>
        private void AlertBanner_Click(object? sender, EventArgs e)
        {
            // Find the matching alert in the log
            foreach (ListViewItem item in alertListView.Items)
            {
                // item.SubItems[2] = Source, item.SubItems[3] = Alert Text
                if (item.SubItems.Count >= 4 &&
                    item.SubItems[2].Text == alertViewModel.Source &&
                    item.SubItems[3].Text == alertViewModel.AlertText)
                {
                    // Build the details string as in AlertListView_MouseUp
                    string count = item.SubItems[0].Text;
                    string time = item.SubItems[1].Text;
                    string source = item.SubItems[2].Text;
                    string alert = item.SubItems[3].Text;
                    string severity = item.SubItems[4].Text;

                    // Configure details string
                    var details = $"Count: {count}\nTime: {time}\nSource: {source}\nAlert Value: {alert}\nSeverity: {severity}";

                    // Show popup
                    var result = ShowAlertPopup(
                        details + "\n\nClear this alert?",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information
                    );
                    if (result == DialogResult.OK)
                    {
                        RunOnUiThread(() => RemoveAlertListViewItemAndUpdate(item));
                    }
                    break;
                }
            }
        }

        private void OnAlertClearAck(MessageEnvelope<AlertClearAckMessage> envelope)
        {
            // Only clear if ack is for all alerts and was successful
            if (envelope.Payload != null && envelope.Payload.Source == "ALL" && envelope.Payload.alertCleared)
            {
                RunOnUiThread(() => {
                    alertListView.Items.Clear();
                    ShowAlert(new AlertViewModel { AlertText = "No active alerts", IsCritical = false, Source = "" });
                });
            }
        }

        /// <summary>
        /// Handles the click event for the "Clear Alerts" button.
        /// </summary>
        /// <remarks>This method clears all items from the alert list, updates the alert count, and
        /// displays a default message indicating that there are no active alerts.</remarks>
        /// <param name="sender">The source of the event, typically the "Clear Alerts" button.</param>
        /// <param name="e">An <see cref="EventArgs"/> instance containing the event data.</param>
        private void ClearAlertsButton_Click(object? sender, EventArgs e)
        {
            // Publish AlertClearMessage with clearAllMessages = true
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                clearAllMessages = true
            };
            var envelope = new MessageEnvelope<AlertClearMessage<IEventMessage>>(
                clearMsg,
                MessageOrigins.AlertManager,
                MessageTypes.AlertClear
            );
            _ = _eventBus.PublishAsync(envelope);
        }

        /// <summary>
        /// Handles the click event for the "Pause Alerts" button.
        /// </summary>
        /// <param name="sender">The source of the event, typically the "Pause Alerts" button.</param>
        /// <param name="e">An <see cref="EventArgs"/> instance containing the event data.</param>
        private void PauseAlertsButton_Click(object? sender, EventArgs e)
        {
            alertsPaused = !alertsPaused;
            if (alertsPaused)
            {
                // Unsubscribe from alert handlers
                if (_alertHandlerSensorTelemetry != null) _eventBus.Unsubscribe(_alertHandlerSensorTelemetry);
                if (_alertHandlerSensorStatus != null) _eventBus.Unsubscribe(_alertHandlerSensorStatus);
                if (_alertHandlerSensorError != null) _eventBus.Unsubscribe(_alertHandlerSensorError);
                if (_alertHandlerActuatorTelemetry != null) _eventBus.Unsubscribe(_alertHandlerActuatorTelemetry);
                if (_alertHandlerActuatorStatus != null) _eventBus.Unsubscribe(_alertHandlerActuatorStatus);
                if (_alertHandlerActuatorError != null) _eventBus.Unsubscribe(_alertHandlerActuatorError);
                RunOnUiThread(() =>
                {
                    pauseAlertsButton.Text = "Resume Alerts";
                    //pauseAlertsButton.BackColor = Color.Red; // Not-active
                    UpdatePauseStatusIndicator();
                });
                
            }
            else
            {
                // Re-subscribe to alert handlers
                if (_alertHandlerSensorTelemetry != null) _eventBus.Subscribe(_alertHandlerSensorTelemetry);
                if (_alertHandlerSensorStatus != null) _eventBus.Subscribe(_alertHandlerSensorStatus);
                if (_alertHandlerSensorError != null) _eventBus.Subscribe(_alertHandlerSensorError);
                if (_alertHandlerActuatorTelemetry != null) _eventBus.Subscribe(_alertHandlerActuatorTelemetry);
                if (_alertHandlerActuatorStatus != null) _eventBus.Subscribe(_alertHandlerActuatorStatus);
                if (_alertHandlerActuatorError != null) _eventBus.Subscribe(_alertHandlerActuatorError);
                RunOnUiThread(() =>
                {
                    pauseAlertsButton.Text = "Pause Alerts";
                    //pauseAlertsButton.BackColor = Color.Green; // Active
                    UpdatePauseStatusIndicator();
                });
            }
        }

        /// <summary>
        /// Handles the click event for the "Vitals" tab button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void VitalsTabButton_Click(object? sender, EventArgs e)
        {
            //logFileTimer.Stop(); // Stop log updates
            ShowSensorGrid();
            mainViewportPanel.BackColor = Color.LightSkyBlue; // Example color for Vitals
        }

        /// <summary>
        /// Handles the click event for the "Actuators" tab button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ActuatorsTabButton_Click(object? sender, EventArgs e)
        {
            //logFileTimer.Stop();
            HideSensorGrid();
            mainViewportPanel.BackColor = Color.LightGreen; // Example color for Actuators
        }

        /// <summary>
        /// Handles the click event for the "Logs" tab button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogsTabButton_Click(object? sender, EventArgs e)
        {
            HideSensorGrid();
            RunOnUiThread(() =>
            {
                mainViewportPanel.Controls.Clear();
                mainViewportPanel.Controls.Add(logGridView);
                mainViewportPanel.Controls.Add(logButtonPanel);
                mainViewportPanel.Controls.Add(logTypeFilterComboBox);
                //mainViewportPanel.Controls.Add(showLogFileButton);
                //mainViewportPanel.Controls.Add(refreshLogFileButton);
            });
            logGridView.Rows.Clear();
            ApplyLogFilter();
        }

        /// <summary>
        /// Handles the click event for the "Settings" tab button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SettingsTabButton_Click(object? sender, EventArgs e)
        {
            // 1. Clear the panel
            mainViewportPanel.Controls.Clear();
            mainViewportPanel.BackColor = Color.DarkGray;

            
            Label titleLabel = new Label
            {
                Text = "Settings",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(40, 30)
            };

            
            Label pollingLabel = new Label
            {
                Text = "Adjust Polling Rate (0-60s):",
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                AutoSize = true,
                // Y=100 puts it comfortably below the Title
                Location = new Point(40, 100)
            };

            NumericUpDown pollingInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 60,
                Increment = 1,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Width = 80,
                
                Location = new Point(350, 96)
            };

            Button saveButton = new Button
            {
                Text = "Apply Settings",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 45),
                BackColor = Color.LightGray, // Disabled initially
                ForeColor = Color.DarkGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Default,
                Enabled = false,
                // MOVED UP: Directly below the Polling Rate line
                Location = new Point(40, 150)
            };

            Button powerOffButton = new Button
            {
                Text = "Shut Down System",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 45),
                BackColor = Color.Crimson,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                
                Location = new Point(40, 230)
            };

            powerOffButton.Click += (s, args) =>
            {
                if (MessageBox.Show("Are you sure you want to shut down?", "Power Off",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    Application.Exit();
                }
            };






            mainViewportPanel.Controls.Add(powerOffButton);
            mainViewportPanel.Controls.Add(titleLabel);
            mainViewportPanel.Controls.Add(pollingLabel);
            mainViewportPanel.Controls.Add(pollingInput);
            mainViewportPanel.Controls.Add(saveButton);
        }

        /// <summary>
        /// Handle AlertActionMessage for sensor payloads - show popup.
        /// </summary>
        private void HandleAlertActionForSensor<TPayload>(MessageEnvelope<AlertActionMessage<TPayload>> envelope)
           where TPayload : SensorMessageBase
        {
            var msg = envelope.Payload;
            if (msg == null) return;

            // Use the message's CreatedAt if available, otherwise use DateTime.Now
            string alertTime = (msg.CreatedAt != default)
                ? msg.CreatedAt.ToString("MMM. dd HH:mm:ss")
                : DateTime.Now.ToString("MMM. dd HH:mm:ss");

            AlertViewModel avm = new AlertViewModel
            {
                AlertText = msg.AlertText,
                IsCritical = msg.Payload?.IsCritical ?? false,
                Source = msg.Source,
                Time = alertTime
            };

            RunOnUiThread(() =>
            {
                ShowAlert(avm);

                // Use alertNumber from the message for the count
                string alertCount = msg.alertNumber.ToString();
                string source = msg.Payload?.SensorID ?? "Unknown";
                string alertText = msg.AlertText;
                string severity = msg.Payload?.IsCritical == true ? "Critical" : "Normal";

                // Create ListViewItem
                var item = new ListViewItem(alertCount);

                // Add subitems: Source, Severity, Alert Text
                item.SubItems.Add(alertTime);
                item.SubItems.Add(source);                
                item.SubItems.Add(alertText);
                item.SubItems.Add(severity);

                // Insert at the top
                alertListView.Items.Insert(0, item);
                //UpdateAlertCount();
            });
           
        }

        /// <summary>
        /// Handle AlertActionMessage for actuator payloads - show popup.
        /// </summary>
        private void HandleAlertActionForActuator<TPayload>(MessageEnvelope<AlertActionMessage<TPayload>> envelope)
            where TPayload : ActuatorMessageBase
        {
            var msg = envelope.Payload;
            if (msg == null) return;

            var entry = new AlertEntry
            {
                Source = msg.Source,
                AlertText = msg.AlertText,
                Payload = msg.Payload,
                IsCritical = msg.Payload?.IsCritical ?? false
            };
        }

        /// <summary>
        /// Shows an alert in the alert banner.
        /// </summary>
        /// <param name="alert"></param>
        private void ShowAlert(AlertViewModel alert, string? time = null)
        {
            alertViewModel.AlertText = alert.AlertText;
            alertViewModel.IsCritical = alert.IsCritical;
            alertViewModel.Source = alert.Source;
            alertViewModel.Time = alert.Time;

            alertBanner = this.Controls["AlertBanner"] as Panel;
            if (alertBanner != null)
            {
                if (string.IsNullOrWhiteSpace(alert.AlertText) || alert.AlertText == "No active alerts")
                {
                    alertBanner.BackColor = NoAlertsActiveColour;
                    alertBannerSeverityValue.Text = "";
                }
                else if (alert.IsCritical)
                {
                    alertBanner.BackColor = SevereAlertColour;
                    alertBannerSeverityValue.Text = "Critical";
                }
                else
                {
                    alertBanner.BackColor = ActiveAlertsColour;
                    alertBannerSeverityValue.Text = "Normal";
                }
            }

            string displayTime = time ?? alert.Time ?? DateTime.Now.ToString("MMM. dd HH:mm:ss");
            alertBannerTimeValue.Text = displayTime;
            alertBannerSourceValue.Text = alert.Source ?? "";
            alertBannerValueValue.Text = alert.AlertText ?? "";
        }

        /// <summary>
        /// Initialize the sensor grid in the main viewport panel.
        /// </summary>
        private void InitializeSensorGrid()
        {
            sensorGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = Color.White
            };
            sensorGridView.Columns.Add("SensorID", "Sensor ID");
            sensorGridView.Columns.Add("Type", "Type");
            sensorGridView.Columns.Add("Value", "Value");
            sensorGridView.Columns.Add("IsCritical", "Critical");
            sensorGridView.Columns.Add("Timestamp", "Timestamp");
        }

        private void ShowSensorGrid()
        {
            mainViewportPanel.Controls.Clear();
            mainViewportPanel.Controls.Add(sensorGridView);
        }

        private void HideSensorGrid()
        {
            if (mainViewportPanel.Controls.Contains(sensorGridView))
                mainViewportPanel.Controls.Remove(sensorGridView);
        }

        /// <summary>
        /// Handles the reception of sensor telemetry data - updates the sensor grid.
        /// </summary>
        private void HandleSensorTelemetry(MessageEnvelope<SensorTelemetryMessage> envelope)
        {
            var msg = envelope.Payload;
            if (msg?.Data == null) return;
            RunOnUiThread(() =>
            {
                if (!sensorRows.TryGetValue(msg.SensorID, out var row))
                {
                    row = new DataGridViewRow();
                    row.CreateCells(sensorGridView,
                        msg.SensorID,
                        msg.TypeOfSensor.ToString(),
                        msg.Data.Value.ToString("F2"),
                        msg.Data.IsCritical ? "Yes" : "No",
                        msg.Data.CreatedAt.ToString("HH:mm:ss")
                    );
                    sensorGridView.Rows.Add(row);
                    sensorRows[msg.SensorID] = row;
                }
                else
                {
                    row.SetValues(
                        msg.SensorID,
                        msg.TypeOfSensor.ToString(),
                        msg.Data.Value.ToString("F2"),
                        msg.Data.IsCritical ? "Yes" : "No",
                        msg.Data.CreatedAt.ToString("HH:mm:ss")
                    );
                }
            });
        }

        #endregion
        #region Methods

        // Helper to parse a log line into structured columns
        private (string Type, string Timestamp, string Source, string Message)? ParseLogLine(string line)
        {
            // Example log format: [INFO] 2025-12-02 10:00:00 SourceName - Message text
            var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(\w+)\]\s+([^\-]+)\s+([^\-]+)\s*-\s*(.*)");
            if (match.Success)
            {
                var type = match.Groups[1].Value;
                var timestamp = match.Groups[2].Value.Trim();
                var source = match.Groups[3].Value.Trim();
                var message = match.Groups[4].Value.Trim();
                return (type, timestamp, source, message);
            }
            // Fallback for lines that don't match
            return null;
        }

        private void ApplyLogFilter()
        {
            // Track the selected MessageId before clearing
            Guid? prevSelectedId = selectedMessageId;
            logGridView.Rows.Clear();
            string selectedOrigin = logTypeFilterComboBox.SelectedItem?.ToString() ?? "All";
            var filtered = allGlobalMessages.Where(msg =>
                selectedOrigin == "All" || msg.MessageOrigin.ToString() == selectedOrigin);

            int selectedRowIndex = -1;
            int rowIndex = 0;
            foreach (var msg in filtered)
            {
                logGridView.Rows.Add(
                    msg.MessageId,
                    msg.MessageOrigin.ToString(),
                    msg.MessageType.ToString(),
                    msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    msg.Payload?.ToString() ?? ""
                );
                if (prevSelectedId.HasValue && msg.MessageId == prevSelectedId.Value)
                    selectedRowIndex = rowIndex;
                rowIndex++;
            }

            // Reselect and scroll to the previously selected row AFTER all rows are added
            if (selectedRowIndex >= 0 && selectedRowIndex < logGridView.Rows.Count)
            {
                logGridView.ClearSelection();
                logGridView.Rows[selectedRowIndex].Selected = true;
                logGridView.FirstDisplayedScrollingRowIndex = selectedRowIndex;
            }
            else if (logGridView.Rows.Count > 0)
            {
                // Optionally, scroll to the bottom if no selection
                logGridView.ClearSelection();
                logGridView.Rows[logGridView.Rows.Count - 1].Selected = true;
                logGridView.FirstDisplayedScrollingRowIndex = logGridView.Rows.Count - 1;
            }
        }

        private void ShowLiveLogs()
        {
            // Clear grid, subscribe to global messages
            logGridView.Rows.Clear();
            _eventBus.SubscribeToGlobalMessages(_globalLogHandler);
        }

        //private void ShowLogFile()
        //{
        //    showingFileLog = true;
        //    _eventBus.UnsubscribeFromGlobalMessages(_globalLogHandler);
        //    refreshLogFileButton.Visible = true;
        //    LoadLogFile();
        //}

        //private void RefreshLogFile()
        //{
        //    if (showingFileLog)
        //        LoadLogFile();
        //}


        //private void LoadLogFile()
        //{
        //    logGridView.Rows.Clear();
        //    if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
        //    {
        //        var logLines = new List<string>();
        //        using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //        using (var reader = new StreamReader(stream))
        //        {
        //            string? line;
        //            while ((line = reader.ReadLine()) != null)
        //            {
        //                logLines.Add(line);
        //            }
        //        }
        //        allLogLines = logLines;
        //        foreach (var entry in allLogLines.Select(ParseLogLine).Where(e => e != null))
        //        {
        //            var (type, timestamp, source, message) = entry!.Value;
        //            logGridView.Rows.Add(type, timestamp, source, message);
        //        }
        //    }
        //}

        //private void LogFileTimer_Tick(object? sender, EventArgs e)
        //{
        //    if (string.IsNullOrEmpty(logFilePath) || !File.Exists(logFilePath))
        //        return;

        //    var fileInfo = new FileInfo(logFilePath);
        //    if (fileInfo.Length == lastLogFileLength)
        //        return; // No new content

        //    // Read new lines from the log file
        //    var newLogLines = new List<string>();
        //    using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        //    using (var reader = new StreamReader(stream))
        //    {
        //        string? line;
        //        while ((line = reader.ReadLine()) != null)
        //        {
        //            newLogLines.Add(line);
        //        }
        //    }

        //    // Only add new lines to the grid
        //    if (newLogLines.Count > allLogLines.Count)
        //    {
        //        var addedLines = newLogLines.Skip(allLogLines.Count);
        //        foreach (var line in addedLines)
        //        {
        //            var entry = ParseLogLine(line);
        //            if (entry != null)
        //            {
        //                var (type, timestamp, source, message) = entry.Value;
        //                logGridView.Rows.Add(type, timestamp, source, message);
        //            }
        //        }
        //        allLogLines = newLogLines;
        //    }

        //    lastLogFileLength = fileInfo.Length;
        //}

        /// <summary>
        /// Requests the log file path via an eventBus message.
        /// </summary>
        private void RequestLogFilePath()
        {
            var request = new LoggerCommandMessage(LoggerCommands.GetLogFilePath);
            var envelope = new MessageEnvelope<LoggerCommandMessage>(request, MessageOrigins.DisplayManager);
            _ = _eventBus.PublishAsync(envelope);
            Console.WriteLine("Firing off the request for GetfilePath");
        }

        /// <summary>
        /// Update the pause status indicator based on the current alert pause state.
        /// </summary>
        private void UpdatePauseStatusIndicator()
        {
            if (alertsPaused)
            {
                pauseStatusLabel.Text = "Alerts Off";
                pauseStatusIndicator.BackColor = Color.Red;
            }
            else
            {
                pauseStatusLabel.Text = "Alerts On";
                pauseStatusIndicator.BackColor = Color.Green;
            }
        }

        /// <summary>
        /// Shows a popup dialog with alert details.
        /// </summary>
        private DialogResult ShowAlertPopup(string details, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var popup = new SingleAlertPopup(details, buttons, icon))
            {
                popup.StartPosition = FormStartPosition.CenterParent;
                return popup.ShowDialog(this);
            }
        }

        /// <summary>
        /// Handler for mouse up event on the alert list view.
        /// </summary>
        private void AlertListView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            var info = alertListView.HitTest(e.Location);
            if (info.Item != null)
            {
                var item = info.Item;
                string count = item.SubItems[0].Text;
                string time = item.SubItems[1].Text;
                string source = item.SubItems[2].Text;
                string alert = item.SubItems[3].Text;
                string severity = item.SubItems[4].Text;

                var details = $"Count: {count}\nTime: {time}\nSource: {source}\nAlert Value: {alert}\nSeverity: {severity}";

                var result = ShowAlertPopup(
                    details + "\n\nClear this alert?",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information
                );

                if (result == DialogResult.OK)
                {
                    RunOnUiThread(() => RemoveAlertListViewItemAndUpdate(item));
                }
            }
        }

        /// <summary>
        /// Removes the specified ListViewItem from the alert list and updates the alert banner accordingly.
        /// </summary>
        /// <param name="item"></param>
        private void RemoveAlertListViewItemAndUpdate(ListViewItem item)
        {
            // Extract alert details before removing
            string alertNumberStr = item.SubItems[0].Text;
            string source = item.SubItems[2].Text;
            string alertText = item.SubItems[3].Text;
            int alertNumber = 0;
            int.TryParse(alertNumberStr, out alertNumber);

            alertListView.Items.Remove(item);
            //UpdateAlertCount();

            // Publish AlertClearMessage on the EventBus
            var clearMsg = new AlertClearMessage<IEventMessage>
            {
                Source = source,
                alertNumber = alertNumber,
                // Payload can be null or you can try to store the original payload if needed
            };
            var clearEnvelope = new MessageEnvelope<AlertClearMessage<IEventMessage>>(
                clearMsg,
                MessageOrigins.AlertManager,
                MessageTypes.AlertClear
            );
            _ = _eventBus.PublishAsync(clearEnvelope);

            if (alertListView.Items.Count == 0)
            {
                ShowAlert(new AlertViewModel { AlertText = "No active alerts", IsCritical = false, Source = "" }, null);
            }
            else
            {
                var nextItem = alertListView.Items[0];
                bool isCritical = nextItem.SubItems.Count > 4 && nextItem.SubItems[4].Text.Equals("Critical", StringComparison.OrdinalIgnoreCase);
                ShowAlert(
                    new AlertViewModel
                    {
                        AlertText = nextItem.SubItems[3].Text, // Value
                        Source = nextItem.SubItems[2].Text,    // Source
                        IsCritical = isCritical
                    },
                    nextItem.SubItems[1].Text // Time
                );
            }
        }

        /// <summary>
        /// Attach click handlers to all controls in the alert banner for interactivity.
        /// </summary>
        /// <param name="control"></param>
        private void AttachAlertBannerClickHandlers(Control control)
        {
            control.Click += AlertBanner_Click;
            foreach (Control child in control.Controls)
            {
                AttachAlertBannerClickHandlers(child);
            }
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
            if (_alertHandlerSensorTelemetry != null) _eventBus.Unsubscribe(_alertHandlerSensorTelemetry);
            if (_alertHandlerSensorStatus != null) _eventBus.Unsubscribe(_alertHandlerSensorStatus);
            if (_alertHandlerSensorError != null) _eventBus.Unsubscribe(_alertHandlerSensorError);

            if (_alertHandlerActuatorTelemetry != null) _eventBus.Unsubscribe(_alertHandlerActuatorTelemetry);
            if (_alertHandlerActuatorStatus != null) _eventBus.Unsubscribe(_alertHandlerActuatorStatus);
            if (_alertHandlerActuatorError != null) _eventBus.Unsubscribe(_alertHandlerActuatorError);

            if (_alertClearAckHandler != null) _eventBus.Unsubscribe(_alertClearAckHandler);

            // Unsubscribe from global messages
            if (_globalLogHandler != null) _eventBus.UnsubscribeFromGlobalMessages(_globalLogHandler);

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

            // Configure the main form
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 800);
            this.Text = "Carebed Dashboard";
        }

        #endregion

        #endregion
    }


}
