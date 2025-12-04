using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.Message.Actuator;
using Carebed.Infrastructure.Message.ActuatorMessages;
using Carebed.Infrastructure.Message.AlertMessages;
using Carebed.Infrastructure.Message.LoggerMessages;
using Carebed.Infrastructure.Message.SensorMessages;
using Carebed.Infrastructure.Message.UI;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.Managers;
using Carebed.Models.Sensors;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;

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

        // Actuator inventory / status handlers (non-alert messages)
        private Action<MessageEnvelope<ActuatorInventoryMessage>>? _actuatorInventoryHandler;
        private Action<MessageEnvelope<ActuatorStatusMessage>>? _actuatorStatusHandler;
        private Action<MessageEnvelope<ActuatorTelemetryMessage>>? _actuatorTelemetryHandler;

        // Stores discovered actuators (id -> type)
        private readonly Dictionary<string, ActuatorTypes> _availableActuators = new();
        // UI state for actuators tab
        private bool actuatorsTabActive = false;

        // Map actuator id -> status label so status updates can be reflected
        private readonly Dictionary<string, Label> actuatorStatusLabels = new();
        // Map actuator id -> toggle button (for binary actuators like lamp)
        private readonly Dictionary<string, Button> actuatorToggleButtons = new();
        // Map actuator id -> icon picture box
        private readonly Dictionary<string, PictureBox> actuatorIconBoxes = new();
        // Map actuator id -> card panel
        private readonly Dictionary<string, Panel> actuatorCards = new();

        // Cache last-known actuator states so UI can be initialized correctly when rebuilding the panel
        private readonly Dictionary<string, ActuatorStates> _actuatorStates = new();

        // Sensor grid for displaying telemetry data
        //private DataGridView sensorGridView;
        private Dictionary<string, DataGridViewRow> sensorRows = new();

        // Charting UI for sensor values over time
        //private Chart sensorChart; // <-- new chart field
        //private SplitContainer splitContainerSensors; // <-- split container to host grid + chart
        private Dictionary<string, Series> sensorSeries = new(); // map sensor id -> chart series
        private readonly int ChartMaxPoints = 120; // keep last N points per series

        // Panel that will host actuators UI when Actuators tab is active
        private Panel actuatorsPanel;
        // Inner grid for actuator cards
        private FlowLayoutPanel actuatorsGrid;

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

        private Color AppColour = Color.DodgerBlue;

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
        private TableLayoutPanel rootLayout;
        private TableLayoutPanel viewportLayout;

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
        private ComboBox logTypeFilterComboBox;
        private Label logTypeFilterLabel;
        private List<string> allLogLines = new();
        //private string currentLogTypeFilter = "All";
        private FlowLayoutPanel logButtonPanel; // Add this field
        private Guid? selectedMessageId = null;
        private const int MaxLogMessages = 1000; // Set your desired limit
        private Panel footerPanel;
        private Button scrollToLatestButton;
        private bool logsTabActive = false;
        private Panel logToolbarContainer;
        private FlowLayoutPanel logButtonRightPanel;
        private Button pauseGlobalMessagesButton;
        private Panel pauseGlobalStatusIndicator;
        private bool globalMessagesPaused = false;
        #endregion

        #region Settings Page Elements

        private class PendingPollingRequest
        {
            public int RequestedSeconds { get; init; }
            public System.Timers.Timer TimeoutTimer { get; init; }
        }

        private int appliedPollingSeconds = 1; // current applied baseline (UI authoritative until ack)
        private NumericUpDown? settingsPollingInput;      // reference to control created in Settings view
        private Button? settingsSaveButton;               // reference to Apply button
        private Label? settingsPollingStatusLabel;        // status label shown while waiting for ack
        private readonly Dictionary<Guid, PendingPollingRequest> _pendingPollingRequests = new();
        private const int PollingRequestTimeoutMs = 5000; // 5s timeout (tweak as needed)
        #endregion

        #region Sensor Charts and Grid Controls

        private FlowLayoutPanel chartsFlowPanel;
        private Dictionary<string, Chart> sensorCharts = new();

        #endregion

        #region Actuator Controls
        // Add these fields near the other UI fields (e.g. next to actuatorsPanel / actuatorsGrid)
        private Label? actuatorsRefreshLabel;
        private Panel? actuatorsRefreshHolder;
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
            InitializeAlertLogPanel();
            InitializeMainViewportPanel();
            InitializeSensorGrid(); // <-- sets up grid + chart controls
            InitializeLogViewer();

            // Subscribe to actuator inventory/status/telemetry early so we don't miss initial inventory messages
            _eventBus.Subscribe<ActuatorInventoryMessage>(HandleActuatorInventory);
            _eventBus.Subscribe<ActuatorStatusMessage>(HandleActuatorStatus);
            _eventBus.Subscribe<ActuatorTelemetryMessage>(HandleActuatorTelemetry);

            // Setup the Alert Banner click event handlers
            AttachAlertBannerClickHandlers(alertBanner);

            // Build the root layout - used to fix docking/order issues
            BuildRootLayout();

            // Keep EnsureDockingOrder for safety during migration (no-op now)
            //EnsureDockingOrder();
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

            // Register actuator handlers with event bus (alerts)
            _eventBus.Subscribe(_alertHandlerActuatorTelemetry);
            _eventBus.Subscribe(_alertHandlerActuatorStatus);
            _eventBus.Subscribe(_alertHandlerActuatorError);

            // Subscribe to sensor telemetry for the grid
            _eventBus.Subscribe<SensorTelemetryMessage>(HandleSensorTelemetry);

            // Register sensor command ack handler with event bus
            _eventBus.Subscribe(_sensorCommandAckHandler);

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
            logGridView.SelectionChanged += LogGridView_SelectionChanged;

            // new subscriptions for scroll/join-to-latest behavior
            logGridView.Scroll += LogGridView_Scrolled;
            logGridView.RowsAdded += LogGridView_RowsAdded;

            // Subscribe to single-click selection
            alertListView.MouseUp += AlertListView_MouseUp;

            // Log viewer event handlers
            logGridView.CellDoubleClick += LogGridView_CellDoubleClick;
            logGridView.SelectionChanged += LogGridView_SelectionChanged;

            // Log type filter change handler
            logTypeFilterComboBox.SelectedIndexChanged += LogTypeFilterComboBox_SelectedIndexChanged;

            // NOTE: actuator inventory/status/telemetry subscriptions moved to constructor to avoid missing initial messages

            // Start the application on the Vitals tab by showing the sensor grid immediately.
            // This makes the Vitals view visible when the form first appears.
            ShowSensorGrid();

        }

        #endregion

        #region Initialize UI Components

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
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, // visual border so you can see edges at runtime
                Padding = new Padding(4),
                Margin = new Padding(0)
            };

            // Internal layout to host toolbar row and the datagrid (prevents overlap / z-order issues)
            viewportLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            viewportLayout.RowStyles.Clear();
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // toolbar row (combo + buttons)
            viewportLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // grid row (fills remaining)

            // Add viewportLayout to the main viewport panel
            mainViewportPanel.Controls.Add(viewportLayout);

            // Floating "jump to newest" button (added to mainViewportPanel so it floats above the grid)
            scrollToLatestButton = new Button
            {
                Text = "Jump to latest message",
                AutoSize = true,
                Visible = false,
                Anchor = AnchorStyles.Bottom, // changed: anchor to bottom only so horizontal position is controlled in RepositionFloatingButton
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8)
            };
            scrollToLatestButton.FlatAppearance.BorderSize = 0;
            scrollToLatestButton.Click += LogViewScrollToLatestButton_Click;

            // Add the floating button directly into the main viewport (so it overlays the grid).
            // We'll reposition on resize so it stays in the bottom-center.
            mainViewportPanel.Controls.Add(scrollToLatestButton);
            scrollToLatestButton.BringToFront();

            // Reposition the floating button whenever the viewport is resized
            mainViewportPanel.Resize += MainViewportPanel_Resize;

            this.Controls.Add(mainViewportPanel);
        }

        /// <summary>
        /// Setup the log viewer UI components.
        /// </summary>
        private void InitializeLogViewer()
        {
            logGridView = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                // Use None so we control widths and allow horizontal scrollbar when total width > control width
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                BackgroundColor = Color.Black,
                ForeColor = Color.LightGreen,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.Black,
                    ForeColor = Color.LightGreen,
                    SelectionBackColor = Color.DarkGreen,
                    SelectionForeColor = Color.White,
                    WrapMode = DataGridViewTriState.False // prevent wrapping so horizontal scroll appears when needed
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    WrapMode = DataGridViewTriState.False
                },
                ScrollBars = ScrollBars.Both,
                MultiSelect = false,
                AllowUserToResizeColumns = true
            };

            // Columns (same as before)
            logGridView.Columns.Add("MessageId", "MessageId");
            logGridView.Columns.Add("Timestamp", "Timestamp");
            logGridView.Columns.Add("Source", "Source");
            logGridView.Columns.Add("Type", "Type");
            logGridView.Columns.Add("Message", "Message");
            logGridView.Columns["MessageId"].Visible = false;

            // fixed widths for the non-message columns
            logGridView.Columns["Timestamp"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            logGridView.Columns["Timestamp"].Width = 180;
            logGridView.Columns["Timestamp"].MinimumWidth = 120;

            logGridView.Columns["Source"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            logGridView.Columns["Source"].Width = 150;
            logGridView.Columns["Source"].MinimumWidth = 140;

            logGridView.Columns["Type"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            logGridView.Columns["Type"].Width = 120;
            logGridView.Columns["Type"].MinimumWidth = 100;

            // Message column controlled dynamically (initial minimum)
            logGridView.Columns["Message"].MinimumWidth = 300;
            // Let the DataGridView fill remaining space for the Message column:
            logGridView.Columns["Message"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            // Optional: control relative share if you add other Fill columns later
            logGridView.Columns["Message"].FillWeight = 100;


            // ensure we adjust when sizes change
            logGridView.SizeChanged += (s, e) => AdjustLogMessageColumnWidth();
            if (mainViewportPanel != null)
                mainViewportPanel.Resize += (s, e) => AdjustLogMessageColumnWidth();

            // call once to initialize column widths
            AdjustLogMessageColumnWidth();

            // build toolbar controls but DO NOT add them to any parent here
            logTypeFilterComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Margin = new Padding(4, 6, 4, 6),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            logTypeFilterComboBox.Items.Add("All");
            foreach (var origin in Enum.GetValues(typeof(MessageOrigins)))
                logTypeFilterComboBox.Items.Add(origin.ToString());
            logTypeFilterComboBox.SelectedIndex = 0;

            logTypeFilterLabel = new Label
            {
                Text = "Filter by Message Origin:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(6, 8, 6, 6)
            };

            var logButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                Padding = new Padding(4),
                WrapContents = false
            };

            // Add controls to toolbar (toolbar is NOT parented to mainViewportPanel here)
            logButtonPanel.Controls.Add(logTypeFilterLabel);
            logButtonPanel.Controls.Add(logTypeFilterComboBox);

            // build right-side panel (pause toggle + status indicator)
            logButtonRightPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(4)
            };

            // pause global messages button
            pauseGlobalMessagesButton = new Button
            {
                Text = "Pause Global Messages",
                AutoSize = true,
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(4)
            };
            pauseGlobalMessagesButton.FlatAppearance.BorderSize = 0;
            pauseGlobalMessagesButton.Click += PauseGlobalMessagesButton_Click;

            // small indicator panel to the right of the button
            pauseGlobalStatusIndicator = new Panel
            {
                Size = new Size(16, 16),
                BackColor = Color.Green,
                Margin = new Padding(4, 8, 8, 4)
            };

            // add button + indicator to right panel (indicator appears to the right)
            logButtonRightPanel.Controls.Add(pauseGlobalMessagesButton);
            logButtonRightPanel.Controls.Add(pauseGlobalStatusIndicator);

            // toolbar container hosts the left flow and right flow so they appear on the same row
            logToolbarContainer = new Panel
            {
                Dock = DockStyle.Top,   // TOP so it sizes to its content and doesn't steal the Fill area
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            // add left flow (fills remaining) and right flow (docked right)
            logButtonPanel.Dock = DockStyle.Fill;
            logToolbarContainer.Controls.Add(logButtonPanel);
            logToolbarContainer.Controls.Add(logButtonRightPanel);

            // store reference only — placement happens in LogsTabButton_Click
            this.logButtonPanel = logButtonPanel;
        }

        /// <summary>
        /// Initialize the sensor grid in the main viewport panel.
        /// Also prepares a chart control for plotting sensor values over time.
        /// </summary>
        //private void InitializeSensorGrid()
        //{
        //    sensorGridView = new DataGridView
        //    {
        //        Dock = DockStyle.Fill,
        //        ReadOnly = true,
        //        AllowUserToAddRows = false,
        //        AllowUserToDeleteRows = false,
        //        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        //        ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        //        BackgroundColor = Color.White
        //    };
        //    sensorGridView.Columns.Add("SensorID", "Sensor ID");
        //    sensorGridView.Columns.Add("Type", "Type");
        //    sensorGridView.Columns.Add("Value", "Value");
        //    sensorGridView.Columns.Add("IsCritical", "Critical");
        //    sensorGridView.Columns.Add("Timestamp", "Timestamp");

        //    // Create split container: left = grid, right = chart
        //    splitContainerSensors = new SplitContainer
        //    {
        //        Dock = DockStyle.Fill,
        //        Orientation = Orientation.Vertical
        //    };

        //    // Add the grid to left panel
        //    splitContainerSensors.Panel1.Controls.Add(sensorGridView);

        //    // Create and configure the chart for the right panel
        //    sensorChart = new Chart
        //    {
        //        Dock = DockStyle.Fill,
        //        BackColor = Color.White
        //    };

        //    var chartArea = new ChartArea("SensorArea")
        //    {
        //        BackColor = Color.White,
        //        AxisX = {
        //            Title = "Time",
        //            LabelStyle = { Format = "HH:mm:ss" },
        //            IntervalAutoMode = IntervalAutoMode.VariableCount,
        //            MajorGrid = { Enabled = false }
        //        },
        //        AxisY = {
        //            Title = "Value",
        //            MajorGrid = { Enabled = true }
        //        }
        //    };

        //    sensorChart.ChartAreas.Add(chartArea);

        //    // Legend
        //    var legend = new Legend("SensorsLegend")
        //    {
        //        Docking = Docking.Top,
        //        LegendStyle = LegendStyle.Row
        //    };
        //    sensorChart.Legends.Add(legend);

        //    // Add chart to right panel
        //    splitContainerSensors.Panel2.Controls.Add(sensorChart);

        //    // Note: we do NOT add splitContainerSensors to the viewport here; ShowSensorGrid will add it
        //}

        private void InitializeSensorGrid()
        {
            chartsFlowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                BackColor = AppColour
            };
        }

        #endregion       

        #region Event Handlers

        /// <summary>
        /// Handles selection changes in the log type filter combo box to apply the selected filter.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogTypeFilterComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {

            ApplyLogFilter();
        }

        /// <summary>
        /// Handles clicks on the "Scroll to Latest" button to jump to the most recent log message.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogViewScrollToLatestButton_Click(object? sender, EventArgs e)
        {
            if (logGridView.Rows.Count == 0) return;

            // Scroll so last row is visible and select it
            int lastIndex = logGridView.Rows.Count - 1;
            try
            {
                logGridView.ClearSelection();
                logGridView.Rows[lastIndex].Selected = true;
                logGridView.FirstDisplayedScrollingRowIndex = lastIndex;
            }
            catch
            {
                // ignore any layout-time exceptions
            }

            // hide the button after jumping
            if (scrollToLatestButton != null) scrollToLatestButton.Visible = false;
            if (footerPanel != null) footerPanel.Visible = false;
        }

        /// <summary>
        /// Handles scrolling events in the log grid view to update the visibility of the scroll button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogGridView_Scrolled(object? sender, ScrollEventArgs e)
        {
            UpdateScrollButtonVisibility();
        }

        /// <summary>
        ///  Handles when new rows are added to the log grid view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogGridView_RowsAdded(object? sender, DataGridViewRowsAddedEventArgs e)
        {
            // New rows arrived — update the footer/button visibility.
            UpdateScrollButtonVisibility();
        }

        /// <summary>
        /// Handles double-clicks on log grid view cells to show detailed message info.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Handles selection changes in the log grid view to track the selected message ID.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LogGridView_SelectionChanged(object? sender, EventArgs e)
        {
            if (logGridView.SelectedRows.Count > 0)
            {
                var row = logGridView.SelectedRows[0];
                if (row.Cells["MessageId"].Value is Guid id)
                    selectedMessageId = id;
            }
        }

        /// <summary>
        /// Handles global log messages to update the log viewer UI.
        /// </summary>
        /// <param name="envelope"></param>
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
                    // Add values in the order: MessageId, Timestamp, Source, Type, Message
                    logGridView.Rows.Add(
                        envelope.MessageId,
                        envelope.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                        envelope.MessageOrigin.ToString(),
                        envelope.MessageType.ToString(),
                        GetReadableEnvelopeMessage(envelope)
                        );
                }
            });
        }

        // Create a concise, human-readable representation of the envelope payload.
        // Uses typed checks where available and reflection as a fallback so the UI shows
        // meaningful text instead of type names like "AlertActionMessage`1[...]".
        private string GetReadableEnvelopeMessage(IMessageEnvelope envelope)
        {
            if (envelope == null) return string.Empty;
            var payload = envelope.Payload;
            if (payload == null) return string.Empty;

            try
            {
                // Sensor telemetry
                if (payload is SensorTelemetryMessage stm)
                {
                    string sensorId = !string.IsNullOrWhiteSpace(stm.SensorID)
                        ? stm.SensorID
                        : (stm.Data?.Source ?? "(unknown sensor)");
                    string value = stm.Data != null ? stm.Data.Value.ToString() : "(no value)";
                    string units = stm.Data?.Metadata != null && stm.Data.Metadata.TryGetValue("units", out var u) ? $" {u}" : "";
                    return $"SensorID: {sensorId}, Sensor Reading: {value}{units}";
                }
                // Sensor status
                if (payload is SensorStatusMessage ssm)
                {
                    string type = ssm.GetType().Name.Replace("Message", "");
                    return $"Sensor: {type}, State: {ssm.CurrentState}";
                }
                // Sensor error
                if (payload is SensorErrorMessage sem)
                {
                    return $"Sensor Error: {sem.ErrorCode}, {sem.Description}, State: {sem.CurrentState}";
                }
                // Actuator telemetry
                if (payload is ActuatorTelemetryMessage atm)
                {
                    string type = atm.TypeOfActuator.ToString();
                    string state = atm.ErrorCode ?? "";
                    string details = $"Actuator: {type}";
                    if (atm.Position != null) details += $", Pos: {atm.Position}";
                    if (atm.Temperature != null) details += $", Temp: {atm.Temperature:F1}°C";
                    if (atm.Watts != null) details += $", Watts: {atm.Watts:F1}";
                    if (!string.IsNullOrEmpty(state)) details += $", State: {state}";
                    return details;
                }
                // Actuator status
                if (payload is ActuatorStatusMessage asm)
                {
                    string type = asm.TypeOfActuator.ToString();
                    return $"Actuator: {type}, State: {asm.CurrentState}";
                }
                // Actuator error
                if (payload is ActuatorErrorMessage aem)
                {
                    string type = aem.TypeOfActuator.ToString();
                    return $"Actuator Error: {type}, Code: {aem.ErrorCode}, {aem.Description}, State: {aem.CurrentState}";
                }

                // Actuator command
                if (payload is ActuatorCommandMessage acm)
                {
                    string paramStr = acm.Parameters != null
                        ? string.Join(", ", acm.Parameters.Select(kv => $"{kv.Key}: {kv.Value}"))
                        : "None";
                    return $"ActuatorCommand: {acm.CommandType}, Params: {paramStr}, CorrelationId: {acm.CorrelationId}";
                }
                // Actuator command ack
                if (payload is ActuatorCommandAckMessage ack)
                {
                    string metaStr = ack.Metadata != null && ack.Metadata.Count > 0
                        ? string.Join(", ", ack.Metadata.Select(kv => $"{kv.Key}: {kv.Value}"))
                        : "None";
                    return $"ActuatorCommandAck: {ack.CommandType}, CanExecute: {ack.CanExecuteCommand}, Metadata: {metaStr}, CorrelationId: {ack.CorrelationId}";
                }

                // Fallback: try to use AlertText, Message, or ToString
                // Fallback: handle AlertActionMessage<T> as "Alert"
                var t = payload.GetType();
                if (t.IsGenericType && t.Name.StartsWith("AlertActionMessage"))
                {
                    var alertTextProp = t.GetProperty("AlertText");
                    var alertNumberProp = t.GetProperty("alertNumber");
                    var payloadProp = t.GetProperty("Payload");
                    string alertText = alertTextProp?.GetValue(payload) as string ?? "";
                    string alertNumber = alertNumberProp?.GetValue(payload)?.ToString() ?? "";

                    // Try to extract an ID from the payload (SensorID, ActuatorId, etc.)
                    string triggeredBy = "";
                    var payloadValue = payloadProp?.GetValue(payload);
                    if (payloadValue != null)
                    {
                        var payloadType = payloadValue.GetType();
                        var sensorIdProp = payloadType.GetProperty("SensorID");
                        var actuatorIdProp = payloadType.GetProperty("ActuatorId");
                        if (sensorIdProp != null)
                            triggeredBy = sensorIdProp.GetValue(payloadValue)?.ToString() ?? "";
                        else if (actuatorIdProp != null)
                            triggeredBy = actuatorIdProp.GetValue(payloadValue)?.ToString() ?? "";
                    }

                    string triggerInfo = !string.IsNullOrWhiteSpace(triggeredBy) ? $"Triggered by: {triggeredBy}" : "";
                    return $"Alert Number: #{alertNumber}, {triggerInfo}, Value: {alertText}";
                }
            }
            catch
            {
                return payload.GetType().Name;
            }
            // Fallback for unknown types
            return payload.ToString() ?? payload.GetType().Name;
        }

        /// <summary>
        /// Handles SensorCommandAckMessage messages to update the UI based on sensor command acknowledgments.
        /// </summary>
        private void OnSensorCommandAck(MessageEnvelope<SensorCommandAckMessage> envelope)
        {
            if (envelope?.Payload == null) return;

            if (envelope.Payload.CommandType != SensorCommands.AdjustPollingRate) return;

            bool ok = envelope.Payload.CommandExecutedSuccessfully;
            var correlation = envelope.Payload.CorrelationId;

            RunOnUiThread(() =>
            {
                if (correlation != Guid.Empty && _pendingPollingRequests.TryGetValue(correlation, out var pending))
                {
                    // cancel timeout timer
                    try { pending.TimeoutTimer.Stop(); pending.TimeoutTimer.Dispose(); } catch { }

                    _pendingPollingRequests.Remove(correlation);

                    if (ok)
                    {
                        appliedPollingSeconds = pending.RequestedSeconds;

                        if (settingsPollingStatusLabel != null)
                            settingsPollingStatusLabel.Text = $"Polling Rate: {appliedPollingSeconds}s";

                        if (settingsPollingInput != null)
                            settingsPollingInput.Value = appliedPollingSeconds;

                        if (settingsSaveButton != null)
                        {
                            settingsSaveButton.Enabled = false;
                            settingsSaveButton.BackColor = Color.LightGray;
                            settingsSaveButton.ForeColor = Color.DarkGray;
                        }

                        // update saveButton UI when value changes — compare to the live appliedPollingSeconds field,
                        // not a captured local variable, so UI stays correct after ack/timeouts.
                        settingsPollingInput.ValueChanged += (s, ev) =>
                        {
                            if (settingsPollingInput == null || settingsSaveButton == null) return;

                            bool changed = (int)settingsPollingInput.Value != appliedPollingSeconds;
                            settingsSaveButton.Enabled = changed;
                            settingsSaveButton.BackColor = changed ? Color.DodgerBlue : Color.LightGray;
                            settingsSaveButton.ForeColor = changed ? Color.White : Color.DarkGray;
                        };

                        MessageBox.Show("Polling rate updated successfully.", "Polling Rate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        if (settingsPollingStatusLabel != null)
                            settingsPollingStatusLabel.Text = $"Failed to update polling rate: {envelope.Payload.Reason ?? "unknown"}";

                        MessageBox.Show($"Failed to update polling rate: {envelope.Payload.Reason ?? "unknown reason"}",
                            "Polling Rate", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        // allow retry: re-enable Save
                        if (settingsSaveButton != null)
                        {
                            settingsSaveButton.Enabled = true;
                            settingsSaveButton.BackColor = Color.DodgerBlue;
                            settingsSaveButton.ForeColor = Color.White;
                        }
                    }
                }
                else
                {
                    // No pending request matched this ack — best-effort feedback
                    if (ok)
                    {
                        // unknown correlation, but success: show generic message
                        MessageBox.Show("Polling rate updated (ack received without matching request).", "Polling Rate", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        if (settingsPollingStatusLabel != null)
                            settingsPollingStatusLabel.Text = "Polling Rate updated";
                    }
                    else
                    {
                        MessageBox.Show($"Failed to update polling rate: {envelope.Payload.Reason ?? "unknown reason"}",
                            "Polling Rate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            });
        }

        /// <summary>
        /// Handles LoggerCommandAckMessage messages to update the UI based on logger command acknowledgments.
        /// </summary>
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
        /// Handles AlertClearAckMessage messages to update the UI based on alert clear acknowledgments.
        /// </summary>
        /// <param name="envelope"></param>
        private void OnAlertClearAck(MessageEnvelope<AlertClearAckMessage> envelope)
        {
            Console.WriteLine("Received AlertClearAckMessage!!!!!");
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
                MessageOrigins.DisplayManager,
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
            Console.WriteLine("Pause button pressed.");
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
            //mainViewportPanel.BackColor = Color.LightSkyBlue; // Example color for Vitals

            logsTabActive = false;
            if (scrollToLatestButton != null && scrollToLatestButton.Visible)
                scrollToLatestButton.Visible = false;
        }

        /// <summary>
        /// Handles the click event for the "Actuators" tab button.
        /// The actuators UI is only populated when this tab is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ActuatorsTabButton_Click(object? sender, EventArgs e)
        {
            // Ensure viewportLayout is parented inside mainViewportPanel.
            if (!mainViewportPanel.Controls.Contains(viewportLayout))
            {
                mainViewportPanel.Controls.Clear();
                viewportLayout.Dock = DockStyle.Fill;
                mainViewportPanel.Controls.Add(viewportLayout);
            }

            // Clear any existing content and prepare actuators view
            viewportLayout.Controls.Clear();

            // Create actuatorsPanel if needed
            if (actuatorsPanel == null)
            {
                actuatorsPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true,
                    BackColor = Color.Transparent,
                    Padding = new Padding(8)
                };
            }
            else
            {
                actuatorsPanel.Controls.Clear();
            }

            // Add the actuatorsPanel to the viewport
            viewportLayout.Controls.Add(actuatorsPanel, 0, 1);

            // Mark tab active so handlers will update UI
            actuatorsTabActive = true;

            // Request a fresh actuator inventory from the system so we get a current list
            // (this will cause ActuatorManager to emit an ActuatorInventoryMessage which the
            // existing HandleActuatorInventory will merge into _availableActuators and call PopulateActuatorsPanel).
            try
            {
                var requestCmd = new ActuatorCommandMessage
                {
                    CommandType = ActuatorCommands.EmitActuatorInventory,
                    ActuatorId = "ActuatorManager",
                    TypeOfActuator = ActuatorTypes.Manager
                };
                var requestEnvelope = new MessageEnvelope<ActuatorCommandMessage>(
                    requestCmd,
                    MessageOrigins.DisplayManager,
                    MessageTypes.ActuatorCommand
                );
                _ = _eventBus.PublishAsync(requestEnvelope);
            }
            catch
            {
                // swallow — inventory request is best-effort
            }

            // Show a "Refreshing..." indicator while waiting for inventory if we have no cached actuators.
            if (_availableActuators.Count == 0)
            {
                if (actuatorsRefreshLabel == null)
                {
                    actuatorsRefreshLabel = new Label
                    {
                        Text = "Refreshing actuators...",
                        AutoSize = true,
                        ForeColor = Color.Black,
                        BackColor = Color.Transparent,
                        Font = new Font("Segoe UI", 10, FontStyle.Italic),
                        Margin = new Padding(8)
                    };
                    actuatorsRefreshHolder = new Panel
                    {
                        Dock = DockStyle.Top,
                        Height = 36,
                        BackColor = Color.Transparent
                    };
                    actuatorsRefreshHolder.Controls.Add(actuatorsRefreshLabel);
                    // position label with a little padding
                    actuatorsRefreshLabel.Location = new Point(8, 8);
                }

                if (!actuatorsPanel.Controls.Contains(actuatorsRefreshHolder))
                    actuatorsPanel.Controls.Add(actuatorsRefreshHolder);
            }
            else
            {
                // We already have cached actuators - show them immediately.
                PopulateActuatorsPanel();
            }

            mainViewportPanel.BackColor = Color.White;
            logsTabActive = false;
            if (scrollToLatestButton != null && scrollToLatestButton.Visible)
                scrollToLatestButton.Visible = false;

            EnsureDockingOrder();
        }

        /// <summary>
        /// Handles the click event for the "Logs" tab button.
        /// </summary>
        private void LogsTabButton_Click(object? sender, EventArgs e)
        {
            HideSensorGrid();

            RunOnUiThread(() =>
            {
                // Ensure viewportLayout is present inside mainViewportPanel.
                // If the Settings view cleared mainViewportPanel, viewportLayout field still exists
                // but is not parented. Re-add it so children we add become visible.
                if (!mainViewportPanel.Controls.Contains(viewportLayout))
                {
                    // Remove any stray controls then add the viewportLayout back.
                    mainViewportPanel.Controls.Clear();
                    viewportLayout.Dock = DockStyle.Fill;
                    mainViewportPanel.Controls.Add(viewportLayout);
                }
                else
                {
                    // If already parented, clear its children before repopulating.
                    viewportLayout.Controls.Clear();
                }

                // Reset panel background so previous page color doesn't bleed through
                mainViewportPanel.BackColor = AppColour;
                // Optional: ensure layout background matches parent so no transparency shows
                viewportLayout.BackColor = Color.Transparent; // or Color.White if you prefer

                // Prepare toolbar (combo + buttons)
                logButtonPanel.AutoSize = true;
                logButtonPanel.Dock = DockStyle.Fill;

                // Prepare the grid
                logGridView.Dock = DockStyle.Fill;

                // Use the toolbar container so left controls sit on the left and pause controls on the right
                viewportLayout.Controls.Add(logToolbarContainer, 0, 0);
                viewportLayout.Controls.Add(logGridView, 0, 1);

                // ensure pause button state reflects current subscription state
                globalMessagesPaused = false;
                UpdateGlobalMessagesStatus();

                // Mark Logs tab active and ensure floating button state
                logsTabActive = true;
                if (scrollToLatestButton != null)
                {
                    scrollToLatestButton.Visible = false;
                    scrollToLatestButton.BringToFront();
                    RepositionFloatingButton();
                }

                try { mainViewportPanel.BringToFront(); } catch { }
                EnsureDockingOrder();
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
            // Ensure viewportLayout is present in the main viewport so all views share the same container.
            if (!mainViewportPanel.Controls.Contains(viewportLayout))
            {
                mainViewportPanel.Controls.Clear();
                viewportLayout.Dock = DockStyle.Fill;
                mainViewportPanel.Controls.Add(viewportLayout);
            }
            // Clear existing view content (toolbar and content rows)
            viewportLayout.Controls.Clear();

            // Use a dedicated panel for settings so it's a single unit we can add/remove.
            var settingsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Gray
            };

            // Title
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
                Location = new Point(40, 100)
            };

            // create and store references to controls so ack handler can update them later
            settingsPollingInput = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 60,
                Increment = 1,
                Font = new Font("Segoe UI", 12, FontStyle.Regular),
                Width = 80,
                Location = new Point(350, 96)
            };

            settingsSaveButton = new Button
            {
                Text = "Apply Settings",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Size = new Size(180, 45),
                BackColor = Color.LightGray,
                ForeColor = Color.DarkGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Default,
                Enabled = false
                // Location will be set below so it can be centered under the numeric input
            };

            // status label to the right of the numeric input (positioned after controls are added)
            settingsPollingStatusLabel = new Label
            {
                Text = $"Polling Rate: {appliedPollingSeconds}s",
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                ForeColor = Color.Black
            };

            // capture baseline from appliedPollingSeconds so Save enables only on change
            int initialPollingValue = appliedPollingSeconds;
            settingsPollingInput.Value = initialPollingValue;

            // update saveButton UI when value changes
            settingsPollingInput.ValueChanged += (s, ev) =>
            {
                bool changed = (int)settingsPollingInput.Value != initialPollingValue;
                settingsSaveButton.Enabled = changed;
                settingsSaveButton.BackColor = changed ? Color.DodgerBlue : Color.LightGray;
                settingsSaveButton.ForeColor = changed ? Color.White : Color.DarkGray;
            };

            // Send command and show "Applying..." — do not update appliedPollingSeconds here
            settingsSaveButton.Click += (s, ev) =>
            {
                var correlationId = Guid.NewGuid();
                var requestedSeconds = (int)settingsPollingInput.Value;

                var cmd = new SensorCommandMessage
                {
                    CommandType = SensorCommands.AdjustPollingRate,
                    SensorID = "SensorManager",
                    TypeOfSensor = SensorTypes.Manager,
                    CorrelationId = correlationId
                };
                cmd.Parameters ??= new Dictionary<string, object>();
                cmd.Parameters["IntervalSeconds"] = (double)requestedSeconds;

                // create timeout timer
                var timer = new System.Timers.Timer(PollingRequestTimeoutMs) { AutoReset = false };
                timer.Elapsed += (ts, te) =>
                {
                    // Timer runs on threadpool — call handler to update UI
                    OnPollingRequestTimedOut(correlationId);
                };
                timer.Start();

                // store pending request
                _pendingPollingRequests[correlationId] = new PendingPollingRequest
                {
                    RequestedSeconds = requestedSeconds,
                    TimeoutTimer = timer
                };

                // update UI status
                if (settingsPollingStatusLabel != null)
                    settingsPollingStatusLabel.Text = "Applying new polling rate...";
                settingsSaveButton.Enabled = false;
                settingsSaveButton.BackColor = Color.LightGray;
                settingsSaveButton.ForeColor = Color.DarkGray;

                var envelope = new MessageEnvelope<SensorCommandMessage>(
                    cmd,
                    MessageOrigins.DisplayManager,
                    MessageTypes.SensorCommand
                );

                _ = _eventBus.PublishAsync(envelope);
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

            // Build an inner TableLayout that auto-sizes and contains the settings controls,
            // a centered Apply button row, a spacer (3 button-heights) and the centered Shutdown button.
            var innerLayout = new TableLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = new Padding(0)
            };
            innerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            innerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            innerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            innerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // polling row
            innerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // apply button row
            innerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, settingsSaveButton.Height * 3)); // spacer row (3 button heights)
            innerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // shutdown row

            // Add the polling row: label, numeric input, status label
            innerLayout.Controls.Add(pollingLabel, 0, 0);
            innerLayout.Controls.Add(settingsPollingInput, 1, 0);
            innerLayout.Controls.Add(settingsPollingStatusLabel, 2, 0);

            // Centered Apply button row: use a FlowLayoutPanel so the button is centered automatically
            var applyRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            applyRow.Controls.Add(settingsSaveButton);
            innerLayout.Controls.Add(applyRow, 0, 1);
            innerLayout.SetColumnSpan(applyRow, 3);

            // Spacer row is already defined via RowStyle (no control required), but keep a tiny panel to ensure height on some runtimes
            var spacer = new Panel { Width = 1, Height = settingsSaveButton.Height * 3 };
            innerLayout.Controls.Add(spacer, 0, 2);
            innerLayout.SetColumnSpan(spacer, 3);

            // Centered Shutdown button row
            var shutdownRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.None,
                Margin = new Padding(0)
            };
            shutdownRow.Controls.Add(powerOffButton);
            innerLayout.Controls.Add(shutdownRow, 0, 3);
            innerLayout.SetColumnSpan(shutdownRow, 3);

            // Add the inner layout to the settings panel and center it horizontally.
            // Keep a resize handler so it stays centered when the viewport or form resizes.
            int topOffset = 48; // adjust this value (pixels) to move the whole group down
            settingsPanel.Controls.Add(innerLayout);
            innerLayout.Location = new Point(
                Math.Max(0, (settingsPanel.ClientSize.Width - innerLayout.Width) / 2),
                topOffset // top offset
            );
            settingsPanel.Resize += (s, ev) =>
            {
                innerLayout.Location = new Point(
                    Math.Max(0, (settingsPanel.ClientSize.Width - innerLayout.Width) / 2),
                    topOffset
                );
            };

            // place the settingsPanel into the content row of the viewportLayout
            viewportLayout.Controls.Add(settingsPanel, 0, 1);

            // update colours and state
            mainViewportPanel.BackColor = Color.DarkGray;
            logsTabActive = false;
            if (scrollToLatestButton != null && scrollToLatestButton.Visible)
                scrollToLatestButton.Visible = false;

            EnsureDockingOrder();
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
        /// Handles the reception of sensor telemetry data - updates the sensor grid.
        /// Also appends the telemetry point to the chart series for that sensor.
        /// </summary>
        private void HandleSensorTelemetry(MessageEnvelope<SensorTelemetryMessage> envelope)
        {
            var msg = envelope.Payload;
            if (msg?.Data == null) return;

            RunOnUiThread(() =>
            {
                try
                {
                    if (chartsFlowPanel == null)
                        InitializeSensorGrid();

                    // Lazily create a chart for this sensor
                    if (!sensorCharts.TryGetValue(msg.SensorID, out var chart))
                    {
                        chart = CreateChartForSensor(msg.SensorID);
                        sensorCharts[msg.SensorID] = chart;

                        // Keep series mapping in sensorSeries for compatibility with existing logic
                        if (chart.Series.Count > 0)
                            sensorSeries[msg.SensorID] = chart.Series[0];

                        chartsFlowPanel.Controls.Add(chart);
                        UpdateChartSizes();
                    }

                    // Append point to series
                    if (sensorSeries.TryGetValue(msg.SensorID, out var series))
                    {
                        var dt = msg.Data.CreatedAt == default ? DateTime.Now : msg.Data.CreatedAt;
                        series.Points.AddXY(dt.ToOADate(), msg.Data.Value);

                        while (series.Points.Count > ChartMaxPoints)
                            series.Points.RemoveAt(0);

                        var area = chart.ChartAreas.FirstOrDefault();
                        if (area != null && series.Points.Count > 0)
                        {
                            var lastX = DateTime.FromOADate(series.Points.Last().XValue);
                            var firstX = DateTime.FromOADate(series.Points.First().XValue);
                            area.AxisX.Minimum = firstX.ToOADate();
                            area.AxisX.Maximum = lastX.ToOADate();
                            area.RecalculateAxesScale();
                        }

                        chart.Invalidate();
                    }
                }
                catch
                {
                    // Swallow chart update exceptions — UI should remain responsive
                }
            });
        }

        /// <summary>
        /// Handles the reception of actuator telemetry data - updates the actuator UI when visible.
        /// </summary>
        private void HandleActuatorTelemetry(MessageEnvelope<ActuatorTelemetryMessage> envelope)
        {
            var msg = envelope.Payload;
            if (msg == null) return;

            RunOnUiThread(() =>
            {
                if (!actuatorsTabActive || actuatorsPanel == null) return;

                string actuatorId = msg.ActuatorId ?? string.Empty;               

                if (actuatorStatusLabels.TryGetValue(actuatorId, out var statusLabel))
                {
                    if (msg.Temperature.HasValue)
                    {
                        statusLabel.Text = $"Temp: {msg.Temperature.Value:F1}°C";
                        statusLabel.ForeColor = msg.IsCritical ? Color.Red : Color.Black;
                    }
                    else if (msg.Position != null)
                    {
                        statusLabel.Text = msg.Position.ToString();
                        statusLabel.ForeColor = Color.Black;
                    }
                }

                if (actuatorToggleButtons.TryGetValue(actuatorId, out var btn))
                {
                    bool isOn = msg.Watts.HasValue ? msg.Watts.Value > 0.1 : (msg.Position != null);
                    btn.Text = isOn ? "Turn Off" : "Turn On";
                    btn.BackColor = isOn ? Color.LightCoral : Color.LightGreen;
                    btn.ForeColor = Color.Black;
                }

                // Update the actuator icon based on the type and state
                if (actuatorIconBoxes.TryGetValue(actuatorId, out var iconBox))
                {
                    if (msg.Watts.HasValue && msg.Watts.Value > 0.1)
                    {
                        // Example: Use a different icon or color when the actuator is active
                        iconBox.Image = GetActuatorIcon(msg.ActuatorId, true);
                    }
                    else
                    {
                        iconBox.Image = GetActuatorIcon(msg.ActuatorId, false);
                    }
                }
            });
        }

        /// <summary>
        /// Handles the reception of actuator status messages - updates the actuator status labels.
        /// </summary>
        private void HandleActuatorStatus(MessageEnvelope<ActuatorStatusMessage> envelope)
        {
            var msg = envelope.Payload;
            if (msg == null) return;

            RunOnUiThread(() =>
            {
                if (!actuatorsTabActive || actuatorsPanel == null) return;

                string actuatorId = msg.ActuatorId ?? string.Empty;
                if (!string.IsNullOrEmpty(actuatorId))
                    _actuatorStates[actuatorId] = msg.CurrentState;

                if (actuatorStatusLabels.TryGetValue(actuatorId, out var statusLabel))
                {
                    statusLabel.Text = msg.CurrentState.ToString();
                    statusLabel.ForeColor = msg.CurrentState == ActuatorStates.On || msg.CurrentState == ActuatorStates.Completed
                        ? Color.LimeGreen : (msg.CurrentState == ActuatorStates.Error ? Color.Red : Color.Gray);
                }

                if (actuatorToggleButtons.TryGetValue(actuatorId, out var btn))
                {
                    if (_availableActuators.TryGetValue(actuatorId, out var type) && type == ActuatorTypes.Lamp)
                    {
                        bool isOn = msg.CurrentState == ActuatorStates.On;
                        btn.Text = isOn ? "Turn Off" : "Turn On";
                    }
                    else
                    {
                        bool isMoving = msg.CurrentState == ActuatorStates.Moving;
                        btn.Text = isMoving ? "Stop Moving" : "Start Moving";
                    }
                    btn.BackColor = (msg.CurrentState == ActuatorStates.On || msg.CurrentState == ActuatorStates.Moving)
                        ? Color.LightCoral : Color.LightGreen;
                    btn.ForeColor = Color.Black;
                }

                // Update the actuator icon based on the type and state
                if (actuatorIconBoxes.TryGetValue(actuatorId, out var iconBox))
                {
                    if (msg.CurrentState == ActuatorStates.On)
                    {
                        iconBox.Image = GetActuatorIcon(actuatorId, true);
                    }
                    else
                    {
                        iconBox.Image = GetActuatorIcon(actuatorId, false);
                    }
                }
            });
        }

        /// <summary>
        /// Handles reception of actuator inventory messages - updates known actuators and optionally UI.
        /// </summary>
        private void HandleActuatorInventory(MessageEnvelope<ActuatorInventoryMessage> envelope)
        {
            var msg = envelope.Payload;
            if (msg == null) return;

            RunOnUiThread(() =>
            {
                // Merge metadata if present
                if (msg.Metadata != null)
                {
                    foreach (var kv in msg.Metadata)
                    {
                        if (Enum.TryParse<ActuatorTypes>(kv.Value, out var t))
                            _availableActuators[kv.Key] = t;
                        else
                            _availableActuators[kv.Key] = ActuatorTypes.Custom;
                    }
                }

                // Merge explicit actuators list if provided
                if (msg.Actuators != null && msg.Actuators.Count > 0)
                {
                    foreach (var a in msg.Actuators)
                    {
                        if (!string.IsNullOrWhiteSpace(a.ActuatorId))
                            _availableActuators[a.ActuatorId] = a.Type;
                    }
                }

                if (actuatorsTabActive)
                    PopulateActuatorsPanel();
            });
        }

        /// <summary>
        /// Build or refresh the actuatorsPanel controls from _availableActuators.
        /// </summary>
        private void PopulateActuatorsPanel()
        {
            if (actuatorsPanel == null) return;

            // Remove any "refreshing" indicator before rebuilding the UI
            if (actuatorsRefreshHolder != null && actuatorsPanel.Controls.Contains(actuatorsRefreshHolder))
            {
                try { actuatorsPanel.Controls.Remove(actuatorsRefreshHolder); } catch { }
            }

            actuatorsPanel.Controls.Clear();
            actuatorStatusLabels.Clear();
            actuatorToggleButtons.Clear();
            actuatorIconBoxes.Clear();
            actuatorCards.Clear();

            // Create a flow layout panel for the actuator cards
            actuatorsGrid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                BackColor = AppColour
            };
            actuatorsPanel.Controls.Add(actuatorsGrid);

            // If no actuators known, show an informative label so user/developer can see nothing was discovered
            if (_availableActuators == null || _availableActuators.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "No actuators discovered",
                    AutoSize = true,
                    ForeColor = Color.Black,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI", 10, FontStyle.Italic),
                    Margin = new Padding(12)
                };
                // Place it centered within the panel
                var holder = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
                holder.Controls.Add(emptyLabel);
                emptyLabel.Location = new Point(Math.Max(8, (holder.ClientSize.Width - emptyLabel.Width) / 2), 8);
                holder.Resize += (s, e) =>
                {
                    emptyLabel.Location = new Point(Math.Max(8, (holder.ClientSize.Width - emptyLabel.Width) / 2), 8);
                };
                actuatorsPanel.Controls.Add(holder);
                return;
            }

            foreach (var kv in _availableActuators)
            {
                string actuatorId = kv.Key;
                ActuatorTypes type = kv.Value;

                // Add a label for actuator name/type/ID
                var nameLabel = new Label
                {
                    Text = $"{type}", // Shows type
                    Name = $"NameLabel_{actuatorId}",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.Black,
                    Margin = new Padding(4, 0, 4, 4)
                };

                // Determine the known state (from cache or default)
                ActuatorStates knownState = _actuatorStates.TryGetValue(actuatorId, out var state)
                    ? state
                    : (type == ActuatorTypes.Lamp ? ActuatorStates.Off : ActuatorStates.Idle);

                var statusLabel = new Label
                {
                    Text = knownState.ToString(),
                    Name = $"StatusLabel_{actuatorId}",
                    AutoSize = true,
                    Width = 120,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = (knownState == ActuatorStates.On || knownState == ActuatorStates.Completed)
                        ? Color.LimeGreen
                        : (knownState == ActuatorStates.Error ? Color.Red : Color.Gray),
                    Margin = new Padding(4)
                };
                actuatorStatusLabels[actuatorId] = statusLabel;

                // Determine button text and color based on actuator type and state
                string buttonOnText, buttonOffText, buttonText;
                Color buttonColor;

                if (type == ActuatorTypes.Lamp)
                {
                    buttonOnText = "Turn On";
                    buttonOffText = "Turn Off";
                    bool isOn = knownState == ActuatorStates.On;
                    buttonText = isOn ? buttonOffText : buttonOnText;
                    buttonColor = isOn ? Color.LightCoral : Color.LightGreen;
                }
                else
                {
                    buttonOnText = "Start Moving";
                    buttonOffText = "Stop Moving";
                    bool isMoving = knownState == ActuatorStates.Moving;
                    buttonText = isMoving ? buttonOffText : buttonOnText;
                    buttonColor = isMoving ? Color.LightCoral : Color.LightGreen;
                }

                var toggleButton = new Button
                {
                    Text = buttonText,
                    Name = $"ToggleButton_{actuatorId}",
                    AutoSize = true,
                    Margin = new Padding(4),
                    BackColor = buttonColor,
                    ForeColor = Color.Black
                };

                // capture variables for closure
                toggleButton.Click += (s, e) => ToggleActuatorState(actuatorId, toggleButton);
                actuatorToggleButtons[actuatorId] = toggleButton;

                // Create an icon box for the actuator
                var iconBox = new PictureBox
                {
                    Name = $"IconBox_{actuatorId}",
                    Size = new Size(64, 64),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Margin = new Padding(0, 0, 8, 0),
                    Image = GetActuatorIcon(actuatorId,
                        (type == ActuatorTypes.Lamp && knownState == ActuatorStates.On) ||
                        (type != ActuatorTypes.Lamp && knownState == ActuatorStates.Moving))
                };
                actuatorIconBoxes[actuatorId] = iconBox;

                // Create a card panel for the actuator
                var cardPanel = new Panel
                {
                    Name = $"CardPanel_{actuatorId}",
                    Size = new Size(220, 140),
                    Margin = new Padding(4),
                    Padding = new Padding(8),
                    BackColor = Color.WhiteSmoke,
                    BorderStyle = BorderStyle.FixedSingle
                };

                // Use an inner FlowLayoutPanel to reliably arrange icon on left and details on right
                var cardLayout = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    AutoSize = false,
                    Padding = new Padding(4)
                };

                // Right-side panel stacks status + button vertically
                var rightPanel = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.TopDown,
                    WrapContents = false,
                    AutoSize = true,
                    Width = 220 - 64 - 32, // cardPanel.Width - iconBox.Width - 32
                    Padding = new Padding(4)
                };
                // Ensure status is at top and button below
                rightPanel.Controls.Add(nameLabel);    // Add name/type label at the top
                rightPanel.Controls.Add(statusLabel);
                rightPanel.Controls.Add(toggleButton);

                cardLayout.Controls.Add(iconBox);
                cardLayout.Controls.Add(rightPanel);

                cardPanel.Controls.Add(cardLayout);

                // Store reference to allow later updates
                actuatorCards[actuatorId] = cardPanel;

                // Add the card to the grid
                actuatorsGrid.Controls.Add(cardPanel);
            }

            // small spacer
            actuatorsPanel.Controls.Add(new Panel { Height = 18 });
        }

        /// <summary>
        /// Returns a simple icon image for an actuator.
        /// If a real icon set is available this can be replaced to load resources instead.
        /// </summary>
        private Image GetActuatorIcon(string actuatorId, bool active)
        {
            ActuatorTypes type = ActuatorTypes.Custom;
            if (!string.IsNullOrEmpty(actuatorId) && _availableActuators.TryGetValue(actuatorId, out var t))
                type = t;

            int w = 64, h = 64;
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                var pen = Pens.DarkGray;
                Brush brush = Brushes.LightGray;

                switch (type)
                {
                    case ActuatorTypes.Lamp:
                        pen = Pens.DarkGoldenrod;
                        brush = active ? Brushes.Gold : Brushes.LightGray;
                        // draw simple bulb
                        g.FillEllipse(brush, 12, 8, 40, 40);
                        g.DrawEllipse(pen, 12, 8, 40, 40);
                        g.FillRectangle(Brushes.Silver, 28, 44, 8, 10);
                        break;
                    case ActuatorTypes.BedLift:
                    case ActuatorTypes.HeadTilt:
                    case ActuatorTypes.LegRaise:
                    case ActuatorTypes.BedRoll:
                    case ActuatorTypes.SideRail:
                        pen = Pens.DimGray;
                        brush = active ? Brushes.LightGreen : Brushes.LightGray;
                        g.FillRectangle(brush, 8, 20, 48, 28);
                        g.DrawRectangle(pen, 8, 20, 48, 28);
                        // arrow to indicate movement
                        var points = new Point[] { new Point(w/2, 6), new Point(w/2 - 6, 18), new Point(w/2 + 6, 18) };
                        g.FillPolygon(active ? Brushes.Green : Brushes.Gray, points);
                        break;
                    default:
                        pen = Pens.Gray;
                        brush = active ? Brushes.LightSkyBlue : Brushes.LightGray;
                        g.FillRectangle(brush, 8, 8, 48, 48);
                        g.DrawRectangle(pen, 8, 8, 48, 48);
                        break;
                }
            }
            return bmp;
        }

        /// <summary>
        /// Map actuator type + desired toggle to a concrete ActuatorCommands value.
        /// </summary>
        private ActuatorCommands GetToggleCommand(ActuatorTypes type, bool currentlyMoving)
        {
            // currentlyOn == true means we want to turn it off (deactivate)
            return type switch
            {
                ActuatorTypes.Lamp => currentlyMoving ? ActuatorCommands.DeactivateLamp : ActuatorCommands.ActivateLamp,
                ActuatorTypes.BedLift or ActuatorTypes.HeadTilt or ActuatorTypes.LegRaise or ActuatorTypes.BedRoll or ActuatorTypes.SideRail
                    => currentlyMoving ? ActuatorCommands.Stop : ActuatorCommands.Raise, // Use Raise to start moving
                _ => currentlyMoving ? ActuatorCommands.Stop : ActuatorCommands.Reset,
            };
        }

        /// <summary>
        /// Toggle the state of an actuator and publish a command message.
        /// </summary>
        /// <param name="actuatorId"></param>
        /// <param name="button"></param>
        private void ToggleActuatorState(string actuatorId, Button button)
        {
            if (string.IsNullOrWhiteSpace(actuatorId) || button == null) return;

            ActuatorTypes type = _availableActuators.ContainsKey(actuatorId) ? _availableActuators[actuatorId] : ActuatorTypes.Custom;
            var currentState = _actuatorStates.TryGetValue(actuatorId, out var state) ? state : ActuatorStates.Idle;
            bool currentlyMoving = currentState == ActuatorStates.Moving;

            // Determine the intended action and new state
            bool willBeOn;
            string buttonText;
            Color buttonColor;

            if (type == ActuatorTypes.Lamp)
            {
                // For Lamp: toggle between On and Off
                willBeOn = currentState != ActuatorStates.On;
                buttonText = willBeOn ? "Turn Off" : "Turn On";
                buttonColor = willBeOn ? Color.LightCoral : Color.LightGreen;
                _actuatorStates[actuatorId] = willBeOn ? ActuatorStates.On : ActuatorStates.Off;
            }
            else
            {
                // For other actuators: toggle between Moving and Idle
                willBeOn = !currentlyMoving;
                buttonText = willBeOn ? "Stop Moving" : "Start Moving";
                buttonColor = willBeOn ? Color.LightCoral : Color.LightGreen;
                _actuatorStates[actuatorId] = willBeOn ? ActuatorStates.Moving : ActuatorStates.Idle;
            }

            // Update UI once
            button.Text = buttonText;
            button.BackColor = buttonColor;

            // Update icon immediately if present
            if (actuatorIconBoxes.TryGetValue(actuatorId, out var iconBox))
                iconBox.Image = GetActuatorIcon(actuatorId, willBeOn);

            // Build and send command
            ActuatorCommands cmd = GetToggleCommand(type, !willBeOn);
            var command = new ActuatorCommandMessage
            {
                ActuatorId = actuatorId,
                TypeOfActuator = type,
                CommandType = cmd
            };
            var envelope = new MessageEnvelope<ActuatorCommandMessage>(command, MessageOrigins.DisplayManager, MessageTypes.ActuatorCommand);
            _ = _eventBus.PublishAsync(envelope);
        }

        /// <summary>
        /// Handles the Resize event of the main viewport panel to reposition the floating button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainViewportPanel_Resize(object? sender, EventArgs e)
        {
            RepositionFloatingButton();
            UpdateChartSizes();
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

        #endregion

        #region Methods

        private void UpdateChartSizes()
        {
            if (chartsFlowPanel == null) return;

            const int columns = 3;
            int available = Math.Max(0, chartsFlowPanel.ClientSize.Width - chartsFlowPanel.Padding.Left - chartsFlowPanel.Padding.Right);

            // account for potential vertical scrollbar width
            if (chartsFlowPanel.VerticalScroll.Visible)
                available = Math.Max(0, available - SystemInformation.VerticalScrollBarWidth);

            int targetWidth = Math.Max(280, (available / columns) - 16); // small spacing allowance

            foreach (Control c in chartsFlowPanel.Controls)
            {
                if (c is Chart ch)
                {
                    ch.Width = targetWidth;
                    ch.Height = 220;
                }
            }
        }

        private Chart CreateChartForSensor(string sensorId)
        {
            var chart = new Chart
            {
                BackColor = Color.White,
                Width = 320,
                Height = 220,
                Margin = new Padding(8)
            };

            var area = new ChartArea($"Area_{sensorId}")
            {
                BackColor = Color.White
            };
            area.AxisX.Title = "Time";
            area.AxisX.LabelStyle.Format = "HH:mm:ss";
            area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY.Title = "Value";
            area.AxisY.MajorGrid.Enabled = true;
            chart.ChartAreas.Add(area);

            var series = new Series(sensorId)
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 2,
                XValueType = ChartValueType.DateTime,
                IsVisibleInLegend = false
            };
            chart.Series.Add(series);

            var title = new Title(sensorId)
            {
                Docking = Docking.Top,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            chart.Titles.Add(title);

            return chart;
        }

        private void OnPollingRequestTimedOut(Guid correlationId)
        {
            // This method is called from a timer (non-UI thread).
            RunOnUiThread(() =>
            {
                if (_pendingPollingRequests.TryGetValue(correlationId, out var pending))
                {
                    try { pending.TimeoutTimer.Stop(); pending.TimeoutTimer.Dispose(); } catch { }
                    _pendingPollingRequests.Remove(correlationId);

                    if (settingsPollingStatusLabel != null)
                        settingsPollingStatusLabel.Text = "Timed out waiting for ack";

                    // Allow the user to retry: re-enable Save if the current input differs from applied baseline
                    if (settingsPollingInput != null && settingsSaveButton != null)
                    {
                        bool changed = (int)settingsPollingInput.Value != appliedPollingSeconds;
                        settingsSaveButton.Enabled = changed;
                        settingsSaveButton.BackColor = changed ? Color.DodgerBlue : Color.LightGray;
                        settingsSaveButton.ForeColor = changed ? Color.White : Color.DarkGray;
                    }
                }
            });
        }

        /// <summary>
        /// Shows an alert in the alert banner.
        /// </summary>
        /// <param name="alert"></param>
        private void ShowAlert(AlertViewModel alert, string? time = null)
        {
            // Update view model first (keeps data-binding consistent)
            alertViewModel.AlertText = alert.AlertText;
            alertViewModel.IsCritical = alert.IsCritical;
            alertViewModel.Source = alert.Source;
            alertViewModel.Time = alert.Time;

            // Don't re-query `this.Controls` for the banner — we store it in the `alertBanner` field.
            // That lookup returned null after we re-parented controls into `rootLayout`.
            if (alertBanner == null)
            {
                // Fallback: try to find it anywhere in the form hierarchy.
                var found = this.Controls.Find("AlertBanner", true).FirstOrDefault() as Panel;
                alertBanner = found ?? alertBanner;
            }

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
            // Update the banner labels (these are separate fields and should be non-null after InitializeAlertBanner)
            if (alertBannerTimeValue != null) alertBannerTimeValue.Text = displayTime;
            if (alertBannerSourceValue != null) alertBannerSourceValue.Text = alert.Source ?? "";
            if (alertBannerValueValue != null) alertBannerValueValue.Text = alert.AlertText ?? "";
        }

        /// <summary>
        /// Shows the sensor grid in the main viewport.
        /// </summary>
        private void ShowSensorGrid()
        {
            // Ensure viewportLayout is parented so content is visible
            if (!mainViewportPanel.Controls.Contains(viewportLayout))
            {
                mainViewportPanel.Controls.Clear();
                viewportLayout.Dock = DockStyle.Fill;
                mainViewportPanel.Controls.Add(viewportLayout);
            }

            // Clear any existing content and show the charts panel in the content row
            viewportLayout.Controls.Clear();

            // Ensure the chartsFlowPanel exists
            if (chartsFlowPanel == null)
                InitializeSensorGrid(); // defensive

            chartsFlowPanel.Dock = DockStyle.Fill;
            viewportLayout.Controls.Add(chartsFlowPanel, 0, 1);

            // Defer a resize to let layout finish, then size charts
            this.BeginInvoke((Action)(() =>
            {
                UpdateChartSizes();
            }));

            // Reset background to the Vitals color
            mainViewportPanel.BackColor = AppColour;

            logsTabActive = false;
            if (scrollToLatestButton != null && scrollToLatestButton.Visible)
                scrollToLatestButton.Visible = false;

            EnsureDockingOrder();
        }

        /// <summary>
        /// Hides the sensor grid from the main viewport.
        /// </summary>
        private void HideSensorGrid()
        {
            // Remove charts container from the viewport if present
            if (viewportLayout != null && viewportLayout.Controls.Contains(chartsFlowPanel))
                viewportLayout.Controls.Remove(chartsFlowPanel);

            if (mainViewportPanel.Controls.Contains(chartsFlowPanel))
                mainViewportPanel.Controls.Remove(chartsFlowPanel);
        }

        /// <summary>
        /// Updates the global messages pause button text and status indicator color.
        /// </summary>
        private void UpdateGlobalMessagesStatus()
        {
            if (pauseGlobalMessagesButton == null || pauseGlobalStatusIndicator == null) return;

            if (globalMessagesPaused)
            {
                pauseGlobalMessagesButton.Text = "Resume Global Messages";
                pauseGlobalStatusIndicator.BackColor = Color.Red;
            }
            else
            {
                pauseGlobalMessagesButton.Text = "Pause Global Messages";
                pauseGlobalStatusIndicator.BackColor = Color.Green;
            }
        }

        /// <summary>
        /// Handles click for Pause/Resume Global Messages button.
        /// Subscribes/unsubscribes the global log handler on the event bus and updates UI.
        /// </summary>
        private void PauseGlobalMessagesButton_Click(object? sender, EventArgs e)
        {
            globalMessagesPaused = !globalMessagesPaused;
            if (globalMessagesPaused)
            {
                try { if (_globalLogHandler != null) _eventBus.UnsubscribeFromGlobalMessages(_globalLogHandler); } catch { }
            }
            else
            {
                try { if (_globalLogHandler != null) _eventBus.SubscribeToGlobalMessages(_globalLogHandler); } catch { }
            }
            RunOnUiThread(UpdateGlobalMessagesStatus);
        }

        /// <summary>
        /// Updates the visibility of the "Scroll to Latest" button based on the current state of the logs view.
        /// </summary>
        /// <remarks>This method ensures that the "Scroll to Latest" button is only visible when the logs
        /// view is active  and the last row in the log grid is not currently visible. If the logs view is inactive, the
        /// button  will always be hidden. Additionally, the button's position is adjusted when it becomes
        /// visible.</remarks>
        private void UpdateScrollButtonVisibility()
        {
            if (!logsTabActive) // only show/hide when Logs view is active
            {
                if (scrollToLatestButton != null && scrollToLatestButton.Visible)
                    scrollToLatestButton.Visible = false;
                return;
            }

            if (logGridView == null || scrollToLatestButton == null || mainViewportPanel == null)
                return;

            if (logGridView.Rows.Count == 0)
            {
                if (scrollToLatestButton.Visible)
                    scrollToLatestButton.Visible = false;
                return;
            }

            // If the grid hasn't laid out yet, bail out
            int first = logGridView.FirstDisplayedScrollingRowIndex;
            if (first < 0)
            {
                if (scrollToLatestButton.Visible)
                    scrollToLatestButton.Visible = false;
                return;
            }

            int visible = logGridView.DisplayedRowCount(false);
            int lastVisible = first + visible - 1;
            int lastIndex = logGridView.Rows.Count - 1;

            bool lastRowVisible = lastVisible >= lastIndex;
            bool shouldShow = !lastRowVisible;

            if (scrollToLatestButton.Visible != shouldShow)
            {
                scrollToLatestButton.Visible = shouldShow;
                if (shouldShow)
                {
                    // Ensure it's positioned correctly and on top
                    RepositionFloatingButton();
                    scrollToLatestButton.BringToFront();
                }
            }
        }

        /// <summary>
        /// Applies the selected log filter to the log grid view.
        /// </summary>
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
                    msg.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    msg.MessageOrigin.ToString(),
                    msg.MessageType.ToString(),
                    GetReadableEnvelopeMessage(msg)
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

        /// <summary>
        /// Configures the root layout of the main dashboard form.
        /// </summary>
        private void BuildRootLayout()
        {
            // Remove any controls that were previously added directly to the form.
            // They will be re-parented into the root layout.
            foreach (var ctrl in new Control[] { alertBanner, tabsPanel, mainViewportPanel, alertLogContainer })
            {
                if (ctrl != null && this.Controls.Contains(ctrl))
                    this.Controls.Remove(ctrl);
            }

            // Create root layout with 4 rows:
            // 0 = AutoSize for alert banner
            // 1 = AutoSize for tabs
            // 2 = Percent (100) for main viewport (fills remaining)
            // 3 = Absolute for alert log (fixed height)
            rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.Transparent
            };
            rootLayout.RowStyles.Clear();
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // alert banner
            rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // tabs
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));// main viewport
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));// alert log (fixed)

            // Ensure controls will fill their allocated cells
            alertBanner.Dock = DockStyle.Fill;
            tabsPanel.Dock = DockStyle.Fill;
            mainViewportPanel.Dock = DockStyle.Fill;
            alertLogContainer.Dock = DockStyle.Fill;

            // Add in correct order
            rootLayout.Controls.Add(alertBanner, 0, 0);
            rootLayout.Controls.Add(tabsPanel, 0, 1);
            rootLayout.Controls.Add(mainViewportPanel, 0, 2);
            rootLayout.Controls.Add(alertLogContainer, 0, 3);

            // Add root layout to form
            this.Controls.Add(rootLayout);

            // Force layout update
            this.PerformLayout();
        }

        /// <summary>
        /// Repositions the floating "Scroll to Latest" button within the main viewport panel.
        /// </summary>
        private void RepositionFloatingButton()
        {
            if (scrollToLatestButton == null || mainViewportPanel == null) return;

            // small margin from edges
            const int margin = 12;

            // ensure button measured (AutoSize may require a layout pass)
            scrollToLatestButton.PerformLayout();

            // center horizontally, clamp to margin
            int x = Math.Max(margin, (mainViewportPanel.ClientSize.Width - scrollToLatestButton.Width) / 2);
            // position near the bottom with a small margin
            int y = Math.Max(margin, mainViewportPanel.ClientSize.Height - scrollToLatestButton.Height - margin);

            scrollToLatestButton.Location = new Point(x, y);
            scrollToLatestButton.BringToFront();
        }

        /// <summary>
        /// Ensures the form controls are in the proper z-order for predictable docking:
        /// - alertLogContainer must be earlier in the Controls collection so its Bottom dock
        ///   reserves space before the Fill mainViewportPanel is laid out.
        /// - tabsPanel and alertBanner remain on top.
        /// Call this whenever you change mainViewportPanel children or re-layout the form.
        /// </summary>
        private void EnsureDockingOrder()
        {
            // Defensive checks
            if (this.Controls == null) return;

            // Place alert log container early so Bottom dock is respected
            if (this.Controls.Contains(alertLogContainer) && this.Controls.Contains(mainViewportPanel))
            {
                try
                {
                    this.Controls.SetChildIndex(alertLogContainer, 0);
                    this.Controls.SetChildIndex(mainViewportPanel, 1);
                }
                catch { /* ignore failures during design-time */ }
            }

            // Keep tabs and banner near top of z-order
            if (this.Controls.Contains(tabsPanel))
            {
                try { this.Controls.SetChildIndex(tabsPanel, Math.Max(0, this.Controls.Count - 2)); }
                catch { }
            }
            if (this.Controls.Contains(alertBanner))
            {
                try { this.Controls.SetChildIndex(alertBanner, Math.Max(0, this.Controls.Count - 1)); }
                catch { }
            }

            // Force layout update so docking takes effect immediately
            this.PerformLayout();
        }

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
                MessageOrigins.DisplayManager,
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

        private void AdjustLogMessageColumnWidth()
        {
            // Intentionally left empty.
            // The DataGridView 'Message' column uses DataGridViewAutoSizeColumnMode.Fill,
            // so the built-in layout handles sizing. Removing manual resizing prevents
            // layout churn and avoids fighting the built-in autosize behaviour.
            return;
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

            // Unsubscribe actuator specific message handlers
            _eventBus.Unsubscribe<ActuatorInventoryMessage>(HandleActuatorInventory);
            _eventBus.Unsubscribe<ActuatorStatusMessage>(HandleActuatorStatus);
            _eventBus.Unsubscribe<ActuatorTelemetryMessage>(HandleActuatorTelemetry);

            if (_alertClearAckHandler != null) _eventBus.Unsubscribe(_alertClearAckHandler);

            // Unsubscribe from global messages
            if (_globalLogHandler != null) _eventBus.UnsubscribeFromGlobalMessages(_globalLogHandler);

            // Dispose any pending timers
            foreach (var kv in _pendingPollingRequests.Values)
            {
                try { kv.TimeoutTimer.Stop(); kv.TimeoutTimer.Dispose(); } catch { }
            }
            _pendingPollingRequests.Clear();

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

        #endregion

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
            this.ClientSize = new System.Drawing.Size(1920, 1080);
            this.Text = "Carebed Dashboard";
        }

        #endregion

        
    }
}
