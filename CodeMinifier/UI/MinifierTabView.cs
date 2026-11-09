using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using CodeMinifier.Controls;
using CodeMinifier.Minification;

namespace CodeMinifier.UI
{
    /// <summary>
    /// One minifier tab: input editor, action bar (minify / open / save /
    /// copy / clear + stats), output viewer, and file drag &amp; drop.
    /// </summary>
    public sealed class MinifierTabView : UserControl
    {
        private const long LargeFileWarningBytes = 50L * 1024 * 1024;

        private readonly IMinifier _minifier;
        private readonly string _fileFilter;
        private readonly string _defaultExtension;

        private UnlimitedRichTextBox _inputBox;
        private UnlimitedRichTextBox _outputBox;
        private Label _statsLabel;
        private Button _minifyButton;

        public MinifierTabView(
            IMinifier minifier,
            Color accentColor,
            string inputLabelText,
            string outputLabelText,
            string minifyButtonText,
            string fileFilter,
            string defaultExtension)
        {
            _minifier = minifier ?? throw new ArgumentNullException(nameof(minifier));
            _fileFilter = fileFilter;
            _defaultExtension = defaultExtension;

            Dock = DockStyle.Fill;
            BuildLayout(accentColor, inputLabelText, outputLabelText, minifyButtonText);
            EnableFileDrop();
        }

        private void BuildLayout(
            Color accentColor,
            string inputLabelText,
            string outputLabelText,
            string minifyButtonText)
        {
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 8,
                SplitterDistance = 300,
                Margin = new Padding(10)
            };

            // ── Input panel ─────────────────────────────────────────────
            var topPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var inputLabel = new Label
            {
                Text = inputLabelText,
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _inputBox = MakeCodeBox(false);

            topPanel.Controls.Add(_inputBox);
            topPanel.Controls.Add(inputLabel);

            // ── Actions panel ───────────────────────────────────────────
            var actionsPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                Padding = new Padding(0, 10, 0, 0)
            };

            _minifyButton = MakeButton(minifyButtonText, accentColor, Color.White);
            var openButton = MakeButton("📂 Open", Color.FromArgb(230, 235, 240), Color.Black);
            var saveButton = MakeButton("💾 Save", Color.FromArgb(230, 235, 240), Color.Black);
            var copyButton = MakeButton("📋 Copy", Color.FromArgb(230, 235, 240), Color.Black);
            var clearButton = MakeButton("🗑 Clear", Color.FromArgb(230, 235, 240), Color.Black);

