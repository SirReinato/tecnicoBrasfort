using System;
using System.Drawing;
using System.Windows.Forms;

namespace TecnicoBrasfort.UI
{
    public class BackgroundStatusBannerForm : Form
    {
        private readonly Label _lblHeader;
        private readonly Label _lblStep;
        private readonly ProgressBar _progressBar;
        private readonly Button _btnExpand;
        private readonly NotifyIcon _notifyIcon;
        private bool _isRunning;

        public event EventHandler ExpandRequested;

        public BackgroundStatusBannerForm()
        {
            _isRunning = true;

            Text = "Suporte TI Brasfort";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ControlBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(370, 126);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            BackColor = Color.White;

            _lblHeader = new Label();
            _lblHeader.Text = "Suporte TI Brasfort em andamento — Nao desligue o computador";
            _lblHeader.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _lblHeader.ForeColor = Color.Navy;
            _lblHeader.Location = new Point(12, 10);
            _lblHeader.Size = new Size(346, 32);
            Controls.Add(_lblHeader);

            _lblStep = new Label();
            _lblStep.Text = "Iniciando fila de procedimentos...";
            _lblStep.Location = new Point(12, 46);
            _lblStep.Size = new Size(346, 20);
            Controls.Add(_lblStep);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(12, 70);
            _progressBar.Size = new Size(346, 16);
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            Controls.Add(_progressBar);

            _btnExpand = new Button();
            _btnExpand.Text = "Expandir";
            _btnExpand.Location = new Point(268, 92);
            _btnExpand.Size = new Size(90, 26);
            _btnExpand.Click += delegate(object sender, EventArgs e)
            {
                RaiseExpandRequested();
            };
            Controls.Add(_btnExpand);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = AppIconProvider.GetAppIcon();
            _notifyIcon.Text = "Tecnico Brasfort - Suporte em andamento";
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate(object sender, EventArgs e)
            {
                RaiseExpandRequested();
            };

            PositionBottomRight();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void UpdateProgress(int currentStep, int totalSteps, string stepName)
        {
            if (IsDisposed)
            {
                return;
            }

            _lblStep.Text = string.Format("Etapa {0}/{1}: {2}", currentStep, totalSteps, stepName);
            int pct = totalSteps > 0 ? (int)((currentStep - 1) * 100.0 / totalSteps) : 0;
            if (pct < 0) pct = 0;
            if (pct > 100) pct = 100;
            _progressBar.Value = pct;
            _notifyIcon.Text = TruncateTrayText(string.Format("Tecnico Brasfort ({0}/{1})", currentStep, totalSteps));
        }

        public void SetCompleted(bool rebootRequired)
        {
            _isRunning = false;
            if (IsDisposed)
            {
                return;
            }

            _progressBar.Value = 100;
            _lblHeader.ForeColor = rebootRequired ? Color.DarkOrange : Color.DarkGreen;
            _lblHeader.Text = rebootRequired
                ? "Suporte concluido — REINICIALIZACAO NECESSARIA"
                : "Suporte concluido com sucesso";
            _lblStep.Text = "Clique em Expandir para ver o resumo de fechamento.";
            _notifyIcon.Text = "Tecnico Brasfort - Concluido";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isRunning && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private void PositionBottomRight()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        }

        private void RaiseExpandRequested()
        {
            EventHandler handler = ExpandRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static string TruncateTrayText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            return text.Length <= 63 ? text : text.Substring(0, 63);
        }
    }
}
