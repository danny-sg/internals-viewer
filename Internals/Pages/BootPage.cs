using System;

namespace InternalsViewer.Internals.Pages
{
    /// <summary>
    /// Boot Page
    /// </summary>
    public class BootPage : Page
    {
        private const int CheckpointLsnOffset = 444;
        private LogSequenceNumber checkpointLsn;

        /// <summary>
        /// Initializes a new instance of the <see cref="BootPage"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        public BootPage(Database database)
            : base(database, new PageAddress(1, 9))
        {
            this.LoadCheckpointLsn();
        }

        /// <summary>
        /// Gets or sets the last checkpoint LSN.
        /// </summary>
        /// <value>The checkpoint LSN.</value>
        public LogSequenceNumber CheckpointLsn
        {
            get { return checkpointLsn; }
            set { checkpointLsn = value; }
        }

        /// <summary>
        /// Loads the checkpoint LSN directly from the page data.
        /// </summary>
        private void LoadCheckpointLsn()
        {
            byte[] checkpointLsnValue = new byte[10];

            Array.Copy(this.PageData, CheckpointLsnOffset, checkpointLsnValue, 0, 10);
            this.CheckpointLsn = new LogSequenceNumber(checkpointLsnValue);
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
