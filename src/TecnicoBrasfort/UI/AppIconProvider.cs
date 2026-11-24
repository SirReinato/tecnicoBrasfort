using System;
using System.Drawing;
using System.Windows.Forms;

namespace TecnicoBrasfort.UI
{
    public static class AppIconProvider
    {
        private static readonly Icon _cachedIcon = LoadAppIcon();

        public static Icon GetAppIcon()
        {
            return _cachedIcon;
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exePath);
                    if (extracted != null)
                    {
                        return extracted;
                    }
                }
            }
            catch
            {
            }

            return SystemIcons.Shield;
        }
    }
}
