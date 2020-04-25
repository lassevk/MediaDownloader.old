using System.Threading.Tasks;

using MediaDownloader.Core;

namespace MediaDownloader
{
    internal class TrayNotification : ITrayNotification
    {
        public async void Notify(string message)
        {
            using (var notification = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Information,
                BalloonTipText = message
            })
            {

                notification.ShowBalloonTip(5000);
                await Task.Delay(10000);
            }
        }
    }
}