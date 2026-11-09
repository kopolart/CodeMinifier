using System.Drawing;
using System.Windows.Forms;
using CodeMinifier.Minification;

namespace CodeMinifier.UI
{
    /// <summary>
    /// Main window: one tab per supported language.
    /// </summary>
    public sealed class MainForm : Form
    {
        public MainForm()
        {
            Text = "Code Minifier Pro";
            Size = new Size(1000, 750);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(800, 600);
            BackColor = Color.FromArgb(240, 242, 245);
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(15, 8),
                ItemSize = new Size(120, 35)
            };

            var cssTab = new TabPage("  CSS Minifier  ") { BackColor = Color.White };
            cssTab.Controls.Add(new MinifierTabView(
                new CssMinifier(),
                Color.FromArgb(0, 120, 215),
                "Original CSS",
                "Minified CSS",
                "⚡ Minify CSS",
                "CSS files (*.css)|*.css|All files (*.*)|*.*",
                "css"));

            var jsTab = new TabPage("  JS Minifier  ") { BackColor = Color.White };
            jsTab.Controls.Add(new MinifierTabView(
                new JsMinifier(),
                Color.FromArgb(230, 126, 34),
                "Original JavaScript",
                "Minified JavaScript",
                "⚡ Minify JS",
                "JavaScript files (*.js;*.mjs)|*.js;*.mjs|All files (*.*)|*.*",
                "js"));

            tabControl.TabPages.Add(cssTab);
            tabControl.TabPages.Add(jsTab);

            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15)
            };

            mainPanel.Controls.Add(tabControl);
            Controls.Add(mainPanel);
        }
    }
}
