using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using JetBrains.Annotations;

using MediaDownloader.Core;

namespace MediaDownloader
{
    internal class TaskProgressReporter : ITaskProgressReporter
    {
        [NotNull]
        private readonly bool[] _InUse = new bool[20];

        public ITaskProgress CreateTask(string title)
        {
            ITaskProgress result = null;
            using (var evt = new ManualResetEvent(false))
            {
                Task.Run(
                    () =>
                    {
                        var fm = new TaskProgressForm();
                        fm.SetTitle(title);

                        int slot = 0;
                        lock (_InUse)
                            for (int index = 0; index < _InUse.Length; index++)
                                if (!_InUse[index])
                                {
                                    _InUse[index] = true;
                                    slot = index;
                                    break;
                                }

                        int y = Screen.PrimaryScreen.WorkingArea.Bottom - 10 - (slot + 1) * (fm.Height + 15);
                        fm.StartPosition = FormStartPosition.Manual;
                        Debug.WriteLine($"Form opened in slot {slot} at y={y}");
                        fm.Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - 10 - fm.Width, y);
                        fm.Closed += (s, e) =>
                        {
                            lock (_InUse)
                                _InUse[slot] = false;
                        };

                        result = fm;
                        evt.Set();

                        Application.Run(fm);
                    });

                evt.WaitOne();
                return result;
            }
        }
    }
}