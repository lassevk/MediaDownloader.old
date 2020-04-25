using System;
using System.Windows.Forms;

using JetBrains.Annotations;

using MediaDownloader.Core;

namespace MediaDownloader
{
    public partial class TaskProgressForm : Form, ITaskProgress
    {
        public TaskProgressForm()
        {
            InitializeComponent();
        }

        public void SetTitle([NotNull] string title)
        {
            lblTitle.Text = title;
        }

        public void Report(long total, long current)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Report(total, current)));
                return;
            }

            pbProgress.Minimum = 0;
            pbProgress.Maximum = 100;
            pbProgress.Value = (int)(current * 100.0 / total);
        }

        public void Complete()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(Complete));
                return;
            }

            Close();
        }
    }
}