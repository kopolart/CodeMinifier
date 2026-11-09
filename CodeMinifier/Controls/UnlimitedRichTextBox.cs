using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CodeMinifier.Controls
{
    /// <summary>
    /// A <see cref="RichTextBox"/> without the default 64 KB text limit.
    /// </summary>
    public sealed class UnlimitedRichTextBox : RichTextBox
    {
        private const int EM_EXLIMITTEXT = 0x0435;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SendMessage(Handle, EM_EXLIMITTEXT, IntPtr.Zero, new IntPtr(int.MaxValue));
        }
    }
}
