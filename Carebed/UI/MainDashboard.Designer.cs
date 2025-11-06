using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message;
using Carebed.Infrastructure.MessageEnvelope;

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

        // basic test UI controls
        private System.Windows.Forms.Button publishButton;
        private System.Windows.Forms.TextBox transportLogTextBox;

        /// <summary>
        /// Constructor for MainDashboard that accepts an IEventBus instance.
        /// </summary>
        /// <param name="eventBus"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public MainDashboard(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
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
            _eventBus.Subscribe<SensorData>(HandleSensorData);
        }

        /// <summary>
        /// Handler for incoming SensorData messages.
        /// </summary>
        /// <param name="envelope"></param>
        private void HandleSensorData(MessageEnvelope<SensorData> envelope)
        {
            // fast guard: if form is closing/disposed, ignore the update
            if (IsDisposed || Disposing) return;

            RunOnUiThread(() =>
            {
                try
                {
                    // Example UI updates - replace with your actual control names
                    // update a label showing the latest value
                    transportLogTextBox.Text = envelope.Payload.Value.ToString("F2");

                    // Append a short receipt message to the transport log so the tester can see the message arrived
                    transportLogTextBox?.AppendText($"{DateTime.Now:HH:mm:ss.fff} Received: {envelope.Payload.Value:F2} from {envelope.MessageOrigin}\r\n");

                    // optional: update other UI elements, logs, status indicators...
                }
                catch (Exception ex)
                {
                    // avoid crashing the UI thread; log for diagnostics
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
            _eventBus.Unsubscribe<SensorData>(HandleSensorData);
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

            // publishButton - generates and publishes a SensorData envelope for testing
            this.publishButton = new System.Windows.Forms.Button();
            this.publishButton.Name = "publishButton";
            this.publishButton.Text = "Publish SensorData";
            this.publishButton.Size = new System.Drawing.Size(140, 30);
            this.publishButton.Location = new System.Drawing.Point(12, 12);
            this.publishButton.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            this.publishButton.Click += publishButton_Click;

            // transportLogTextBox - multi-line text box to show send/receive traces
            this.transportLogTextBox = new System.Windows.Forms.TextBox();
            this.transportLogTextBox.Name = "transportLogTextBox";
            this.transportLogTextBox.Multiline = true;
            this.transportLogTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.transportLogTextBox.ReadOnly = true;
            this.transportLogTextBox.Size = new System.Drawing.Size(760, 120);
            this.transportLogTextBox.Location = new System.Drawing.Point(12, 50);
            this.transportLogTextBox.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;

            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "Form1";

            // Add test controls to the form
            this.Controls.Add(this.publishButton);
            this.Controls.Add(this.transportLogTextBox);
        }

        #endregion

        /// <summary>
        /// Click handler for the publish test button. Creates a SensorData payload,
        /// wraps it in a MessageEnvelope and publishes it on the event bus.
        /// </summary>
        private async void publishButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // create a simple random value for testing
                var rnd = new Random();
                //double value = Math.Round(rnd.NextDouble() * 100.0, 2);
                double value = 80085.00f;

                // construct the sensor data instance (use whichever constructor/record signature you have)
                var sensorData = new SensorData(value);

                // wrap in an envelope with origin and explicit type
                var envelope = new MessageEnvelope<SensorData>(sensorData, MessageOriginEnum.DisplayManager, MessageTypeEnum.SensorData);

                // publish asynchronously
                await _eventBus.PublishAsync(envelope);

                // append a publish trace to the transport log so tester can see send time
                transportLogTextBox?.AppendText($"{DateTime.Now:HH:mm:ss.fff} Published: {value:F2}\r\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Publish failed: {ex}");
                MessageBox.Show($"Publish failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
