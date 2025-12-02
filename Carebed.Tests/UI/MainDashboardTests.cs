using Carebed.Infrastructure.Enums;
using Carebed.Infrastructure.EventBus;
using Carebed.Infrastructure.Message.AlertMessages;
using Carebed.Infrastructure.MessageEnvelope;
using Carebed.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Windows.Forms;
using System.Reflection;

[TestClass]
public class MainDashboardTests
{
    // Subclass to provide RunOnUiThread for test
    private class TestDashboard : MainDashboard
    {
        public TestDashboard(IEventBus bus) : base(bus) { }
        // Remove 'override' since RunOnUiThread is not virtual in MainDashboard
        protected void RunOnUiThread(Action action)
        {
            action(); // Run synchronously for test
        }
    }

    [TestMethod]
    public void OnAlertClearAck_ClearsAlertList_WhenAckIsForAllAndCleared()
    {
        // Arrange
        var eventBus = new Mock<IEventBus>();
        var dashboard = new TestDashboard(eventBus.Object);

        // Access the private alertListView field via reflection
        var alertListViewField = typeof(MainDashboard).GetField("alertListView", BindingFlags.NonPublic | BindingFlags.Instance);
        var alertListView = alertListViewField.GetValue(dashboard) as ListView;
        alertListView.Items.Add(new ListViewItem("1"));

        // Create a valid ack message
        var ack = new AlertClearAckMessage
        {
            Source = "ALL",
            alertCleared = true
        };
        var envelope = new MessageEnvelope<AlertClearAckMessage>(ack, MessageOrigins.AlertManager, MessageTypes.AlertClearAck);

        // Act
        var method = typeof(MainDashboard).GetMethod("OnAlertClearAck", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Invoke(dashboard, new object[] { envelope });

        // Assert
        Assert.AreEqual(0, alertListView.Items.Count, "Alert list should be cleared when ack is for ALL and alertCleared is true.");
    }
}