            _statsLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 204, 113)
            };

            _minifyButton.Dock = DockStyle.Left;
            openButton.Dock = DockStyle.Left;
            saveButton.Dock = DockStyle.Left;
            copyButton.Dock = DockStyle.Left;
            clearButton.Dock = DockStyle.Left;

            // Fill label first so the left-docked buttons claim their edge
            // before it takes the remaining width.
            actionsPanel.Controls.Add(_statsLabel);
            actionsPanel.Controls.Add(clearButton);
            actionsPanel.Controls.Add(copyButton);
            actionsPanel.Controls.Add(saveButton);
            actionsPanel.Controls.Add(openButton);
            actionsPanel.Controls.Add(_minifyButton);

            topPanel.Controls.Add(actionsPanel);

            // ── Output panel ────────────────────────────────────────────
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var outputLabel = new Label
            {
                Text = outputLabelText,
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };

            _outputBox = MakeCodeBox(true);

            bottomPanel.Controls.Add(_outputBox);
            bottomPanel.Controls.Add(outputLabel);

            splitContainer.Panel1.Controls.Add(topPanel);
            splitContainer.Panel2.Controls.Add(bottomPanel);
            Controls.Add(splitContainer);

            // ── Events ──────────────────────────────────────────────────
            _minifyButton.Click += (s, e) => RunMinify();
            openButton.Click += (s, e) => OpenFile();
            saveButton.Click += (s, e) => SaveOutput();
            copyButton.Click += (s, e) => CopyOutput();
            clearButton.Click += (s, e) => ClearAll();
        }

        private static UnlimitedRichTextBox MakeCodeBox(bool readOnly)
        {
            var box = new UnlimitedRichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = readOnly
            };

            if (readOnly)
                box.BackColor = Color.FromArgb(249, 250, 251);

            return box;
        }

        private static Button MakeButton(string text, Color backColor, Color foreColor)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(110, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 10, 0)
            };

            button.FlatAppearance.BorderSize = 0;
            return button;
        }

        // ── Drag & drop ─────────────────────────────────────────────────
        private void EnableFileDrop()
        {
            AllowDrop = true;
            DragEnter += OnFileDragEnter;
            DragDrop += OnFileDragDrop;

            _inputBox.AllowDrop = true;
            _inputBox.DragEnter += OnFileDragEnter;
            _inputBox.DragDrop += OnFileDragDrop;
        }

        private void OnFileDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnFileDragDrop(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length > 0)
                LoadFile(paths[0]);
        }

        // ── Actions ─────────────────────────────────────────────────────
        private void RunMinify()
        {
            string code = _inputBox.Text;

            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.Show(
                    $"Please enter {_minifier.Name} code to minify.",
                    "No Input",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;
                _minifyButton.Enabled = false;
                _statsLabel.Text = "⏳ Processing…";
                Application.DoEvents();

                string minified = _minifier.Minify(code);
                _outputBox.Text = minified;

                long originalBytes = Encoding.UTF8.GetByteCount(code);
                long minifiedBytes = Encoding.UTF8.GetByteCount(minified);
                int savedPercent = originalBytes > 0
                    ? (int)(100L - (minifiedBytes * 100L / originalBytes))
                    : 0;

                _statsLabel.Text =
                    $"✅ Lines: {CountLines(code):N0} → {CountLines(minified):N0} | " +
                    $"Size: {originalBytes:N0} B → {minifiedBytes:N0} B | " +
                    $"Saved: {savedPercent}%";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error during minification:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                _statsLabel.Text = "❌ Error";
            }
            finally
            {
                Cursor = Cursors.Default;
                _minifyButton.Enabled = true;
            }
        }

        private void OpenFile()
        {
            using (var dialog = new OpenFileDialog
            {
                Title = $"Open {_minifier.Name} file",
                Filter = _fileFilter
            })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    LoadFile(dialog.FileName);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                var info = new FileInfo(path);

                if (info.Length > LargeFileWarningBytes)
                {
                    DialogResult result = MessageBox.Show(
                        "This file is larger than 50 MB and may be slow to process.\nContinue?",
                        "Large File",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                        return;
                }

                _inputBox.Text = File.ReadAllText(path);
                _outputBox.Clear();
                _statsLabel.Text = "📄 Loaded: " + Path.GetFileName(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open file:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SaveOutput()
        {
            if (string.IsNullOrWhiteSpace(_outputBox.Text))
            {
                MessageBox.Show(
                    "Nothing to save yet. Minify some code first.",
                    "Empty",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Title = $"Save minified {_minifier.Name}",
                Filter = _fileFilter,
                DefaultExt = _defaultExtension,
                FileName = "minified." + _defaultExtension
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    File.WriteAllText(dialog.FileName, _outputBox.Text, new UTF8Encoding(false));
                    _statsLabel.Text = "💾 Saved: " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Could not save file:\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void CopyOutput()
        {
            if (string.IsNullOrWhiteSpace(_outputBox.Text))
            {
                MessageBox.Show(
                    "Nothing to copy yet.",
                    "Empty",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                Clipboard.SetText(_outputBox.Text);
                _statsLabel.Text = "✅ Copied to clipboard";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Copy failed:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ClearAll()
        {
            _inputBox.Clear();
            _outputBox.Clear();
            _statsLabel.Text = string.Empty;
            _inputBox.Focus();
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 1;

            int count = 1;
            foreach (char c in text)
            {
                if (c == '\n')
                    count++;
            }

            return count;
        }
    }
}
