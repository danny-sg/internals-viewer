using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Pages
{
    /// <inheritdoc />
    /// <summary>
    /// Boot Page
    /// </summary>
    public class BootPage : Page
    {
        private const int CheckpointLsnOffset = 444;

        public BootPage(string connectionString, string databaseName)
            :base(connectionString, databaseName, new PageAddress(1, 9))
        {
            LoadCheckpointLsn();
        }

        /// <summary>
        /// Gets or sets the last checkpoint LSN.
        /// </summary>
        /// <value>The checkpoint LSN.</value>
        public LogSequenceNumber CheckpointLsn { get; set; }

        /// <summary>
        /// Loads the checkpoint LSN directly from the page data.
        /// </summary>
        private void LoadCheckpointLsn()
        {
            var checkpointLsnValue = new byte[10];

            Array.Copy(PageData, CheckpointLsnOffset, checkpointLsnValue, 0, LogSequenceNumber.Size);
            CheckpointLsn = new LogSequenceNumber(checkpointLsnValue);
        }

        /// <summary>
        /// Refresh the Page
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();

            LoadCheckpointLsn();
        }
    }
}
