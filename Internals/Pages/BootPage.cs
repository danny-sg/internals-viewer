using System;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Boot Page
    /// </summary>
    public class BootPage : Page
    {
        private const int CheckpointLsnOffset = 444;

        /// <summary>
        /// Initializes a new instance of the <see cref="BootPage"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        public BootPage(Database database)
            : base(database, new PageAddress(1, 9))
        {
            LoadCheckpointLsn();
        }

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

            Array.Copy(PageData, CheckpointLsnOffset, checkpointLsnValue, 0, 10);
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
